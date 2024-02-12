Shader "AGXUnity/CableDamageShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows 

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        #pragma multi_compile_instancing

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
            UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color); // Override if there is an instanced prop
            color.rgb = GammaToLinearSpace(color);
            float yScale = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));

            IN.uv_MainTex.y = 0.5 - IN.uv_MainTex.y;
            IN.uv_MainTex.y *= yScale;

            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * color;
            //c.rgb = worldScale / 2.0f -1.0f;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
