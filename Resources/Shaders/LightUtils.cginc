#include "UnityCG.cginc"
#include "AutoLight.cginc"

#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD)

half3 _LightColor0;

// Some of the unity lighting macros requires the shadow coords to be in a struct
// A dummy struct is created and passed to these macros.
struct shadow_dummy_struct
{
    SHADOW_COORDS(0)
};

#endif

// Calculates a lighting factor given the world position, clip position, and normal of a fragment
// This works for point, spot, and directional lights.
// returns all ones if the current pass is not a lighting pass
float3 CustomLighting(float3 worldPos, float4 clipPos, float3 normal) {
    #if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD)
        #if defined (SHADOWS_SCREEN)
            // setup shadow struct for screen space shadows
            shadow_dummy_struct shadow_dummy;
            #if defined(UNITY_NO_SCREENSPACE_SHADOWS)
                // mobile directional shadow
                shadow_dummy._ShadowCoord = mul(unity_WorldToShadow[0], worldPos);
            #else
                // screen space directional shadow
                shadow_dummy._ShadowCoord = ComputeScreenPos(clipPos);
            #endif // UNITY_NO_SCREENSPACE_SHADOWS
        #else
            // no shadow, or no directional shadow
            float shadow_dummy = 0;
        #endif // SHADOWS_SCREEN

        half3 worldLightDir = UnityWorldSpaceLightDir(worldPos);
        half ndotl = saturate(dot(normal, worldLightDir));
        UNITY_LIGHT_ATTENUATION(atten, shadow_dummy, worldPos);            
                
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
        return lighting;
    #else
        return half3(1.0f,1.0f,1.0f);
    #endif
}