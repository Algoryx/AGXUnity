using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using NUnit.Framework;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class MovableTerrainTests : AGXUnityFixture
  {
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

    [TestCase( true, true )]
    [TestCase( false, true )]
    [TestCase( true, false )]
    [TestCase( false, false )]
    public void OtherCollidersWarnOnlyWithDynamicMassUpdates( bool enableDynamicMassUpdates, bool enableColliderMass )
    {
      var rb = new GameObject( "TerrainBody" ).AddComponent<RigidBody>();
      var terr = CreateManualTerrain( rb );
      terr.EnableDynamicMassUpdates = enableDynamicMassUpdates;

      var collider = new GameObject( "OtherCollider" ).AddComponent<Box>();
      collider.transform.parent = rb.transform;
      collider.EnableMassProperties = enableColliderMass;

      TestUtils.InitializeAll();

      if ( enableDynamicMassUpdates && enableColliderMass )
        LogAssert.Expect( LogType.Warning, new Regex( "Movable terrain .*contains other colliders.*" ) );

      Simulation.Instance.DoStep();

      Assert.That( terr.EnableDynamicMassUpdates, Is.EqualTo( enableDynamicMassUpdates ) );
      LogAssert.NoUnexpectedReceived();
    }

    [TestCase( "mass" )]
    [TestCase( "inertia diagonal" )]
    [TestCase( "center of mass" )]
    public void ManualMassPropertyDisablesDynamicMassUpdates( string property )
    {
      var rb = new GameObject( "TerrainBody" ).AddComponent<RigidBody>();
      var terr = CreateManualTerrain( rb );
      terr.EnableDynamicMassUpdates = true;

      switch ( property ) {
        case "mass":
          rb.MassProperties.Mass.UserValue = 5.0f;
          rb.MassProperties.Mass.UseDefault = false;
          break;
        case "inertia diagonal":
          rb.MassProperties.InertiaDiagonal.UserValue = new Vector3( 2, 3, 4 );
          rb.MassProperties.InertiaDiagonal.UseDefault = false;
          break;
        case "center of mass":
          rb.MassProperties.CenterOfMassOffset.UserValue = new Vector3( 0.1f, 0, 0 );
          rb.MassProperties.CenterOfMassOffset.UseDefault = false;
          break;
      }

      TestUtils.InitializeAll();
      Assert.That( terr.EnableDynamicMassUpdates, Is.True );

      LogAssert.Expect( LogType.Warning, new Regex( $"RigidBody .*manually specified {property} .*Disabling Dynamic Mass Updates\\." ) );
      Simulation.Instance.DoStep();

      Assert.That( terr.EnableDynamicMassUpdates, Is.False );
      Assert.That( terr.GetProperties().getEnableUpdateDynamicBodyMass(), Is.False );

      Simulation.Instance.DoStep();
      LogAssert.NoUnexpectedReceived();
    }

    [TestCase( true )]
    [TestCase( false )]
    public void WorksWithoutParentRigidBody( bool enableDynamicMassUpdates )
    {
      var terr = CreateManualTerrain();
      terr.EnableDynamicMassUpdates = enableDynamicMassUpdates;

      TestUtils.InitializeAll();
      Simulation.Instance.DoStep();
      Simulation.Instance.DoStep();

      Assert.That( terr.Native, Is.Not.Null );
      Assert.That( terr.RigidBody, Is.Null );
      Assert.That( terr.EnableDynamicMassUpdates, Is.EqualTo( enableDynamicMassUpdates ) );
      LogAssert.NoUnexpectedReceived();
    }

    private static MovableTerrain CreateManualTerrain( RigidBody rb = null )
    {
      var go = new GameObject( "Terrain" );
      if ( rb != null )
        go.transform.parent = rb.transform;

      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Manual;
      return terr;
    }

    [UnityTest]
    public IEnumerator DynamicMassUpdates()
    {
      var bedGO = new GameObject("BedBody");

      var terrainGO = new GameObject("Terrain");
      terrainGO.transform.parent = bedGO.transform;

      var terrainRB = terrainGO.AddComponent<RigidBody>();
      var terr = terrainGO.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.EnableDynamicMassUpdates = true;
      terr.MaximumDepth = 0.0f;
      terr.TerrainBedMargin = 0;
      var constraint = Constraint.Create( ConstraintType.LockJoint, new ConstraintFrame( terrainRB.gameObject ), new ConstraintFrame() );

      // This geometry is just a modelling geometry
      var boxGO = new GameObject( "Box" );
      boxGO.transform.parent = bedGO.transform;
      var boxRB = boxGO.AddComponent<RigidBody>();
      var boxGeom = boxGO.AddComponent<Box>();
      boxGeom.HalfExtents = new Vector3( 2, 0.1f, 2 );
      terr.AddBedGeometry( boxGeom );

      var bedLock = bedGO.AddComponent<KinematicLock>();
      bedLock.Add( terrainRB );
      bedLock.Add( boxRB );

      TestUtils.InitializeAll();
      constraint.Native.setEnableComputeForces( true );

      yield return TestUtils.SimulateSeconds( .2f );

      agx.Vec3 force = new agx.Vec3(),torque = new agx.Vec3();
      constraint.Native.getLastForce( terrainRB.Native, ref force, ref torque );
      Assert.That( torque.x, Is.Zero.Within( 1e-9 ) );
      Assert.That( torque.y, Is.Zero.Within( 1e-9 ) );
      Assert.That( torque.z, Is.Zero.Within( 1e-9 ) );

      var preCM = terrainRB.MassProperties.CenterOfMassOffset.Value;
      var preMass = terrainRB.MassProperties.Mass.Value;
      var preInertiaDiag = terrainRB.MassProperties.InertiaDiagonal.Value;
      var preInertiaOffDiag = terrainRB.MassProperties.InertiaOffDiagonal.Value;

      var numCreatedParticles = 0;
      var t1 = Simulation.Instance.Native.getTimeStamp();
      while ( Simulation.Instance.Native.getTimeStamp() - t1 < 3 ) {
        var particle = terr.GetSoilSimulationInterface().createSoilParticle( 0.1, new agx.Vec3( 1, 1, 1 ) );
        ++numCreatedParticles;
        yield return TestUtils.Step();
      }

      Assert.That( terr.GetParticles().size(), Is.LessThan( numCreatedParticles ), "Particles should merge into the terrain" );
      Assert.That( terr.EnableDynamicMassUpdates, Is.True );

      var cmDiff = terrainRB.MassProperties.CenterOfMassOffset.Value - preCM;
      Assert.That( cmDiff.x, Is.LessThan( -0.1f ), "Center-of-mass should move towards the direction of the added mass" );
      Assert.That( cmDiff.z, Is.GreaterThan( 0.1f ), "Center-of-mass should move towards the direction of the added mass" );

      var massDiff = terrainRB.MassProperties.Mass.Value - preMass;
      Assert.That( massDiff, Is.GreaterThan( 100 ), "The added mass should be registered in the body" );
      Assert.That( terrainRB.Native.getMassProperties().getMass(), Is.EqualTo( terrainRB.MassProperties.Mass.Value ).Within( 1e-3 ), "Native and Unity mass should agree" );

      var inertiaDiagDiff = terrainRB.MassProperties.InertiaDiagonal.Value - preInertiaDiag;
      Assert.That( inertiaDiagDiff.x, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );
      Assert.That( inertiaDiagDiff.y, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );
      Assert.That( inertiaDiagDiff.z, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia diagonal should change with added mass" );

      var inertiaOffDiagDiff = terrainRB.MassProperties.InertiaOffDiagonal.Value - preInertiaOffDiag;
      Assert.That( inertiaOffDiagDiff.x, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );
      Assert.That( inertiaOffDiagDiff.y, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );
      Assert.That( inertiaOffDiagDiff.z, Is.Not.EqualTo( 0 ).Within( 1 ), "Intertia off-diagonal should change with added mass" );

      constraint.Native.getLastForce( terrainRB.Native, ref force, ref torque );
      Assert.That( torque.x, Is.LessThan( -800 ), "Constraint should apply a torque to the body after adding mass" );
      Assert.That( torque.z, Is.GreaterThan( 800 ), "Constraint should apply a torque to the body after adding mass" );
      LogAssert.NoUnexpectedReceived();
    }
  }
}
