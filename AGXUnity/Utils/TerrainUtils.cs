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
  }
}
