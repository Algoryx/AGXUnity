Shader "AGXUnity/Terrain Particle Impostor"
{
    Properties
    {
        _ColorLow("Dark Color", Color) = (1, 1, 1, 1)	
        _ColorHigh("Light Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        LOD 100
        
        Tags { "Queue" = "AlphaTest" "DisableBatching"="True" }
        CGINCLUDE

        #if defined(UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS)
        #undef UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS
        #endif

        #include "UnityCG.cginc"
        #include "AutoLight.cginc"
        #include "./Compute/ConstantsAndStructs.cginc"            

        #define BOX_CORRECTION 1.5

        fixed4 _ColorLow;
        fixed4 _ColorHigh;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

        StructuredBuffer<FineParticle> fineParticles;
        float fineRadius;

        struct appdata
        {
            float4 vertex : POSITION;
            uint instanceID : SV_InstanceID;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float4 particlePos : POSITION1;
            float3 rayDir : TEXCOORD0;
            float3 rayOrigin : TEXCOORD1;
            fixed3 color : TEXCOORD2;
            float4 screenPos : TEXCOORD3;
        };

        v2f vert(appdata v)
        {
            float3 particlePos = FP_POSITION(fineParticles[v.instanceID]);
            // Convert from agx handedness to unity handedness
            particlePos.x = -particlePos.x;
            float absEase = abs(EASE(fineParticles[v.instanceID]));

            v2f o;
            o.particlePos.xyz = particlePos;
            o.particlePos.w = absEase;

            o.color = lerp(_ColorLow, _ColorHigh, RAND(fineParticles[v.instanceID]));

            // check if the current projection is orthographic or not from the current projection matrix
            bool isOrtho = UNITY_MATRIX_P._m33 == 1.0;

            // viewer position, equivalent to _WorldSpaceCameraPos.xyz, but for the current view
            float3 worldSpaceViewerPos = UNITY_MATRIX_I_V._m03_m13_m23;

            // view forward
            float3 worldSpaceViewForward = -UNITY_MATRIX_I_V._m02_m12_m22;

            // pivot position
            float3 worldSpacePivotPos = particlePos.xyz;

            // offset between pivot and camera
            float3 worldSpacePivotToView = worldSpaceViewerPos - worldSpacePivotPos;

            float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
            //Flip 
            float3 forward = -normalize(worldSpacePivotToView);
            float3 right = normalize(cross(forward, up));
            up = cross(right, forward);
            float3x3 quadOrientationMatrix = float3x3(right, up, forward);

            float quadScale = absEase * fineRadius * 2;
            if (!isOrtho)
            {
                // get the sine of the right triangle with the hyp of the sphere pivot distance and the opp of the sphere radius
                float sinAngle = absEase * fineRadius / length(worldSpacePivotToView);
                // convert to cosine
                float cosAngle = sqrt(1.0 - sinAngle * sinAngle);
                // convert to tangent
                float tanAngle = sinAngle / cosAngle;

                // basically this, but should be faster
                //tanAngle = tan(asin(sinAngle));

                // get the opp of the right triangle with the 90 degree at the sphere pivot * 2
                quadScale = tanAngle * length(worldSpacePivotToView) * 2.0;
            }  

            //o.worldPos = float4(particlePos.xyz + absEase * fineRadius * 2 * BOX_CORRECTION * (cameraRight * v.vertex.x + cameraUp * v.vertex.y), 1);
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

            o.pos = UnityWorldToClipPos(float4(worldPos, 1));

            o.screenPos = ComputeScreenPos(o.pos);
            o.screenPos.z = -UnityWorldToViewPos( float4(worldPos, 1) ).z;
            
            return o;
        }

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

        float rand(float2 co) {
            float x = sin(dot(co, float2(12.9898, 78.233))) * 43758.5453;
            return x-floor(x);
        }

    #if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD)
        half3 _LightColor0;

        struct shadow_dummy_struct
        {
            SHADOW_COORDS(0)
        };

        fixed4 frag_forward(v2f i, out float outDepth : SV_Depth) : SV_Target
        {               
            // ray origin
            float3 rayOrigin = i.rayOrigin;

            // normalize ray vector
            float3 rayDir = normalize(i.rayDir);

            // ray sphere intersection
            float rayHit = sphIntersect(rayOrigin, rayDir, float4(i.particlePos.xyz, i.particlePos.w * fineRadius));

            clip(rayHit);

            rayHit = rayHit < 0.0 ? dot(rayDir, -rayOrigin) : rayHit;

            // calculate object space position from ray, front hit ray length, and ray origin
            float3 fragPosOnSphere = rayDir * rayHit + rayOrigin;
            float3 localPos = fragPosOnSphere.xyz - i.particlePos.xyz;

            // surface normal
            float3 normal = normalize(localPos);

            fixed4 col;
            col.rgb = i.color;
            col.a = 1;

            float4 fragPosOnSphereClipSpace = UnityWorldToClipPos(float4(fragPosOnSphere,1.0f));

            outDepth = fragPosOnSphereClipSpace.z / fragPosOnSphereClipSpace.w;

            #if !defined(UNITY_REVERSED_Z)
                outDepth = outDepth * 0.5 + 0.5;
            #endif

            #if defined (SHADOWS_SCREEN)
                // setup shadow struct for screen space shadows
                shadow_dummy_struct shadow_dummy;
                #if defined(UNITY_NO_SCREENSPACE_SHADOWS)
                    // mobile directional shadow
                    shadow_dummy._ShadowCoord = mul(unity_WorldToShadow[0], fragPosOnSphere);
                #else
                    // screen space directional shadow
                    shadow_dummy._ShadowCoord = ComputeScreenPos(fragPosOnSphereClipSpace);
                #endif // UNITY_NO_SCREENSPACE_SHADOWS
            #else
                // no shadow, or no directional shadow
                float shadow_dummy = 0;
            #endif // SHADOWS_SCREEN

            half3 worldLightDir = UnityWorldSpaceLightDir(fragPosOnSphere);
            half ndotl = saturate(dot(normal, worldLightDir));
            UNITY_LIGHT_ATTENUATION(atten, shadow_dummy, fragPosOnSphere.xyz);
                
            // Per pixel lighting
            half3 lighting = _LightColor0 * ndotl * atten;


            #if defined(UNITY_SHOULD_SAMPLE_SH)
                // Add ambient only in base pass
                lighting += ShadeSH9(float4(normal, 1.0));

                #if defined(VERTEXLIGHT_ON)
                    // "per vertex" non-important lights
                    half3 vertexLighting = Shade4PointLights(
                    unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                    unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                    unity_4LightAtten0, fragPosOnSphere, normal);

                    lighting += vertexLighting;
                #endif // VERTEXLIGHT_ON
            #endif
            col.rgb *= lighting;
                
            return col;
        }
    #endif

        float4 UnityClipSpaceShadowCasterPosFromWorld(float4 worldPos, float3 normal){
            if (unity_LightShadowBias.z != 0.0)
            {
                float3 wNormal = normal;
                float3 wLight = normalize(UnityWorldSpaceLightDir(worldPos.xyz));

                float shadowCos = dot(wNormal, wLight);
                float shadowSine = sqrt(1-shadowCos*shadowCos);
                float normalBias = unity_LightShadowBias.z * shadowSine;

                worldPos.xyz -= wNormal * normalBias;
            }

            return mul(UNITY_MATRIX_VP, worldPos);
        }

        float frag_shadowcast(v2f i) : SV_Depth
        {            
            bool isOrtho = UNITY_MATRIX_P._m33 == 1.0;

            // ray origin
            float3 rayOrigin = i.rayOrigin;

            // normalize ray vector
            float3 rayDir = normalize(i.rayDir);

            // ray sphere intersection
            float rayHit = sphIntersect(rayOrigin, rayDir, float4(i.particlePos.xyz, i.particlePos.w * fineRadius));

            clip(rayHit);

            rayHit = rayHit < 0.0 ? dot(rayDir, -rayOrigin) : rayHit;

            // calculate object space position from ray, front hit ray length, and ray origin
            float3 fragPosOnSphere = rayDir * rayHit + rayOrigin;
            // surface normal
            float3 normal = normalize(fragPosOnSphere.xyz - i.particlePos.xyz);
            float4 fragPosOnSphereClipSpace = UnityClipSpaceShadowCasterPosFromWorld(float4(fragPosOnSphere,1.0f), normal);
            //fragPosOnSphereClipSpace = UnityApplyLinearShadowBias(fragPosOnSphereClipSpace);

            //float sceneZ = LinearEyeDepth (SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
            //float depth = sceneZ - i.screenPos.z;

            //if(depth < _DiscardDepth)
            //    discard;

            float outDepth = fragPosOnSphereClipSpace.z / fragPosOnSphereClipSpace.w;

            #if !defined(UNITY_REVERSED_Z)
                outDepth = outDepth * 0.5 + 0.5;
            #endif

            return outDepth;
        }

        ENDCG

        Pass {
            Tags {"LightMode" = "ForwardBase"}
            
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag_forward
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing
            #pragma multi_compile _ VERTEXLIGHT_ON
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK
            #pragma target 4.5                          

            ENDCG
        }

        Pass{
            Tags {"LightMode" = "ForwardAdd"}

            Blend One One, Zero One
            ZWrite Off ZTest LEqual
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag_forward
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_instancing
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK SPOT_COOKIE
            #pragma target 4.5

            ENDCG
        }

        Pass{
            Tags {"LightMode" = "ShadowCaster"}
            ZWrite On ZTest LEqual
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag_shadowcast
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #pragma target 4.5

            ENDCG
        }
    }
}
