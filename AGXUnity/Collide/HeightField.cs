using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Height field object to be used with Unity "Terrain".
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Height Field" )]
  [RequireComponent( typeof( Terrain ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#height-field" )]
  public sealed class HeightField : Shape
  {
    /// <summary>
    /// Returns the native height field object if created.
    /// </summary>
    public agxCollide.HeightField Native { get { return NativeShape?.asHeightField(); } }

    /// <summary>
    /// Debug rendering scale and debug rendering in general not supported.
    /// </summary>
    public override Vector3 GetScale()
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
      return TerrainUtils.CalculateNativeOffset( transform, TerrainData );
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
    private Terrain Terrain
    {
      get { return m_terrain ?? ( m_terrain = GetComponent<Terrain>() ); }
    }

    /// <summary>
    /// Finds Unity Terrain data given current setup.
    /// </summary>
    /// <returns>Unity TerrainData object, if found.</returns>
    private TerrainData TerrainData
    {
      get { return Terrain?.terrainData; }
    }

    /// <returns>Width of the height field.</returns>
    private float GetWidth()
    {
      var data = TerrainData;
      return data != null ? data.size.x : 0.0f;
    }

    /// <returns>Global height reference.</returns>
    private float GetHeight()
    {
      var data = TerrainData;
      return data != null ? data.size.z : 0.0f;
    }

    /// <summary>
    /// Creates native height field object given current Unity Terrain
    /// object - if present (in component level or in parents).
    /// </summary>
    /// <returns>Native height field shape object.</returns>
    protected override agxCollide.Geometry CreateNative()
    {
      var terrainData = TerrainData;
      if ( terrainData == null )
        return null;

      var heights = TerrainUtils.FindHeights( terrainData );
      var hf = new agxCollide.HeightField( (uint)heights.ResolutionX,
                                           (uint)heights.ResolutionY,
                                           GetWidth(),
                                           GetHeight(),
                                           heights.Heights,
                                           false,
                                           150.0 );
      return new agxCollide.Geometry( hf,
                                      GetNativeGeometryOffset() );
    }

    private Terrain m_terrain = null;
  }
}
