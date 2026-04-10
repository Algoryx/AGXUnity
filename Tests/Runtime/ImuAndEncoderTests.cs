using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Sensor;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

using GOList = System.Collections.Generic.List<UnityEngine.GameObject>;

namespace AGXUnityTesting.Runtime
{
  public class ImuAndEncoderTests : AGXUnityFixture
  {
    private GOList m_keep = new GOList();

    [OneTimeSetUp]
    public void SetupSensorScene()
    {
      Simulation.Instance.PreIntegratePositions = true;
      m_keep.Add( Simulation.Instance.gameObject );

      SensorEnvironment.Instance.FieldType = SensorEnvironment.MagneticFieldType.Uniform;
      SensorEnvironment.Instance.MagneticFieldVector = Vector3.one;
      m_keep.Add( SensorEnvironment.Instance.gameObject );
    }

    [UnityTearDown]
    public IEnumerator CleanSensorScene()
    {
#if UNITY_2022_2_OR_NEWER
      var objects = Object.FindObjectsByType<ScriptComponent>( FindObjectsSortMode.None );
#else
      var objects = Object.FindObjectsOfType<ScriptComponent>( );
#endif
      GOList toDestroy = new GOList();

      foreach ( var obj in objects ) {
        var root = obj.gameObject;
        while ( root.transform.parent != null )
          root = root.transform.parent.gameObject;
        if ( !m_keep.Contains( root ) )
          toDestroy.Add( root );
      }

      yield return TestUtils.DestroyAndWait( toDestroy.ToArray() );
    }

    [OneTimeTearDown]
    public void TearDownSensorScene()
    {
#if UNITY_2022_2_OR_NEWER
      var geoms = Object.FindObjectsByType<Shape>( FindObjectsSortMode.None );
#else
      var geoms = Object.FindObjectsOfType<Shape>( );
#endif      

      foreach ( var g in geoms )
        GameObject.Destroy( g.gameObject );

      GameObject.Destroy( SensorEnvironment.Instance.gameObject );
    }

    private (AGXUnity.RigidBody, ImuSensor) CreateDefaultTestImu( Vector3 position = default )
    {
      var rbGO = new GameObject("RB");
      rbGO.transform.position = position;
      var rbComp = rbGO.AddComponent<AGXUnity.RigidBody>();

      var imuGO = new GameObject("IMU");
      imuGO.transform.position = position;
      imuGO.transform.parent = rbGO.transform;
      var imuComp = imuGO.AddComponent<ImuSensor>();

      return (rbComp, imuComp);
    }

    private AGXUnity.Constraint CreateTestHinge( Vector3 position = default )
    {
      var go1 = Factory.Create< AGXUnity.RigidBody >( Factory.Create<Box>() );
      go1.transform.position = new Vector3( 0, 2, 0 );
      go1.GetComponent<AGXUnity.RigidBody>().MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      var go2 = Factory.Create< AGXUnity.RigidBody >( Factory.Create<Box>() );
      var constraintGO = Factory.Create( ConstraintType.Hinge, Vector3.zero, Quaternion.identity, go1.GetComponent<AGXUnity.RigidBody>(), go2.GetComponent<AGXUnity.RigidBody>() );

      return constraintGO.GetComponent<AGXUnity.Constraint>();
    }


    [Test]
    public void TestCreateImu()
    {
      var (_, imu) = CreateDefaultTestImu();

      TestUtils.InitializeAll();

      Assert.NotNull( imu.Native, "Couldn't create IMU" );
    }

    [UnityTest]
    public IEnumerator TestAccelerometerOutput()
    {
      var (rb, imu) = CreateDefaultTestImu();

      var g = Simulation.Instance.Gravity.y;

      rb.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      TestUtils.InitializeAll();

      yield return TestUtils.SimulateSeconds( 0.1f );

      Assert.That( imu.OutputBuffer[ 1 ], Is.EqualTo( Mathf.Abs( g ) ).Within( 0.001f ), "Test value should be close to g" );
    }

