using AGXUnity.Model;
using AGXUnity.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class CollisionGroupsTests
  {
    private readonly List<GameObject> m_objects = new List<GameObject>();
    private readonly List<UnityEngine.Object> m_resources = new List<UnityEngine.Object>();

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
      if ( terrain is MovableTerrain movable ) {
        movable.PlacementMode = MovableTerrain.Placement.Manual;
        movable.SizeCells = new Vector2Int( 5, 5 );
        m_resources.Add( movable.TerrainMesh.sharedMesh );
      }
      return terrain;
    }

    [TearDown]
    public void DestroyTerrainObjects()
    {
      foreach ( var go in m_objects )
        if ( go != null )
          UnityEngine.Object.DestroyImmediate( go );
      foreach ( var resource in m_resources )
        if ( resource != null )
          UnityEngine.Object.DestroyImmediate( resource );
      m_objects.Clear();
      m_resources.Clear();
    }

    [TestCase( typeof( DeformableTerrain ) )]
    [TestCase( typeof( MovableTerrain ) )]
    [TestCase( typeof( DeformableTerrainPager ) )]
    public void LeafObjectsFindsTerrainOnSameObject( Type type )
    {
      var terrain = CreateTerrain( type );

      Assert.That( Find.LeafObjects( terrain.gameObject, false ).Terrains,
                   Is.EquivalentTo( new[] { terrain } ) );
    }

    [TestCase( false )]
    [TestCase( true )]
    public void LeafObjectsRespectsContainerChildSearchForAllTerrainTypes( bool searchChildren )
    {
      var parent = CreateObject( "Terrain container" );
      var standard = CreateTerrain( typeof( DeformableTerrain ), parent.transform );
      var nested = CreateObject( "Nested container", parent.transform );
      var movable = CreateTerrain( typeof( MovableTerrain ), nested.transform );
      var pager = CreateTerrain( typeof( DeformableTerrainPager ), nested.transform );

      var expected = searchChildren ?
                       new[] { standard, movable, pager } :
                       new DeformableTerrainBase[] { };
      Assert.That( Find.LeafObjects( parent, searchChildren ).Terrains, Is.EquivalentTo( expected ) );
    }

    [TestCase( typeof( DeformableTerrain ), false )]
    [TestCase( typeof( DeformableTerrain ), true )]
    [TestCase( typeof( MovableTerrain ), false )]
    [TestCase( typeof( MovableTerrain ), true )]
    [TestCase( typeof( DeformableTerrainPager ), false )]
    [TestCase( typeof( DeformableTerrainPager ), true )]
    public void LeafObjectsRespectsTerrainChildSearchForAllTerrainTypes( Type type, bool searchChildren )
    {
      var parent = CreateTerrain( type );
      var standard = CreateTerrain( typeof( DeformableTerrain ), parent.transform );
      var movable = CreateTerrain( typeof( MovableTerrain ), parent.transform );
      var pager = CreateTerrain( typeof( DeformableTerrainPager ), parent.transform );

      var expected = searchChildren ?
                       new[] { parent, standard, movable, pager } :
                       new[] { parent };
      Assert.That( Find.LeafObjects( parent.gameObject, searchChildren ).Terrains, Is.EquivalentTo( expected ) );
    }
  }
}
