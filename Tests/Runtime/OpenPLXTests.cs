using AGXUnity;
using AGXUnity.IO;
using AGXUnity.IO.OpenPLX;
using AGXUnity.Model;
using AGXUnity.Sensor;
using AGXUnity.Utils;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using static AGXUnity.ContactMaterial;

namespace AGXUnityTesting.Runtime
{

  [TestFixture]
  public class OpenPLXTests : AGXUnityFixture
  {
    private string TestDataFolder => "Assets/AGXUnity/Tests/Runtime/Test Resources";
    private T FindComponentByName<T>( string name ) where T : MonoBehaviour
    {
      var go = GameObject.Find(name);
      Assert.IsNotNull( go, "Finding object by name" );
      var comp = go.GetComponent<T>();
      Assert.IsNotNull( comp, "Finding component on object" );
      return comp;
    }

    private void AssertAnglesEqual( double expected, double actual, double delta, string message )
    {
      var normalized = actual > 180 ? actual - 360 : actual;
      Assert.AreEqual( expected, normalized, delta, message );
    }

    public OpenPLXRoot LoadOpenPLX( string source, string modelName = null, MapperOptions? options = null )
    {
      MapperOptions localOptions;
      if ( !options.HasValue ) {
        localOptions = new MapperOptions();
        localOptions.RotateUp = true;
      }
      else
        localOptions = options.Value;

      var openPLXObj = OpenPLXImporter.ImportOpenPLXFile<GameObject>( System.IO.Path.Combine( TestDataFolder, source ), localOptions, null, modelName );
      Assert.NotNull( openPLXObj, $"Failed to import OpenPLX file '{source}'" );
      openPLXObj.InitializeAll();

      return openPLXObj.GetComponent<OpenPLXRoot>();
    }

    [UnityTest]
    public IEnumerator TestLinearSpring()
    {
      LoadOpenPLX( "LinearSpringTest.openplx" );

      yield return TestUtils.SimulateSeconds( 2.0f );

      var rodRB = FindComponentByName<RigidBody>("LinearSpringTest/FallingRodScene/falling_rod/rod1");
      Assert.NotNull( rodRB );
      Assert.AreEqual( 0.0f, rodRB.LinearVelocity.y, 0.001f );
      Assert.AreEqual( -0.5f, rodRB.transform.position.y, 0.001f );
    }

    [UnityTest]
    public IEnumerator TestLinearRange()
    {
      LoadOpenPLX( "LinearRangeTest.openplx" );

      yield return TestUtils.SimulateSeconds( 2.0f );

      var rodRB = FindComponentByName<RigidBody>("LinearRangeTest/FallingRodScene/falling_rod/rod1");
      Assert.AreEqual( 0.0f, rodRB.LinearVelocity.y, 0.001f );
      // MC is offset by -0.5 and range is -0.5 = -1.0 total displacement
      Assert.AreEqual( -1f, rodRB.transform.position.y, 0.001f );
    }

    [Test]
    public void TestWorldMCOffset()
    {
      LoadOpenPLX( "world_mc.openplx", null, new MapperOptions() );

      var attach1 = GameObject.Find("world_mc/World Proxy Body/SignalScene.box1.local_attachment");
      var attach2 = GameObject.Find("world_mc/World Proxy Body/SignalScene.box2.local_attachment");
      Assert.That( attach1.transform.position, Is.EqualTo( new agx.Vec3( 1, 1, 1 ).ToHandedVector3() ) );
      Assert.That(
        attach1.transform.rotation,
        Is.EqualTo(
          new agx.Quat( new agx.Vec3( 0, 0, 1 ), new agx.Vec3( 0, 1, 0 ) )
          .ToHandedQuaternion() )
        .Using( QuaternionEqualityComparer.Instance ) );
      Assert.That( attach2.transform.position, Is.EqualTo( new agx.Vec3( -1, -1, 1 ).ToHandedVector3() ) );
      Assert.That( attach2.transform.rotation,
        Is.EqualTo( Quaternion.identity )
        .Using( QuaternionEqualityComparer.Instance ) );
    }

    [UnityTest]
    public IEnumerator TestSimplerInvertedPendulum()
    {
      LoadOpenPLX( "simpler_inverted_pendulum.openplx" );
      GameObject.Find( "simpler_inverted_pendulum" ).AddComponent<Aux.SimplerInvertedPendulum>();
      var rodRB = FindComponentByName<RigidBody>("simpler_inverted_pendulum/PendulumScene/rod");

      rodRB.Native.addForceAtLocalPosition( new agx.Vec3( 10000, 0, 0 ), new agx.Vec3( 0, 1, 0 ) );

      yield return TestUtils.SimulateSeconds( 2.0f );

      AssertAnglesEqual( 0.0f, rodRB.transform.localRotation.eulerAngles.y, 0.1f, "Pendulum Angles" );
    }

