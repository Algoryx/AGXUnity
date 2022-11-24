using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Sphere shape object given radius.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Sphere" )]
  public sealed class Sphere : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Radius of this sphere paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.5f;

    /// <summary>
    /// Get or set radius of this sphere.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        m_radius = AGXUnity.Utils.Math.ClampAbove( value, MinimumSize );

        if ( Native != null )
          Native.setRadius( Radius );

        SizeUpdated();
      }
    }
    #endregion

    /// <summary>
    /// Returns the native sphere object if created.
    /// </summary>
    public agxCollide.Sphere Native { get { return NativeShape?.asSphere(); } }

    /// <summary>
    /// Debug rendering scale assuming the rendered sphere has diameter 1.
    /// </summary>
    public override Vector3 GetScale()
    {
      return new Vector3( 2.0f * Radius, 2.0f * Radius, 2.0f * Radius );
    }

    /// <summary>
    /// Create native sphere object given current radius.
    /// </summary>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.Sphere( Radius ),
                                      GetNativeGeometryOffset() );
    }
  }
}
