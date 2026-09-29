using AGXUnityEditor.UIElements;
using AGXUnityEditor.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  [CustomPropertyDrawer( typeof( AGXUnity.Sensor.ILensDistortion ) )]
  [CanEditMultipleObjects]
  public class LensDistortionEditor : PropertyDrawer
  {
    private static AGXUnity.Sensor.CameraSensor.LensDistortionModel ToDistortionModel( SerializedProperty prop )
    {
      var selected = AGXUnity.Sensor.CameraSensor.LensDistortionModel.None;
      if ( prop.boxedValue is AGXUnity.Sensor.LensDistortionBrownConrady )
        selected = AGXUnity.Sensor.CameraSensor.LensDistortionModel.BrownConrady;
      return selected;
    }

    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var container = new VisualElement();

      var typeSelect = new EnumField( "Lens Distortion", ToDistortionModel(property) );
      typeSelect.TrackPropertyValue( property, prop => typeSelect.value = ToDistortionModel( prop ) );
      typeSelect.AddUnityAlignment();

      typeSelect.RegisterValueChangedCallback( type => {
        var val = (AGXUnity.Sensor.CameraSensor.LensDistortionModel)type.newValue;
        if ( val == AGXUnity.Sensor.CameraSensor.LensDistortionModel.None )
          property.boxedValue = null;
        else if ( val == AGXUnity.Sensor.CameraSensor.LensDistortionModel.BrownConrady )
          property.boxedValue = new AGXUnity.Sensor.LensDistortionBrownConrady();
        property.serializedObject.ApplyModifiedProperties();
      } );
      container.Add( typeSelect );

      var subContainer = new VisualElement();

      foreach ( var child in property.FindChildren() )
        subContainer.Add( new PropertyField( child ) );

      subContainer.style.marginLeft = 15;
      container.Add( subContainer );
      return container;
    }
  }

  [CustomPropertyDrawer( typeof( AGXUnity.Sensor.CameraAutofocuser ) )]
  [CanEditMultipleObjects]
  public class CameraAutofocuserEditor : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var container = new VisualElement();
      container.Add( new PropertyField( property.FindPropertyRelative( nameof( AGXUnity.Sensor.CameraAutofocuser.AutofocusMode ) ) ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_minimumFocusDistance" ) ) );
      return container;
    }
  }
}