    [UnityTest]
    public IEnumerator TestInvertedPendulum()
    {
      LoadOpenPLX( "inverted_pendulum.openplx" );
      GameObject.Find( "inverted_pendulum" ).AddComponent<Aux.InvertedPendulumController>();

      var cartRB = FindComponentByName<RigidBody>("inverted_pendulum/PendulumScene/cart");
      var rodRB = FindComponentByName<RigidBody>("inverted_pendulum/PendulumScene/rod");

      // SNAP does not work properly for this model so allow model to "settle" manually until this is fixed
      yield return TestUtils.SimulateSeconds( 1.0f );

      rodRB.Native.addForceAtLocalPosition( new agx.Vec3( 25, 0, 0 ), new agx.Vec3( 0, 0, 1 ) );

      yield return TestUtils.SimulateSeconds( 4.0f );

      AssertAnglesEqual( 0.0f, rodRB.transform.localRotation.eulerAngles.y, 0.1f, "Pendulum Angles" );
      Assert.AreEqual( 0.0f, rodRB.transform.position.x, 0.01f, "Pendulum Position" );

      Assert.AreEqual( 0.0f, cartRB.transform.position.x, 0.01f, "Cart Position" );
      Assert.AreEqual( 0.0f, cartRB.LinearVelocity.x, 0.1f, "Cart Velocity" );
    }

    [UnityTest]
    public IEnumerator TestImportAndMove()
    {
      yield return TestUtils.WaitUntilLoaded();

      var openPLXObj = OpenPLXImporter.ImportOpenPLXFile<GameObject>(TestDataFolder + "/LinearSpringTest.openplx");

      openPLXObj.name = "Imported OpenPLX Object";
      openPLXObj.transform.position = new Vector3( 30, 0, 3 );
      openPLXObj.transform.rotation = Quaternion.AngleAxis( -90, Vector3.right );
      yield return TestUtils.SimulateSeconds( 2.0f );

      var rodRB = FindComponentByName<RigidBody>("Imported OpenPLX Object/FallingRodScene/falling_rod/rod1");
      Assert.AreEqual( 0.0f, rodRB.LinearVelocity.y, 0.001f );
      Assert.AreEqual( -0.5f, rodRB.transform.position.y, 0.001f );

      Assert.AreEqual( 30f, rodRB.transform.position.x, 0.001f );
      Assert.AreEqual( 3f, rodRB.transform.position.z, 0.001f );
    }

    [UnityTest]
    public IEnumerator TestImportExtendedAGXAtRuntime()
    {
      LoadOpenPLX( "extended_pendulum_from_agx.openplx" );

      yield return TestUtils.SimulateSeconds( 1.2f );

      var tip = GameObject.Find("extended_pendulum_from_agx/MyScene/inv_pendulum/top_extension/arrow");
      Assert.NotNull( tip );
      Assert.Less( tip.transform.position.y, 0.0f );
    }

