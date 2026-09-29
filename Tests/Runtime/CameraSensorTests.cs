using AGXUnity.Sensor;
using NUnit.Framework;
using UnityEngine;

namespace AGXUnityTesting.Runtime
{
  public class CameraSensorTests : AGXUnityFixture
  {
    private CameraSensor CreateCameraSensor()
    {
      var gameObject = new GameObject( "CameraSensorTest" );
      var sensor = gameObject.AddComponent<CameraSensor>();
      sensor.FocalLength = 0.05f;
      sensor.Autofocus = false;
      sensor.MinimumFocusDistance = 0.2f;
      sensor.FocusDistance = 4.0f;
      sensor.FStop = 2.8f;
      sensor.SensorSize = new Vector2( 0.036f, 0.024f );
      sensor.ISO = 200;
      sensor.ShutterSpeed = 0.01f;
      sensor.Resolution = new Vector2Int( 64, 32 );
      return sensor;
    }

    [Test]
    public void ScalarPropertiesSynchronizeNativeCamera()
    {
      var sensor = CreateCameraSensor();
      sensor.GetInitialized<CameraSensor>();

      var lens = sensor.NativeLens as agxSensor.CameraLensSingleElement;
      var detector = sensor.NativePhotodetector as agxSensor.CameraCMOSSensor;

      Assert.That( lens.getFocalLength(), Is.EqualTo( 0.05 ).Within( 1.0e-6 ) );
      Assert.That( lens.getFocusDistance(), Is.EqualTo( 4.0 ).Within( 1.0e-6 ) );
      Assert.That( lens.getFStop(), Is.EqualTo( 2.8 ).Within( 1.0e-6 ) );
      Assert.That( lens.isAutofocusEnabled(), Is.False );
      Assert.That( detector.getSize().x, Is.EqualTo( 0.036 ).Within( 1.0e-6 ) );
      Assert.That( detector.getSize().y, Is.EqualTo( 0.024 ).Within( 1.0e-6 ) );
      Assert.That( detector.getISO(), Is.EqualTo( 200.0 ).Within( 1.0e-6 ) );
      Assert.That( detector.getShutterSpeed(), Is.EqualTo( 0.01 ).Within( 1.0e-6 ) );
      Assert.That( detector.getResolution().x, Is.EqualTo( 64 ) );
      Assert.That( detector.getResolution().y, Is.EqualTo( 32 ) );
    }

    [Test]
    public void CollectionsHotSynchronizeAfterInitialization()
    {
      var sensor = CreateCameraSensor();
      sensor.GetInitialized<CameraSensor>();

      var outputA = new ColorOutput();
      var outputB = new ColorOutput();
      var illuminatorA = new Illuminator();
      var illuminatorB = new Illuminator { IlluminatorType = Illuminator.Type.Point };

      sensor.Outputs.Add( outputA );
      sensor.Outputs.Add( outputB );
      sensor.Illuminators.Add( illuminatorA );
      sensor.Illuminators.Add( illuminatorB );

      Assert.That( outputA.Native, Is.Not.Null );
      Assert.That( outputB.Native, Is.Not.Null );
      Assert.That( sensor.Native.getOutputHandler().getNumChildren(), Is.EqualTo( 2 ) );
      Assert.That( sensor.Native.getModel().getIlluminators().Count, Is.EqualTo( 2 ) );

      sensor.Outputs.Move( 1, 0 );
      sensor.Illuminators.Move( 1, 0 );
      Assert.That( sensor.Outputs[ 0 ], Is.SameAs( outputB ) );
      Assert.That( sensor.Illuminators[ 0 ], Is.SameAs( illuminatorB ) );

      illuminatorA.IlluminatorType = Illuminator.Type.Point;
      Assert.That( illuminatorA.Native, Is.TypeOf<agxSensor.CameraPointLight>() );

      Assert.That( sensor.Outputs.Remove( outputA ), Is.True );
      Assert.That( sensor.Illuminators.Remove( illuminatorA ), Is.True );
      Assert.That( outputA.Native, Is.Null );
      Assert.That( illuminatorA.Native, Is.Null );
      Assert.That( sensor.Native.getOutputHandler().getNumChildren(), Is.EqualTo( 1 ) );
      Assert.That( sensor.Native.getModel().getIlluminators().Count, Is.EqualTo( 1 ) );
    }
  }
}
