using System.Collections.Generic;

using UnityEngine;

namespace AGXUnity.Utils
{
  public static class TerrainUtils
  {
    /// <summary>
    /// Struct containing height map resolution and heights in
    /// native format.
    /// </summary>
    public struct NativeHeights
    {
      public int ResolutionX;
      public int ResolutionY;
      public agx.RealVector Heights;
    }

    /// <summary>
    /// Finds native resolution and heights given Unity terrain data.
    /// </summary>
    /// <param name="data">Unity terrain data.</param>
    /// <returns>Converted native height map data.</returns>
    public static NativeHeights FindHeights( TerrainData data )
    {
      // width:  Number of samples to retrieve along the height map's x axis.
      // height: Number of samples to retrieve along the height map's y axis.
      // The array has the dimensions [height,width] and is indexed as [y,x].

      var result = new NativeHeights()
      {
        ResolutionX = TerrainDataResolution( data ),
        ResolutionY = TerrainDataResolution( data ),
        Heights     = new agx.RealVector( TerrainDataResolution( data ) *
                                          TerrainDataResolution( data ) )
      };
      Vector3 scale = data.heightmapScale;
      float[,] heights = data.GetHeights( 0, 0, result.ResolutionX, result.ResolutionY );

      for ( int y = result.ResolutionY - 1; y >= 0; --y )
        for ( int x = result.ResolutionX - 1; x >= 0; --x )
          result.Heights.Add( heights[ y, x ] * scale.y );

      return result;
    }

    /// <summary>
    /// Calculates native height map offset relative an Unity terrain, given
    /// current transform of the terrain and terrain data (containing size).
    /// </summary>
    /// <param name="transform">Transform of the terrain.</param>
    /// <param name="data">Unity terrain data (containing size of the terrain).</param>
    /// <returns>Native transform.</returns>
    public static agx.AffineMatrix4x4 CalculateNativeOffset( Transform transform, TerrainData data )
    {
      if ( transform == null || data == null )
        return new agx.AffineMatrix4x4();

      return agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(),
                                         agx.Vec3.Y_AXIS() ) *
             agx.AffineMatrix4x4.translate( transform.position.ToHandedVec3() +
                                            new Vector3( 0.5f * data.size.x,
                                                         0,
                                                         0.5f * data.size.z ).ToHandedVec3() );
    }

    public static int TerrainDataResolution( TerrainData terrainData )
    {
#if UNITY_2019_3_OR_NEWER
      return terrainData.heightmapResolution;
#else
      return terrainData.heightmapWidth;
#endif
    }

    /// <summary>
    /// Writes <paramref name="offset"/> to <paramref name="terrain"/> height data.
    /// </summary>
    /// <param name="terrainData">Terrain to modify.</param>
    /// <param name="offset">Height offset.</param>
    public static NativeHeights WriteTerrainDataOffset( Terrain terrain, float offset )
    {
      var terrainData        = terrain.terrainData;
      var nativeHeightData   = FindHeights( terrainData );
      var tmp                = new float[,] { { 0.0f } };
      var dataMaxHeight      = terrainData.size.y;
      var maxClampedHeight   = -1.0f;
      var resolution         = TerrainDataResolution( terrainData );
      var scale              = terrainData.heightmapScale.y;

      for ( int i = 0; i < nativeHeightData.Heights.Count; ++i ) {
        var newHeight = nativeHeightData.Heights[ i ] += offset;

        var vertexX = i % nativeHeightData.ResolutionX;
        var vertexY = i / nativeHeightData.ResolutionY;

        tmp[ 0, 0 ] = (float)newHeight / scale;
        if ( newHeight > dataMaxHeight )
          maxClampedHeight = System.Math.Max( maxClampedHeight, (float)newHeight );

        terrainData.SetHeightsDelayLOD( resolution - vertexX - 1,
                                        resolution - vertexY - 1,
                                        tmp );
      }

      if ( maxClampedHeight > 0.0f ) {
        Debug.LogWarning( "Terrain heights were clamped: UnityEngine.TerrainData max height = " +
                          dataMaxHeight +
                          " and AGXUnity.Model.DeformableTerrain.MaximumDepth = " +
                          offset +
                          ". Resolve this by increasing max height and lower the terrain or decrease Maximum Depth.", terrain );
      }

#if UNITY_2019_1_OR_NEWER
      terrainData.SyncHeightmap();
#else
      terrain.ApplyDelayedHeightmapModification();
#endif

      return nativeHeightData;
    }