    [UnityTest]
    public IEnumerator TestSimpleDrivetrain()
    {
      LoadOpenPLX( "drive_train_velocity_input.openplx" );

      yield return TestUtils.Step();

      var hinge = FindComponentByName<Constraint>("drive_train_velocity_input/PendulumScene/pendulum/hinge");
      Assert.AreEqual( 1, hinge.GetCurrentSpeed() );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainVelocitySignal()
    {
      LoadOpenPLX( "drive_train_velocity_input.openplx" );

      yield return TestUtils.Step();

      var signals = FindComponentByName<OpenPLXSignals>("drive_train_velocity_input");
      FindComponentByName<RigidBody>( "drive_train_velocity_input/PendulumScene/pendulum/rod" );

      var motorInput = signals.FindInputTarget<float>("PendulumScene.velocity_motor_input");
      Assert.NotNull( motorInput, "Finding input target" );
      var hinge = signals.Root.FindMappedObject("PendulumScene.pendulum.hinge").GetComponent<Constraint>();
      Assert.AreEqual( 1, hinge.GetCurrentSpeed() );
      motorInput.Write( -1 );

      yield return TestUtils.SimulateSeconds( 0.1f );
      Assert.AreEqual( -1, hinge.GetCurrentSpeed() );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainTorqueSignal()
    {
      LoadOpenPLX( "drive_train_torque_input.openplx" );

      yield return TestUtils.Step();

      var signals = FindComponentByName<OpenPLXSignals>("drive_train_torque_input");
      FindComponentByName<RigidBody>( "drive_train_torque_input/PendulumScene/pendulum/rod" );

      var motorInput = signals.FindInputTarget<float>("PendulumScene.torque_motor_input");
      Assert.NotNull( motorInput, "Finding input target" );

      void torqueMotorSignalSender() => motorInput.Write( -1000 );

      Simulation.Instance.StepCallbacks.PreStepForward += torqueMotorSignalSender;
      yield return TestUtils.SimulateSeconds( 1 );
      Simulation.Instance.StepCallbacks.PreStepForward -= torqueMotorSignalSender;

      var hinge = signals.Root.FindMappedObject("PendulumScene.pendulum.hinge").GetComponent<Constraint>();
      Assert.LessOrEqual( hinge.GetCurrentSpeed(), -10 );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainDifferentialAndGear()
    {
      LoadOpenPLX( "differential_test.openplx" );
      yield return TestUtils.SimulateSeconds( 0.5f );

      var rod1RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum1/rod");
      var rod2RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum2/rod");
      Assert.GreaterOrEqual( Mathf.Abs( rod1RB.AngularVelocity.z ), 0.1f );
      Assert.GreaterOrEqual( Mathf.Abs( rod2RB.AngularVelocity.z ), 0.1f );
      Assert.AreEqual( rod1RB.AngularVelocity.z, -rod2RB.AngularVelocity.z, 1e-9f, "Gear gives similar and opposite AVs" );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainBrake()
    {
      LoadOpenPLX( "drive_train_brake.openplx" );
      var signals = FindComponentByName<OpenPLXSignals>( "drive_train_brake" );

      var engageBrakeInput = signals.FindInputTarget<bool>( "PendulumScene.pendulum.brake.engage_input" );
      var engagedBrakeOutput = signals.FindOutputSource<bool>( "PendulumScene.pendulum.brake.engaged_output" );
      var angularVelocityOutput = signals.FindOutputSource<float>("PendulumScene.pendulum.hinge.angular_velocity_output");

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( !engagedBrakeOutput.Read() );
      var speed = angularVelocityOutput.Read();
      Assert.AreEqual( 1.0f, speed, 0.0001f );

      engageBrakeInput.Write( true );

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( engagedBrakeOutput.Read() );
      speed = angularVelocityOutput.Read();
      Assert.AreEqual( 0.0f, speed, 0.0001f );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainClutch()
    {
      LoadOpenPLX( "drive_train_clutch.openplx" );
      var signals = FindComponentByName<OpenPLXSignals>( "drive_train_clutch" );

      var engageClutchInput = signals.FindInputTarget<bool>( "PendulumScene.pendulum.clutch.engage_input" );
      var engagedClutchOutput = signals.FindOutputSource<bool>( "PendulumScene.pendulum.clutch.engaged_output" );
      var torqueOutput = signals.FindOutputSource<float>("PendulumScene.pendulum.motor.torque_output");

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( engagedClutchOutput.Read() );

      var torque = torqueOutput.Read();
      Assert.Greater( torque, 1.0f );

      engageClutchInput.Write( false );

      yield return TestUtils.SimulateSeconds( 0.5f );

      Assert.That( !engagedClutchOutput.Read() );
      // Torque should drop as the clutch is disengaged
      torque = torqueOutput.Read();
      Assert.Less( torque, 1.0f );
    }

    [Test]
    public void TestLoadWithMaterials()
    {
      LoadOpenPLX( "material_scene.openplx" );
      var root = FindComponentByName<OpenPLXRoot>( "material_scene" );

      var b1 = root.FindMappedObject("MaterialScene.Box1.geometry").GetComponent<AGXUnity.Collide.Shape>();
      var b2 = root.FindMappedObject("MaterialScene.Box2.geometry").GetComponent<AGXUnity.Collide.Shape>();
      var b3 = root.FindMappedObject("MaterialScene.BoxInline.geometry").GetComponent<AGXUnity.Collide.Shape>();
      var ground = root.FindMappedObject("MaterialScene.Ground.geometry").GetComponent<AGXUnity.Collide.Shape>();

      Assert.NotNull( b1.Material, "Box1 has material" );
      Assert.NotNull( b2.Material, "Box2 has material" );
      Assert.NotNull( b3.Material, "BoxInline has material" );
      Assert.NotNull( ground.Material, "Ground has material" );

      Assert.IsTrue( ContactMaterialManager.Instance.ContactMaterials.Any( cm => cm.Material1 == ground.Material && cm.Material2 == b1.Material ), "Loaded Box1 <-> Ground CM" );
      Assert.IsTrue( ContactMaterialManager.Instance.ContactMaterials.Any( cm => cm.Material1 == ground.Material && cm.Material2 == b2.Material ), "Loaded Box2 <-> Ground CM" );
      Assert.IsTrue( ContactMaterialManager.Instance.ContactMaterials.Any( cm => cm.Material1 == ground.Material && cm.Material2 == b3.Material ), "Loaded BoxInline <-> Ground CM" );
    }

    [Test]
    public void TestMultiInteractions()
    {
      LoadOpenPLX( "multi_interaction.openplx" );

      var openPLX = FindComponentByName<OpenPLXRoot>("multi_interaction");
      var hinge = openPLX.FindMappedObject("PendulumScene.hingePendulum");
      Assert.NotNull( hinge );
      var constraints = hinge.gameObject.GetComponentsInChildren<Constraint>();
      // Pendulum system contains 1 hinge, 2 motors, 2 ranges, 2 springs. Should map to 7 constraints
      Assert.AreEqual( 7, constraints.Length );

      var prismatic = openPLX.FindMappedObject("PendulumScene.linearPendulum");
      Assert.NotNull( prismatic );
      constraints = prismatic.gameObject.GetComponentsInChildren<Constraint>();
      // Pendulum system contains 1 prismatic, 2 motors, 2 ranges, 2 springs. Should map to 7 constraints
      Assert.AreEqual( 7, constraints.Length );
    }

    [UnityTest]
    public IEnumerator TestImportOBJAtRuntime()
    {
      LoadOpenPLX( "obj_import_test.openplx" );

      yield return TestUtils.SimulateSeconds( 1.0f );
    }

    [UnityTest]
    public IEnumerator TestRBLinearVelocitySignal()
    {
      LoadOpenPLX( "rb_velocity_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("rb_velocity_signal_test");

      var input = signals.FindInputTarget<agx.Vec3>("SignalScene.box_linear_vel_in");
      var output = signals.FindOutputSource<agx.Vec3>("SignalScene.box_linear_vel_out");

      var expected = new agx.Vec3( 1, 2, 3 );
      input.Write( expected );

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.Read();

      Assert.AreEqual( expected, signals.Root.FindMappedObject( "SignalScene.box" ).GetComponent<RigidBody>().Native.getVelocity() );
      Assert.AreEqual( expected, outVel );
    }

    [UnityTest]
    public IEnumerator TestRBAngularVelocitySignal()
    {
      LoadOpenPLX( "rb_velocity_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("rb_velocity_signal_test");

      var input = signals.FindInputTarget<agx.Vec3>("SignalScene.box_angular_vel_in");
      var output = signals.FindOutputSource<agx.Vec3>("SignalScene.box_angular_vel_out");

      var expected = new agx.Vec3( 4, 5, 6 );
      input.Write( expected );

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.Read();

      Assert.AreEqual( expected, signals.Root.FindMappedObject( "SignalScene.box" ).GetComponent<RigidBody>().Native.getAngularVelocity() );
      Assert.AreEqual( expected, outVel );
    }

    [TestCase( false )]
    [TestCase( true )]
    public void TestBundledMotorSignalWritesToMappedController( bool omitBundlePrefix )
    {
      var instance = OpenPLXImporter.ImportOpenPLXFile<GameObject>( TestDataFolder + "/TestBundle/motor_signal.openplx" );
      Assert.That( instance, Is.Not.Null );
      // Existing imported prefabs can store declarations without the bundle
      // prefix even though the runtime model exposes fully qualified names.
      if ( omitBundlePrefix )
        foreach ( var obj in instance.GetComponentsInChildren<OpenPLXObject>() )
          for ( int i = 0; i < obj.SourceDeclarations.Count; ++i )
            if ( obj.SourceDeclarations[ i ].StartsWith( "Test.", System.StringComparison.Ordinal ) )
              obj.SourceDeclarations[ i ] = obj.SourceDeclarations[ i ].Substring( "Test.".Length );
      instance.InitializeAll();
      var root = instance.GetComponent<OpenPLXRoot>();
      Assert.That( root.Native.getName(), Is.EqualTo( "Test.SignalScene" ) );
      var signals = root.GetComponent<OpenPLXSignals>().GetInitialized();
      var input = signals.FindInputTarget<double>( root.Native.getName() + ".motor_speed" );
      var motor = instance.GetComponentsInChildren<Generic1DOFControlledConstraint>().Single()
        .GetInitialized().Native.asConstraint1DOF().getMotor1D();

      input.Write( 2.75 );
      Assert.That( motor.getSpeed(), Is.EqualTo( 2.75 ).Within( 1e-10 ) );
      input.Write( -1.5 );
      Assert.That( motor.getSpeed(), Is.EqualTo( -1.5 ).Within( 1e-10 ) );
    }

    [UnityTest]
    public IEnumerator TestWrappersAfterRootDestruction()
    {
      var root = LoadOpenPLX( "signal_test.openplx" );
      var signals = root.GetComponent<OpenPLXSignals>();
      var input = signals.FindInputTarget<float>( "SignalScene.motorSpeedInput" );
      var output = signals.FindOutputSource<float>( "SignalScene.angularVelocity" );
      yield return TestUtils.DestroyAndWait( root.gameObject );

      LogAssert.Expect( LogType.Warning, new Regex( "Read\\(\\) called on OutputWrapper wrapping invalid Output" ) );
      Assert.That( output.Read(), Is.EqualTo( 0.0f ) );
      LogAssert.Expect( LogType.Warning, new Regex( "Write\\(\\) called on InputWrapper wrapping invalid Input" ) );
      input.Write( 1.0f );
    }

    [UnityTest]
    public IEnumerator TestLidarInterfaceReadsLiveSamplesAndInvalidatesAliasesOnDestroy()
    {
      var instance = OpenPLXImporter.ImportOpenPLXFile<GameObject>(
        "Assets/AGXUnity/lidar_signal_wrappers_test.openplx" );
      Assert.That( instance, Is.Not.Null );
      try {
        var signals = instance.GetComponent<OpenPLXSignals>().GetInitialized<OpenPLXSignals>();
        Assert.That( signals, Is.Not.Null );
        var controls = signals.Interfaces.Single( value => value.Name == "controls" );
        var alias = controls.FindOutput( "samples" );
        var output = new OutputWrapper<SignalWrapperTests.LidarSample[]>( alias );
        // Constructing a wrapper and reading before the first raytrace must
        // succeed with an empty payload instead of a null marshalling.
        Assert.That( output.Read(), Is.Empty );

        yield return TestUtils.SimulateSeconds( 0.3f );
        var samples = output.Read();
        Assert.That( samples, Is.Not.Empty, "The lidar should hit the fixture's targets." );
        foreach ( var sample in samples ) {
          Assert.That( sample.is_hit, Is.EqualTo( 1 ) );
          var position = sample.position3d;
          var distance = System.Math.Sqrt( position.x * position.x + position.y * position.y + position.z * position.z );
          Assert.That( sample.distance, Is.EqualTo( distance ).Within( 0.01 ), "Positions are sensor-relative metres." );
        }

        Object.Destroy( signals );
        yield return null;
        Assert.That( alias.IsValid, Is.False, "Interface aliases must be invalidated before releasing native outputs." );
        LogAssert.Expect( LogType.Warning, new Regex( "Read\\(\\) called on OutputWrapper wrapping invalid Output" ) );
        Assert.That( output.Read(), Is.Null );
      }
      finally {
        Object.Destroy( instance );
      }
    }

    [UnityTest]
    public IEnumerator TestImportedLidarOutputResizesAtOneScanPerStep()
    {
      yield return CheckImportedLidarOutputSizing( 1.0f, false );
    }

    [UnityTest]
    public IEnumerator TestImportedLidarOutputResizesAtLowerScanFrequency()
    {
      yield return CheckImportedLidarOutputSizing( 1.0f / 7.0f, true );
    }

    private IEnumerator CheckImportedLidarOutputSizing( float scansPerStep, bool expectPartialScans )
    {
      var instance = OpenPLXImporter.ImportOpenPLXFile<GameObject>(
        "Assets/AGXUnity/lidar_signal_wrappers_test.openplx" );
      Assert.That( instance, Is.Not.Null );
      try {
        var sensor = instance.GetComponentInChildren<LidarSensor>();
        Assert.That( sensor, Is.Not.Null );
        var sweep = sensor.ModelData as GenericSweepData;
        Assert.That( sweep, Is.Not.Null );
        sensor.RemoveRayMisses = false;
        sweep.Frequency = scansPerStep / (float)Simulation.Instance.TimeStep;

        var signals = instance.GetComponent<OpenPLXSignals>().GetInitialized<OpenPLXSignals>();
        Assert.That( signals, Is.Not.Null );
        var controls = signals.Interfaces.Single( value => value.Name == "controls" );
        var output = new OutputWrapper<SignalWrapperTests.LidarSample[]>( controls.FindOutput( "samples" ) );
        Assert.That( output.Read(), Is.Empty, "The initial lidar buffer has no scan data." );

        const int raysPerScan = 180 * 5;
        var reused = new SignalWrapperTests.LidarSample[ raysPerScan ];
        var sizes = new System.Collections.Generic.List<int>();
        for ( int step = 0; step < 16; ++step ) {
          yield return TestUtils.Step();
          var samples = output.Read();
          sizes.Add( samples.Length );
          Assert.That( output.ReadInto( reused ), Is.EqualTo( samples.Length ),
                       "ReadInto must use the current scan size." );
          Assert.That( samples.Length, Is.LessThanOrEqualTo( raysPerScan ) );
        }

        Assert.That( sizes.Max(), Is.GreaterThan( 0 ), "The imported lidar must produce samples." );
        if ( expectPartialScans ) {
          Assert.That( sizes.Max(), Is.LessThan( raysPerScan ), "The slower sweep should produce partial scans per step." );
          Assert.That( sizes.Distinct().Count(), Is.GreaterThan( 1 ),
                       "The slower sweep should exercise changing array lengths." );
        }
        else
          Assert.That( sizes.Skip( 2 ), Is.All.EqualTo( raysPerScan ),
                       "One complete scan should be emitted on each settled step." );
      }
      finally {
        Object.Destroy( instance );
      }
    }

    [UnityTest]
    public IEnumerator TestSignalInterfaceExampleOnSerializedRootStartup()
    {
#if UNITY_EDITOR
      var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/AGXUnity/signal_wrappers_test.openplx" );
      Assert.NotNull( asset, "The signal wrapper test asset must be imported." );
      var exampleType = System.AppDomain.CurrentDomain.GetAssemblies()
        .Select( assembly => assembly.GetType( "AGXUnity.Examples.SignalWrapperInterfaceExample" ) )
        .FirstOrDefault( type => type != null );
      Assert.NotNull( exampleType, "The interface example component must be compiled." );

      var parent = new GameObject( "Inactive signal example prototype" );
      parent.SetActive( false );
      GameObject instance = null;
      try {
        var prototype = Object.Instantiate( asset, parent.transform );
        var example = prototype.AddComponent( exampleType );
        var expected = new Vector3( 1, 2, 3 );
        exampleType.GetField( "TargetLinearVelocity" ).SetValue( example, expected );

        // Clone with the example already attached, as when loading a saved
        // scene. OnEnable runs before every imported child has started.
        instance = Object.Instantiate( prototype );
        yield return null;
        yield return null;
        Assert.That( instance.GetComponent<OpenPLXRoot>().State, Is.EqualTo( ScriptComponent.States.INITIALIZED ) );
        Assert.That( ( (Behaviour)instance.GetComponent( exampleType ) ).enabled, Is.True );

        var signals = instance.GetComponent<OpenPLXSignals>();
        var controls = signals.Interfaces.Single( value => value.Name == "controls" );
        var output = new OutputWrapper<Vector3>( controls.FindOutput( "linear_velocity" ) );
        yield return TestUtils.Step();
        Assert.That( output.Read(), Is.EqualTo( expected ) );

        instance.SetActive( false );
        instance.SetActive( true );
        yield return null;
        yield return null;
        yield return TestUtils.Step();
        Assert.That( output.Read(), Is.EqualTo( expected ) );
      }
      finally {
        Object.Destroy( instance );
        Object.Destroy( parent );
      }
#else
      yield break;
#endif
    }

    [UnityTest]
    public IEnumerator TestMCLinearVelocitySignal()
    {
      LoadOpenPLX( "mc_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("mc_signal_test");

      var output = signals.FindOutputSource<agx.Vec3>("SignalScene.mc_linvel_signal");

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.Read();

      Assert.That( outVel.x, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.y, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.z, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
    }

    [UnityTest]
    public IEnumerator TestMCAngularVelocitySignal()
    {
      LoadOpenPLX( "mc_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("mc_signal_test");

      var output = signals.FindOutputSource<agx.Vec3>("SignalScene.mc_angvel_signal");

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.Read();

      Assert.That( outVel.x, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.y, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.z, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
    }

    [Test]
    public void TestImportTexturedVisualMaterial()
    {
      var material = OpenPLXImporter.ImportOpenPLXFile<Material>( TestDataFolder + "/textured_mat.openplx" );
      Assert.NotNull( material );

      byte[] data = System.IO.File.ReadAllBytes( OpenPLXImporter.TransformOpenPLXPath(TestDataFolder + "/simple_checkerboard.png") );
      Assert.NotNull( data );
      var groundTruthTexture = new Texture2D(2, 2);
      ImageConversion.LoadImage( groundTruthTexture, data );

      var plxTex = material.mainTexture as Texture2D;
      Assert.NotNull( plxTex );
      Assert.That( plxTex.height, Is.EqualTo( groundTruthTexture.height ) );
      Assert.That( plxTex.width, Is.EqualTo( groundTruthTexture.width ) );

      for ( int y = 0; y < plxTex.height; y++ )
        for ( int x = 0; x < plxTex.width; x++ )
          Assert.That( plxTex.GetPixel( x, y ), Is.EqualTo( groundTruthTexture.GetPixel( x, y ) ) );
    }

    [Test]
    public void TestImportSwizzledVisualMaterial()
    {
      var material = OpenPLXImporter.ImportOpenPLXFile<Material>( TestDataFolder + "/swizzle_mat.openplx" );
      Assert.NotNull( material );

      byte[] data = System.IO.File.ReadAllBytes( OpenPLXImporter.TransformOpenPLXPath(TestDataFolder + "/checkerboardRGBA.png") );
      Assert.NotNull( data );
      var groundTruthTexture = new Texture2D(2, 2);
      ImageConversion.LoadImage( groundTruthTexture, data );

      var plxTex = material.GetTexture("_Metallic_Map") as Texture2D;
      Assert.NotNull( plxTex );
      Assert.That( plxTex.height, Is.EqualTo( groundTruthTexture.height ) );
      Assert.That( plxTex.width, Is.EqualTo( groundTruthTexture.width ) );
      Assert.That( plxTex.format, Is.EqualTo( TextureFormat.R8 ) );

      for ( int y = 0; y < plxTex.height; y++ )
        for ( int x = 0; x < plxTex.width; x++ )
          Assert.That( plxTex.GetPixel( x, y ).r, Is.EqualTo( groundTruthTexture.GetPixel( x, y ).r ) );
    }

    [Test]
    public void TestImportVisualMaterial()
    {
      var material = OpenPLXImporter.ImportOpenPLXFile<Material>( TestDataFolder + "/visual_mat.openplx" );
      Assert.NotNull( material );

      Assert.That( material.GetFloat( "_Smoothness" ), Is.EqualTo( 0.8f ).Within( 1 ).Ulps );
      Assert.That( material.GetFloat( "_Metallic" ), Is.EqualTo( 0.4f ).Within( 1 ).Ulps );
    }

    [Test]
    public void TestImportVisualMesh()
    {
      var scene = OpenPLXImporter.ImportOpenPLXFile<GameObject>( TestDataFolder + "/visual_mesh_import.openplx" );
      Assert.NotNull( scene );

      var mf = scene.GetComponentInChildren<MeshFilter>();
      Assert.NotNull( mf );
      Assert.NotNull( mf.sharedMesh );
    }

    [Test]
    public void TestBundleDirectory()
    {
      OpenPLXSettings.Instance.AdditionalBundleDirs.Add( TestDataFolder + "/TestBundle" );
      var dependant = OpenPLXImporter.ImportOpenPLXFile<GameObject>( TestDataFolder + "/use_test_bundle.openplx" );

      Assert.NotNull( dependant );
    }

    //[UnityTest]
    //public IEnumerator TestRangeSignals()
    //{
    //  LoadOpenPLX( "torque_range.openplx" );
    //  var signals = FindComponentByName<OpenPLXSignals>( "torque_range" );
    //  Assert.NotNull( signals );

    //  var rangeInput = signals.FindInputTarget("PendulumScene.pendulum.motor.torque_range_input");
    //  var rangeOutput = signals.FindOutputSource("PendulumScene.pendulum.motor.torque_range_output");
    //  var torqueOutput = signals.FindOutputSource("PendulumScene.pendulum.motor.torque_output");

    //  yield return TestUtils.Step();

    //  Assert.That( Mathf.Abs( torqueOutput.GetValue<float>() ), Is.GreaterThan( 2 ) );

    //  rangeInput.SendSignal( new Vector2( -1, 1 ) );
    //  yield return TestUtils.Step();
    //  Assert.That( Mathf.Abs( torqueOutput.GetValue<float>() ), Is.LessThanOrEqualTo( 1 ) );
    //  var rangeSignalVector2 = rangeOutput.GetValue<Vector2>();
    //  Assert.That( rangeSignalVector2.x, Is.EqualTo( -1.0f ).Within( 1e-10 ) );
    //  Assert.That( rangeSignalVector2.y, Is.EqualTo( 1.0f ).Within( 1e-10 ) );

    //  rangeInput.SendSignal( new agx.Vec2( 2, 3 ) );
    //  yield return TestUtils.Step();
    //  var rangeSignalVec2 = rangeOutput.GetValue<agx.Vec2>();
    //  Assert.That( rangeSignalVec2.x, Is.EqualTo( 2.0f ).Within( 1e-10 ) );
    //  Assert.That( rangeSignalVec2.y, Is.EqualTo( 3.0f ).Within( 1e-10 ) );

    //  rangeInput.SendSignal( new agx.Vec2f( 4, 5 ) );
    //  yield return TestUtils.Step();
    //  var rangeSignalVec2f = rangeOutput.GetValue<agx.Vec2f>();
    //  Assert.That( rangeSignalVec2f.x, Is.EqualTo( 4.0f ).Within( 1e-10 ) );
    //  Assert.That( rangeSignalVec2f.y, Is.EqualTo( 5.0f ).Within( 1e-10 ) );
    //}

    [Test]
    public void TestAGXContactModelReduction()
    {
      var dependant = OpenPLXImporter.ImportOpenPLXFile<GameObject>( TestDataFolder + "/agx_bundle_test.openplx" );
      Assert.NotNull( dependant );
      var level = dependant.GetComponent<SavedPrefabLocalData>().ContactMaterials[0].ContactReductionLevel;
      Assert.That( level, Is.EqualTo( ContactReductionLevelType.Moderate ) );
    }

    [Test]
    public void TestWheelImportComponents()
    {
      var root = LoadOpenPLX( "elastic_wheel.openplx" );
      var leftWheel = root.FindMappedObject( "WheelScene.left_wheel" );
      Assert.IsTrue( leftWheel.TryGetComponent<TwoBodyTire>( out var leftTire ), "Elastic wheel should be mapepd to TwoBodyTire" );
      var leftSuspension = root.FindMappedObject("WheelScene.suspension_left.mate");
      Assert.IsTrue( leftSuspension.TryGetComponent<WheelJoint>( out var leftWheelJoint ), "Suspensions should be mapped to WheelJoint" );
      Assert.That( leftWheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Steering ).Enable, Is.False, "Suspensions with steering trait should not be constrained around the steering axis" );
    }

    [UnityTest]
    public IEnumerator TestSteering()
    {
      var root = LoadOpenPLX( "elastic_wheel.openplx" );
      var steering = root.FindMappedObject( "WheelScene.steering" );
      Assert.IsTrue( steering.TryGetComponent<Steering>( out var steeringComp ), "Steering should map to steering constraint" );

      Assert.That( steeringComp.Mechanism, Is.EqualTo( Steering.SteeringMechanism.Ackermann ), "Specific steering mechanisms should map to proper enum flag" );

      var signals = root.GetComponent<OpenPLXSignals>().GetInitialized();
      var steeringInput = signals.FindInputTarget<float>( "WheelScene.steering.interaction.steering_angle_input" );
      var steeringOutput = signals.FindOutputSource<float>( "WheelScene.steering.interaction.steering_angle_output" );

      yield return TestUtils.Step();

      Assert.That( steeringOutput.Read(), Is.EqualTo( 0.0f ).Within( 1e-6f ), "Steering angle should default to 0.0f" );
      steeringInput.Write( 0.5f );

      yield return TestUtils.SimulateSeconds( 1.0f );

      Assert.That( steeringOutput.Read(), Is.EqualTo( 0.5 ).Within( 1e-6f ), "Sending steering angle input should cause the output to match" );
    }

    [UnityTest]
    public IEnumerator TestShovelTerrainImport()
    {
      LogAssert.Expect( LogType.Warning, new Regex( @"AGXUnity\.Rendering\.DeformableTerrainParticleRenderer:.*" ) );

      var root = LoadOpenPLX( "shovel.openplx" );
      var shovelGO = root.FindMappedObject( "ShovelScene.shovel.shovel" );
      Assert.NotNull( shovelGO );
      Assert.IsTrue( shovelGO.TryGetComponent<DeformableTerrainShovel>( out var shovel ), "Shovels should be properly mapped" );
      var terrainGO = root.FindMappedObject("ShovelScene.terrain");
      Assert.NotNull( terrainGO );
      Assert.IsTrue( terrainGO.TryGetComponent<MovableTerrain>( out var terrain ), "Terrains should be properly mapped" );

      Assert.NotNull( shovel.Settings );

      yield return TestUtils.SimulateSeconds( 1 );

      Assert.That( terrain.GetSoilSimulationInterface().getNumSoilParticles(), Is.GreaterThan( 0 ), "After some time, the shovel should have dug up some particles" );
    }

    [Test]
    public void TestValidLidarOK()
    {
      LoadOpenPLX( "simple_lidar.openplx", "ValidLidar" );

      var lidar = FindComponentByName<LidarSensor>( "simple_lidar/ValidLidar/sensor" );

      Assert.That( lidar.DistanceGaussianNoise.Enable );
      Assert.That( lidar.DistanceGaussianNoise.Mean, Is.EqualTo( 0f ) );
      Assert.That( lidar.DistanceGaussianNoise.StandardDeviationBase, Is.EqualTo( 0.01f ) );
      Assert.That( lidar.DistanceGaussianNoise.StandardDeviationSlope, Is.EqualTo( 0.02f ) );
    }

    [Test]
    public void TestDoubleDistanceDistortionLidarError()
    {
      LogAssert.Expect( LogType.Error, new Regex( ".*distance distortion.*" ) );

      var go = OpenPLXImporter.ImportOpenPLXFile<GameObject>(
        TestDataFolder + "/simple_lidar.openplx", default, null, "DoubleDistanceDistortionLidar" );
    }

    [Test]
    public void TestMapperErrorsSurviveGarbageCollection()
    {
      var data = new MapperData();
      data.ErrorReporter = new openplx.ErrorReporter();
      var errorReference = ReportErrorForGarbageCollectionTest( data );

      System.GC.Collect();
      System.GC.WaitForPendingFinalizers();
      System.GC.Collect();

      Assert.That( errorReference.IsAlive, Is.True, "Managed error callbacks must stay alive while the mapper retains native errors" );
      var errors = data.ErrorReporter.getErrors();
      Assert.That( errors.Count, Is.EqualTo( 1 ) );
      Assert.That( errors[ 0 ].getMessage( true ), Does.Contain( "gc-test.openplx" ) );
      System.GC.KeepAlive( data );
    }

    [System.Runtime.CompilerServices.MethodImpl( System.Runtime.CompilerServices.MethodImplOptions.NoInlining )]
    private static System.WeakReference ReportErrorForGarbageCollectionTest( MapperData data )
    {
      var error = new FileDoesNotExistError( "gc-test.openplx" );
      data.ReportError( error );
      return new System.WeakReference( error );
    }

    [Test]
    public void TestImportSimpleTrack()
    {
      var go = LoadOpenPLX("track_system.openplx", "Scene" );
      Assert.NotNull( go.gameObject );

      var track = go.FindMappedObject( "Scene.track_system" ).GetComponent<Track>();
      Assert.NotNull( track );

      Assert.False( track.FullDoF );
    }

    [Test]
    public void TestImportFullDoFTrack()
    {
      var go = LoadOpenPLX("track_system.openplx", "FullDoFScene" );
      Assert.NotNull( go.gameObject );

      var track = go.FindMappedObject( "FullDoFScene.track_system" ).GetComponent<Track>();
      Assert.NotNull( track );

      Assert.True( track.FullDoF );
    }

    [Test]
    public void TestTrackImportMaterials()
    {
      var go = LoadOpenPLX("track_system.openplx", "FullDoFScene" );
      Assert.NotNull( go.gameObject );

      var track = go.FindMappedObject( "FullDoFScene.track_system" ).GetComponent<Track>();
      Assert.NotNull( track );

      Assert.True( track.Material != null );
    }

    [TestCase( "attribute_naming.openplx", "BaseScene.child.nested.backticks" )]
    [TestCase( "attribute_naming.openplx", "child.nested.backticks" )]
    [TestCase( "attribute_naming.openplx", "BaseScene.backtick.child.nested.backticks" )]
    [TestCase( "attribute_naming.openplx", "backtick.child.nested.backticks" )]
    [TestCase( "TestBundle/attribute_naming.openplx", "Test.BaseScene.child.nested.backticks" )]
    [TestCase( "TestBundle/attribute_naming.openplx", "child.nested.backticks" )]
    [TestCase( "TestBundle/attribute_naming.openplx", "Test.BaseScene.backtick.child.nested.backticks" )]
    [TestCase( "TestBundle/attribute_naming.openplx", "backtick.child.nested.backticks" )]
    public void TestFindNestedNativeFindsRelative( string openplxFile, string declaration )
    {
      var go = LoadOpenPLX(openplxFile);
      Assert.NotNull( go, "Failed to import OpenPLX file" );

      Assert.NotNull( go.FindMappedObject( declaration ), "A mapped representation of the declaration should be found" );
      Assert.NotNull( go.FindNestedNative( declaration ), "A native OpenPLX object of the declaration should be found" );
    }
  }
}
