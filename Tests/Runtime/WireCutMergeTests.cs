using AGXUnity;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class WireCutMergeTests : AGXUnityFixture
  {
    private Wire SourceWire;
    [UnitySetUp]
    public IEnumerator SetupWireScene()
    {
      var wireObject = new GameObject("Source Wire");
      SourceWire = wireObject.AddComponent<Wire>();

      SourceWire.Route.Add( Wire.NodeType.WinchNode, null, Vector3.left );
      SourceWire.Route.Add( Wire.NodeType.WinchNode, null, Vector3.right );

      yield return TestUtils.WaitUntilLoaded();
    }

    private Wire[] FindWires()
    {
#if UNITY_2022_2_OR_NEWER
      return UnityEngine.Object.FindObjectsByType<Wire>( FindObjectsSortMode.None );
#else
      return UnityEngine.Object.FindObjectsOfType<Wire>();
#endif
    }

    [TearDown]
    public void TearDownWireScene()
    {
      foreach ( var wire in FindWires() )
        Object.Destroy( wire.gameObject );
    }

    [Test]
    public void SimpleCutSucceeds()
    {
      var result = SourceWire.Cut( Vector3.zero );
      Assert.NotNull( result, "Cutting failed" );
      Assert.AreEqual( 2, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [Test]
    public void MultipleCutsSucceeds()
    {
      var current = SourceWire;
      for ( float x = -0.5f; x < 0.9f; x += 0.5f ) {
        current = current.Cut( new Vector3( x, 0, 0 ) );
        Assert.NotNull( current, "Cutting failed" );
      }
      Assert.AreEqual( 4, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [Test]
    public void CuttingMigratesWinches()
    {
      var result = SourceWire.Cut(Vector3.zero);
      Assert.NotNull( result, "Cutting failed" );

      Assert.NotNull( SourceWire.BeginWinch, "SourceWire did not retain it's BeginWinch after cut" );
      Assert.Null( result.BeginWinch, "Cut result contains a BeginWinch after cut" );

      Assert.Null( SourceWire.EndWinch, "SourceWire still has an EndWire after cut" );
      Assert.NotNull( result.EndWinch, "EndWinch was not moved to the cut result wire" );
    }

    [Test]
    public void MultipleCutsMigratesWinches()
    {
      var result1 = SourceWire.Cut(new Vector3(-0.5f,0.0f,0.0f));
      Assert.NotNull( result1, "Cutting failed" );
      var result2 = result1.Cut(Vector3.zero);
      Assert.NotNull( result2, "Cutting failed" );
      var result3 = result2.Cut(new Vector3(0.5f,0.0f,0.0f));
      Assert.NotNull( result3, "Cutting failed" );

      Assert.NotNull( SourceWire.BeginWinch, "SourceWire did not retain it's BeginWinch after cut" );

      Assert.Null( result1.BeginWinch, "Cut result 1 contains a BeginWinch after cut" );
      Assert.Null( result1.EndWinch, "Cut result 1 still has an EndWire after cut" );
      Assert.Null( result2.BeginWinch, "Cut result 2 contains a BeginWinch after cut" );
      Assert.Null( result2.EndWinch, "Cut result 2 still has an EndWire after cut" );
      Assert.Null( result3.BeginWinch, "Cut result 3 contains a BeginWinch after cut" );

      Assert.NotNull( result3.EndWinch, "EndWinch was not moved to the cut result wire" );
    }

    [Test]
    public void EndCutFails()
    {
      var result = SourceWire.Cut( Vector3.left );
      Assert.Null( result, "Cutting should fail" );
      Assert.AreEqual( 1, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [UnityTest]
    public IEnumerator SimpleMergeSucceeds()
    {
      var result = SourceWire.Cut( Vector3.zero );
      Assert.NotNull( result, "Cutting failed" );

      Assert.True( SourceWire.Merge( result ), "Merge failed" );
      // Need to wait a frame to ensure object is destroyed
      yield return null;
      Assert.AreEqual( 1, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [UnityTest]
    public IEnumerator MultipleMergesSucceeds()
    {
      var result1 = SourceWire.Cut(new Vector3(-0.5f,0.0f,0.0f));
      Assert.NotNull( result1, "Cutting failed" );
      var result2 = result1.Cut(Vector3.zero);
      Assert.NotNull( result2, "Cutting failed" );
      var result3 = result2.Cut(new Vector3(0.5f,0.0f,0.0f));
      Assert.NotNull( result3, "Cutting failed" );

      Assert.True( result2.Merge( result3 ), "Merge failed" );
      Assert.True( result1.Merge( result2 ), "Merge failed" );
      Assert.True( SourceWire.Merge( result1 ), "Merge failed" );

      // Need to wait a frame to ensure object is destroyed
      yield return null;
      Assert.AreEqual( 1, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [UnityTest]
    public IEnumerator InvalidWinchesFailsMerge()
    {
      var result = SourceWire.Cut( Vector3.zero );
      Assert.NotNull( result, "Cutting failed" );

      Assert.False( result.Merge( SourceWire ), "Merge should fail" );
      // Need to wait a frame to ensure object is destroyed
      yield return null;
      Assert.AreEqual( 2, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [UnityTest]
    public IEnumerator MergingMigratesWinches()
    {
      var result = SourceWire.Cut( Vector3.zero );
      Assert.NotNull( result, "Cutting failed" );

      Assert.True( SourceWire.Merge( result ), "Merge failed" );

      Assert.NotNull( SourceWire.BeginWinch, "BeginWinch should not have been touched" );
      Assert.NotNull( SourceWire.EndWinch, "EndWinch was not moved to the merged wire after merge" );

      // Need to wait a frame to ensure object is destroyed
      yield return null;
      Assert.AreEqual( 1, FindWires().Length, "Unexpected number of wires in the simulation" );
    }

    [UnityTest]
    public IEnumerator MultipleMergesMigratesWinches()
    {
      var result1 = SourceWire.Cut(new Vector3(-0.5f,0.0f,0.0f));
      Assert.NotNull( result1, "Cutting failed" );
      var result2 = result1.Cut(Vector3.zero);
      Assert.NotNull( result2, "Cutting failed" );
      var result3 = result2.Cut(new Vector3(0.5f,0.0f,0.0f));
      Assert.NotNull( result3, "Cutting failed" );

      Assert.True( result2.Merge( result3 ), "Merge failed" );
      Assert.NotNull( result2.EndWinch, "EndWinch was not moved to the merged wire after merge" );
      Assert.True( result1.Merge( result2 ), "Merge failed" );
      Assert.NotNull( result1.EndWinch, "EndWinch was not moved to the merged wire after merge" );
      Assert.True( SourceWire.Merge( result1 ), "Merge failed" );

      Assert.NotNull( SourceWire.BeginWinch, "BeginWinch should not have been touched" );
      Assert.NotNull( SourceWire.EndWinch, "EndWinch was not moved to the merged wire after merge" );

      // Need to wait a frame to ensure object is destroyed
      yield return null;
      Assert.AreEqual( 1, FindWires().Length, "Unexpected number of wires in the simulation" );
    }
  }
}
