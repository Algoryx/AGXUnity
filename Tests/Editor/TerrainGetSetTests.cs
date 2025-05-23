using AGXUnity.Model;
using NUnit.Framework;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class TerrainGetSetTests
  {
    private DeformableTerrain testTerrain;
    private Terrain unityTerrain;

    private const float HEIGHT_DELTA = 0.0005f;

    private float GetHeight( int x, int y, bool normalize = false )
    {
      var heightScale = normalize ? unityTerrain.terrainData.size.y : 1;
      var res = unityTerrain.terrainData.heightmapResolution;
      return ( 2 + Mathf.Sin( (float)y / res ) + Mathf.Cos( (float)x / res ) ) / heightScale;
    }

    private float RescaleUnityHeight( float height, bool normalized = false )
    {
      return height * ( normalized ? unityTerrain.terrainData.size.y : 1.0f );
    }

    [SetUp]
    public void SetupWireScene()
    {
      GameObject go = new GameObject("Test terrain");

      unityTerrain = go.AddComponent<Terrain>();
      unityTerrain.terrainData = new TerrainData();
      unityTerrain.terrainData.size = new Vector3( 30, 20, 30 );
      unityTerrain.terrainData.heightmapResolution = 33;

      float [,] heights = new float[33,33];
      for ( int y = 0; y < 33; y++ )
        for ( int x = 0; x < 33; x++ )
          heights[ y, x ] = GetHeight( x, y, true );

      unityTerrain.terrainData.SetHeights( 0, 0, heights );

      testTerrain = go.AddComponent<DeformableTerrain>();
      testTerrain.MaximumDepth = 2;
    }

    [Test]
    public void TestTerrainGetSingleHeight()
    {
      var height = testTerrain.GetHeight( 16, 16 );
      var expected = 2 + Mathf.Sin( (float)16 / 33 ) + Mathf.Cos( (float)16 /33 );
      Assert.AreEqual( expected, height, HEIGHT_DELTA );
    }

    [Test]
    public void TestTerrainGetMultipleHeights()
    {
      var height = testTerrain.GetHeights( 10, 10, 10, 10 );
      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( GetHeight( x+10, y+10 ), height[ y, x ], HEIGHT_DELTA );
    }

    [Test]
    public void TestSetSingleHeight()
    {
      testTerrain.SetHeight( 16, 16, 5f );
      var result = testTerrain.GetHeight( 16, 16 );

      Assert.AreEqual( 5f, result, HEIGHT_DELTA );
    }

    [Test]
    public void TestSetSingleHeightIsPropagated()
    {
      testTerrain.SetHeight( 16, 16, 5f );

      var result = unityTerrain.terrainData.GetHeight( 16, 16 );

      Assert.AreEqual( 5f, RescaleUnityHeight( result ), HEIGHT_DELTA );
    }

    [Test]
    public void TestSetMultipleHeights()
    {
      var source = new float[10,10];
      var expected = new float[10,10];
      for ( int y = 0; y < 10; y++ ) {
        for ( int x = 0; x < 10; x++ ) {
          source[ y, x ] = GetHeight( x, y );
          expected[ y, x ] = GetHeight( x, y );
        }
      }

      testTerrain.SetHeights( 10, 10, source );
      var results = testTerrain.GetHeights( 10, 10, 10, 10 );

      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( expected[ y, x ], results[ y, x ], HEIGHT_DELTA );
    }

    [Test]
    public void TestSetMultipleHeightsArePropagated()
    {
      var source = new float[10,10];
      var expected = new float[10,10];
      for ( int y = 0; y < 10; y++ ) {
        for ( int x = 0; x < 10; x++ ) {
          source[ y, x ] = GetHeight( x, y );
          expected[ y, x ] = GetHeight( x, y );
        }
      }
      testTerrain.SetHeights( 10, 10, source );

      var results = unityTerrain.terrainData.GetHeights( 10, 10, 10, 10 );

      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( expected[ y, x ], RescaleUnityHeight( results[ y, x ], true ), HEIGHT_DELTA );
    }
  }
}
