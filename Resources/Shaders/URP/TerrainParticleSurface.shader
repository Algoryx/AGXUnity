Shader "Hidden/AGXUnity/TerrainParticleSurface"
{
  SubShader
  {
    Tags { "RenderPipeline" = "UniversalPipeline" }
    Cull Off
    HLSLINCLUDE
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    struct Particle { float3 position; float radius; float4 rotation; float4 color; uint id; uint eligible; int terrain; uint material; };
    StructuredBuffer<Particle> _SoilParticles;
    StructuredBuffer<uint2> _SoilBridges;
    TEXTURE2D(_SoilDepth); TEXTURE2D(_SoilColor); TEXTURE2D(_SoilNormal);
    TEXTURE2D(_SoilSceneDepth); TEXTURE2D(_SoilTexture);
    TEXTURE2D(_SoilAlbedo);
    float4x4 _SoilProjection, _SoilInverseProjection, _SoilView, _SoilInverseView;
    float4 _SoilSize, _SoilTint;
    float2 _SoilFilterDirection;
    float _SoilSmoothing, _SoilThreshold;
    float _SoilNormalContribution, _SoilRepeatDistance, _SoilSmoothness;
    float _SoilParticleColorContribution;
    float _SoilParticleTexture;

    struct FullscreenVaryings { float4 positionCS : SV_POSITION; };
    FullscreenVaryings FullscreenVertex(uint id : SV_VertexID)
    {
      FullscreenVaryings output;
      output.positionCS = GetFullScreenTriangleVertexPosition(id);
      return output;
    }
    float2 ClipXY(float2 uv)
    {
      float2 xy = uv * 2 - 1;
      #if UNITY_UV_STARTS_AT_TOP
      xy.y = -xy.y;
      #endif
      return xy;
    }
    float3 Unproject(float2 uv, float clipDepth)
    {
      float4 p = mul(_SoilInverseProjection, float4(ClipXY(uv), clipDepth, 1));
      return p.xyz / p.w;
    }
    float3 PositionAt(float2 uv, float eyeDepth)
    {
      float3 a = Unproject(uv, UNITY_NEAR_CLIP_VALUE);
      float3 b = Unproject(uv, 0.5);
      return a + (b-a) * ((-eyeDepth-a.z) / (b.z-a.z));
    }
    float HardwareDepth(float3 eye)
    {
      float4 p = mul(_SoilProjection, float4(eye, 1));
      float d = p.z / p.w;
      #if !UNITY_REVERSED_Z
      d = (d-UNITY_NEAR_CLIP_VALUE) / (1-UNITY_NEAR_CLIP_VALUE);
      #endif
      return d;
    }
    float SceneEyeDepth(float2 uv)
    {
      float hardware = SAMPLE_TEXTURE2D_LOD(_SoilSceneDepth, sampler_PointClamp, uv, 0).r;
      #if !UNITY_REVERSED_Z
      hardware = lerp(UNITY_NEAR_CLIP_VALUE, 1, hardware);
      #endif
      return -Unproject(uv, hardware).z;
    }
    float4 NormalizeCoverage(float4 value)
    {
      if (value.a > 0) { value.rgb /= value.a; value.a = 1; }
      return value;
    }
    float4 SampleDepth(float2 uv) { return NormalizeCoverage(SAMPLE_TEXTURE2D_LOD(_SoilDepth, sampler_LinearClamp, uv, 0)); }
    // Alpha carries the original front/back depth interval, not coverage.
    float4 SampleColor(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_SoilColor, sampler_LinearClamp, uv, 0) / max(SAMPLE_TEXTURE2D_LOD(_SoilDepth, sampler_LinearClamp, uv, 0).a, 1e-8); }
    float4 SampleNormal(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_SoilNormal, sampler_LinearClamp, uv, 0) / max(SAMPLE_TEXTURE2D_LOD(_SoilDepth, sampler_LinearClamp, uv, 0).a, 1e-8); }
    float3 Rotate(float3 p, float4 q) { return p + 2 * cross(q.xyz, cross(q.xyz, p) + q.w * p); }
    float3 TriplanarTexture(float3 position, float3 normal)
    {
      float3 weights = pow(abs(normal), 4); weights /= max(dot(weights, 1), 1e-6);
      float3 coordinates = position / max(_SoilRepeatDistance, 1e-6);
      return SAMPLE_TEXTURE2D(_SoilTexture, sampler_LinearRepeat, coordinates.yz).rgb * weights.x
           + SAMPLE_TEXTURE2D(_SoilTexture, sampler_LinearRepeat, coordinates.xz).rgb * weights.y
           + SAMPLE_TEXTURE2D(_SoilTexture, sampler_LinearRepeat, coordinates.xy).rgb * weights.z;
    }
    float3 ParticleTexture(float3 world, float3 normal, float3 center, float4 rotation)
    {
      if (_SoilParticleTexture < 0.5) return 1;
      float4 inverseRotation = float4(-rotation.xyz, rotation.w);
      return TriplanarTexture(Rotate(world-center, inverseRotation), Rotate(normal, inverseRotation));
    }

    struct InputVaryings
    {
      float4 positionCS : SV_POSITION;
      float3 eye : TEXCOORD0;
      float3 normalWS : TEXCOORD1;
      nointerpolation float radius : TEXCOORD2;
      nointerpolation float4 color : TEXCOORD3;
      nointerpolation uint material : TEXCOORD4;
      nointerpolation float3 centerWS : TEXCOORD5;
      nointerpolation float4 rotation : TEXCOORD6;
    };
    InputVaryings SphereVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
    {
      Particle particle = _SoilParticles[instance];
      InputVaryings output = (InputVaryings)0;
      float3 center = mul(_SoilView, float4(particle.position, 1)).xyz;
      float radius = particle.radius;
      float2 lo = 1e20, hi = -1e20;
      bool crossesEye = false;
      [unroll] for (int z=-1; z<=1; z+=2)
        [unroll] for (int y=-1; y<=1; y+=2)
          [unroll] for (int x=-1; x<=1; x+=2) {
            float4 p = mul(_SoilProjection, float4(center + float3(x,y,z)*radius, 1));
            crossesEye = crossesEye || p.w <= 0;
            lo = min(lo, p.xy / max(p.w, 1e-8)); hi = max(hi, p.xy / max(p.w, 1e-8));
          }
      if (crossesEye) { lo = -1; hi = 1; }
      lo = max(lo, -1); hi = min(hi, 1);
      if (any(lo > hi) || center.z >= radius) { lo = -2; hi = -2; }
      // Two triangles, without a geometry shader. Bounds include off-axis and
      // near-plane-crossing spheres, including orthographic projections.
      const float2 corners[6] = {float2(0,0),float2(1,0),float2(0,1),float2(0,1),float2(1,0),float2(1,1)};
      output.positionCS = float4(lerp(lo, hi, corners[vertex]), UNITY_NEAR_CLIP_VALUE, 1);
      output.eye = center; output.radius = radius; output.color = particle.color;
      output.material = particle.material;
      output.centerWS = particle.position; output.rotation = particle.rotation;
      return output;
    }
    struct MeshAttributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; uint instance : SV_InstanceID; };
    InputVaryings MeshVertex(MeshAttributes input)
    {
      Particle particle = _SoilParticles[input.instance];
      InputVaryings output;
      float3 world = particle.position + Rotate(input.positionOS * (2 * particle.radius), particle.rotation);
      output.eye = mul(_SoilView, float4(world, 1)).xyz;
      output.positionCS = mul(_SoilProjection, float4(output.eye, 1));
      output.normalWS = Rotate(input.normalOS, particle.rotation);
      output.radius = particle.radius; output.color = particle.color;
      output.material = particle.material;
      output.centerWS = particle.position; output.rotation = particle.rotation;
      return output;
    }
    struct AttributesOutput { float4 depthRadius : SV_Target0; float4 color : SV_Target1; float4 normal : SV_Target2; float4 albedo : SV_Target3; };
    struct InputOutput { float4 depthRadius : SV_Target0; float4 color : SV_Target1; float4 normal : SV_Target2; float4 albedo : SV_Target3; float depth : SV_Depth; };
    InputOutput MakeInput(float2 uv, float3 eye, float3 normal, float radius, float4 color, uint material, float backDepth, float3 textureColor)
    {
      float depth = HardwareDepth(eye);
      if (eye.z >= 0 || depth < 0 || depth > 1 || -eye.z > SceneEyeDepth(uv) + 1e-4) discard;
      InputOutput output;
      output.depthRadius = float4(-eye.z, radius, material, 1);
      output.color = float4(color.rgb, -eye.z);
      output.normal = float4(normalize(normal), max(-eye.z, backDepth));
      output.albedo = float4(textureColor, 1);
      output.depth = depth;
      return output;
    }
    InputOutput SphereFragment(InputVaryings input)
    {
      float2 uv = input.positionCS.xy * _SoilSize.zw;
      float3 origin = Unproject(uv, UNITY_NEAR_CLIP_VALUE);
      float3 direction = normalize(Unproject(uv, 0.5) - origin);
      float3 offset = origin - input.eye;
      float h = dot(offset, direction);
      float discriminant = h*h - dot(offset, offset) + input.radius * input.radius;
      if (discriminant < 0) discard;
      float t = -h - sqrt(discriminant);
      if (t < 0) t = -h + sqrt(discriminant);
      if (t < 0) discard;
      float3 eye = origin + t * direction;
      float3 normal = mul((float3x3)_SoilInverseView, normalize(eye - input.eye));
      float3 world = mul(_SoilInverseView, float4(eye, 1)).xyz;
      return MakeInput(uv, eye, normal, input.radius, input.color, input.material, -(origin + (-h + sqrt(discriminant)) * direction).z,
                       ParticleTexture(world, normal, input.centerWS, input.rotation));
    }
    InputOutput MeshFragment(InputVaryings input)
    {
      // Mesh input has no analytic back face interval: retain its physical front
      // depth while filtering its color and normals. Bridges have analytic bounds.
      float3 world = mul(_SoilInverseView, float4(input.eye, 1)).xyz;
      return MakeInput(input.positionCS.xy * _SoilSize.zw, input.eye, input.normalWS, input.radius, input.color, input.material, -input.eye.z,
                       ParticleTexture(world, normalize(input.normalWS), input.centerWS, input.rotation));
    }

    struct BridgeVaryings
    {
      float4 positionCS : SV_POSITION;
      nointerpolation float4 a : TEXCOORD0;
      nointerpolation float4 b : TEXCOORD1;
      nointerpolation float3 colorA : TEXCOORD2;
      nointerpolation float3 colorB : TEXCOORD3;
      nointerpolation uint material : TEXCOORD4;
      nointerpolation uint2 pair : TEXCOORD5;
    };
    BridgeVaryings BridgeVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
    {
      uint2 pair = _SoilBridges[instance];
      Particle a = _SoilParticles[pair.x], b = _SoilParticles[pair.y];
      BridgeVaryings output;
      output.pair = pair;
      output.a = float4(mul(_SoilView, float4(a.position, 1)).xyz, a.eligible == 2 ? 0 : a.radius);
      output.b = float4(mul(_SoilView, float4(b.position, 1)).xyz, b.eligible == 2 ? 0 : b.radius);
      output.colorA = a.color.rgb; output.colorB = b.color.rgb; output.material = a.material;
      float2 lo = 1e20, hi = -1e20;
      bool crossesEye = false;
      [unroll] for (int endpoint=0; endpoint<2; ++endpoint) {
        float4 sphere = endpoint == 0 ? output.a : output.b;
        [unroll] for (int z=-1; z<=1; z+=2)
          [unroll] for (int y=-1; y<=1; y+=2)
            [unroll] for (int x=-1; x<=1; x+=2) {
              float4 p = mul(_SoilProjection, float4(sphere.xyz + float3(x,y,z)*sphere.w, 1));
              crossesEye = crossesEye || p.w <= 0;
              lo = min(lo, p.xy / max(p.w, 1e-8)); hi = max(hi, p.xy / max(p.w, 1e-8));
            }
      }
      if (crossesEye) { lo = -1; hi = 1; }
      lo = max(lo, -1); hi = min(hi, 1);
      if (any(lo > hi) || (output.a.z >= output.a.w && output.b.z >= output.b.w)) { lo = -2; hi = -2; }
      const float2 corners[6] = {float2(0,0),float2(1,0),float2(0,1),float2(0,1),float2(1,0),float2(1,1)};
      output.positionCS = float4(lerp(lo, hi, corners[vertex]), UNITY_NEAR_CLIP_VALUE, 1);
      return output;
    }
    void HullHit(float t, float3 normal, inout float first, inout float last, inout float3 firstNormal)
    {
      last = max(last, t);
      if (t >= 0 && t < first) { first = t; firstNormal = normal; }
    }
    void HullCap(float3 origin, float3 direction, float3 center, float radius, float3 axis,
                 float3 a, float seam, bool start, inout float first, inout float last, inout float3 normal)
    {
      if (radius <= 0) return;
      float3 offset = origin - center;
      float h = dot(offset, direction), discriminant = h*h - dot(offset, offset) + radius*radius;
      if (discriminant < 0) return;
      [unroll] for (int sign=-1; sign<=1; sign+=2) {
        float t = -h + sign * sqrt(discriminant);
        float3 p = origin + t * direction;
        float z = dot(p-a, axis);
        if ((start && z <= seam + 1e-5) || (!start && z >= seam - 1e-5))
          HullHit(t, normalize(p-center), first, last, normal);
      }
    }
    InputOutput BridgeFragment(BridgeVaryings input)
    {
      float2 uv = input.positionCS.xy * _SoilSize.zw;
      float3 origin = Unproject(uv, UNITY_NEAR_CLIP_VALUE);
      float3 direction = normalize(Unproject(uv, 0.5) - origin);
      float axisLength = distance(input.a.xyz, input.b.xyz);
      float3 axis = (input.b.xyz-input.a.xyz) / axisLength;
      // The external tangent frustum plus its spherical end caps is exactly
      // conv(sphere A, sphere B), also when particle radii differ.
      float slope = (input.a.w-input.b.w) / axisLength;
      float c2 = max(1e-8, 1-slope*slope), c = sqrt(c2);
      float seamA = input.a.w*slope, seamB = axisLength + input.b.w*slope;
      float3 offset = origin-input.a.xyz;
      float z = dot(offset, axis), dz = dot(direction, axis);
      float3 radial = offset-z*axis, dr = direction-dz*axis;
      float r = input.a.w-slope*z;
      float qa = c2*dot(dr,dr)-slope*slope*dz*dz;
      float qb = 2*(c2*dot(radial,dr)+r*slope*dz);
      float qc = c2*dot(radial,radial)-r*r;
      float first = 1e20, last = -1e20;
      float3 normal = float3(0,0,1);
      float discriminant = qb*qb-4*qa*qc;
      if (abs(qa) < 1e-7) {
        if (abs(qb) > 1e-7) {
          float t = -qc/qb, hitZ = z+t*dz;
          if (hitZ >= seamA && hitZ <= seamB)
            HullHit(t, normalize(c*(radial+t*dr)/max(length(radial+t*dr), 1e-8)+slope*axis), first, last, normal);
        }
      }
      else if (discriminant >= 0) {
        [unroll] for (int sign=-1; sign<=1; sign+=2) {
          float t = (-qb+sign*sqrt(discriminant))/(2*qa), hitZ = z+t*dz;
          if (hitZ >= seamA && hitZ <= seamB)
            HullHit(t, normalize(c*(radial+t*dr)/max(length(radial+t*dr), 1e-8)+slope*axis), first, last, normal);
        }
      }
      HullCap(origin, direction, input.a.xyz, input.a.w, axis, input.a.xyz, seamA, true, first, last, normal);
      HullCap(origin, direction, input.b.xyz, input.b.w, axis, input.a.xyz, seamB, false, first, last, normal);
      if (first == 1e20 || last < 0) discard;
      float3 eye = origin+first*direction;
      float blend = saturate(dot(eye-input.a.xyz, axis)/axisLength);
      float3 world = mul(_SoilInverseView, float4(eye, 1)).xyz;
      float3 normalWS = mul((float3x3)_SoilInverseView, normal);
      Particle a = _SoilParticles[input.pair.x], b = _SoilParticles[input.pair.y];
      float3 textureColor = lerp(ParticleTexture(world, normalWS, a.position, a.rotation), ParticleTexture(world, normalWS, b.position, b.rotation), blend);
      return MakeInput(uv, eye, normalWS, lerp(input.a.w, input.b.w, blend),
                       float4(lerp(input.colorA, input.colorB, blend), 1), input.material, -(origin+last*direction).z, textureColor);
    }

    AttributesOutput FilterFragment(FullscreenVaryings input)
    {
      float2 uv = input.positionCS.xy * _SoilSize.zw;
      float4 center = SampleDepth(uv);
      if (center.a == 0 || center.x > SceneEyeDepth(uv) + 1e-4) discard;
      float4 centerColor = SampleColor(uv);
      float4 centerNormal = SampleNormal(uv);
      float projectionScale = abs(_SoilProjection[1][1]) * _SoilSize.y * 0.5;
      bool perspective = abs(_SoilProjection[3][3]) < 1e-6;
      float kernel = clamp(projectionScale * center.y * _SoilSmoothing / (perspective ? center.x : 1), 0, 24);
      float4 depthSum = 0, colorSum = 0;
      float3 normalSum = 0;
      float3 albedoSum = 0;
      float weightSum = 0;
      [loop] for (int i=-12; i<=12; ++i) {
        float offset = i * max(kernel/6, 1);
        if (abs(offset) > 2*kernel && i != 0) continue;
        float2 sampleUV = uv + _SoilFilterDirection * offset * _SoilSize.zw;
        if (any(sampleUV < 0) || any(sampleUV > 1)) continue;
        float4 d = SampleDepth(sampleUV);
        float range = max(1e-5, _SoilThreshold * min(center.y, d.y));
        if (d.a == 0 || abs(d.z-center.z) > 1e-3 || abs(d.x-center.x) > range || d.x > SceneEyeDepth(sampleUV) + 1e-4) continue;
        float4 c = SampleColor(sampleUV);
        float3 colorDelta = c.rgb - centerColor.rgb;
        float depthDelta = (d.x-center.x)/range;
        float rangeWeight = pow(max(0, 1-depthDelta*depthDelta), 2);
        float w = rangeWeight * exp(-2*offset*offset/max(kernel*kernel, 1) - 2*depthDelta*depthDelta - 8*dot(colorDelta,colorDelta));
        depthSum += d*w; colorSum += c*w; normalSum += SampleNormal(sampleUV).xyz*w;
        albedoSum += NormalizeCoverage(SAMPLE_TEXTURE2D_LOD(_SoilAlbedo, sampler_LinearClamp, sampleUV, 0)).rgb*w;
        weightSum += w;
      }
      AttributesOutput output;
      output.depthRadius = depthSum / max(weightSum, 1e-8);
      // Keep both passes inside the original physical sphere/tangent hull.
      output.depthRadius.x = clamp(output.depthRadius.x, centerColor.a, centerNormal.a);
      if (output.depthRadius.x > SceneEyeDepth(uv) + 1e-4) discard;
      output.color = colorSum / max(weightSum, 1e-8);
      output.color.a = centerColor.a;
      output.normal = float4(normalize(normalSum + float3(0,0,1e-8)), centerNormal.a);
      output.albedo = float4(albedoSum / max(weightSum, 1e-8), 1);
      return output;
    }

    float3 TangentAt(float2 uv, float2 delta, float3 position, float4 center)
    {
      float4 forward = SampleDepth(uv+delta), backward = SampleDepth(uv-delta);
      bool validForward = forward.a > 0 && abs(forward.x-center.x) < center.y && all(uv+delta <= 1);
      bool validBackward = backward.a > 0 && abs(backward.x-center.x) < center.y && all(uv-delta >= 0);
      float3 tangent = PositionAt(uv+delta, center.x) - position;
      if (validForward && (!validBackward || abs(forward.x-center.x) < abs(backward.x-center.x)))
        tangent = PositionAt(uv+delta, forward.x) - position;
      else if (validBackward) tangent = position - PositionAt(uv-delta, backward.x);
      return tangent;
    }
    void SurfaceAt(float2 uv, out float3 world, out float3 normal, out float hardware)
    {
      world = 0; normal = float3(0,1,0); hardware = 0;
      float4 d = SampleDepth(uv);
      // Coverage test uses unnormalized alpha, so bilinear upsampling cannot
      // extend the silhouette from a single covered low-resolution neighbor.
      if (SAMPLE_TEXTURE2D_LOD(_SoilDepth, sampler_LinearClamp, uv, 0).a < 0.5) discard;
      d.x = clamp(d.x, SampleColor(uv).a, SampleNormal(uv).a);
      float3 eye = PositionAt(uv, d.x);
      hardware = HardwareDepth(eye);
      if (hardware < 0 || hardware > 1) discard;
      world = mul(_SoilInverseView, float4(eye, 1)).xyz;
      float3 grain = normalize(SampleNormal(uv).xyz);
      float3 dx = TangentAt(uv, float2(_SoilSize.z, 0), eye, d);
      float3 dy = TangentAt(uv, float2(0, _SoilSize.w), eye, d);
      float3 smooth = normalize(cross(dx, dy));
      if (dot(smooth, eye) > 0) smooth = -smooth;
      smooth = normalize(mul((float3x3)_SoilInverseView, smooth));
      normal = normalize(lerp(smooth, grain, _SoilNormalContribution));
    }
    struct CompositeOutput { half4 color : SV_Target; float depth : SV_Depth; };
    CompositeOutput CompositeFragment(FullscreenVaryings input)
    {
      float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
      float3 world, normal; float hardware;
      SurfaceAt(uv, world, normal, hardware);
      half3 textureColor;
      if (_SoilParticleTexture > 0.5)
        textureColor = NormalizeCoverage(SAMPLE_TEXTURE2D_LOD(_SoilAlbedo, sampler_LinearClamp, uv, 0)).rgb;
      else textureColor = TriplanarTexture(world, normal);
      SurfaceData surface = (SurfaceData)0;
      surface.albedo = lerp(float3(1,1,1), SampleColor(uv).rgb, _SoilParticleColorContribution) * _SoilTint.rgb * textureColor;
      surface.alpha = 1; surface.smoothness = _SoilSmoothness; surface.occlusion = 1; surface.normalTS = half3(0,0,1);
      InputData lighting = (InputData)0;
      lighting.positionWS = world; lighting.normalWS = normal;
      lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(world);
      #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
      lighting.shadowCoord = float4(uv, 0, 1);
      #else
      lighting.shadowCoord = TransformWorldToShadowCoord(world);
      #endif
      lighting.bakedGI = SampleSH(normal); lighting.normalizedScreenSpaceUV = uv;
      lighting.shadowMask = half4(1,1,1,1);
      CompositeOutput output;
      output.color = UniversalFragmentPBR(lighting, surface);
      output.color.rgb = MixFog(output.color.rgb, ComputeFogFactor(TransformWorldToHClip(world).z));
      output.depth = hardware;
      return output;
    }
    CompositeOutput DepthNormalsFragment(FullscreenVaryings input)
    {
      float3 world, normal; float hardware;
      SurfaceAt(input.positionCS.xy / _ScaledScreenParams.xy, world, normal, hardware);
      CompositeOutput output;
      #if defined(_GBUFFER_NORMALS_OCT)
      float2 oct = PackNormalOctQuadEncode(normal);
      output.color = half4(PackFloat2To888(saturate(oct*0.5+0.5)), 0);
      #else
      output.color = half4(normal, 0);
      #endif
      output.depth = hardware;
      return output;
    }
    ENDHLSL

    Pass
    {
      Name "SphereInput"
      ZWrite On ZTest LEqual
      HLSLPROGRAM
      #pragma vertex SphereVertex
      #pragma fragment SphereFragment
      ENDHLSL
    }
    Pass
    {
      Name "MeshInput"
      ZWrite On ZTest LEqual
      HLSLPROGRAM
      #pragma vertex MeshVertex
      #pragma fragment MeshFragment
      #pragma multi_compile_instancing
      ENDHLSL
    }
    Pass
    {
      Name "Filter"
      ZWrite Off ZTest Always
      HLSLPROGRAM
      #pragma vertex FullscreenVertex
      #pragma fragment FilterFragment
      ENDHLSL
    }
    Pass
    {
      Name "Composite"
      ZWrite On ZTest LEqual
      HLSLPROGRAM
      #pragma vertex FullscreenVertex
      #pragma fragment CompositeFragment
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #pragma multi_compile _ _ADDITIONAL_LIGHTS
      #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
      #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
      #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
      #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
      #pragma multi_compile_fragment _ _LIGHT_COOKIES
      #pragma multi_compile_fog
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      ZWrite On ZTest LEqual
      HLSLPROGRAM
      #pragma vertex FullscreenVertex
      #pragma fragment DepthNormalsFragment
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      ENDHLSL
    }
    Pass
    {
      Name "ParticleBridges"
      ZWrite On ZTest LEqual
      HLSLPROGRAM
      #pragma vertex BridgeVertex
      #pragma fragment BridgeFragment
      ENDHLSL
    }
  }
}
