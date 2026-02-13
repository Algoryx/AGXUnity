using AGXUnity.Collide;
using AGXUnity.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Editor
{
  public class MovableTerrainTests
  {

    [TearDown]
    public void CleanScene()
    {
      var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
      foreach ( var obj in scene.GetRootGameObjects() )
        GameObject.DestroyImmediate( obj );
    }

    [Test]
    public void TestCreateMovable()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Manual;
    }

    [Test]
    public void CreateAutomatic()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();

      terr.PlacementMode = MovableTerrain.Placement.Automatic;
    }

    [Test]
    public void AutomaticDissallowsManualChanges()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();

      terr.PlacementMode = MovableTerrain.Placement.Automatic;

      var preSizeM = terr.SizeMeters;
      var preSizeC = terr.SizeCells;
      var preSizeE = terr.ElementSize;

      LogAssert.Expect( LogType.Error, new System.Text.RegularExpressions.Regex( ".*size cannot.*" ) );
      terr.SizeMeters = Vector3.one;
      Assert.AreEqual( terr.SizeMeters, preSizeM );

      LogAssert.Expect( LogType.Error, new System.Text.RegularExpressions.Regex( ".*size cannot.*" ) );
      terr.SizeCells = Vector2Int.one;
      Assert.AreEqual( terr.SizeCells, preSizeC );

      LogAssert.Expect( LogType.Error, new System.Text.RegularExpressions.Regex( ".*size cannot.*" ) );
      terr.ElementSize = 1.0f;
      Assert.AreEqual( terr.ElementSize, preSizeE );
    }

    [Test]
    public void SideRenderingIncreasesVertexCount()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Manual;
      terr.EditorUpdate();

      var mf = go.GetComponent<MeshFilter>();
      Assert.NotNull( mf );

      var preVertexCount = mf.sharedMesh.vertexCount;

      terr.RenderSides = false;

      Assert.That( mf.sharedMesh.vertexCount, Is.LessThan( preVertexCount ) );
    }

    [Test]
    public void HeightsAsVertexColorsSetsColors()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      var mf = go.GetComponent<MeshFilter>();
      Assert.NotNull( mf );

      terr.MaximumDepth = 1.0f;
      terr.MaxDepthAsInitialHeight = true;
      terr.HeightsAsVertexColors = true;

      Assert.That( mf.sharedMesh.colors[ 0 ].r, Is.EqualTo( 1.0f ) );
    }

    [Test]
    public void CellSizeMatchesVertexCount()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      var mf = go.GetComponent<MeshFilter>();
      Assert.NotNull( mf );

      terr.PlacementMode = MovableTerrain.Placement.Manual;
      terr.SizeCells = new Vector2Int( 15, 20 );
      terr.RenderSides = false;

      Assert.That( mf.sharedMesh.vertexCount, Is.EqualTo( 15 * 20 ) );
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

      Assert.That( go.transform.position.x, Is.EqualTo( boxPos.x ) );
      Assert.That( go.transform.position.y, Is.EqualTo( boxPos.y + boxSize.y ) );
      Assert.That( go.transform.position.z, Is.EqualTo( boxPos.z ) );

      Assert.That( terr.SizeMeters.x, Is.EqualTo( boxSize.x * 2 ) );
      Assert.That( terr.SizeMeters.y, Is.EqualTo( boxSize.z * 2 ) );
    }

    [Test]
    public void AutomaticBedMarginShrinksTerrain()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.MaximumDepth = 0.0f;
      terr.TerrainBedMargin = 0.02f;

      var box = new GameObject( "BedBox" );
      var boxGeom = box.AddComponent<Box>();

      var boxSize = new Vector3( 1, 0.2f, 1 );

      boxGeom.HalfExtents = boxSize;

      terr.AddBedGeometry( boxGeom );

      Assert.That( terr.SizeMeters.x, Is.EqualTo( boxSize.x * 2 - 2 * 0.02f ) );
      Assert.That( terr.SizeMeters.y, Is.EqualTo( boxSize.z * 2 - 2 * 0.02f ) );
    }

    [Test]
    public void AutomaticBedFindsHeights()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      var mf = go.GetComponent<MeshFilter>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.MaximumDepth = 0.0f;

      // Base terrain heights
      var b1 = new GameObject( "BedBox" );
      var b1geom = b1.AddComponent<Box>();

      var b1Size = new Vector3( 1, 0.2f, 1 );
      b1geom.HalfExtents = b1Size;

      // Offset half of the terrain
      var b2 = new GameObject( "BedBox2" );
      var b2geom = b2.AddComponent<Box>();

      var b2Size = new Vector3( 1, 0.4f, 0.5f );
      b2geom.HalfExtents = b2Size;
      b2geom.transform.position = new Vector3( 0, 0, -0.5f );

      // Ensure the middle is clear from grid points
      terr.Resolution = 10;

      terr.AddBedGeometry( b1geom );
      terr.AddBedGeometry( b2geom );

      var vertices = mf.sharedMesh.vertices;
      for ( int y = 0; y < terr.SizeCells.y; y++ )
        for ( int x = 0; x < terr.SizeCells.x; x++ ) {
          if ( y < terr.SizeCells.y / 2 )
            Assert.That( vertices[ y * terr.SizeCells.x + x ].y, Is.EqualTo( b2Size.y - b1Size.y ).Within( 1e-9 ) );
          else
            Assert.That( vertices[ y * terr.SizeCells.x + x ].y, Is.EqualTo( 0 ).Within( 1e-9 ) );
        }
    }

    [Test]
    public void AutomaticBedCalculatesElementSize()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.TerrainBedMargin = 0.0f;
      terr.Resolution = 10;

      var box = new GameObject( "BedBox" );
      var boxGeom = box.AddComponent<Box>();

      var boxSize = new Vector3( 1, 0.2f, 1 );
      boxGeom.HalfExtents = boxSize;

      terr.AddBedGeometry( boxGeom );

      Assert.That( terr.ElementSize, Is.EqualTo( boxSize.x * 2 / ( terr.Resolution-1 ) ) );
    }

    [Test]
    public void RemovingFinalBedIsOK()
    {
      var go = new GameObject("Terrain");
      var terr = go.AddComponent<MovableTerrain>();
      terr.PlacementMode = MovableTerrain.Placement.Automatic;
      terr.TerrainBedMargin = 0.0f;
      terr.Resolution = 10;

      var box = new GameObject( "BedBox" );
      var boxGeom = box.AddComponent<Box>();

      terr.AddBedGeometry( boxGeom );
      terr.RemoveBedGeometry( boxGeom );
    }
  }
}
