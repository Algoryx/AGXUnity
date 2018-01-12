using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Height field object to be used with Unity "Terrain".
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/HeightField" )]
  public sealed class HeightField : Shape
  {
    /// <summary>
    /// Returns the native height field object if created.
    /// </summary>
    public agxCollide.HeightField Native { get { return m_shape as agxCollide.HeightField; } }

    /// <summary>
    /// Debug rendering scale and debug rendering in general not supported.
    /// </summary>
    public override UnityEngine.Vector3 GetScale()
    {
      return new Vector3( 1, 1, 1 );
    }

    /// <summary>
    /// Shape offset, rotates native height field from z up to y up, flips x and z (?) and
    /// moves to center of the terrain (Unity Terrain has origin "lower corner").
    /// </summary>
    /// <returns>Shape transform to be used between native geometry and shape.</returns>
    public override agx.AffineMatrix4x4 GetNativeGeometryOffset()
    {
      return agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() ).Multiply(
             agx.AffineMatrix4x4.translate( transform.position.ToHandedVec3() + new Vector3( 0.5f * GetWidth(), 0, 0.5f * GetHeight() ).ToHandedVec3() ) );
    }

    /// <summary>
    /// Overriding synchronization of native transform since the UnityEngine.Terrain
    /// by default is static and doesn't support rotation.
    /// 
    /// IF we want to synchronize we have to ignore rotation.
    /// </summary>
    protected override void SyncNativeTransform()
    {
    }

    /// <summary>
    /// Overriding synchronization of UnityEngine.Terrain transform since the UnityEngine.Terrain
    /// (and most often agxCollide.HeightField) is static by default.
    /// 
    /// IF we want to synchronize we have to ignore rotation.
    /// </summary>
    protected override void SyncUnityTransform()
    {
    }

    /// <summary>
    /// Finds and returns the Unity Terrain object. Searches on this
    /// component level and in all parents.
    /// </summary>
    /// <returns>Unity Terrain object, if found.</returns>
    private UnityEngine.Terrain GetTerrain()
    {
      return Find.FirstParentWithComponent<Terrain>( transform );
    }

    /// <summary>
    /// Finds Unity Terrain data given current setup.
    /// </summary>
    /// <returns>Unity TerrainData object, if found.</returns>
    private UnityEngine.TerrainData GetTerrainData()
    {
      Terrain terrain = GetTerrain();
      return terrain != null ? terrain.terrainData : null;
    }

    /// <returns>Width of the height field.</returns>
    private float GetWidth()
    {
      TerrainData data = GetTerrainData();
      return data != null ? data.size.x : 0.0f;
    }

    /// <returns>Global height reference.</returns>
    private float GetHeight()
    {
      TerrainData data = GetTerrainData();
      return data != null ? data.size.z : 0.0f;
    }

    /// <summary>
    /// Creates native height field object given current Unity Terrain
    /// object - if present (in component level or in parents).
    /// </summary>
    /// <returns>Native height field shape object.</returns>
    protected override agxCollide.Shape CreateNative()
    {
      TerrainData terrainData = GetTerrainData();
      if ( terrainData == null )
        return null;

      Vector3 scale = terrainData.heightmapScale;
      int[] res = new int[]{ terrainData.heightmapWidth, terrainData.heightmapHeight };
      float[,] heights = terrainData.GetHeights( 0, 0, res[ 0 ], res[ 1 ] );

      int resX = res[ 0 ];
      int resY = res[ 1 ];
      agx.RealVector agxHeights = new agx.RealVector( resX * resY );
      for ( int x = resX - 1; x >= 0; --x )
        for ( int y = resY - 1; y >= 0; --y )
          agxHeights.Add( heights[ x, y ] * scale.y );

      agxCollide.HeightField hf = new agxCollide.HeightField( (uint)res[ 0 ], (uint)res[ 1 ], GetWidth(), GetHeight(), agxHeights, false, 150.0 );
      return hf;
    }
  }
}
