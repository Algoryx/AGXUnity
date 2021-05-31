Shader "Custom/DepthGrayscale" {
  Properties{
    _Fov( "FOV", Float ) = 90
  }

  SubShader{
    Tags{ "RenderType" = "Opaque" }

    Pass{
      ZTest Always Cull Off ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      sampler2D _CameraDepthTexture;

      struct v2f
      {
        float4 pos : SV_POSITION;
        float4 uv : TEXCOORD1;
      };

      //Vertex Shader
      v2f vert( appdata_base v )
      {
        v2f o;
        o.pos = UnityObjectToClipPos( v.vertex );
        o.uv = ComputeScreenPos( o.pos );
        return o;
      }

      //Fragment Shader
      float4 frag( v2f i ) : SV_Target
      {
        //get depth from depth texture
        float depth = tex2D(_CameraDepthTexture, i.uv).r;
        //linear depth between camera and far clipping plane
        depth = Linear01Depth(depth);
        //depth as distance from camera in units
        depth = depth * _ProjectionParams.z;

        return depth;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"
}