    [UnityTest]
    public IEnumerator TestGyroscopeOutput()
    {
      var (rb, imu) = CreateDefaultTestImu();

      rb.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      rb.AngularVelocity = Vector3.one;

      TestUtils.InitializeAll();

      yield return TestUtils.SimulateSeconds( 0.1f );

      Assert.That( Mathf.Abs( (float)imu.OutputBuffer[ 5 ] ), Is.EqualTo( 1 ).Within( 0.01f ), "Test value should be 1 like the change in rotation" );
    }

    [UnityTest]
    public IEnumerator TestMagnetometerOutput()
    {
      var (rb, imu) = CreateDefaultTestImu();

      rb.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      rb.AngularVelocity = Vector3.one;

      TestUtils.InitializeAll();

      yield return TestUtils.Step();
      yield return TestUtils.Step();

      Assert.That( Mathf.Abs( (float)imu.OutputBuffer[ 8 ] ), Is.EqualTo( 1 ).Within( 0.001f ), "Test value should be 1 as the magnetic field was set up to be 1 in each direction" );
    }

    [UnityTest]
    public IEnumerator TestEncoderOutput()
    {
      var constraint = CreateTestHinge();
      var encoder = constraint.gameObject.AddComponent<EncoderSensor>();
      encoder.OutputSpeed = true;
      var controller = constraint.GetController<AGXUnity.TargetSpeedController>();
      controller.Speed = 1;
      controller.Enable = true;

      TestUtils.InitializeAll();

      yield return TestUtils.SimulateSeconds( 0.2f );

      Assert.That( Mathf.Abs( (float)encoder.SpeedBuffer ), Is.EqualTo( 1 ).Within( 0.01f ), "Value should be close to target speed controller speed" );
    }

    [UnityTest]
    public IEnumerator TestOdometerOutput()
    {
      var constraint = CreateTestHinge();
      var odometer = constraint.gameObject.AddComponent<OdometerSensor>();
      var controller = constraint.GetController<AGXUnity.TargetSpeedController>();
      controller.Speed = 1;
      controller.Enable = true;
      TestUtils.InitializeAll();

      yield return TestUtils.SimulateSeconds( 0.2f );

      Assert.That( Mathf.Abs( (float)odometer.OutputBuffer ), Is.GreaterThan( 0.01 ), "Testing odometer output" );
    }

    [UnityTest]
    public IEnumerator TestOdometerDisableToggling()
    {
      var constraint = CreateTestHinge();
      var odometer = constraint.gameObject.AddComponent<OdometerSensor>();
      var controller = constraint.GetController<TargetSpeedController>();
      controller.Speed = 1;
      controller.Enable = true;
      TestUtils.InitializeAll();

      odometer.enabled = false;

      yield return TestUtils.Step();
      yield return TestUtils.Step();

      Assert.That( Mathf.Abs( (float)odometer.OutputBuffer ), Is.EqualTo( 0.0 ), "Should be 0 when disabled" );

      odometer.enabled = true;

      yield return TestUtils.Step();
      yield return TestUtils.Step();

      Assert.That( Mathf.Abs( (float)odometer.OutputBuffer ), Is.GreaterThan( 0.01 ), "Testing odometer output" );
    }

    [UnityTest]
    public IEnumerator TestEncoderDisableToggling()
    {
      var constraint = CreateTestHinge();
      var encoder = constraint.gameObject.AddComponent<EncoderSensor>();
      var controller = constraint.GetController<TargetSpeedController>();
      controller.Speed = 1;
      controller.Enable = true;

      encoder.enabled = false;
      TestUtils.InitializeAll();

      yield return TestUtils.Step();
      yield return TestUtils.Step();

      Assert.That( Mathf.Abs( (float)encoder.PositionBuffer ), Is.EqualTo( 0.0 ), "Should be 0 when disabled" );

      encoder.enabled = true;

      yield return TestUtils.Step();
      yield return TestUtils.Step();

      Assert.That( Mathf.Abs( (float)encoder.PositionBuffer ), Is.GreaterThan( 0.01 ), "Testing odometer output" );
    }
  }
}
