Shader "AGXUnity/Built-In/PointCloudShader"
{
  Properties
  {
    _MainTex ("Texture", 2D) = "white" {}
    _PointSize ("Point Size", Float) = 0.02
    _ColorStart ("Start Color", Color) = (1, 0, 0, 1)
    _ColorEnd ("End Color", Color) = (0, 1, 0, 1)
  }
  SubShader
  {
    Tags { "Queue" = "AlphaTest" "DisableBatching"="True" }
    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "UnityCG.cginc"

      struct PointData
      {
        float3 position;
        float intensity;
      };

      StructuredBuffer<PointData> pointBuffer;

      float _PointSize;
      float4 _MainTex_ST;
      float4x4 _ObjectToWorld;
      float4 _ColorStart;
      float4 _ColorEnd;

      struct appdata
      {
        float3 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        float4 vertex : SV_Position;
        float4 color : COLOR0; 
        float4 particlePos : POSITION1;
        float3 rayDir : TEXCOORD0;
        float3 rayOrigin : TEXCOORD1;
        float4 screenPos : TEXCOORD2;
      };

      v2f vert(appdata v, uint instanceID : SV_InstanceID)
      {
        v2f o;

        // Fetch point data from the buffer
        PointData hit = pointBuffer[instanceID];

        float3 particlePos = hit.position;
        particlePos.x = -particlePos.x;
        particlePos = mul(_ObjectToWorld,float4(particlePos.x,particlePos.y,particlePos.z,1)).xyz;

        o.particlePos.xyz = particlePos;
        o.particlePos.w = 1;

        // check if the current projection is orthographic or not from the current projection matrix
        bool isOrtho = UNITY_MATRIX_P._m33 == 1.0;

        // viewer position, equivalent to _WorldSpaceCameraPos.xyz, but for the current view
        float3 worldSpaceViewerPos = UNITY_MATRIX_I_V._m03_m13_m23;

        // view forward
        float3 worldSpaceViewForward = -UNITY_MATRIX_I_V._m02_m12_m22;

        // pivot position
        float3 worldSpacePivotPos = particlePos.xyz;
        worldSpacePivotPos = particlePos;
        //;

        // offset between pivot and camera
        float3 worldSpacePivotToView = worldSpaceViewerPos - worldSpacePivotPos;

        float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
        //Flip 
        float3 forward = normalize(worldSpacePivotToView);
        float3 right = normalize(cross(forward, up));
        up = cross(right, forward);
        float3x3 quadOrientationMatrix = float3x3(right, up, forward);

        float quadScale = 1; // _PointSize * 2;
        if (!isOrtho)
        {
            // get the sine of the right triangle with the hyp of the sphere pivot distance and the opp of the sphere radius
            float sinAngle = _PointSize / length(worldSpacePivotToView);
            // convert to cosine
            float cosAngle = sqrt(1.0 - sinAngle * sinAngle);
            // convert to tangent
            float tanAngle = sinAngle / cosAngle;

            // basically this, but should be faster
            //tanAngle = tan(asin(sinAngle));

            // get the opp of the right triangle with the 90 degree at the sphere pivot * 2
            quadScale = tanAngle * length(worldSpacePivotToView) * 2.0;
        }  

        //o.worldPos = float4(particlePos.xyz + _PointSize * 2 * BOX_CORRECTION * (cameraRight * v.vertex.x + cameraUp * v.vertex.y), 1);
        float3 worldPos = mul(v.vertex.xyz * quadScale, quadOrientationMatrix) + worldSpacePivotPos;
        //o.pos = mul(UNITY_MATRIX_VP, o.worldPos);


        if (isOrtho)
        {
            o.rayDir = worldSpaceViewForward * -dot(worldSpacePivotToView, worldSpaceViewForward);
            o.rayOrigin = worldPos - o.rayDir;
        } 
        else 
        {
            o.rayOrigin = float4(worldSpaceViewerPos, 1);
            o.rayDir = float4(worldPos - o.rayOrigin, 0);
        }

        o.vertex = UnityWorldToClipPos(float4(worldPos, 1));

        o.screenPos = ComputeScreenPos(o.vertex);
        o.screenPos.z = -UnityWorldToViewPos( float4(worldPos, 1) ).z;
            
        //o.color.rgb = mul(_ObjectToWorld,float4(particlePos.x,particlePos.y,particlePos.z,1));
        o.color = lerp(_ColorStart, _ColorEnd, saturate(hit.intensity));
        return o;
      }

      sampler2D _MainTex;

      // https://www.iquilezles.org/www/articles/spherefunctions/spherefunctions.htm
        float sphIntersect(float3 ro, float3 rd, float4 sph)
        {
            float3 oc = ro - sph.xyz;
            float b = dot(oc, rd);
            float c = dot(oc, oc) - sph.w * sph.w;
            float h = b * b - c;
            if (h < 0.0) return -1.0;
            h = sqrt(h);
            return -b - h;
        }

      float4 frag(v2f i, out float outDepth : SV_Depth) : SV_Target
      {
        bool isOrtho = UNITY_MATRIX_P._m33 == 1.0;

        // ray origin
        float3 rayOrigin = i.rayOrigin;

        // normalize ray vector
        float3 rayDir = normalize(i.rayDir);

        // ray sphere intersection
        float rayHit = sphIntersect(rayOrigin, rayDir, float4(i.particlePos.xyz, i.particlePos.w * _PointSize));

        clip(rayHit);

        rayHit = rayHit < 0.0 ? dot(rayDir, -rayOrigin) : rayHit;

        // calculate object space position from ray, front hit ray length, and ray origin
        float3 fragPosOnSphere = rayDir * rayHit + rayOrigin;
        // surface normal
        float3 normal = normalize(fragPosOnSphere.xyz - i.particlePos.xyz);
        float4 fragPosOnSphereClipSpace = UnityWorldToClipPos(float4(fragPosOnSphere,1.0f));

        outDepth = fragPosOnSphereClipSpace.z / fragPosOnSphereClipSpace.w;

        #if !defined(UNITY_REVERSED_Z)
            outDepth = outDepth * 0.5 + 0.5;
        #endif

        return  i.color;
      }
      ENDCG
    }
  }
}
