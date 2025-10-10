using AGXUnity;
using AGXUnity.Model;

using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class TerrainTests
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
      return height * ( normalized ? unityTerrain.terrainData.size.y : 1.0f ) - testTerrain.MaximumDepth;
    }

    [UnitySetUp]
    public IEnumerator SetupTerrainScene()
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

      yield return TestUtils.WaitUntilLoaded();
    }

    [UnityTearDown]
    public IEnumerator TearDownTerrainScene()
    {
      GameObject.Destroy( unityTerrain.gameObject );
      yield return null;
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

      Assert.AreEqual( 5f, result );
    }

    [UnityTest]
    public IEnumerator TestSetSingleHeightIsPropagated()
    {
      testTerrain.SetHeight( 16, 16, 5f );

      yield return TestUtils.SimulateSeconds( 0.1f );

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

    [UnityTest]
    public IEnumerator TestSetMultipleHeightsArePropagated()
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
      yield return TestUtils.SimulateSeconds( 0.1f );

      var results = unityTerrain.terrainData.GetHeights( 10, 10, 10, 10 );

      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( expected[ y, x ], RescaleUnityHeight( results[ y, x ], true ), HEIGHT_DELTA );
    }

    [Test]
    public void TestClampedTerrainGivesWarning()
    {
      GameObject go = new GameObject("Clamped Terrain");

      unityTerrain = go.AddComponent<Terrain>();
      unityTerrain.terrainData = new TerrainData();
      unityTerrain.terrainData.size = new Vector3( 30, 10, 30 );
      unityTerrain.terrainData.heightmapResolution = 33;

      float [,] heights = new float[33,33];
      for ( int y = 0; y < 33; y++ )
        for ( int x = 0; x < 33; x++ )
          heights[ y, x ] = 5.0f / unityTerrain.terrainData.heightmapScale.y;

      unityTerrain.terrainData.SetHeights( 0, 0, heights );

      testTerrain = go.AddComponent<DeformableTerrain>();
      testTerrain.MaximumDepth = 10;

      LogAssert.Expect( LogType.Warning, $"Terrain heights were clamped! Max allowed: 10, Max Encountered: 15 and AGXUnity.Model.DeformableTerrain.MaximumDepth = 10. Resolve this by increasing max height and lower the terrain or decrease Maximum Depth." );
      testTerrain.GetInitialized();
    }
  }
  public class PagerTests
  {
    private DeformableTerrainPager testTerrain;
    private Terrain unityTerrain;
    private GameObject pagerProbe;

    private const float HEIGHT_DELTA = 0.0005f;

    private float GetHeight( int x, int y, bool normalize = false )
    {
      var heightScale = normalize ? unityTerrain.terrainData.size.y : 1;
      var res = unityTerrain.terrainData.heightmapResolution;
      return ( 2 + Mathf.Sin( (float)y / res ) + Mathf.Cos( (float)x / res ) ) / heightScale;
    }

    private float RescaleUnityHeight( float height, bool normalized = false )
    {
      return height * ( normalized ? unityTerrain.terrainData.size.y : 1.0f ) - testTerrain.MaximumDepth;
    }

    [UnitySetUp]
    public IEnumerator SetupTerrainScene()
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

      testTerrain = go.AddComponent<DeformableTerrainPager>();
      testTerrain.MaximumDepth = 2;

      pagerProbe = Factory.Create<RigidBody>();

      testTerrain.Add( pagerProbe.GetComponent<RigidBody>() );

      // Ensure that the middle tile is paged in
      yield return TestUtils.SimulateSeconds( 0.2f );
    }

    [UnityTearDown]
    public IEnumerator TearDownTerrainScene()
    {
      GameObject.Destroy( unityTerrain.gameObject );
      GameObject.Destroy( pagerProbe );
      yield return null;
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

      Assert.AreEqual( 5f, result );
    }

    [UnityTest]
    public IEnumerator TestSetSingleHeightIsPropagated()
    {
      testTerrain.SetHeight( 16, 16, 5f );

      yield return TestUtils.SimulateSeconds( 0.1f );

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

    [UnityTest]
    public IEnumerator TestSetMultipleHeightsArePropagated()
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
      yield return TestUtils.SimulateSeconds( 0.1f );

      var results = unityTerrain.terrainData.GetHeights( 10, 10, 10, 10 );

      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( expected[ y, x ], RescaleUnityHeight( results[ y, x ], true ), HEIGHT_DELTA );
    }

    [UnityTest]
    public IEnumerator TestPagerReset()
    {
      var initial = testTerrain.GetHeights(10,10,10,10);

      var source = new float[10,10];
      for ( int y = 0; y < 10; y++ ) {
        for ( int x = 0; x < 10; x++ ) {
          source[ y, x ] = GetHeight( x, y );
        }
      }

      testTerrain.SetHeights( 10, 10, source );
      yield return TestUtils.SimulateSeconds( 0.1f );
      testTerrain.Native.reset();
      yield return TestUtils.SimulateSeconds( 0.1f );
      var results = testTerrain.GetHeights( 10, 10, 10, 10 );

      for ( int y = 0; y < 10; y++ )
        for ( int x = 0; x < 10; x++ )
          Assert.AreEqual( initial[ y, x ], results[ y, x ], HEIGHT_DELTA );
    }
  }
}
