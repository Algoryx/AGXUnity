Shader "AGXUnity/Instanced Terrain Particle"
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
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow

        #pragma target 5.0

        #define EASE(p) ((p).positionAndEase.w)
        #define FP_POSITION(p) ((p).positionAndEase.xyz)

        //#define FineParticle float4
        struct FineParticle
        {
            float4 positionAndEase; // xyz is position, w is particle ease
            float4 velocityAndMass;
        };

        #ifdef SHADER_API_D3D11
        StructuredBuffer<FineParticle> fineParticles;
        #endif

		float fineRadius;
        float3 offset;

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        struct appdata_id
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 texcoord : TEXCOORD0;

            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;

			uint instanceID : SV_InstanceID;
		};

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void vert (inout appdata_id v)
        {
        #ifdef SHADER_API_D3D11
            uint id = v.instanceID;
            float3 data = FP_POSITION(fineParticles[id]);
			data.x = -data.x;

			float absEase = abs(EASE(fineParticles[id]));
        #else
            float3 data = float3(0.0f,0.0f,0.0f);
            float absEase = 0.0f;
        #endif
            float3 worldSpaceViewerPos = UNITY_MATRIX_I_V._m03_m13_m23;

			float3 localPosition = v.vertex.xyz * fineRadius * absEase * 2.0f;
			float3 worldPosition = data.xyz + localPosition - offset;
            v.vertex.xyz = worldPosition;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
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
