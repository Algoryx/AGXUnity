using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    [HideInInspector]
    [field: NonSerialized]
    public ElementaryConstraint ElementaryConstraint { get; internal set; }

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
        if ( ElementaryConstraint?.Native != null )
          ElementaryConstraint.Native.setCompliance( m_compliance, Row );
      }
    }

    /// <summary>
    /// Attenuation of this row in the elementary constraint. Paired with property Attenuation.
    /// </summary>
    [SerializeField]
    [FormerlySerializedAs("m_damping")]
    private float m_attenuation = 2.0f;

    /// <summary>
    /// Attenuation of this row in the elementary constraint.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float Attenuation
    {
      get { return m_attenuation; }
      set
      {
        m_attenuation = value;
        if ( ElementaryConstraint?.Native != null )
          ElementaryConstraint.Native.setAttenuation( m_attenuation, Row );
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
        if ( ElementaryConstraint?.Native != null )
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
      ElementaryConstraint = elementaryConstraint;
      m_row = row;
      if ( tmpEc != null ) {
        m_compliance = Convert.ToSingle( tmpEc.getCompliance( RowUInt ) );
        m_attenuation = Convert.ToSingle( tmpEc.getAttenuation( RowUInt ) );
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
      ElementaryConstraint = elementaryConstraint;
      m_row = source.m_row;
      CopyFrom( source );
    }

    public void CopyFrom( ElementaryConstraintRowData source )
    {
      m_compliance = source.m_compliance;
      m_attenuation = source.m_attenuation;
      m_forceRange = new RangeReal( source.m_forceRange );
    }
  }
}
