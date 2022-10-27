using AGXUnity.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  /// <summary>
  /// The UnityTerrainAdapter is responsible for synchronising terrain data between the Unity Heightmap and the 
  /// AGX terrains used in the TerrainPager. This puts a few limitations on the TerrainPager:
  /// 1. The reference position of the native TerrainPager MUST be the same as the position of the root terrain.
  /// 2. The reference rotation of the native TerrainPager MUST align the index y and x axes of the AGX and Unity terrains
  /// 3. The size of each cell must be equal for the AGX and Unity terrains
  /// 
  /// These properties allow for a 'global index' to be calculated from the tile local index of a cell and the tile index, and vice versa
  /// This global index allows us to map from one local index to another.
  /// 
  /// For the Unity --> AGX synchronisation, this class implements agxTerrain.TerrainDataSource
  /// which provides heightdata for the terrain tiles the TerrainPager requests
  /// 
  /// Since the TerrainPager will attempt to fetch tiles on a background thread while the unity terrain data is inaccessible,
  /// the Adapter will instead queue a load and store away the loaded terrain data at the end of the current timestep.
  /// This means that the terrain tile loads will be delayed by one timestep
  /// </summary>
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

    public UnityTerrainAdapter( Terrain terr, float maximumDepth )
    {
      m_tileResolution = TerrainUtils.TerrainDataResolution( terr.terrainData );
      m_maximumDepth = maximumDepth;

      // Terrain connect is deferred by default, force terrains to connect here
      UnityEngine.TerrainUtils.TerrainUtility.AutoConnect();

      Queue<UnityTile> terrainQueue = new();

      ProcessTile( terr, new Vector2Int( 0, 0 ), ref terrainQueue );

      UnityTile? terrain = terrainQueue.Dequeue();
      while (terrain != null) {
        UnityTile tile = terrain.Value;
        ProcessTile( tile.tile.leftNeighbor, tile.index + new Vector2Int( -1, 0 ), ref terrainQueue );
        ProcessTile( tile.tile.rightNeighbor, tile.index + new Vector2Int( 1, 0 ), ref terrainQueue );
        ProcessTile( tile.tile.topNeighbor, tile.index + new Vector2Int( 0, 1 ), ref terrainQueue );
        ProcessTile( tile.tile.bottomNeighbor, tile.index + new Vector2Int( 0, -1 ), ref terrainQueue );

        terrain = terrainQueue.Count > 0 ? terrainQueue.Dequeue() : null;
      }
    }

    public void SetUnityHeightDelayed( float[,] height, Vector2Int globalIndex )
    {
      int elemPerTile = m_tileResolution - 1;
      Vector2Int unityTileIndex = GlobalToUnityIndex(globalIndex);
      Vector2Int unityLocalIndex = globalIndex - elemPerTile * unityTileIndex;
      if(m_unityTiles.ContainsKey(unityTileIndex))
        m_unityTiles[unityTileIndex].terrainData.SetHeightsDelayLOD( unityLocalIndex.x, unityLocalIndex.y, height );

      var neighborIndex = unityTileIndex + new Vector2Int( -1, 0 );
      if (unityLocalIndex.x == 0 && m_unityTiles.ContainsKey(neighborIndex))
        m_unityTiles[neighborIndex].terrainData.SetHeightsDelayLOD( elemPerTile, unityLocalIndex.y, height );

      neighborIndex = unityTileIndex + new Vector2Int( 0, -1 );
      if (unityLocalIndex.y == 0 && m_unityTiles.ContainsKey( neighborIndex ))
        m_unityTiles[neighborIndex].terrainData.SetHeightsDelayLOD( unityLocalIndex.x, elemPerTile, height );

      neighborIndex = unityTileIndex + new Vector2Int( -1, -1 );
      if (unityLocalIndex.y == 0 && unityLocalIndex.x == 0 && m_unityTiles.ContainsKey( neighborIndex ))
        m_unityTiles[neighborIndex].terrainData.SetHeightsDelayLOD( elemPerTile, elemPerTile, height );

    }

    private void ProcessTile( Terrain terr, Vector2Int index, ref Queue<UnityTile> tileQueue )
    {
      if (terr == null || m_addedTerrains.Contains( terr )) return;

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
      while (m_tilesToLoad.TryDequeue( out UnityTile tile )) {
        tile.tile.gameObject.GetComponent<TerrainConnector>().WriteTerrainDataOffset();

        var res     = tile.tile.terrainData.heightmapResolution;
        var data    = tile.tile.terrainData.GetHeights( 0, 0, res, res );
        float scale = tile.tile.terrainData.heightmapScale.y;

        for (int y = 0; y < res; y++)
          for (int x = 0; x < res; x++)
            data[y, x] *= scale;
        m_unityData.Add( tile.index, data );
      }
    }

    public override agx.RealVector fetchTerrainTile( agxTerrain.TileSpecification ts, agx.Vec2i32 id )
    {
      int resolution  = (int)ts.getTileResolution();
      int overlap     = (int)ts.getTileMarginSize();

      var elementsPerTile   = resolution - (overlap + 1);

      Vector2Int globalIndex = new(id.x * elementsPerTile, id.y * elementsPerTile);

      bool dataAvailable = true;
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( resolution - 2, 0 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( 0, resolution - 2 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( resolution - 2, resolution - 2 ) ) );

      if (!dataAvailable) return null;

      agx.RealVector heights = new (resolution*resolution);

      try {
        for (int y = 0; y < resolution; y++) {
          for (int x = 0; x < resolution; x++) {
            var index = globalIndex + new Vector2Int(x,y);
            var unityIndex = GlobalToUnityIndex(index);
            var localUnityIndex = index - (m_tileResolution - 1) * unityIndex;
            float height;
            if(m_unityData.ContainsKey(unityIndex))
              height = m_unityData[unityIndex][localUnityIndex.y, localUnityIndex.x];
            else if (localUnityIndex.x == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int(-1 , 0)))
              height = m_unityData[unityIndex + new Vector2Int( -1, 0 )][localUnityIndex.y, m_tileResolution - 1];
            else if (localUnityIndex.y == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int( 0, -1 ) ))
              height = m_unityData[unityIndex + new Vector2Int( 0, -1 )][m_tileResolution - 1, localUnityIndex.x];
            else if (localUnityIndex.x == 0 && localUnityIndex.y == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int( -1, -1 ) ))
              height = m_unityData[unityIndex + new Vector2Int( -1, -1 )][m_tileResolution - 1, m_tileResolution - 1];
            else {
              Debug.LogError( $"Cannot map global index {index} to unity tile" );
              height = 0;
            }
            heights.Add( height );
          }
        }
      }
      catch (System.Exception) {
        Debug.Log( "Error" );
        return null;
      }
      return heights;
    }

    public Vector2Int GlobalToUnityIndex( Vector2Int globalIndex )
    {
      return new( (int)Mathf.Floor( (float)globalIndex.x / (m_tileResolution - 1) ),
                  (int)Mathf.Floor( (float)globalIndex.y / (m_tileResolution - 1) ) );
    }

    public bool VerifyAndQueueTileData( Vector2Int unityIndex )
    {
      if (!m_unityData.ContainsKey( unityIndex )) {
        if (m_tilesToLoad.Where( tile => tile.index == unityIndex ).Count() > 0) return false;
        if (!m_unityTiles.ContainsKey( unityIndex )) return false;

        m_tilesToLoad.Enqueue( new()
        {
          tile = m_unityTiles[unityIndex],
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

      foreach (var hit in hits) {
        var terrain = hit.collider.gameObject.GetComponent<Terrain>();
        if (terrain != null && m_addedTerrains.Contains( terrain ))
          return true;
      }

      return false;
    }
  }
}
