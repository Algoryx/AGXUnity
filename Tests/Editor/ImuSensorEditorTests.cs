using AGXUnity.Sensor;
using AGXUnityEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityTesting.Editor
{
  public class ImuSensorEditorTests
  {
    private GameObject m_gameObject;
    private ImuSensor m_sensor;

    [SetUp]
    public void SetUp()
    {
      m_gameObject = new GameObject( "ImuSensorEditorTest" );
      m_sensor = m_gameObject.AddComponent<ImuSensor>();
    }

    [TearDown]
    public void TearDown()
    {
      Object.DestroyImmediate( m_gameObject );
    }

    [Test]
    public void InspectorExposesMeasuredBodyAndSubcomponents()
    {
      var editor = UnityEditor.Editor.CreateEditor( m_sensor );
      try {
        Assert.That( editor, Is.TypeOf<AGXUnityEditor.Editors.ImuSensorEditor>() );
        var inspector = ( editor as AGXUnityEditor.Editors.ImuSensorEditor ).CreateInspectorGUI();
        Assert.That( inspector.Query<PropertyField>().ToList().Count, Is.EqualTo( 2 ) );
      }
      finally {
        Object.DestroyImmediate( editor );
      }
    }

    [Test]
    public void TopMenuCreatesConfiguredImu()
    {
      var created = TopMenu.IMU( new MenuCommand( null ) );
      try {
        var imu = created.GetComponent<ImuSensor>();
        Assert.That( imu, Is.Not.Null );
        Assert.That( imu.Subcomponents.Count, Is.EqualTo( 2 ) );
        Assert.That( imu.Subcomponents[ 0 ], Is.TypeOf<Accelerometer>() );
        Assert.That( imu.Subcomponents[ 1 ], Is.TypeOf<AGXUnity.Sensor.Gyroscope>() );
      }
      finally {
        Object.DestroyImmediate( created );
      }
    }

    [Test]
    public void SubcomponentDrawerShowsConditionalImportFields()
    {
      m_sensor.Subcomponents.Add( new Accelerometer() );
      var serializedSensor = new SerializedObject( m_sensor );
      var subcomponent = serializedSensor.FindProperty( "m_subcomponents" )
                                          .FindPropertyRelative( "m_backing" )
                                          .GetArrayElementAtIndex( 0 );
      var drawer = new AGXUnityEditor.Editors.ImuSensorSubcomponentDrawer();

      var defaultInspector = drawer.CreatePropertyGUI( subcomponent );
      Assert.That( FindPropertyField( defaultInspector, "Cross-Axis Sensitivity Matrix" ).style.display.value, Is.EqualTo( DisplayStyle.None ) );
      Assert.That( FindPropertyField( defaultInspector, "Total Gaussian Noise Mean" ).style.display.value, Is.EqualTo( DisplayStyle.None ) );

      subcomponent.FindPropertyRelative( "m_useCrossAxisSensitivityMatrix" ).boolValue = true;
      subcomponent.FindPropertyRelative( "m_enableTotalGaussianNoise" ).boolValue = true;
      serializedSensor.ApplyModifiedPropertiesWithoutUndo();

      var configuredInspector = drawer.CreatePropertyGUI( subcomponent );
      Assert.That( FindPropertyField( configuredInspector, "Cross-Axis Sensitivity Matrix" ).style.display.value, Is.EqualTo( DisplayStyle.Flex ) );
      Assert.That( FindPropertyField( configuredInspector, "Total Gaussian Noise Mean" ).style.display.value, Is.EqualTo( DisplayStyle.Flex ) );
    }

    private static PropertyField FindPropertyField( VisualElement root, string label )
    {
      foreach ( var field in root.Query<PropertyField>().ToList() )
        if ( field.label == label )
          return field;
      Assert.Fail( $"Unable to find property field '{label}'." );
      return default;
    }
  }
}
