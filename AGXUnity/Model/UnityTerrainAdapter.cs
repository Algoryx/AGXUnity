using AGXUnity.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  public class UnityTerrainAdapter : agxTerrain.TerrainDataSource
  {
    private struct UnityTile
    {
      public Vector2Int index;
      public Terrain tile;
    }

    private readonly ConcurrentQueue<UnityTile> m_tilesToLoad = new();
    private readonly HashSet<Terrain> m_addedTerrains = new();
    private readonly Dictionary<Vector2Int,Terrain> m_unityTiles = new();
    private readonly Dictionary<Vector2Int,float[,]> m_unityData = new();
    private readonly int m_tileResolution;
    private float m_maximumDepth;

    private Vector3 m_rootTerrainPos;

    public UnityTerrainAdapter( Terrain terr, float maximumDepth )
    {
      m_rootTerrainPos = terr.gameObject.transform.position;
      m_tileResolution = TerrainUtils.TerrainDataResolution( terr.terrainData );
      m_maximumDepth = maximumDepth;

      // Terrain connect is deferred by default, force terrains to connect here
      UnityEngine.TerrainUtils.TerrainUtility.AutoConnect();

      Queue<UnityTile> terrainQueue = new();

      ProcessTile( terr, new Vector2Int( 0, 0 ), ref terrainQueue );

      UnityTile? terrain = terrainQueue.Dequeue();
      while ( terrain != null ) {
        UnityTile tile = terrain.Value;
        ProcessTile( tile.tile.leftNeighbor, tile.index + new Vector2Int( -1, 0 ), ref terrainQueue );
        ProcessTile( tile.tile.rightNeighbor, tile.index + new Vector2Int( 1, 0 ), ref terrainQueue);
        ProcessTile( tile.tile.topNeighbor, tile.index + new Vector2Int( 0, 1 ), ref terrainQueue);
        ProcessTile( tile.tile.bottomNeighbor, tile.index + new Vector2Int( 0, -1 ), ref terrainQueue);

        terrain = terrainQueue.Count > 0 ? terrainQueue.Dequeue() : null;
      }
    }

    public void SetUnityHeightDelayed( float[,] height, Vector2Int globalIndex )
    {
      Vector2Int unityTileIndex = GlobalToUnityIndex(globalIndex);
      globalIndex -= (m_tileResolution - 1) * unityTileIndex;
      if ( globalIndex.x != 0 && globalIndex.y != 0 )
        m_unityTiles.GetValueOrDefault( unityTileIndex )?.terrainData.SetHeightsDelayLOD( globalIndex.x, globalIndex.y, height );

      if ( globalIndex.x == m_tileResolution - 1 )
        m_unityTiles.GetValueOrDefault( unityTileIndex + new Vector2Int( 1, 0 ) )?.terrainData.SetHeightsDelayLOD( 0, globalIndex.y, height );

      if ( globalIndex.y == m_tileResolution - 1 )
        m_unityTiles.GetValueOrDefault( unityTileIndex + new Vector2Int( 0, 1 ) )?.terrainData.SetHeightsDelayLOD( globalIndex.x, 0, height );

    }

    private void ProcessTile( Terrain terr, Vector2Int index, ref Queue<UnityTile> tileQueue )
    {
      if ( terr == null || m_addedTerrains.Contains( terr ) ) return;

      terr.gameObject.AddComponent<TerrainConnector>().MaximumDepth = m_maximumDepth;

      m_addedTerrains.Add( terr );

      tileQueue.Enqueue( new UnityTile()
      {
        tile = terr,
        index = index
      } );

      m_unityTiles.Add( index, terr );
    }

    public void Update()
    {
      while ( m_tilesToLoad.TryDequeue( out UnityTile tile ) ) {
        tile.tile.gameObject.GetComponent<TerrainConnector>().WriteTerrainDataOffset();

        var res     = tile.tile.terrainData.heightmapResolution;
        var data    = tile.tile.terrainData.GetHeights( 0, 0, res, res );
        float scale = tile.tile.terrainData.heightmapScale.y;

        for ( int y = 0; y < res; y++ )
          for ( int x = 0; x < res; x++ )
            data[ y, x ] *= scale;
        m_unityData.Add( tile.index, data );
      }

      //while ( m_tilesToLoad.TryDequeue( out UnityTile tile ) ) {
      //  var heights = tile.tile.gameObject.GetComponent<TerrainConnector>().WriteTerrainDataOffset();

      //  var res     = heights.ResolutionX;
      //  var raw     = heights.Heights;

      //  float[,] data = new float[res,res];

      //  for ( int y = 0; y < res; y++ )
      //    for ( int x = 0; x < res; x++ )
      //      data[ y, x ] = (float)raw[ y * res + x ];
      //  m_unityData.Add( tile.index, data );
      //}
    }

    public override agx.RealVector fetchTerrainTile( agxTerrain.TileSpecification ts, agx.Vec2i32 id )
    {
      var tileSize    = ts.getTileSize();
      int resolution  = (int)ts.getTileResolution();
      int overlap     = (int)ts.getTileMarginSize();
      var elementSize = tileSize / resolution;

      var worldPos    = ts.convertTilePositionToWorld(id,new agx.Vec3(0,0,0.0f)).ToHandedVector3();
      var offset      = m_rootTerrainPos;
      var relTilePos  = worldPos - offset;

      var elementsPerTile   = resolution - overlap + 1;
      float tileOffset      = (float)(elementsPerTile * elementSize);

      Debug.Log( $"({id.x},{id.y})" );

      Vector2Int baseIndex  = new( Mathf.FloorToInt( (float)(relTilePos.x / tileOffset) ), Mathf.FloorToInt( (float)(relTilePos.z / tileOffset) ) );

      Vector2Int globalIndex = new(id.x * elementsPerTile, id.y * elementsPerTile);

      baseIndex *= elementsPerTile;

      bool dataAvailable = true;
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( baseIndex ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( baseIndex + new Vector2Int( resolution - 1, 0 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( baseIndex + new Vector2Int( 0, resolution - 1 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( baseIndex + new Vector2Int( resolution - 1, resolution - 1 ) ) );

      if ( !dataAvailable ) return null;

      agx.RealVector heights = new (resolution*resolution);

      try {
        for ( int y = resolution - 1; y >= 0; y-- ) {
          for ( int x = resolution - 1; x >= 0; x-- ) {
            var index = baseIndex + new Vector2Int(x,y);
            var unityIndex = GlobalToUnityIndex(index);
            index -= ( m_tileResolution  ) * unityIndex;
            heights.Add( m_unityData[ unityIndex ][ index.y, index.x ] );
          }
        }
      }
      catch (System.Exception _) {
        return null;
      }
      return heights;
    }

    public Vector2Int GlobalToUnityIndex( Vector2Int globalIndex )
    {
      return new( (int)Mathf.Floor( (float)globalIndex.x / m_tileResolution ),
                  (int)Mathf.Floor( (float)globalIndex.y / m_tileResolution ) );
    }

    public bool VerifyAndQueueTileData( Vector2Int unityIndex )
    {
      if ( !m_unityData.ContainsKey( unityIndex ) ) {
        if ( m_tilesToLoad.Where( tile => tile.index == unityIndex ).Count() > 0 ) return false;
        if ( !m_unityTiles.ContainsKey( unityIndex ) ) return false;

        m_tilesToLoad.Enqueue( new()
        {
          tile = m_unityTiles[ unityIndex ],
          index = unityIndex
        } );
        return false;
      }

      return true;
    }

    public override bool raycast( agx.Vec3 start, agx.Vec3 end, ref agx.Vec3 raycastResult )
    {
      Ray ray = new()
      {
        direction = (end-start).ToHandedVector3(),
        origin    = start.ToHandedVector3()
      };

      var hits = Physics.RaycastAll( ray );

      foreach ( var hit in hits ) {
        var terrain = hit.collider.gameObject.GetComponent<Terrain>();
        if ( terrain != null && m_addedTerrains.Contains( terrain ) )
          return true;
      }

      return false;
    }
  }
}
