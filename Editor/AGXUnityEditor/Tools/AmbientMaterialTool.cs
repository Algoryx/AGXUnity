using AGXUnity.Sensor;
using UnityEditor;
using UnityEngine;
using AmbType = AGXUnity.Sensor.AmbientMaterial.ConfigurationType;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( AmbientMaterial ) )]
  class AmbientMaterialTool : CustomTargetTool
  {
    public AmbientMaterial AmbientMaterial
    {
      get
      {
        return Targets[ 0 ] as AmbientMaterial;
      }
    }

    public AmbientMaterialTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var t = (AmbType)EditorGUILayout.EnumPopup( "Ambient Type", AmbientMaterial.AmbientType );
      AmbientMaterial.AmbientType = t;
      if ( t == AmbType.Air || t == AmbType.Fog )
        AmbientMaterial.Visibility = Mathf.Max( EditorGUILayout.FloatField( "Visibility", AmbientMaterial.Visibility ), 0.0001f );
      if ( t == AmbType.Rainfall || t == AmbType.Snowfall )
        AmbientMaterial.Rate = Mathf.Max( EditorGUILayout.FloatField( "Rate", AmbientMaterial.Rate ), 0.0f );
      if ( t == AmbType.Fog || t == AmbType.Snowfall )
        AmbientMaterial.Wavelength = Mathf.Max( EditorGUILayout.FloatField( "Wavelength", AmbientMaterial.Wavelength ), 0.0f );
      if ( t == AmbType.Fog )
        AmbientMaterial.Maritimeness = EditorGUILayout.Slider( "Maritimeness", AmbientMaterial.Maritimeness, 0.0f, 1.0f );
      if ( t == AmbType.Rainfall )
        AmbientMaterial.Tropicalness = EditorGUILayout.Slider( "Tropicalness", AmbientMaterial.Tropicalness, 0.0f, 1.0f );
    }
  }
}
