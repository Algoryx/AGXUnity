using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Data for each row in an elementary constraint.
  /// </summary>
  [AddComponentMenu( "" )]
  [Serializable]
  public class ElementaryConstraintRowData
  {
    /// <summary>
    /// Row index in the elementary constraint. This is normally only 0
    /// but can be 0 .. 2 for QuatLock and SphericalRel.
    /// </summary>
    [SerializeField]
    private int m_row = -1;

    /// <summary>
    /// Row index in the elementary constraint.
    /// </summary>
    [HideInInspector]
    public int Row { get { return m_row; } }

    /// <summary>
    /// Row index (unsigned version) in the elementary constraint.
    /// Some methods in the native elementary constraint takes uint as argument. 
    /// </summary>
    /// <remarks>
    /// 64-bit builds of AGX Dynamics takes ulong as argument but uint can be
    /// implicitly converted to ulong - not the other way around.
    /// </remarks>
    [HideInInspector]
    public uint RowUInt { get { return Convert.ToUInt32( Row ); } }

    /// <summary>
    /// Reference back to the elementary constraint.
    /// </summary>
    [SerializeField]
    private ElementaryConstraint m_elementaryConstraint = null;

    /// <summary>
    /// Reference back to the elementary constraint.
    /// </summary>
    [HideInInspector]
    public ElementaryConstraint ElementaryConstraint { get { return m_elementaryConstraint; } }

    /// <summary>
    /// Compliance of this row in the elementary constraint. Paired with property Compliance.
    /// </summary>
    [SerializeField]
    private float m_compliance = 1.0E-10f;

    /// <summary>
    /// Compliance of this row in the elementary constraint.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float Compliance
    {
      get { return m_compliance; }
      set
      {
        m_compliance = value;
        if ( ElementaryConstraint.Native != null )
          ElementaryConstraint.Native.setCompliance( m_compliance, Row );
      }
    }

    /// <summary>
    /// Damping of this row in the elementary constraint. Paired with property Damping.
    /// </summary>
    [SerializeField]
    private float m_damping = 2.0f / 50.0f;

    /// <summary>
    /// Damping of this row in the elementary constraint.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float Damping
    {
      get { return m_damping; }
      set
      {
        m_damping = value;
        if ( ElementaryConstraint.Native != null )
          ElementaryConstraint.Native.setDamping( m_damping, Row );
      }
    }

    /// <summary>
    /// Force range of this row in the elementary constraint. Paired with property ForceRange.
    /// </summary>
    [SerializeField]
    private RangeReal m_forceRange = new RangeReal( float.NegativeInfinity, float.PositiveInfinity );

    /// <summary>
    /// Force range of this row in the elementary constraint.
    /// </summary>
    public RangeReal ForceRange
    {
      get { return m_forceRange; }
      set
      {
        m_forceRange = value;
        if ( ElementaryConstraint.Native != null )
          ElementaryConstraint.Native.setForceRange( m_forceRange.Native, RowUInt );
      }
    }

    /// <summary>
    /// Construct given elementary constraint, row in the elementary constraint and (optional)
    /// a native instance to copy default values from.
    /// </summary>
    /// <param name="elementaryConstraint">Elementary constraint this row data belongs to.</param>
    /// <param name="row">Row index in the elementary constraint.</param>
    /// <param name="tmpEc">Temporary native instance to copy default values from.</param>
    public ElementaryConstraintRowData( ElementaryConstraint elementaryConstraint, int row, agx.ElementaryConstraint tmpEc = null )
    {
      m_elementaryConstraint = elementaryConstraint;
      m_row = row;
      if ( tmpEc != null ) {
        m_compliance = Convert.ToSingle( tmpEc.getCompliance( RowUInt ) );
        // AGX Dynamics damping is optimized for 60 Hz simulations. Assuming
        // a fixed update of 50 Hz in Unity we scale the damping by 60 / 50 = 1.2
        // to transform the damping to 50 Hz.
        m_damping = 1.2f * Convert.ToSingle( tmpEc.getDamping( RowUInt ) );
        m_forceRange = new RangeReal( tmpEc.getForceRange( RowUInt ) );
      }
    }

    /// <summary>
    /// Construct given elementary constraint and source instance.
    /// </summary>
    /// <param name="elementaryConstraint"></param>
    /// <param name="source"></param>
    public ElementaryConstraintRowData( ElementaryConstraint elementaryConstraint, ElementaryConstraintRowData source )
    {
      m_elementaryConstraint = elementaryConstraint;
      m_row = source.m_row;
      CopyFrom( source );
    }

    public void CopyFrom( ElementaryConstraintRowData source )
    {
      m_compliance = source.m_compliance;
      m_damping = source.m_damping;
      m_forceRange = new RangeReal( source.m_forceRange );
    }
  }
}
