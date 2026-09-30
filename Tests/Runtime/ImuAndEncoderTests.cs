using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Sensor;
using AGXUnity.Utils;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class ImuAndEncoderTests : AGXUnityFixture
  {
    [SetUp]
    public void SetupSensorScene()
    {
      SensorEnvironment.Instance.FieldType = SensorEnvironment.MagneticFieldType.Uniform;
      SensorEnvironment.Instance.MagneticFieldVector = Vector3.one;
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

    private static T AddSubcomponent<T>( ImuSensor imu ) where T : ImuSensorSubcomponent, new()
    {
      var subcomponent = new T();
      imu.Subcomponents.Add( subcomponent );
      return subcomponent;
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
    public void InitializesWithParentRigidBody()
    {
      var (rigidBody, imu) = CreateDefaultTestImu();
      AddSubcomponent<Accelerometer>( imu );
      AddSubcomponent<AGXUnity.Sensor.Gyroscope>( imu );

      TestUtils.InitializeAll();

      Assert.That( imu.Native, Is.Not.Null );
      Assert.That( imu.TrackedRigidBody, Is.EqualTo( rigidBody ) );
    }

    [Test]
    public void ExplicitMeasuredRigidBodyOverridesHierarchy()
    {
      var (parentRigidBody, imu) = CreateDefaultTestImu();
      var measuredObject = new GameObject( "Measured RB" );
      var measuredRigidBody = measuredObject.AddComponent<AGXUnity.RigidBody>();
      imu.MeasuredRigidBody = measuredRigidBody;
      AddSubcomponent<Accelerometer>( imu );

      TestUtils.InitializeAll();

      Assert.That( imu.Native, Is.Not.Null );
      Assert.That( imu.TrackedRigidBody, Is.EqualTo( measuredRigidBody ) );
      Assert.That( imu.TrackedRigidBody, Is.Not.EqualTo( parentRigidBody ) );
    }

    [UnityTest]
    public IEnumerator AccelerometerReportsGravity()
    {
      var (rigidBody, imu) = CreateDefaultTestImu();
      var accelerometer = AddSubcomponent<Accelerometer>( imu );
      rigidBody.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      TestUtils.InitializeAll();
      yield return TestUtils.SimulateSeconds( 0.1f );

      Assert.That( accelerometer.Output.magnitude,
                   Is.EqualTo( Mathf.Abs( Simulation.Instance.Gravity.y ) ).Within( 0.1f ) );
    }

    [UnityTest]
    public IEnumerator GyroscopeReportsAngularVelocity()
    {
      var (rigidBody, imu) = CreateDefaultTestImu();
      var gyroscope = AddSubcomponent<AGXUnity.Sensor.Gyroscope>( imu );
      rigidBody.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      rigidBody.AngularVelocity = Vector3.one;

      TestUtils.InitializeAll();
      yield return TestUtils.SimulateSeconds( 0.1f );

      Assert.That( gyroscope.Output.magnitude, Is.EqualTo( Vector3.one.magnitude ).Within( 0.1f ) );
    }

    [UnityTest]
    public IEnumerator MagnetometerReportsConfiguredField()
    {
      var (rigidBody, imu) = CreateDefaultTestImu();
      var magnetometer = AddSubcomponent<Magnetometer>( imu );
      rigidBody.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      TestUtils.InitializeAll();
      yield return TestUtils.SimulateSeconds( 0.1f );

      Assert.That( magnetometer.Output.magnitude, Is.EqualTo( Vector3.one.magnitude ).Within( 0.1f ) );
    }

    [Test]
    public void ConfigurationSynchronizesNativeModel()
    {
      var (rigidBody, imu) = CreateDefaultTestImu();
      var accelerometer = AddSubcomponent<Accelerometer>( imu );
      accelerometer.TriaxialRange.Mode = TriaxialRangeData.ConfigurationMode.IndividualAxisRanges;
      accelerometer.TriaxialRange.RangeX = new Vector2( -2, 3 );
      accelerometer.ZeroBias = new Vector3( 0.1f, 0.2f, 0.3f );

      TestUtils.InitializeAll();

      var model = typeof( Accelerometer )
        .GetField( "m_nativeModel", BindingFlags.Instance | BindingFlags.NonPublic )
        .GetValue( accelerometer ) as agxSensor.AccelerometerModel;
      Assert.That( model, Is.Not.Null );
      Assert.That( model.getZeroGBias().ToHandedVector3(), Is.EqualTo( accelerometer.ZeroBias ) );
      accelerometer.TriaxialRange.RangeX = new Vector2( -4, 5 );
      accelerometer.ZeroBias = new Vector3( 0.4f, 0.5f, 0.6f );
      Assert.That( model.getRange().getRangeX().lower(), Is.EqualTo( -4 ).Within( 1e-6 ) );
      Assert.That( model.getRange().getRangeX().upper(), Is.EqualTo( 5 ).Within( 1e-6 ) );
      Assert.That( model.getZeroGBias().ToHandedVector3(), Is.EqualTo( accelerometer.ZeroBias ) );
      Assert.That( imu.TrackedRigidBody, Is.EqualTo( rigidBody ) );
    }

    [Test]
    public void ImuWithoutMeasuredRigidBodyReportsWarning()
    {
      var imu = new GameObject( "Unattached IMU" ).AddComponent<ImuSensor>();
      AddSubcomponent<Accelerometer>( imu );
      LogAssert.Expect( LogType.Warning, new Regex( "No Rigidbody found.*IMU will be inactive" ) );

      TestUtils.InitializeAll();

      Assert.That( imu.Native, Is.Null );
    }

    [Test]
    public void EnableStateSynchronizesToNativeImu()
    {
      var (_, imu) = CreateDefaultTestImu();
      AddSubcomponent<Accelerometer>( imu );
      TestUtils.InitializeAll();

      imu.enabled = false;
      Assert.That( imu.Native.getEnable(), Is.False );
      imu.enabled = true;
      Assert.That( imu.Native.getEnable(), Is.True );
    }

    // Legacy OutputBuffer tests are retained as historical reference. The
    // current output contract is exposed by each configured subcomponent.
#if false
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
#endif

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
