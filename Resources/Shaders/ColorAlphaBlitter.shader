Shader "AGXUnity/ColorAlphaBlitter"
{
    Properties
    {
        _ColorTex ("RGB Texture", 2D) = "white" {}
        _AlphaTex ("Alpha Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _ColorTex;
            sampler2D _AlphaTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 color = tex2D(_ColorTex, i.uv);
                float alpha = tex2D(_AlphaTex, i.uv).r;
                color.a = alpha;
                return color;
            }
            ENDCG
        }
    }
}