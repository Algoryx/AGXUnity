using AGXUnity.Sensor;
using AGXUnityEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class CameraSensorEditorTests
  {
    private GameObject m_gameObject;
    private CameraSensor m_sensor;

    [SetUp]
    public void SetUp()
    {
      m_gameObject = new GameObject( "CameraSensorEditorTest" );
      m_sensor = m_gameObject.AddComponent<CameraSensor>();
    }

    [TearDown]
    public void TearDown()
    {
      Object.DestroyImmediate( m_gameObject );
    }

    [Test]
    public void UsesThinDefaultInspectorEditor()
    {
      var editor = UnityEditor.Editor.CreateEditor( m_sensor );
      try {
        //Assert.That( editor, Is.TypeOf<AGXUnitySensorCameraSensorEditor>() );
        Assert.That( editor, Is.Not.InstanceOf<InspectorEditor>() );
      }
      finally {
        Object.DestroyImmediate( editor );
      }
    }

    [Test]
    public void SerializedConfigurationSynchronizesUnityCameraWithoutNativeState()
    {
      var serializedSensor = new SerializedObject( m_sensor );
      serializedSensor.FindProperty( "m_focalLength" ).floatValue = 0.05f;
      serializedSensor.FindProperty( "m_focusDistance" ).floatValue = 3.0f;
      serializedSensor.FindProperty( "m_fStop" ).floatValue = 2.8f;
      serializedSensor.FindProperty( "m_sensorSize" ).vector2Value = new Vector2( 0.036f, 0.024f );
      serializedSensor.FindProperty( "m_iso" ).intValue = 200;
      serializedSensor.FindProperty( "m_shutterSpeed" ).floatValue = 0.01f;
      serializedSensor.FindProperty( "m_resolution" ).vector2IntValue = new Vector2Int( 320, 200 );
      serializedSensor.ApplyModifiedPropertiesWithoutUndo();

      Assert.DoesNotThrow( m_sensor.SynchronizeNative );

      var camera = m_sensor.CameraComponent;
      Assert.That( camera.focalLength, Is.EqualTo( 50.0f ).Within( 1.0e-5f ) );
      Assert.That( camera.focusDistance, Is.EqualTo( 3.0f ).Within( 1.0e-5f ) );
      Assert.That( camera.aperture, Is.EqualTo( 2.8f ).Within( 1.0e-5f ) );
      Assert.That( camera.sensorSize, Is.EqualTo( new Vector2( 36.0f, 24.0f ) ) );
      Assert.That( camera.iso, Is.EqualTo( 200 ) );
      Assert.That( camera.shutterSpeed, Is.EqualTo( 0.01f ).Within( 1.0e-5f ) );
      Assert.That( m_sensor.Output.width, Is.EqualTo( 320 ) );
      Assert.That( m_sensor.Output.height, Is.EqualTo( 200 ) );
    }

    [Test]
    public void CollectionApiRejectsDuplicateAndSharedEntries()
    {
      var illuminator = new Illuminator();
      var output = new ColorOutput();

      m_sensor.Illuminators.Add( illuminator );
      m_sensor.Outputs.Add( output );

      Assert.Throws<System.InvalidOperationException>( () => m_sensor.Illuminators.Add( illuminator ) );
      Assert.Throws<System.InvalidOperationException>( () => m_sensor.Outputs.Add( output ) );

      var other = new GameObject( "OtherCameraSensor" ).AddComponent<CameraSensor>();
      try {
        Assert.Throws<System.InvalidOperationException>( () => other.Illuminators.Add( illuminator ) );
        Assert.Throws<System.InvalidOperationException>( () => other.Outputs.Add( output ) );
      }
      finally {
        Object.DestroyImmediate( other.gameObject );
      }
    }

    [Test]
    public void SubcomponentListAddDefaultPreservesFieldInitializers()
    {
      m_sensor.Illuminators.AddDefault();
      m_sensor.Outputs.AddDefault();

      Assert.That( m_sensor.Illuminators.Count, Is.EqualTo( 1 ) );
      Assert.That( m_sensor.Illuminators[ 0 ].IlluminatorType, Is.EqualTo( Illuminator.Type.Spot ) );
      Assert.That( m_sensor.Illuminators[ 0 ].Color, Is.EqualTo( Color.white ) );
      Assert.That( m_sensor.Illuminators[ 0 ].Intensity, Is.EqualTo( 10.0f ) );
      Assert.That( m_sensor.Illuminators[ 0 ].ConeAngles, Is.EqualTo( new Vector2( 20.0f, 25.0f ) ) );

      Assert.That( m_sensor.Outputs.Count, Is.EqualTo( 1 ) );
      Assert.That( m_sensor.Outputs[ 0 ].Resolution, Is.EqualTo( new Vector2Int( 128, 128 ) ) );
      Assert.That( m_sensor.Outputs[ 0 ].ChannelCount, Is.EqualTo( 4 ) );
      Assert.That( m_sensor.Outputs[ 0 ].Gamma, Is.EqualTo( 1.0f ) );
    }
  }
}
