Shader "AGXUnity/BuiltIn/TerrainPatchDecal"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Overlay"  "Queue" = "Geometry-99" }
        LOD 100

        CGINCLUDE

        // Depth texture is used in DepthInverseProjectionUtils it has to be declared before including
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

        #include "./LightUtils.cginc"
        #include "./DepthInverseProjectionUtils.cginc"

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            float3 ray : TEXCOORD1;
        };

        float _TerrainResolution;
        float3 _TerrainScale;
        float3 _TerrainPosition;
        fixed4 _Color;

        sampler2D _Heightmap;
        sampler2D _Materials;
        sampler2D _Decal0;
        sampler2D _Decal1;
        sampler2D _Decal2;
        sampler2D _Decal3;

        v2f vert (uint vertexID : SV_VertexID)
        {
            v2f o;
            // Populate vertex out with data to prepare for view space position recomputation in fragment shader
            // This creates a fullscreen triangle based on the vertex index of the given vertex
            DIPVertex(vertexID, o.vertex, o.texcoord, o.ray);
            return o;
        }

        // Calculate normal based on heightmap height differences
        float3 filterNormal(float2 uv, float texelSize)
        {
            // Sample heightmap orthogonal to the uv
            float4 h;
            h[0] = tex2D(_Heightmap, uv + texelSize * float2(0,-1)).r * _TerrainScale.y;
            h[1] = tex2D(_Heightmap, uv + texelSize * float2(-1,0)).r * _TerrainScale.y;
            h[2] = tex2D(_Heightmap, uv + texelSize * float2(1,0)).r * _TerrainScale.y;
            h[3] = tex2D(_Heightmap, uv + texelSize * float2(0,1)).r * _TerrainScale.y;
     
            float3 n;
            n.z = (h[0] - h[3]);
            n.x = (h[1] - h[2]);
            n.y = 2 * texelSize * _TerrainScale.x; // pixel space -> uv space -> world space
     
            return normalize(n);
        }

        // Calculates the barycentric coordinates for point p relative to points a,b, and c
        float3 Barycentric(float3 a, float3 b, float3 c, float3 p)
        {
            float3 v0 = b - a;
            float3 v1 = c - a;
            float3 v2 = p - a;
            float den = v0.x * v1.y - v1.x * v0.y;
            float3 res;
            res.y = (v2.x * v1.y - v1.x * v2.y) / den;
            res.z = (v0.x * v2.y - v2.x * v0.y) / den;
            res.x = 1.0f - res.y - res.z;
            return res;
        }

        // Since we compare the fragment depth against the terrain the height needs to be rather close to the actual
        // height used by unity when rendering. It turns out that Unity uses barycentric interpolation
        // based on the triangle from which the height is sampled. This function replicates this.
        // See https://github.com/chanfort/Terrain-heightmap-manual-interpolation/tree/master
        float textureBarycentric(sampler2D samp, float2 texCoords){
            // Convert texCoords to texel space and fake point sampling of the heightmap texture to avoid 
            // hardware texture sampler interpolation
            float texSize = _TerrainResolution -1;
            float invTexSize = 1.0 / texSize;
            float2 texInd = texCoords * texSize;
   
            float2 fxy = frac(texInd);
            float2 ltc = floor(texInd+0.5);
            float2 utc = ceil(texInd-0.5);

            float4 tc = float4(ltc,utc) * invTexSize; 

            // Sample heights at low (l) and high (h) texel coords xy
            float4 ll = tex2Dlod(samp,float4(tc.xy,0,0));
            float4 hh = tex2Dlod(samp,float4(tc.zw,0,0));
            float4 lh = tex2Dlod(samp,float4(tc.xw,0,0));
            float4 hl = tex2Dlod(samp,float4(tc.zy,0,0));

            float4 last;
            float3 bary;

            // Different handling depending on if triangle is upper or lower
            // Calculate the barycentric coordinates for the texCoord and assign the relevant height to last
            if(fxy.x > fxy.y) {
                last = hl;
                bary = Barycentric(float3(tc.xy,0.0f),float3(tc.zw,0.0f),float3(tc.zy,0.0f),float3(texCoords,0.0f));
            }
            else {
                last = lh;
                bary = Barycentric(float3(tc.xy,0.0f),float3(tc.zw,0.0f),float3(tc.xw,0.0f),float3(texCoords,0.0f));
            }

            // Interpolate heights based on barycentric coordinates
            return (ll * bary.x + hh * bary.y + last * bary.z).x;
        }

        fixed4 SampleDecal(int index,float2 uv){
            if(index == 0)      return float4(tex2D(_Decal0,uv).rgb,1.0f);
            else if(index == 1) return float4(tex2D(_Decal1,uv).rgb,1.0f);
            else if(index == 2) return float4(tex2D(_Decal2,uv).rgb,1.0f);
            else                return float4(tex2D(_Decal3,uv).rgb,1.0f);
        }

        // Find the bilinear interpolation of the decal materials at the four closest texel coords.
        fixed4 FilterDecals(float2 indexUV, float2 decalUV){
            // Convert indexUVs to texel space and find the 4 closest texels to sample
            float texSize = _TerrainResolution -1;
            float invTexSize = 1.0 / texSize;   
            float2 texInd = indexUV * texSize;
   
            float2 fxy = frac(texInd);
            float2 ltc = floor(texInd);
            float2 utc = ceil(texInd);

            float4 tc = float4(ltc,utc) * invTexSize; 

            // Sample indices at low (l) and high (h) texel coords xy
            int ill = int(round(tex2D(_Materials,tc.xy).r * 255.0f)) - 1;
            int ilh = int(round(tex2D(_Materials,tc.xw).r * 255.0f)) - 1;
            int ihl = int(round(tex2D(_Materials,tc.zy).r * 255.0f)) - 1;
            int ihh = int(round(tex2D(_Materials,tc.zw).r * 255.0f)) - 1;

            // Discard fragments where no material is present
            if(all(int4(ill,ilh,ihl,ihh) < 0))
                discard;

            // Sample corresponding decal texture for each index with transparent black as a default.
            float4 ll,lh,hl,hh;

            if(ill > -1) ll = SampleDecal(ill,decalUV);
            else ll = float4(0.0f,0.0f,0.0f,0.0f);
            if(ilh > -1) lh = SampleDecal(ilh,decalUV);
            else lh = float4(0.0f,0.0f,0.0f,0.0f);
            if(ihl > -1) hl = SampleDecal(ihl,decalUV);
            else hl = float4(0.0f,0.0f,0.0f,0.0f);
            if(ihh > -1) hh = SampleDecal(ihh,decalUV);
            else hh = float4(0.0f,0.0f,0.0f,0.0f);

            // Bilinearly interpolate between the materials
            float4 xl = lerp(ll,hl,fxy.x);
            float4 xh = lerp(lh,hh,fxy.x);

            return lerp(xl,xh,fxy.y);
        }

        fixed4 frag (v2f i) : SV_Target
        {
            // Find position of the fragment in view space
            float3 viewPos = DIPFragment(i.texcoord,i.ray);
            if(viewPos.z == 0)
                discard;

            // Find position of fragment in world space
            float3 pixelPos = mul(unity_MatrixInvV, float4(viewPos,1.0f)).xyz;
            // Find position of fragment relative to terrain, scaled to [0,1]
            float3 terrainPos = (pixelPos.xyz - _TerrainPosition) / _TerrainScale.xyz ;

            // Discard any fragments outside of the terrain bounds
            if(any(terrainPos != saturate(terrainPos)))
                discard;
            
            // Find the height of the terrain at terrain XZ pos and compare it against the Y pos
            float rawHeight = textureBarycentric (_Heightmap, terrainPos.xz);
            float height = rawHeight * _TerrainScale.y * 2.0f + _TerrainPosition.y;
            float absDiff = abs(height - pixelPos.y);
            
            // Discard fragments which are not on the terrain
            if(absDiff > 0.01f)
                discard;

            // Get the color of the decals at the fragment position based on the material texture
            fixed4 c = _Color;
            c *= FilterDecals(terrainPos.xz,pixelPos.xz);

            // Compute normal from the terrain heightmap
            half3 normal = filterNormal(terrainPos.xz,1.0 / (_TerrainResolution - 1));

            // CustomLighting function handles various light sources
            float3 lighting = CustomLighting(pixelPos,float4(i.texcoord,0.0f,0.0f),normal);
            c.xyz *= lighting;
            return c;
        }

        ENDCG

        Pass {
            Tags {"LightMode" = "ForwardBase"}
            ZWrite Off ZTest Off
            Blend One OneMinusSrcAlpha
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile _ VERTEXLIGHT_ON
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK
            #pragma shader_feature_local _ _MAPPING_CUBEMAP                       

            ENDCG
        }

        Pass{
            Tags {"LightMode" = "ForwardAdd"}

            Blend One One, Zero One
            ZWrite Off ZTest Off
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK SPOT_COOKIE
            #pragma shader_feature_local _ _MAPPING_CUBEMAP

            ENDCG
        }
    }
}
