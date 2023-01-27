using System.Linq;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Capsule shape object given radius and height.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Capsule" )]
  public sealed class Capsule : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Radius of this capsule paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.5f;
    /// <summary>
    /// Height of this capsule paired with property Height.
    /// </summary>
    [SerializeField]
    private float m_height = 1.0f;

    /// <summary>
    /// Get or set radius of this capsule.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        m_radius = AGXUnity.Utils.Math.ClampAbove( value, MinimumSize );

        if ( Native != null )
          Native.setRadius( m_radius );

        SizeUpdated();
      }
    }

    /// <summary>
    /// Get or set height of this capsule.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Height
    {
      get { return m_height; }
      set
      {
        m_height = AGXUnity.Utils.Math.ClampAbove( value, MinimumSize );

        if ( Native != null )
          Native.setHeight( m_height );

        SizeUpdated();
      }
    }
    #endregion

    /// <summary>
    /// Returns the native capsule object if created.
    /// </summary>
    public agxCollide.Capsule Native { get { return NativeShape?.asCapsule(); } }

    /// <summary>
    /// Debug rendering scale is unsupported since debug render object
    /// contains three objects - two spheres and one cylinder.
    /// <see cref="SyncDebugRenderingScale"/>
    /// </summary>
    /// <returns>(1, 1, 1) since debug rendering is handled explicitly.</returns>
    public override Vector3 GetScale()
    {
      return new Vector3( 1, 1, 1 );
    }

    /// <summary>
    /// Creates the native capsule object given current radius and height.
    /// </summary>
    /// <returns>Native capsule object.</returns>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.Capsule( m_radius, m_height ),
                                      GetNativeGeometryOffset() );
    }
  }
}
