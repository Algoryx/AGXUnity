using System;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Cylinder shape object given radius and height.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Cylinder" )]
  public sealed class Cylinder : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Radius of the cylinder paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.5f;
    /// <summary>
    /// Height of this cylinder paired with property Height.
    /// </summary>
    [SerializeField]
    private float m_height = 1.0f;

    /// <summary>
    /// Get or set radius of this cylinder.
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
    /// Get or set height of this cylinder.
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
    /// Returns the native cylinder object if created.
    /// </summary>
    public agxCollide.Cylinder Native { get { return NativeShape?.asCylinder(); } }

    /// <summary>
    /// Debug rendering scale assuming the rendered cylinder has diameter 1 and height 1.
    /// </summary>
    public override Vector3 GetScale()
    {
      return new Vector3( 2.0f * m_radius, 0.5f * m_height, 2.0f * m_radius );
    }

    /// <summary>
    /// Creates the native cylinder object given current radius and height.
    /// </summary>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.Cylinder( m_radius, m_height ),
                                      GetNativeGeometryOffset() );
    }
  }
}
