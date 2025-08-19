using AGXUnity;
using AGXUnity.IO.OpenPLX;
using AGXUnity.Utils;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{

  [TestFixture]
  public class OpenPLXTests
  {
    private string TestDataFolder => "Assets/Tests/";
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

    [UnityTearDown]
    public IEnumerator RemoveLoadedObjects()
    {
      foreach ( var roots in Object.FindObjectsOfType<OpenPLXRoot>() )
        Object.Destroy( roots.gameObject );
      yield return new WaitForEndOfFrame();
    }

    public void LoadOpenPLX( string source )
    {
      var openPLXObj = OpenPLXImporter.ImportOpenPLXFile<GameObject>( System.IO.Path.Combine( TestDataFolder, source ) );
      Assert.NotNull( openPLXObj, $"Failed to import OpenPLX file '{source}'" );
      openPLXObj.transform.rotation = Quaternion.AngleAxis( -90, Vector3.right );
      openPLXObj.InitializeAll();
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
      // Inverted pendulum is calibrated for 60hz
      //Time.fixedDeltaTime = 1.0f/60.0f;
      var cartRB = FindComponentByName<RigidBody>("inverted_pendulum/PendulumScene/cart");
      var rodRB = FindComponentByName<RigidBody>("inverted_pendulum/PendulumScene/rod");

      rodRB.Native.addForceAtLocalPosition( new agx.Vec3( 25, 0, 0 ), new agx.Vec3( 0, 0, 1 ) );

      yield return TestUtils.SimulateSeconds( 4.0f );

      AssertAnglesEqual( 0.0f, rodRB.transform.localRotation.eulerAngles.y, 0.1f, "Pendulum Angles" );
      Assert.AreEqual( 0.0f, rodRB.transform.position.x, 0.01f, "Pendulum Position" );

      Assert.AreEqual( 0.0f, cartRB.transform.position.x, 0.01f, "Cart Position" );
      Assert.AreEqual( 0.0f, cartRB.LinearVelocity.x, 0.1f, "Cart Velocity" );
      //Time.fixedDeltaTime = 1.0f/50.0f;
    }

    [UnityTest]
    public IEnumerator TestImportAndMove()
    {
      yield return TestUtils.WaitUntilLoaded();

      var openPLXObj = OpenPLXImporter.ImportOpenPLXFile<GameObject>("Assets/Tests/LinearSpringTest.openplx");

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

      yield return TestUtils.SimulateSeconds( 5.0f );

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

      var motorInput = signals.FindInputTarget("PendulumScene.velocity_motor_input");
      Assert.NotNull( motorInput, "Finding input target" );
      var hinge = signals.Root.FindMappedObject("PendulumScene.pendulum.hinge").GetComponent<Constraint>();
      Assert.AreEqual( 1, hinge.GetCurrentSpeed() );
      signals.SendInputSignal( openplx.Physics.Signals.RealInputSignal.create( -1, motorInput.Native ) );

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

      var motorInput = signals.FindInputTarget("PendulumScene.torque_motor_input");
      Assert.NotNull( motorInput, "Finding input target" );

      void torqueMotorSignalSender() { signals.SendInputSignal( openplx.Physics.Signals.RealInputSignal.create( -1000, motorInput.Native ) ); }

      Simulation.Instance.StepCallbacks.PreStepForward += torqueMotorSignalSender;
      yield return TestUtils.SimulateSeconds( 1 );
      Simulation.Instance.StepCallbacks.PreStepForward -= torqueMotorSignalSender;

      var hinge = signals.Root.FindMappedObject("PendulumScene.pendulum.hinge").GetComponent<Constraint>();
      Assert.LessOrEqual( hinge.GetCurrentSpeed(), -10 );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainDifferential()
    {
      LoadOpenPLX( "differential_test.openplx" );
      yield return TestUtils.Step();

      var rod1RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum1/rod");
      var rod2RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum2/rod");
      Assert.GreaterOrEqual( Mathf.Abs( rod1RB.AngularVelocity.z ), 0.1f );
      Assert.GreaterOrEqual( Mathf.Abs( rod2RB.AngularVelocity.z ), 0.1f );
    }

    [UnityTest]
    public IEnumerator TestDrivetrainGear()
    {
      LoadOpenPLX( "differential_test.openplx" );
      yield return TestUtils.Step();

      var rod1RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum1/rod");
      var rod2RB = FindComponentByName<RigidBody>("differential_test/PendulumScene/pendulum2/rod");
      Assert.AreEqual( rod1RB.AngularVelocity.z, -rod2RB.AngularVelocity.z, 0.0001f, "Gear gives similar and opposite AVs" );
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
    public IEnumerator TestCachedSignals()
    {
      LoadOpenPLX( "signal_test.openplx" );
      var signals = FindComponentByName<OpenPLXSignals>("signal_test");
      var input = signals.FindInputTarget("SignalScene.motorSpeedInput");
      signals.SendInputSignal( openplx.Physics.Signals.RealInputSignal.create( 1, input.Native ) );
      yield return TestUtils.SimulateSeconds( 0.2f );

      Assert.AreEqual( 1, signals.GetValue<float>( "SignalScene.angularVelocity" ), 1e-10 );
      Assert.AreEqual( 1, signals.GetValue<double>( "SignalScene.angularVelocity" ), 1e-10 );

      var groundTruth = new agx.EulerAngles(signals.Root.FindMappedObject("SignalScene.box").GetComponent<RigidBody>().Native.getRotation(),agx.EulerConvention.ZYXs);

      var vec3Result = signals.GetValue<agx.Vec3>("SignalScene.boxRPY");
      Assert.AreEqual( groundTruth.x, vec3Result.x, 1e-10 );
      Assert.AreEqual( groundTruth.y, vec3Result.y, 1e-10 );
      Assert.AreEqual( groundTruth.z, vec3Result.z, 1e-10 );

      var vec3fResult = signals.GetValue<agx.Vec3f>("SignalScene.boxRPY");
      Assert.AreEqual( (float)groundTruth.x, vec3fResult.x, 1e-10 );
      Assert.AreEqual( (float)groundTruth.y, vec3fResult.y, 1e-10 );
      Assert.AreEqual( (float)groundTruth.z, vec3fResult.z, 1e-10 );

      var vector3Result = signals.GetValue<Vector3>("SignalScene.boxRPY");
      Assert.AreEqual( (float)groundTruth.x, vector3Result.x, 1e-10 );
      Assert.AreEqual( (float)groundTruth.y, vector3Result.y, 1e-10 );
      Assert.AreEqual( (float)groundTruth.z, vector3Result.z, 1e-10 );

      var openPLXVec3Result = signals.GetValue<openplx.Math.Vec3>("SignalScene.boxRPY");
      Assert.AreEqual( groundTruth.x, openPLXVec3Result.x(), 1e-10 );
      Assert.AreEqual( groundTruth.y, openPLXVec3Result.y(), 1e-10 );
      Assert.AreEqual( groundTruth.z, openPLXVec3Result.z(), 1e-10 );
    }

    [UnityTest]
    public IEnumerator TestEndpointWrapper()
    {
      LoadOpenPLX( "signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("signal_test");
      var input = signals.FindInputTarget("SignalScene.motorSpeedInput");
      var velocityOutput = signals.FindOutputSource("SignalScene.angularVelocity");
      var boxRPY = signals.FindOutputSource("SignalScene.boxRPY");

      input.SendSignal( -1 );

      yield return TestUtils.SimulateSeconds( 0.2f );

      Assert.AreEqual( -1, velocityOutput.GetValue<float>(), 1e-10 );
      Assert.AreEqual( -1, velocityOutput.GetValue<double>(), 1e-10 );

      var groundTruth = new agx.EulerAngles(signals.Root.FindMappedObject("SignalScene.box").GetComponent<RigidBody>().Native.getRotation(), agx.EulerConvention.ZYXs);

      var vec3Result = boxRPY.GetValue<agx.Vec3>();
      Assert.AreEqual( groundTruth.x, vec3Result.x, 1e-10 );
      Assert.AreEqual( groundTruth.y, vec3Result.y, 1e-10 );
      Assert.AreEqual( groundTruth.z, vec3Result.z, 1e-10 );

      var vec3fResult = boxRPY.GetValue<agx.Vec3f>();
      Assert.AreEqual( (float)groundTruth.x, vec3fResult.x, 1e-10 );
      Assert.AreEqual( (float)groundTruth.y, vec3fResult.y, 1e-10 );
      Assert.AreEqual( (float)groundTruth.z, vec3fResult.z, 1e-10 );

      var vector3Result = boxRPY.GetValue<Vector3>();
      Assert.AreEqual( (float)groundTruth.x, vector3Result.x, 1e-10 );
      Assert.AreEqual( (float)groundTruth.y, vector3Result.y, 1e-10 );
      Assert.AreEqual( (float)groundTruth.z, vector3Result.z, 1e-10 );

      var openPLXVec3Result = boxRPY.GetValue<openplx.Math.Vec3>();
      Assert.AreEqual( groundTruth.x, openPLXVec3Result.x(), 1e-10 );
      Assert.AreEqual( groundTruth.y, openPLXVec3Result.y(), 1e-10 );
      Assert.AreEqual( groundTruth.z, openPLXVec3Result.z(), 1e-10 );
    }

    [UnityTest]
    public IEnumerator TestRBLinearVelocitySignal()
    {
      LoadOpenPLX( "rb_velocity_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("rb_velocity_signal_test");

      var input = signals.FindInputTarget("SignalScene.box_linear_vel_in");
      var output = signals.FindOutputSource("SignalScene.box_linear_vel_out");

      input.SendSignal( new agx.Vec3( 1, 1, 1 ) );

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.GetValue<agx.Vec3>();

      Assert.AreEqual( outVel, new agx.Vec3( 1, 1, 1 ) );
    }

    [UnityTest]
    public IEnumerator TestRBAngularVelocitySignal()
    {
      LoadOpenPLX( "rb_velocity_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("rb_velocity_signal_test");

      var input = signals.FindInputTarget("SignalScene.box_angular_vel_in");
      var output = signals.FindOutputSource("SignalScene.box_angular_vel_out");

      input.SendSignal( new agx.Vec3( 1, 1, 1 ) );

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.GetValue<agx.Vec3>();

      Assert.AreEqual( outVel, new agx.Vec3( 1, 1, 1 ) );
    }

    [UnityTest]
    public IEnumerator TestMCLinearVelocitySignal()
    {
      LoadOpenPLX( "mc_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("mc_signal_test");

      var output = signals.FindOutputSource("SignalScene.mc_linvel_signal");

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.GetValue<agx.Vec3>();

      Assert.That( outVel.x, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.y, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.z, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
    }

    [UnityTest]
    public IEnumerator TestMCAngularVelocitySignal()
    {
      LoadOpenPLX( "mc_signal_test.openplx" );

      var signals = FindComponentByName<OpenPLXSignals>("mc_signal_test");

      var output = signals.FindOutputSource("SignalScene.mc_angvel_signal");

      yield return TestUtils.SimulateSeconds( 0.1f );

      var outVel = output.GetValue<agx.Vec3>();

      Assert.That( outVel.x, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.y, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
      Assert.That( outVel.z, Is.Not.EqualTo( 0 ).Within( 1e-10 ) );
    }

    [Test]
    public void TestImportVisualMaterial()
    {
      var material = OpenPLXImporter.ImportOpenPLXFile<Material>( "Assets/Tests/visual_mat.openplx" );

      byte[] data = System.IO.File.ReadAllBytes("Assets/Tests/simple_checkerboard.png");
      var groundTruthTexture = new Texture2D(2, 2);
      groundTruthTexture.LoadImage( data );

      var plxTex = material.mainTexture as Texture2D;

      Assert.That( plxTex.height, Is.EqualTo( groundTruthTexture.height ) );
      Assert.That( plxTex.width, Is.EqualTo( groundTruthTexture.width ) );

      for ( int y = 0; y < plxTex.height; y++ )
        for ( int x = 0; x < plxTex.width; x++ )
          Assert.That( plxTex.GetPixel( x, y ), Is.EqualTo( groundTruthTexture.GetPixel( x, y ) ) );
    }

    [Test]
    public void TestBundleDirectory()
    {
      OpenPLXSettings.Instance.AdditionalBundleDirs.Add( "Assets/Tests/TestBundle" );
      var dependant = OpenPLXImporter.ImportOpenPLXFile<GameObject>("Assets/Tests/use_test_bundle.openplx");

      Assert.NotNull( dependant );
    }
  }
}
