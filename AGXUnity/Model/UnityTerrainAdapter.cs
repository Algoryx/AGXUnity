using AGXUnity.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  /// <summary>
  /// The UnityTerrainAdapter is responsible for synchronising terrain data between the Unity Heightmap and the 
  /// AGX terrains used in the DeformableTerrainPager. This puts a few limitations on the DeformableTerrainPager:
  /// 1. The reference position of the native DeformableTerrainPager MUST be the same as the position of the root terrain.
  /// 2. The reference rotation of the native DeformableTerrainPager MUST align the index y and x axes of the AGX and Unity terrains
  /// 3. The size of each cell must be equal for the AGX and Unity terrains
  /// 
  /// These properties allow for a 'global index' to be calculated from the tile local index of a cell and the tile index, and vice versa
  /// This global index allows us to map from one local index to another.
  /// 
  /// Since there is a shared vertex between adjacent unity tiles the local --> global mapping maps edge indices of
  /// adjacent tiles to a single global index. This means that the reverse mapping has to pick one of the tiles.
  /// This reverse mapping is done in <see cref="GlobalToUnityIndex(Vector2Int)"/>
  /// 
  /// For the Unity --> AGX synchronisation, this class implements agxTerrain.TerrainDataSource
  /// which provides heightdata for the terrain tiles the DeformableTerrainPager requests
  /// 
  /// Since the DeformableTerrainPager will attempt to fetch tiles on a background thread while the unity terrain data is inaccessible,
  /// the Adapter will instead queue a load and store away the loaded terrain data at the end of the current timestep.
  /// This means that the terrain tile loads will be delayed by one timestep
  /// </summary>
  public class UnityTerrainAdapter : agxTerrain.ExternalTerrainDataSource
  {
    private struct UnityTile
    {
      public Vector2Int index;
      public Terrain tile;
    }

    private readonly ConcurrentQueue<UnityTile> m_tilesToLoad = new ConcurrentQueue<UnityTile>();
    private readonly HashSet<Terrain> m_addedTerrains = new HashSet<Terrain>();
    private readonly Dictionary<Vector2Int,Terrain> m_unityTiles = new Dictionary<Vector2Int, Terrain>();
    private readonly Dictionary<Vector2Int,float[,]> m_unityData = new Dictionary<Vector2Int, float[,]>();
    private readonly int m_tileResolution;
    private readonly float m_maximumDepth;

    /// <summary>
    /// Creates a new terrain adapter using the provided Unity terrain root and maximum depth.
    /// All unity terrains managed by this adapter should be connected via unity's terrain neighbor system or they wont be found
    /// </summary>
    /// <param name="rootTerrain">The root unity terrain to use</param>
    /// <param name="maximumDepth">The maximum diggable depth</param>
    public UnityTerrainAdapter( Terrain rootTerrain, float maximumDepth )
    {
      m_tileResolution = TerrainUtils.TerrainDataResolution( rootTerrain.terrainData );
      m_maximumDepth = maximumDepth;

#if UNITY_2021_2_OR_NEWER
      // Terrain connect is deferred by default, force terrains to connect here
      UnityEngine.TerrainUtils.TerrainUtility.AutoConnect();
#else
      UnityEngine.Experimental.TerrainAPI.TerrainUtility.AutoConnect();
#endif

      var terrainQueue = new Queue<UnityTile>();

      // Flood fill process all connected tiles
      ProcessTile( rootTerrain, new Vector2Int( 0, 0 ), ref terrainQueue );
      UnityTile? terrain = terrainQueue.Dequeue();
      while ( terrain != null ) {
        UnityTile tile = terrain.Value;
        ProcessTile( tile.tile.leftNeighbor, tile.index + new Vector2Int( -1, 0 ), ref terrainQueue );
        ProcessTile( tile.tile.rightNeighbor, tile.index + new Vector2Int( 1, 0 ), ref terrainQueue );
        ProcessTile( tile.tile.topNeighbor, tile.index + new Vector2Int( 0, 1 ), ref terrainQueue );
        ProcessTile( tile.tile.bottomNeighbor, tile.index + new Vector2Int( 0, -1 ), ref terrainQueue );

        terrain = terrainQueue.Count > 0 ? terrainQueue.Dequeue() : (UnityTile?)null;
      }
    }

    /// <summary>
    /// Mirrors <see cref="TerrainData.SetHeightsDelayLOD(int, int, float[,])"/> but additionally performs a mapping from
    /// a global index to the underlying unity terrain tila and index.
    /// Note that no bounds checking is performed against the underlying unity tiles, 
    /// using 1x1 height arrays is recommended.
    /// </summary>
    /// <param name="height">A 2D array with the height values to set</param>
    /// <param name="globalIndex">The global index at which to set the height</param>
    public void SetUnityHeightDelayed( float[,] height, Vector2Int globalIndex )
    {
      int elemPerTile = m_tileResolution - 1;
      Vector2Int unityTileIndex = GlobalToUnityIndex(globalIndex);
      Vector2Int unityLocalIndex = globalIndex - elemPerTile * unityTileIndex;

      // For edge/corner indices we need to set up to four unity terrain heights due to the index mapping process not being one to one.
      // The GlobalToUnityIndex method picks the higher index so here we need to set the heights of (x-1,y), (x,y-1) and (x-1,y-1)
      // when the index is on an edge with index 0.

      if ( m_unityTiles.ContainsKey( unityTileIndex ) )
        m_unityTiles[ unityTileIndex ].terrainData.SetHeightsDelayLOD( unityLocalIndex.x, unityLocalIndex.y, height );

      var neighborIndex = unityTileIndex + new Vector2Int( -1, 0 );
      if ( unityLocalIndex.x == 0 && m_unityTiles.ContainsKey( neighborIndex ) )
        m_unityTiles[ neighborIndex ].terrainData.SetHeightsDelayLOD( elemPerTile, unityLocalIndex.y, height );

      neighborIndex = unityTileIndex + new Vector2Int( 0, -1 );
      if ( unityLocalIndex.y == 0 && m_unityTiles.ContainsKey( neighborIndex ) )
        m_unityTiles[ neighborIndex ].terrainData.SetHeightsDelayLOD( unityLocalIndex.x, elemPerTile, height );

      neighborIndex = unityTileIndex + new Vector2Int( -1, -1 );
      if ( unityLocalIndex.y == 0 && unityLocalIndex.x == 0 && m_unityTiles.ContainsKey( neighborIndex ) )
        m_unityTiles[ neighborIndex ].terrainData.SetHeightsDelayLOD( elemPerTile, elemPerTile, height );

    }

    /// <summary>
    /// Process the terrain at the given index if it has not yet been proccessed. This the tile to the collections used
    /// by the adapter and adds the <see cref="DeformableTerrainConnector"/> component to the terrain tile gameobject.
    /// </summary>
    private void ProcessTile( Terrain terr, Vector2Int index, ref Queue<UnityTile> tileQueue )
    {
      if ( terr == null || m_addedTerrains.Contains( terr ) ) return;

      terr.gameObject.AddComponent<DeformableTerrainConnector>().MaximumDepth = m_maximumDepth;
      m_addedTerrains.Add( terr );
      tileQueue.Enqueue( new UnityTile()
      {
        tile = terr,
        index = index
      } );
      m_unityTiles.Add( index, terr );
    }

    /// <summary>
    /// Loads data for the tiles which where requested since the last call to Update
    /// </summary>
    public void Update()
    {
      while ( m_tilesToLoad.TryPeek( out UnityTile tile ) ) {
        // FIXME: Loading tiles currently takes quite a long time due to the write/read
        // optimally this should happen asynchronously but it is uncertain whether the Unity API allows it.

        float[,] data = tile.tile.gameObject.GetComponent<DeformableTerrainConnector>().WriteTerrainDataOffset();
        int res       = tile.tile.terrainData.heightmapResolution;
        float scale   = tile.tile.terrainData.heightmapScale.y;

        for ( int y = 0; y < res; y++ )
          for ( int x = 0; x < res; x++ )
            data[ y, x ] *= scale;
        m_unityData.Add( tile.index, data );
        m_tilesToLoad.TryDequeue( out UnityTile _ );
      }
    }

    /// <summary>
    /// Fetches height data for the specified tile id given a tile specification
    /// </summary>
    /// <param name="ts">The tile specification used to construct the tile</param>
    /// <param name="id">The tile id of the tile to fetch</param>
    /// <returns>A vector of height data</returns>
    public override agx.RealVector fetchTerrainTile( agxTerrain.TileSpecification ts, agx.Vec2i32 id )
    {
      int resolution  = (int)ts.getTileResolution();
      int overlap     = (int)ts.getTileMarginSize();

      var elementsPerTile   = resolution - (overlap + 1);

      Vector2Int globalIndex = new Vector2Int(id.x * elementsPerTile, id.y * elementsPerTile);

      // We cannot get height data in fetch since it will be called in a background thread.
      // Instead, check if data has already been loaded. If not, mark the terrain to be loaded on the main thread and skip fetching tile for now
      // Since the tiles are *resolution* cells in size the highest index is *resolution* - 1. However, this index could be a shared unity vertex
      // which would lead to an unity tile index of 1 greater than the required. Instead we use *resolution* - 2 since if that index is converted
      // a required tile the index at *resolution* - 1 is guaranteed to be in the required tile as well
      bool dataAvailable = true;
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( resolution - 2, 0 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( 0, resolution - 2 ) ) );
      dataAvailable &= VerifyAndQueueTileData( GlobalToUnityIndex( globalIndex + new Vector2Int( resolution - 2, resolution - 2 ) ) );

      // Defer load if data is not yet available
      if ( !dataAvailable ) return null;

      var heights = new agx.RealVector( resolution * resolution );

      for ( int y = 0; y < resolution; y++ ) {
        for ( int x = 0; x < resolution; x++ ) {

          var index = globalIndex + new Vector2Int(x,y);
          var unityIndex = GlobalToUnityIndex(index);
          var localUnityIndex = index - (m_tileResolution - 1) * unityIndex;

          // Due to the index mapping process not being one-to-one the tile at the calculated unity index might not be loaded.
          // Check other candidates if so is the case.
          float height;
          if ( m_unityData.ContainsKey( unityIndex ) )
            height = m_unityData[ unityIndex ][ localUnityIndex.y, localUnityIndex.x ];
          else if ( localUnityIndex.x == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int( -1, 0 ) ) )
            height = m_unityData[ unityIndex + new Vector2Int( -1, 0 ) ][ localUnityIndex.y, m_tileResolution - 1 ];
          else if ( localUnityIndex.y == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int( 0, -1 ) ) )
            height = m_unityData[ unityIndex + new Vector2Int( 0, -1 ) ][ m_tileResolution - 1, localUnityIndex.x ];
          else if ( localUnityIndex.x == 0 && localUnityIndex.y == 0 && m_unityData.ContainsKey( unityIndex + new Vector2Int( -1, -1 ) ) )
            height = m_unityData[ unityIndex + new Vector2Int( -1, -1 ) ][ m_tileResolution - 1, m_tileResolution - 1 ];
          else {
            Debug.LogError( $"Cannot map global index {index} to unity tile" );
            height = 0;
          }
          heights.Add( height );
        }
      }

      return heights;
    }

    /// <summary>
    /// Converts from the given global index to the corresponding unity tile index. Since the local --> global index mapping proccess is 
    /// not one-to-one this function will return only the tile where the global index is at local index x != resolution and y != resolution
    /// </summary>
    /// <param name="globalIndex">The global index to convert to a unity tile index</param>
    /// <returns>The unity tile index for the given global index</returns>
    public Vector2Int GlobalToUnityIndex( Vector2Int globalIndex )
    {
      return new Vector2Int( (int)Mathf.Floor( (float)globalIndex.x / ( m_tileResolution - 1 ) ),
                             (int)Mathf.Floor( (float)globalIndex.y / ( m_tileResolution - 1 ) ) );
    }

    // Checks if unity tile data is loaded for the tile at the given index and queues the tile to be loaded if it is not
    private bool VerifyAndQueueTileData( Vector2Int unityIndex )
    {
      // Is data loaded?
      if ( !m_unityData.ContainsKey( unityIndex ) ) {
        // Dont queue tiles twice
        if ( m_tilesToLoad.Where( tile => tile.index == unityIndex ).Count() > 0 )
          return false;
        
        // Dont queue tiles that are not tracked by the adapter
        if ( !m_unityTiles.ContainsKey( unityIndex ) )
          return false;

        m_tilesToLoad.Enqueue( new UnityTile()
        {
          tile = m_unityTiles[ unityIndex ],
          index = unityIndex
        } );

        return false;
      }

      return true;
    }

    /// <summary>
    /// Performs a raycast query against the unity terrains tracked by the adapter
    /// </summary>
    /// <param name="start">The start of the ray</param>
    /// <param name="end">The end of the ray</param>
    /// <param name="raycastResult">
    /// Is modified to the point where the ray intersects. 
    /// It is left unmodified if the ray doesn't intersect the terrains
    /// </param>
    /// <returns>true if the ray intersects the terrain, false otherwise</returns>
    public override bool raycast( agx.Vec3 start, agx.Vec3 end, ref agx.Vec3 raycastResult )
    {
      Ray ray = new Ray()
      {
        direction = (end-start).ToHandedVector3(),
        origin    = start.ToHandedVector3()
      };

      var hits = Physics.RaycastAll( ray );

      foreach ( var hit in hits ) {
        var terrain = hit.collider.gameObject.GetComponent<Terrain>();
        if ( terrain != null && m_addedTerrains.Contains( terrain ) ) {
          raycastResult = hit.point.ToHandedVec3();
          return true;
        }
      }

      return false;
    }
  }
}
