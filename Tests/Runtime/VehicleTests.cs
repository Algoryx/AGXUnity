using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Rendering;
using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace AGXUnityTesting.Runtime
{
  public class VehicleTests
  {
    private GameObject CreateShape<T>( Vector3 transform = new Vector3() )
      where T : Shape
    {
      var go = Factory.Create<T>();
      if ( go == null )
        return null;

      go.transform.localPosition = transform;

      AGXUnity.Rendering.ShapeVisual.Create( go.GetComponent<T>() );

      return go;
    }

    struct BasicVehicle
    {
      public RigidBody Chassis;
      public RigidBody LeftWheel;
      public RigidBody RightWheel;
      public WheelJoint LeftJoint;
      public WheelJoint RightJoint;
    }

    private BasicVehicle CreateBasicVehicle( System.Func<RigidBody, RigidBody, int, WheelJoint> jointCreateFunc = null )
    {
      var chassisBox = CreateShape<Box>();
      var chassis = Factory.Create<RigidBody>();
      chassis.GetComponent<RigidBody>().MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      chassisBox.transform.parent = chassis.transform;

      var leftWheelCyl = CreateShape<Cylinder>();
      leftWheelCyl.GetComponent<Cylinder>().Height = 0.2f;
      leftWheelCyl.transform.rotation = Quaternion.Euler( 0, 0, -90 );
      var leftWheel = Factory.Create<RigidBody>();
      leftWheelCyl.transform.parent = leftWheel.transform;
      leftWheel.transform.position = new Vector3( -0.6f, -0.5f, 0 );

      var rightWheelCyl = GameObject.Instantiate( leftWheelCyl );
      var rightWheel = Factory.Create<RigidBody>();
      rightWheelCyl.transform.parent = rightWheel.transform;
      rightWheel.transform.position = new Vector3( 0.6f, -0.5f, 0 ); ;

      if ( jointCreateFunc == null )
        jointCreateFunc = ( wheel, chassis, _ ) => WheelJoint.Create( wheel, chassis, Vector3.up, Vector3.right );

      var left = jointCreateFunc( leftWheel.GetComponent<RigidBody>(), chassis.GetComponent<RigidBody>(), 0 );
      var right = jointCreateFunc( rightWheel.GetComponent<RigidBody>(), chassis.GetComponent<RigidBody>(), 1 );

      return new BasicVehicle
      {
        Chassis = chassis.GetComponent<RigidBody>(),
        LeftWheel = leftWheel.GetComponent<RigidBody>(),
        RightWheel = rightWheel.GetComponent<RigidBody>(),
        LeftJoint = left,
        RightJoint = right,
      };
    }

    [OneTimeSetUp]
    public void EnableLogging()
    {
      Simulation.Instance.LogToUnityConsole = true;
      Simulation.Instance.AGXUnityLogLevel = LogLevel.Debug;
    }

    [UnityTearDown]
    public IEnumerator CleanVehicleScene()
    {
#if UNITY_2022_2_OR_NEWER
      var objects = Object.FindObjectsByType<ScriptComponent>( FindObjectsSortMode.None );
#else
      var objects = Object.FindObjectsOfType<ScriptComponent>( );
#endif

      var toDelete = objects.Where(x => x is not Simulation).Select(x => x.gameObject).ToArray();

      yield return TestUtils.DestroyAndWait( toDelete );
    }

    [Test]
    public void TestBasicVehicleInitializes()
    {
      var vehicle = CreateBasicVehicle();

      Assert.IsNotNull( vehicle.LeftJoint );
      Assert.IsNotNull( vehicle.RightJoint );

      TestUtils.InitializeAll();
    }

    [Test]
    public void TestNonOrthogonalWheelFails()
    {
      System.Func<RigidBody,RigidBody, int, WheelJoint> createInvalidJoints = (wheel, chassis, _) => {
        LogAssert.Expect( LogType.Error, new Regex( ".*orthogonal.*" ) );
        return WheelJoint.Create(wheel,chassis,Vector3.up, new Vector3(1,1,0));
      };

      var vehicle = CreateBasicVehicle( createInvalidJoints );
      Assert.Null( vehicle.LeftJoint );
      Assert.Null( vehicle.RightJoint );
    }

    [Test]
    public void TestZeroLengthAxesFails()
    {
      System.Func<RigidBody,RigidBody, int, WheelJoint> createInvalidJoints = (wheel, chassis, _) => {
        LogAssert.Expect( LogType.Error, new Regex( ".*non-zero.*" ) );
        return WheelJoint.Create(wheel,chassis,Vector3.up, Vector3.zero);
      };

      var vehicle = CreateBasicVehicle( createInvalidJoints );
      Assert.Null( vehicle.LeftJoint );
      Assert.Null( vehicle.RightJoint );
    }

    [UnityTest]
    public IEnumerator TestWheelPositionIsLocked()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      var comparer = new Vector3EqualityComparer(1e-4f);

      var prePosLeft = vehicle.LeftWheel.transform.position;
      var prePosRight = vehicle.RightWheel.transform.position;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftWheel.transform.position, Is.EqualTo( prePosLeft ).Using( comparer ), "Position should be locked for the left wheel" );
      Assert.That( vehicle.RightWheel.transform.position, Is.EqualTo( prePosRight ).Using( comparer ), "Position should be locked for the right wheel" );
    }

    [UnityTest]
    public IEnumerator TestDisableWheelJoint()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      vehicle.LeftJoint.enabled = false;

      yield return TestUtils.SimulateSeconds( 1.0f );

      Assert.That( vehicle.LeftWheel.transform.position.y, Is.LessThan( -2.0f ), "Wheel should fall when joint is disabled" );
      Assert.That( vehicle.LeftWheel.LinearVelocity.y, Is.LessThan( -2.0f ), "Wheel should fall when joint is disabled" );
    }

    [UnityTest]
    public IEnumerator TestWheelAxisRotation()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      var comparer = new Vector3EqualityComparer(1e-4f);

      vehicle.LeftWheel.AngularVelocity = Vector3.right;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Wheel ), Is.EqualTo( 1.0f ), "Wheel should rotate given an initial angular velocity" );
    }

    [UnityTest]
    public IEnumerator TestSteerAxisRotation()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      var comparer = new Vector3EqualityComparer(1e-4f);

      vehicle.LeftWheel.AngularVelocity = Vector3.up;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Steering ), Is.EqualTo( 1.0f ), "Wheel should rotate given an initial angular velocity" );
    }

    [UnityTest]
    public IEnumerator TestWheelAxisControllers()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      var motor = vehicle.LeftJoint.GetController<TargetSpeedController>( WheelJoint.WheelDimension.Wheel );

      motor.Enable = true;
      motor.Speed = 1;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Wheel ), Is.EqualTo( 1.0f ), "Wheel should rotate given a non zero speed passed to an enabled TargetSpeedController" );

      var wheelLock = vehicle.LeftJoint.GetController<LockController>( WheelJoint.WheelDimension.Wheel );

      motor.Enable = false;

      wheelLock.Enable = true;
      wheelLock.Position = vehicle.LeftJoint.GetCurrentAngle( WheelJoint.WheelDimension.Wheel );

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Wheel ), Is.EqualTo( 0.0f ).Within( 1e-6f ), "Wheel should not rotate given an enabled LockController" );
    }

    [UnityTest]
    public IEnumerator TestSteerAxisControllers()
    {
      var vehicle = CreateBasicVehicle();
      TestUtils.InitializeAll();

      var motor = vehicle.LeftJoint.GetController<TargetSpeedController>( WheelJoint.WheelDimension.Steering );
      var range = vehicle.LeftJoint.GetController<RangeController>( WheelJoint.WheelDimension.Steering );

      motor.Enable = true;
      motor.Speed = 1;
      motor.Compliance = 1e-6f;

      range.Enable = true;
      range.Range = new RangeReal( -1, 1 );
      range.Compliance = 1e-15f;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Steering ), Is.EqualTo( 1.0f ), "Wheel should rotate given a non zero speed passed to an enabled TargetSpeedController" );

      yield return TestUtils.SimulateSeconds( 1.0f );

      Assert.That( vehicle.LeftJoint.GetCurrentSpeed( WheelJoint.WheelDimension.Steering ), Is.EqualTo( 0.0f ).Within( 1e-6f ), "Wheel should rotate not rotate as it hits the range of a RangeController" );
      Assert.That( vehicle.LeftJoint.GetCurrentAngle( WheelJoint.WheelDimension.Steering ), Is.EqualTo( 1 ), "Wheel rotation should match that of the the RangeController as it hits the limit" );
    }

    [Test]
    public void TestSteeringInitializes()
    {
      var vehicle = CreateBasicVehicle();

      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();
      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      Assert.True( steering.GetInitialized() );
    }

    [Test]
    public void TestNonCommonSteeringAxisFails()
    {
      Func<RigidBody,RigidBody,int,WheelJoint> createJointFunc = (wheel,chassis,idx) => {
        if(idx == 0)
          return WheelJoint.Create(wheel,chassis,Vector3.up,Vector3.right);
        else
          return WheelJoint.Create(wheel,chassis,new Vector3(-1,1,0),new Vector3(1,1,0));
      };

      var vehicle = CreateBasicVehicle(createJointFunc);

      LogAssert.Expect( LogType.Error, new Regex( ".*common steering axis.*" ) );
      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();
      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      Assert.False( steering.GetInitialized() );
    }

    [Test]
    public void TestNullWheelSteeringFails()
    {
      Func<RigidBody,RigidBody,int,WheelJoint> createJointFunc = (wheel,chassis,idx) => {
        if(idx == 0)
          return WheelJoint.Create(wheel,chassis,Vector3.up,Vector3.right);
        else
          return null;
      };

      var vehicle = CreateBasicVehicle(createJointFunc);

      LogAssert.Expect( LogType.Error, new Regex( ".*both WheelJoints.*" ) );
      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();

      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      Assert.False( steering.GetInitialized() );
    }

    [Test]
    public void TestNonCommonChassisFails()
    {
      var altChassis = Factory.Create<RigidBody>().GetComponent<RigidBody>();

      Func<RigidBody,RigidBody,int,WheelJoint> createJointFunc = (wheel,chassis,idx) => {
        if(idx == 0)
          return WheelJoint.Create(wheel,chassis,Vector3.up,Vector3.right);
        else
          return WheelJoint.Create(wheel,altChassis,Vector3.up,Vector3.right);
      };

      var vehicle = CreateBasicVehicle(createJointFunc);

      LogAssert.Expect( LogType.Error, new Regex( ".*common Connected.*" ) );
      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();

      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      Assert.False( steering.GetInitialized() );
    }

    [UnityTest]
    public IEnumerator TestSteeringSetAngle()
    {
      var vehicle = CreateBasicVehicle();

      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();
      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      TestUtils.InitializeAll();

      steering.SteeringAngle = 0.5f;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentAngle(), Is.GreaterThan( 0.0f ), "Steering constraint should set wheel angle" );
      Assert.That( vehicle.RightJoint.GetCurrentAngle(), Is.GreaterThan( 0.0f ), "Steering constraint should set wheel angle" );
      Assert.That( vehicle.LeftJoint.GetCurrentAngle(), Is.GreaterThan( vehicle.RightJoint.GetCurrentAngle() ), "Turning right should yield a higher angle on the left wheel than the right" );
    }

    [UnityTest]
    public IEnumerator TestEnableDisableSteering()
    {
      var vehicle = CreateBasicVehicle();

      var steering = vehicle.Chassis.gameObject.AddComponent<Steering>();
      steering.LeftWheel = vehicle.LeftJoint;
      steering.RightWheel = vehicle.RightJoint;

      TestUtils.InitializeAll();

      steering.enabled = false;
      steering.SteeringAngle = 0.5f;

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentAngle(), Is.EqualTo( 0.0f ), "Disabled steering constraint should not affect wheel angle" );

      steering.enabled = true;
      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( vehicle.LeftJoint.GetCurrentAngle(), Is.GreaterThan( 0.1f ), "Re-enabled steering constraint should affect wheel angle" );
    }

    private (RigidBody, TrackWheel) CreateWheel( Vector3 pos, TrackWheelModel model = TrackWheelModel.Idler )
    {
      var frontWheel = Factory.Create<RigidBody>();
      var wheelCollider = Factory.Create<Cylinder>();
      var cylinderComp = wheelCollider.GetComponent<Cylinder>();
      ShapeVisual.Create( cylinderComp ).transform.parent = wheelCollider.transform;
      cylinderComp.Radius = 0.2f;
      wheelCollider.transform.parent = frontWheel.transform;

      frontWheel.transform.rotation = Quaternion.FromToRotation( Vector3.up, Vector3.forward );
      frontWheel.transform.position = pos;

      var trackWheel = TrackWheel.Create(wheelCollider);
      trackWheel.Model = model;

      var constraint = Constraint.Create( ConstraintType.Hinge, new ConstraintFrame( frontWheel,Vector3.zero, Quaternion.FromToRotation( Vector3.up, Vector3.forward ) ), new ConstraintFrame() );
      constraint.transform.parent = frontWheel.transform;
      if ( model == TrackWheelModel.Sprocket ) {
        var motor = constraint.GetController<TargetSpeedController>();
        motor.Speed = 1;
        motor.Enable = true;
      }

      return (frontWheel.GetComponent<RigidBody>(), trackWheel);
    }

    private Track CreateSimpleTrack()
    {
      GameObject root = new GameObject("Simple Track");

      var (frontRB, frontWheel) = CreateWheel( Vector3.left, TrackWheelModel.Sprocket );
      var (backRB, backWheel) = CreateWheel( Vector3.right );

      var track = Factory.Create<Track>();
      var trackComp = track.GetComponent<Track>();
      track.AddComponent<TrackRenderer>();
      trackComp.Width = 1;

      trackComp.Add( frontWheel );
      trackComp.Add( backWheel );

      frontRB.transform.parent = root.transform;
      backRB.transform.parent = root.transform;
      track.transform.parent = root.transform;

      return trackComp;
    }

    [UnityTest]
    public IEnumerator TestSimpleTrackInitializes()
    {
      var track = CreateSimpleTrack();

      yield return TestUtils.SimulateSeconds( 0.5f );
    }

    [UnityTest]
    public IEnumerator TestFullDoFTrackInitializes()
    {
      var track = CreateSimpleTrack();
      track.FullDoF = true;

      yield return TestUtils.SimulateSeconds( 0.5f );
    }

    private GameObject CreateBox( Vector3 pos )
    {
      var boxRB = Factory.Create<RigidBody>();
      var boxCollider = Factory.Create<Box>();
      var cylinderComp = boxCollider.GetComponent<Box>();
      ShapeVisual.Create( cylinderComp ).transform.parent = boxCollider.transform;
      cylinderComp.HalfExtents = Vector3.one * 0.2f;
      boxCollider.transform.parent = boxRB.transform;
      boxRB.transform.position = pos;

      return boxRB.gameObject;
    }

    [UnityTest]
    public IEnumerator TestBoxTrackConveyor()
    {
      var track = CreateSimpleTrack();

      var box = CreateBox(Vector3.up);

      yield return TestUtils.SimulateSeconds( 3f );

      Assert.That( box.transform.position.y, Is.GreaterThan( 0 ), "Conveyor should carry the box" );
      Assert.That( box.transform.position.x, Is.LessThan( -0.5 ), "Conveyor should move the box" );
    }

    [UnityTest]
    public IEnumerator TestBoxTrackFriction()
    {
      var track = CreateSimpleTrack();

      // Rotate 45 degrees to verify that the friction model properly finds the reference frame from the track
      track.transform.parent.rotation = Quaternion.FromToRotation( Vector3.forward, new Vector3( 1, 0, 1 ).normalized );

      var box = CreateBox(Vector3.up);

      var sm1 = ShapeMaterial.CreateInstance<ShapeMaterial>();
      track.Material = sm1;

      var sm2 = ShapeMaterial.CreateInstance<ShapeMaterial>();
      box.GetComponentInChildren<Box>().Material = sm2;

      var cm = ContactMaterial.CreateInstance<ContactMaterial>();
      cm.Material1 = sm1;
      cm.Material2 = sm2;

      // Set zero friction in primary direction
      cm.FrictionCoefficients = new Vector2( 0, 1 );

      cm.FrictionModel = FrictionModel.CreateInstance<FrictionModel>();
      cm.FrictionModel.Type = FrictionModel.EType.ConstantNormalForceBoxFriction;
      cm.FrictionModel.NormalForceMagnitude = 1000;
      cm.FrictionModel.TrackFrictionModel = true;

      ContactMaterialManager.Instance.Add( cm );

      yield return TestUtils.SimulateSeconds( 3f );

      Assert.That( box.transform.position.y, Is.GreaterThan( 0 ), "Conveyor should carry the box" );
      Assert.That( box.transform.position.x, Is.EqualTo( 0 ).Within( 0.1f ), "Conveyor should not move the box" );

      // Set friciton in primary direction
      cm.FrictionCoefficients = new Vector2( 1, 0 );

      yield return TestUtils.SimulateSeconds( 3f );

      Assert.That( box.transform.position.y, Is.GreaterThan( 0 ), "Conveyor should carry the box" );
      Assert.That( box.transform.position.x, Is.LessThan( -0.5 ), "Conveyor should not move the box" );
    }
  }
}
