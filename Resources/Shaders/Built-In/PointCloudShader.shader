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
    Tags { "RenderType"="Opaque" }
    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "UnityCG.cginc"

      struct ParticleData
      {
        float3 position;
        float intensity;
      };

      StructuredBuffer<ParticleData> particleBuffer;

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
        float2 uv : TEXCOORD0;
        float4 vertex : SV_Position;
        float4 color : COLOR0;
      };

      v2f vert(appdata v, uint instanceID : SV_InstanceID)
      {
        v2f o;

        // Fetch particle data from the buffer
        ParticleData particle = particleBuffer[instanceID];

        // Apply global transformation: rotate 90 degrees around X-axis and invert X-axis
        float3 rotatedPosition = float3(
          -particle.position.x, // Invert X-axis
          -particle.position.z,  // Rotate around X-axis
          particle.position.y  // Rotate around X-axis
        );

        // Transform particle position to world space
        float4 localPosition = float4(rotatedPosition, 1.0);
        float4 worldPosition = mul(_ObjectToWorld, localPosition);

        // Get the camera-facing transformation
        float3 cameraRight = UNITY_MATRIX_IT_MV._m00_m01_m02;
        float3 cameraUp = UNITY_MATRIX_IT_MV._m10_m11_m12;

        // Calculate quad vertex position in world space
        float3 quadVertex = cameraRight * v.vertex.x * _PointSize + cameraUp * v.vertex.y * _PointSize;
        o.vertex = UnityObjectToClipPos(worldPosition + float4(quadVertex, 0.0));

        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        //o.color = float4(particle.intensity, 1 - particle.intensity, 0, 1);
        o.color = lerp(_ColorStart, _ColorEnd, saturate(particle.intensity));
        return o;
      }

      sampler2D _MainTex;

      float4 frag(v2f i) : SV_Target
      {
        return tex2D(_MainTex, i.uv) * i.color;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"
}
