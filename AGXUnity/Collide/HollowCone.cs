using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Truncated right cone shape object given top and bottom radius, plus height.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Hollow Cone" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#additional-shapes" )]
  public sealed class HollowCone : Shape
  {
    #region Serialized Properties
    /// <summary>
    /// Thickness of the hollow cone paired with property Thickness.
    /// </summary>
    [SerializeField]
    private float m_thickness = 0.1f;
    /// <summary>
    /// Top radius of hollow cone paired with property TopRadius.
    /// </summary>
    [SerializeField]
    private float m_topRadius = 0.3f;
    /// <summary>
    /// Bottom radius of hollow cone paired with property TopRadius.
    /// </summary>
    [SerializeField]
    private float m_bottomRadius = 0.5f;
    /// <summary>
    /// Height of this cone hollow paired with property Height.
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
        m_thickness = Utils.Math.ClampAbove( Mathf.Min( m_bottomRadius - MinimumSize, value ), MinimumSize );

        if ( Native != null )
          Native.setThickness( m_thickness );

        SizeUpdated();
      }
    }

    /// <summary>
    /// Get or set top radius.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TopRadius
    {
      get { return m_topRadius; }
      set
      {
        m_topRadius = Utils.Math.ClampAbove( Mathf.Min( m_bottomRadius - MinimumSize, value ), MinimumSize );

        if ( Native != null )
          Native.setTopOuterRadius( m_topRadius );

        SizeUpdated();
      }
    }

    /// <summary>
    /// Get or set bottom radius.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float BottomRadius
    {
      get { return m_bottomRadius; }
      set
      {
        m_bottomRadius = Utils.Math.ClampAbove( Mathf.Max( value, m_topRadius + MinimumSize ), MinimumSize );

        if ( Native != null )
          Native.setBottomOuterRadius( m_bottomRadius );

        SizeUpdated();
      }
    }

    /// <summary>
    /// Get or set height of this cone.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Height
    {
      get { return m_height; }
      set
      {
        m_height = Utils.Math.ClampAbove( value, MinimumSize );

        if ( Native != null )
          Native.setHeight( m_height );

        SizeUpdated();
      }
    }
    #endregion

    /// <summary>
    /// Returns the native cone object if created.
    /// </summary>
    public agxCollide.HollowCone Native { get { return NativeShape?.asHollowCone(); } }

    /// <summary>
    /// Scale of meshes are inherited by the parents and supports non-uniform scaling.
    /// </summary>
    public override Vector3 GetScale()
    {
      return Vector3.one;
    }

    /// <summary>
    /// Creates the native cone object given current top and bottom radii plus height.
    /// </summary>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.HollowCone( m_topRadius, m_bottomRadius, m_height, m_thickness ),
                                      GetNativeGeometryOffset() );
    }
  }
}
