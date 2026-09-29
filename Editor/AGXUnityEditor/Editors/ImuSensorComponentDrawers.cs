using AGXUnity.Sensor;
using AGXUnityEditor.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  [CustomPropertyDrawer( typeof( ImuSensorSubcomponent ), true )]
  public class ImuSensorSubcomponentDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var container = new VisualElement();
      container.Add( new Label( property.managedReferenceFullTypename.Split( ' ' )[ 1 ].Split( '.' )[ ^1 ] ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_triaxialRange" ), "Sensor Measurement Range" ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_crossAxisSensitivity" ) ) );
      var useCrossAxisMatrix = property.FindPropertyRelative( "m_useCrossAxisSensitivityMatrix" );
      var crossAxisMatrix = new PropertyField( property.FindPropertyRelative( "m_crossAxisSensitivityMatrix" ), "Cross-Axis Sensitivity Matrix" );
      container.Add( new PropertyField( useCrossAxisMatrix, "Use Cross-Axis Sensitivity Matrix" ) );
      crossAxisMatrix.style.display = useCrossAxisMatrix.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
      container.TrackPropertyValue( useCrossAxisMatrix,
                                    changed => crossAxisMatrix.style.display = changed.boolValue ? DisplayStyle.Flex : DisplayStyle.None );
      container.Add( crossAxisMatrix );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_zeroBias" ), "Zero Bias" ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_attachmentPosition" ), "Attachment Position" ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "m_attachmentRotation" ), "Attachment Rotation" ) );
      container.Add( new PropertyField( property.FindPropertyRelative( "OutputFlags" ), "Output Values" ) );

      var runtimeOutput = new Label();
      runtimeOutput.style.marginTop = 4;
      container.Add( runtimeOutput );
      container.schedule.Execute( () => {
        if ( property.managedReferenceValue is ImuSensorSubcomponent subcomponent )
          runtimeOutput.text = $"Output: {subcomponent.Output}";
      } ).Every( 100 );

      var modifiers = new Foldout { text = "Modifiers" };
      AddOptionalVector3( modifiers,
                          property.FindPropertyRelative( "m_enableTotalGaussianNoise" ),
                          property.FindPropertyRelative( "m_totalGaussianNoise" ),
                          "Total Gaussian Noise" );
      var totalGaussianNoiseEnabled = property.FindPropertyRelative( "m_enableTotalGaussianNoise" );
      var totalGaussianNoiseMean = new PropertyField( property.FindPropertyRelative( "m_totalGaussianNoiseMean" ), "Total Gaussian Noise Mean" );
      totalGaussianNoiseMean.style.marginLeft = 16;
      totalGaussianNoiseMean.style.display = totalGaussianNoiseEnabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
      modifiers.TrackPropertyValue( totalGaussianNoiseEnabled,
                                    changed => totalGaussianNoiseMean.style.display = changed.boolValue ? DisplayStyle.Flex : DisplayStyle.None );
      modifiers.Add( totalGaussianNoiseMean );
      AddOptionalVector3( modifiers,
                          property.FindPropertyRelative( "m_enableSignalScaling" ),
                          property.FindPropertyRelative( "m_signalScaling" ),
                          "Signal Scaling" );
      AddOptionalVector3( modifiers,
                          property.FindPropertyRelative( "m_enableGaussianSpectralNoise" ),
                          property.FindPropertyRelative( "m_gaussianSpectralNoise" ),
                          "Gaussian Spectral Noise" );

      if ( property.managedReferenceValue is Gyroscope )
        AddOptionalVector3( modifiers,
                            property.FindPropertyRelative( "m_enableLinearAccelerationEffects" ),
                            property.FindPropertyRelative( "m_linearAccelerationEffects" ),
                            "Linear Acceleration Effects" );

      container.Add( modifiers );
      return container;
    }

    private static void AddOptionalVector3( VisualElement parent,
                                            SerializedProperty toggleProperty,
                                            SerializedProperty valueProperty,
                                            string label )
    {
      var toggle = new PropertyField( toggleProperty, label );
      var value = new PropertyField( valueProperty, label );
      value.style.marginLeft = 16;
      value.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
      toggle.TrackPropertyValue( toggleProperty,
                                 changed => value.style.display = changed.boolValue ? DisplayStyle.Flex : DisplayStyle.None );
      parent.Add( toggle );
      parent.Add( value );
    }
  }

  [CustomPropertyDrawer( typeof( TriaxialRangeData ) )]
  public class TriaxialRangeDataDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var container = new VisualElement();
      var mode = property.FindPropertyRelative( "m_mode" );
      var equalAxesRange = new PropertyField( property.FindPropertyRelative( "m_equalAxesRange" ), "XYZ Range" );
      var rangeX = new PropertyField( property.FindPropertyRelative( "m_rangeX" ), "X Axis Range" );
      var rangeY = new PropertyField( property.FindPropertyRelative( "m_rangeY" ), "Y Axis Range" );
      var rangeZ = new PropertyField( property.FindPropertyRelative( "m_rangeZ" ), "Z Axis Range" );

      container.Add( new PropertyField( mode, "Mode" ) );
      container.Add( equalAxesRange );
      container.Add( rangeX );
      container.Add( rangeY );
      container.Add( rangeZ );

      void UpdateVisibility( SerializedProperty changed )
      {
        var configuration = (TriaxialRangeData.ConfigurationMode)changed.enumValueIndex;
        equalAxesRange.style.display = configuration == TriaxialRangeData.ConfigurationMode.EqualAxisRanges ? DisplayStyle.Flex : DisplayStyle.None;
        var individual = configuration == TriaxialRangeData.ConfigurationMode.IndividualAxisRanges ? DisplayStyle.Flex : DisplayStyle.None;
        rangeX.style.display = individual;
        rangeY.style.display = individual;
        rangeZ.style.display = individual;
      }

      UpdateVisibility( mode );
      container.TrackPropertyValue( mode, UpdateVisibility );
      return container;
    }
  }
}
