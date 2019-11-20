﻿using System;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Hollow cylinder shape object given radius and height and wall thickness.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/HollowCylinder" )]
  public sealed class HollowCylinder : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Thickness of the hollow cylinder paired with property Thickness.
    /// </summary>
    [SerializeField]
    private float m_thickness = 0.1f;
    /// <summary>
    /// Radius of the hollow cylinder paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.5f;
    /// <summary>
    /// Height of this hollow cylinder paired with property Height.
    /// </summary>
    [SerializeField]
    private float m_height = 1.0f;

    /// <summary>
    /// Get or set thickness of this hollow cylinder.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Thickness
    {
      get { return m_thickness; }
      set
      {
        m_thickness = AGXUnity.Utils.Math.ClampAbove( Mathf.Min(m_radius- MinimumLength, value), MinimumLength );

        if ( Native != null )
          Native.setThickness( m_thickness );

        SizeUpdated();
      }
    }

    /// <summary>
    /// Get or set radius of this hollow cylinder.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        m_radius = AGXUnity.Utils.Math.ClampAbove( value, MinimumLength );

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
        m_height = AGXUnity.Utils.Math.ClampAbove( value, MinimumLength );

        if ( Native != null )
          Native.setHeight( m_height );

        SizeUpdated();
      }
    }
    #endregion

    /// <summary>
    /// Returns the native cylinder object if created.
    /// </summary>
    public agxCollide.HollowCylinder Native { get { return m_shape as agxCollide.HollowCylinder; } }

    /// <summary>
    /// Scale of meshes are inherited by the parents and supports non-uniform scaling.
    /// </summary>
    public override Vector3 GetScale()
    {
      return Vector3.one;
    }

    /// <summary>
    /// Creates the native cylinder object given current radius and height.
    /// </summary>
    protected override agxCollide.Shape CreateNative()
    {
      return new agxCollide.HollowCylinder( m_radius, m_height, m_thickness );
    }
  }
}
