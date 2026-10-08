using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Utils;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class CollisionGroupsTests : AGXUnityFixture
  {
    private const string GroupTag = "Terrain collision group";
    private const string OtherGroupTag = "Other terrain collision group";
    private const string PagerWarning = "DeformableTerrainPager does not currently support collision groups";

    private readonly List<GameObject> m_objects = new List<GameObject>();
    private readonly HashSet<UnityEngine.Object> m_resources = new HashSet<UnityEngine.Object>();

    private GameObject CreateObject( string name, Transform parent = null )
    {
      var go = new GameObject( name );
      go.transform.SetParent( parent );
      m_objects.Add( go );
      return go;
    }

    private DeformableTerrainBase CreateTerrain( Type type, Transform parent = null )
    {
      var go = CreateObject( type.Name, parent );
      if ( type != typeof( MovableTerrain ) ) {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3( 4, 2, 4 ) };
        m_resources.Add( data );
        go.AddComponent<Terrain>().terrainData = data;
      }

      var terrain = (DeformableTerrainBase)go.AddComponent( type );
      terrain.MaximumDepth = 0.5f;
      if ( terrain is MovableTerrain movable ) {
        movable.PlacementMode = MovableTerrain.Placement.Manual;
        movable.SizeCells = new Vector2Int( 5, 5 );
      }
      // The collision group warning should not require initializing a pager.
      if ( terrain is DeformableTerrainPager )
        terrain.enabled = false;
      return terrain;
    }

    private agxCollide.Geometry GetGeometry( DeformableTerrainBase terrain )
    {
      Assert.NotNull( terrain.GetInitialized() );
      return terrain is MovableTerrain movable ?
               movable.Native.getGeometry() :
               ( (DeformableTerrain)terrain ).Native.getGeometry();
    }

    [UnityTearDown]
    public IEnumerator DestroyTerrainObjects()
    {
      foreach ( var go in m_objects ) {
        if ( go == null )
          continue;
        var unityTerrain = go.GetComponent<Terrain>();
        if ( unityTerrain != null )
          m_resources.Add( unityTerrain.terrainData );
        var movable = go.GetComponent<MovableTerrain>();
        if ( movable != null ) {
          m_resources.Add( movable.TerrainMesh.sharedMesh );
          m_resources.Add( movable.TerrainRenderer.sharedMaterial );
        }
      }

      // Destroy collision group components before the native terrain they reference.
      foreach ( var go in m_objects )
        if ( go != null && go.TryGetComponent<CollisionGroups>( out var groups ) )
          UnityEngine.Object.DestroyImmediate( groups );

      yield return TestUtils.DestroyAndWait( m_objects.ToArray() );
      foreach ( var resource in m_resources )
        if ( resource != null )
          UnityEngine.Object.Destroy( resource );
      m_objects.Clear();
      m_resources.Clear();
    }

    [TestCase( typeof( DeformableTerrain ), false )]
    [TestCase( typeof( DeformableTerrain ), true )]
    [TestCase( typeof( MovableTerrain ), false )]
    [TestCase( typeof( MovableTerrain ), true )]
    public void InitializationAddsConfiguredGroupToTerrainGeometry( Type type, bool forceContactUpdate )
    {
      var terrain = CreateTerrain( type );
      var groups = terrain.gameObject.AddComponent<CollisionGroups>();
      Assert.True( groups.AddGroup( GroupTag, false, forceContactUpdate ) );

      Assert.NotNull( groups.GetInitialized() );

      Assert.That( terrain.State, Is.EqualTo( ScriptComponent.States.INITIALIZED ) );
      Assert.True( GetGeometry( terrain ).hasGroup( GroupTag.To32BitFnv1aHash() ) );
    }

    [TestCase( typeof( DeformableTerrain ), false )]
    [TestCase( typeof( DeformableTerrain ), true )]
    [TestCase( typeof( MovableTerrain ), false )]
    [TestCase( typeof( MovableTerrain ), true )]
    public void RuntimeAddAndRemoveUpdateTerrainGeometry( Type type, bool forceContactUpdate )
    {
      var terrain = CreateTerrain( type );
      var groups = terrain.gameObject.AddComponent<CollisionGroups>();
      Assert.NotNull( groups.GetInitialized() );
      var geometry = GetGeometry( terrain );
      Assert.False( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );

      Assert.True( groups.AddGroup( GroupTag, false, forceContactUpdate ) );
      Assert.True( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.False( groups.AddGroup( GroupTag, false, forceContactUpdate ) );
      Assert.That( groups.Groups.Count, Is.EqualTo( 1 ) );
      Assert.True( groups.AddGroup( OtherGroupTag, false, forceContactUpdate ) );

      Assert.True( groups.RemoveGroup( GroupTag ) );
      Assert.False( groups.HasGroup( GroupTag ) );
      Assert.False( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.True( geometry.hasGroup( OtherGroupTag.To32BitFnv1aHash() ) );
      Assert.False( groups.RemoveGroup( GroupTag ) );
    }

    [TestCase( typeof( DeformableTerrain ), false )]
    [TestCase( typeof( DeformableTerrain ), true )]
    [TestCase( typeof( MovableTerrain ), false )]
    [TestCase( typeof( MovableTerrain ), true )]
    public void DisableAndEnableRemoveAndRestoreTerrainGroups( Type type, bool forceContactUpdate )
    {
      var terrain = CreateTerrain( type );
      var groups = terrain.gameObject.AddComponent<CollisionGroups>();
      groups.AddGroup( GroupTag, false, forceContactUpdate );
      Assert.NotNull( groups.GetInitialized() );
      var geometry = GetGeometry( terrain );
      Assert.True( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );

      groups.enabled = false;
      Assert.False( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.True( groups.HasGroup( GroupTag ) );

      groups.enabled = true;
      Assert.True( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
    }

    [TestCase( false )]
    [TestCase( true )]
    public void ContainerGroupsRespectPropagationForMixedTerrainChildren( bool propagate )
    {
      var parent = CreateObject( "Terrain container" );
      var standard = CreateTerrain( typeof( DeformableTerrain ), parent.transform );
      var child = CreateObject( "Nested container", parent.transform );
      var movable = CreateTerrain( typeof( MovableTerrain ), child.transform );
      var standardGeometry = GetGeometry( standard );
      var movableGeometry = GetGeometry( movable );
      var groups = parent.AddComponent<CollisionGroups>();
      groups.AddGroup( GroupTag, propagate );

      Assert.NotNull( groups.GetInitialized() );
      Assert.That( standardGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ), Is.EqualTo( propagate ) );
      Assert.That( movableGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ), Is.EqualTo( propagate ) );

      Assert.True( groups.RemoveGroup( GroupTag ) );
      Assert.False( standardGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.False( movableGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
    }

    [TestCase( typeof( DeformableTerrain ), false )]
    [TestCase( typeof( DeformableTerrain ), true )]
    [TestCase( typeof( MovableTerrain ), false )]
    [TestCase( typeof( MovableTerrain ), true )]
    public void TerrainGroupsRespectPropagationToChildTerrain( Type type, bool propagate )
    {
      var parent = CreateTerrain( type );
      var childType = type == typeof( MovableTerrain ) ? typeof( DeformableTerrain ) : typeof( MovableTerrain );
      var child = CreateTerrain( childType, parent.transform );
      var parentGeometry = GetGeometry( parent );
      var childGeometry = GetGeometry( child );
      var groups = parent.gameObject.AddComponent<CollisionGroups>();
      groups.AddGroup( GroupTag, propagate );

      Assert.NotNull( groups.GetInitialized() );
      Assert.True( parentGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.That( childGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ), Is.EqualTo( propagate ) );

      groups.enabled = false;
      Assert.False( parentGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.False( childGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      groups.enabled = true;
      Assert.True( parentGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
      Assert.That( childGeometry.hasGroup( GroupTag.To32BitFnv1aHash() ), Is.EqualTo( propagate ) );
    }

    [TestCase( false )]
    [TestCase( true )]
    public void PagerGroupsWarnWithoutInitializingPager( bool addAtRuntime )
    {
      var pager = (DeformableTerrainPager)CreateTerrain( typeof( DeformableTerrainPager ) );
      var groups = pager.gameObject.AddComponent<CollisionGroups>();
      if ( addAtRuntime )
        Assert.NotNull( groups.GetInitialized() );

      LogAssert.Expect( LogType.Warning, PagerWarning );
      Assert.True( groups.AddGroup( GroupTag, false ) );
      Assert.NotNull( groups.GetInitialized() );
      Assert.Null( pager.Native );

      Assert.True( groups.RemoveGroup( GroupTag ) );
      Assert.False( groups.HasGroup( GroupTag ) );
      LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PropagatingToPagerWarnsAndStillUpdatesSupportedTerrain()
    {
      var parent = CreateObject( "Terrain container" );
      CreateTerrain( typeof( DeformableTerrainPager ), parent.transform );
      var movable = CreateTerrain( typeof( MovableTerrain ), parent.transform );
      var groups = parent.AddComponent<CollisionGroups>();
      groups.AddGroup( GroupTag, true );

      LogAssert.Expect( LogType.Warning, PagerWarning );
      Assert.NotNull( groups.GetInitialized() );
      var geometry = GetGeometry( movable );
      Assert.True( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );

      Assert.True( groups.RemoveGroup( GroupTag ) );
      Assert.False( geometry.hasGroup( GroupTag.To32BitFnv1aHash() ) );
    }
  }
}
