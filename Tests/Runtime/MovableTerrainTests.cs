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
  public class MovableTerrainTests
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
  }
}
