using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class MovableTerrainTests : AGXUnityFixture
  {

    [UnityTearDown]
    public IEnumerator CleanScene()
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
    public void TestCreateMovable()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Manual;

      TestUtils.InitializeAll();
    }

    [Test]
    public void CreateAutomaticFailsWithNoGeoms()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();

      terr.PlacementMode = MovableTerrain.Placement.Automatic;

      LogAssert.Expect( LogType.Error, new Regex( ".*no bed geometries.*" ) );

      TestUtils.InitializeAll();
    }

    [Test]
    public void ChangingInitializedIsDisallowed()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();

      terr.PlacementMode = MovableTerrain.Placement.Manual;

      TestUtils.InitializeAll();

      var preSizeM = terr.SizeMeters;
      var preSizeC = terr.SizeCells;
      var preSizeE = terr.ElementSize;

      LogAssert.Expect( LogType.Error, new Regex( "Cannot.*" ) );
      terr.SizeMeters = Vector3.one;
      Assert.AreEqual( terr.SizeMeters, preSizeM );

      LogAssert.Expect( LogType.Error, new Regex( "Cannot.*" ) );
      terr.SizeCells = Vector2Int.one;
      Assert.AreEqual( terr.SizeCells, preSizeC );

      LogAssert.Expect( LogType.Error, new Regex( "Cannot.*" ) );
      terr.ElementSize = 1.0f;
      Assert.AreEqual( terr.ElementSize, preSizeE );
    }

    [Test]
    public void CreateSimpleAutomaticBed()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.MaximumDepth = 0.0f;
      terr.TerrainBedMargin = 0;

      var box = new GameObject( "BedBox" );
      var boxGeom = box.AddComponent<Box>();

      var boxPos = new Vector3( 1, 2, 3 );
      var boxSize = new Vector3( 1, 0.2f, 2 );

      boxGeom.HalfExtents = boxSize;
      box.transform.position = boxPos;

      terr.AddBedGeometry( boxGeom );

      TestUtils.InitializeAll();

      Assert.That( go.transform.position.x, Is.EqualTo( boxPos.x ) );
      Assert.That( go.transform.position.y, Is.EqualTo( boxPos.y + boxSize.y ) );
      Assert.That( go.transform.position.z, Is.EqualTo( boxPos.z ) );

      Assert.That( terr.SizeMeters.x, Is.EqualTo( boxSize.x * 2 ) );
      Assert.That( terr.SizeMeters.y, Is.EqualTo( boxSize.z * 2 ) );
    }

    [UnityTest]
    public IEnumerator DynamicMassUpdates()
    {
      var rbGO = new GameObject("BedBody");
      var rb = rbGO.AddComponent<RigidBody>();

      var constraint = Constraint.Create( ConstraintType.LockJoint, new ConstraintFrame( rb.gameObject ), new ConstraintFrame() );

      var go = new GameObject("Terrain");
      go.transform.parent = rb.transform;
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.MaximumDepth = 0.0f;
      terr.TerrainBedMargin = 0;

      var box = new GameObject( "BedBox" );
      box.transform.parent = rb.transform;
      var boxGeom = box.AddComponent<Box>();


      boxGeom.HalfExtents = new Vector3( 2, 0.1f, 2 );
      terr.AddBedGeometry( boxGeom );

      TestUtils.InitializeAll();
      constraint.Native.setEnableComputeForces( true );

      yield return TestUtils.SimulateSeconds( 0.2f );

      agx.Vec3 force = new agx.Vec3(),torque = new agx.Vec3();
      constraint.Native.getLastForce( rb.Native, ref force, ref torque );
      Assert.That( torque.x, Is.Zero.Within( 1e-9 ) );
      Assert.That( torque.y, Is.Zero.Within( 1e-9 ) );
      Assert.That( torque.z, Is.Zero.Within( 1e-9 ) );

      var preCM = rb.MassProperties.CenterOfMassOffset.Value;
      var preMass = rb.MassProperties.Mass.Value;
      var preInertiaDiag = rb.MassProperties.InertiaDiagonal.Value;
      var preInertiaOffDiag = rb.MassProperties.InertiaOffDiagonal.Value;

      var t1 = Simulation.Instance.Native.getTimeStamp();
      while ( Simulation.Instance.Native.getTimeStamp() - t1 < 2 ) {
        var particle = terr.GetSoilSimulationInterface().createSoilParticle( 0.1, new agx.Vec3( 1, 1, 1 ) );
        yield return TestUtils.Step();
      }

      var cmDiff = rb.MassProperties.CenterOfMassOffset.Value - preCM;
      Assert.That( cmDiff.x, Is.LessThan( -0.1f ), "Center-of-mass should move towards the direction of the added mass" );
      Assert.That( cmDiff.z, Is.GreaterThan( 0.1f ), "Center-of-mass should move towards the direction of the added mass" );

      var massDiff = rb.MassProperties.Mass.Value - preMass;
      Assert.That( massDiff, Is.GreaterThan( 100 ), "The added mass should be registered in the body" );

      var inertiaDiagDiff = rb.MassProperties.InertiaDiagonal.Value - preInertiaDiag;
      Assert.That( inertiaDiagDiff.x, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );
      Assert.That( inertiaDiagDiff.y, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );
      Assert.That( inertiaDiagDiff.z, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );

      var inertiaOffDiagDiff = rb.MassProperties.InertiaOffDiagonal.Value - preInertiaOffDiag;
      Assert.That( inertiaOffDiagDiff.x, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );
      Assert.That( inertiaOffDiagDiff.y, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );
      Assert.That( inertiaOffDiagDiff.z, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );

      constraint.Native.getLastForce( rb.Native, ref force, ref torque );
      Assert.That( torque.x, Is.LessThan( -800 ), "Constraint should apply a torque to the body after adding mass" );
      Assert.That( torque.z, Is.GreaterThan( 800 ), "Constraint should apply a torque to the body after adding mass" );
    }
  }
}
