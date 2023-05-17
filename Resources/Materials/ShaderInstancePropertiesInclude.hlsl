#ifndef CUSTOM_INSTANCING
#define CUSTOM_INSTANCING

// Set up instance properties buffer
UNITY_INSTANCING_BUFFER_START(Props)
  UNITY_DEFINE_INSTANCED_PROP(float4, _InstancedColor)
UNITY_INSTANCING_BUFFER_END(Props)
 
float4 _Color = (0.3,0.3,0.3,1);

// Returns _Color for this instance
void InstanceColor_float(float4 _BaseColor, out float4 Color)
{
  Color = _Color;
  Color = UNITY_ACCESS_INSTANCED_PROP(Props, _InstancedColor); // Override if there is an instanced prop
}

#endif