    /// <summary>
    /// Writes <paramref name="offset"/> to <paramref name="terrain"/> height data.
    /// </summary>
    /// <param name="terrainData">Terrain to modify.</param>
    /// <param name="offset">Height offset.</param>
    public static float[,] WriteTerrainDataOffsetRaw( Terrain terrain, float offset )
    {
      var terrainData        = terrain.terrainData;
      var dataMaxHeight      = terrainData.size.y;
      var maxClampedHeight   = -1.0f;
      var resolution         = TerrainDataResolution( terrainData );
      var data               = terrainData.GetHeights(0,0,resolution,resolution);
      var scale              = terrainData.heightmapScale.y;

      for (int y = 0; y < resolution; y++ ) {
        for(int x = 0; x < resolution; x++ ) {
          data[ y, x ] = data[ y, x ] + offset / scale;
          if ( data[ y, x ] > dataMaxHeight )
            maxClampedHeight = System.Math.Max( maxClampedHeight, data[ y, x ] );
        }
      }

      terrainData.SetHeightsDelayLOD( 0, 0, data );

#if UNITY_2019_1_OR_NEWER
      terrainData.SyncHeightmap();
#else
      terrain.ApplyDelayedHeightmapModification();
#endif

      if ( maxClampedHeight > 0.0f ) {
        Debug.LogWarning( "Terrain heights were clamped: UnityEngine.TerrainData max height = " +
                          dataMaxHeight +
                          " and AGXUnity.Model.DeformableTerrain.MaximumDepth = " +
                          offset +
                          ". Resolve this by increasing max height and lower the terrain or decrease Maximum Depth.", terrain );
      }

      return data;
    }

    /// <summary>
    /// True if a deformable terrain instance is present in any tile (including the root terrain)
    /// of the given deformable terrain pager.
    /// </summary>
    /// <param name="pager">Deformable terrain pager.</param>
    /// <returns>True if an AGXUnity.Model.DeformableTerrain instance is present in a tile or root of the given pager.</returns>
    public static bool HasDeformableTerrainInTiles( Model.DeformableTerrainPager pager )
    {
      return pager != null &&
             System.Array.Find( CollectTerrains( pager.Terrain ),
                                terrain =>
                                  terrain.GetComponent<Model.DeformableTerrain>() != null ) != null;
    }

    /// <summary>
    /// True if another deformable terrain pager is present in any tile of the given <paramref name="pager"/>.
    /// </summary>
    /// <param name="pager">Deformable terrain pager.</param>
    /// <returns>True if another AGXUnity.Model.DeformableTerrainPager exists in a tile of the given pager.</returns>
    public static bool HasDeformableTerrainPagerInTiles( Model.DeformableTerrainPager pager )
    {
      return pager != null &&
             System.Array.FindLast( CollectTerrains( pager.Terrain ),
                                    terrain =>
                                      terrain.GetComponent<Model.DeformableTerrainPager>() != null &&
                                      terrain.GetComponent<Model.DeformableTerrainPager>() != pager );
    }

    /// <summary>
    /// True if zero AGXUnity.Model.DeformableTerrain and (other) AGXUnity.Model.DeformableTerrainPager components
    /// found in any tile(s) controlled by the given <paramref name="pager"/>.
    /// </summary>
    /// <param name="pager">Deformable terrain pager to check.</param>
    /// <param name="issueError">True to log error.</param>
    /// <returns>True if valid, false if the configuration is invalid.</returns>
    public static bool IsValid( Model.DeformableTerrainPager pager, bool issueError = false )
    {
      var hasDeformableTerrainInTiles = HasDeformableTerrainInTiles( pager );
      var hasDeformableTerrainPagerInTiles = HasDeformableTerrainPagerInTiles( pager );
      
      if ( pager != null && hasDeformableTerrainInTiles && issueError )
        Debug.LogError( $"{pager.GetType().FullName}: Configuration error - one or more AGXUnity.Model.DeformableTerrain components in " +
                        $"a tile of this deformable terrain pager. Remove any AGXUnity.Model.DeformableTerrain component from the tile(s) " +
                        $"controlled by this pager.", pager );
      if ( pager != null && hasDeformableTerrainPagerInTiles && issueError )
        Debug.LogError( $"{pager.GetType().FullName}: Configuration error - one or more AGXUnity.Model.DeformableTerrainPager components in " +
                        $"a tile of this deformable terrain pager. Remove any other AGXUnity.Model.DeformableTerrainPager component from the tile(s) " +
                        $"controlled by this pager.", pager );

      return pager != null && !hasDeformableTerrainInTiles && !hasDeformableTerrainPagerInTiles;
    }

    /// <summary>
    /// Collects all terrains connected to the given <paramref name="terrain"/>,
    /// including the given <paramref name="terrain"/>. I.e., if the given terrain
    /// instance isn't null, the returned array size is >= 1, with the given terrain
    /// at index 0.
    /// </summary>
    /// <param name="terrain">Root terrain instance.</param>
    /// <returns>
    /// Array containing all the connected (tiled) terrains (including <paramref name="terrain"/> and index 0).
    /// </returns>
    public static Terrain[] CollectTerrains( Terrain terrain )
    {
      var terrains = new List<Terrain>();
      CollectTerrain( terrain, terrains );
      return terrains.ToArray();
    }

    private static void CollectTerrain( Terrain neighbor, List<Terrain> terrains )
    {
      if ( neighbor == null || terrains.Contains( neighbor ) )
        return;

      terrains.Add( neighbor );

      CollectTerrain( neighbor.leftNeighbor, terrains );
      CollectTerrain( neighbor.rightNeighbor, terrains );
      CollectTerrain( neighbor.topNeighbor, terrains );
      CollectTerrain( neighbor.bottomNeighbor, terrains );
    }
  }
}
