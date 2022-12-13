using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Box shape object given half extents.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Box" )]
  public sealed class Box : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Box half extents paired with property HalfExtents.
    /// </summary>
    [SerializeField]
    private Vector3 m_halfExtents = new Vector3( 0.5f, 0.5f, 0.5f );
    /// <summary>
    /// Get or set half extents of the box.
    /// </summary>
    [ClampAboveZeroInInspector]
    public Vector3 HalfExtents
    {
      get { return m_halfExtents; }
      set
      {
        m_halfExtents = value.ClampedElementsAbove( MinimumSize );

        if ( Native != null )
          Native.setHalfExtents( m_halfExtents.ToVec3() );

        SizeUpdated();
      }
    }
    #endregion

    /// <summary>
    /// Returns the native box object if created.
    /// </summary>
    public agxCollide.Box Native { get { return NativeShape?.asBox(); } }

    /// <summary>
    /// Debug rendering scale assuming the rendered box is 1x1x1.
    /// </summary>
    public override Vector3 GetScale()
    {
      return 2.0f * m_halfExtents;
    }

    /// <summary>
    /// Creates the native box with current half extents.
    /// </summary>
    /// <returns>Native box object.</returns>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.Box( HalfExtents.ToVec3() ),
                                      GetNativeGeometryOffset() );
    }
  }
}
