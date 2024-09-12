using System;
using UnityEngine;

namespace AGXUnity.Deprecated
{
  /// <summary>
  /// Range controller object, constraining the angle of the constraint to be
  /// within a given range.
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
  [DoNotGenerateCustomEditor]
  [Obsolete]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class RangeController : ElementaryConstraintController
  {
    /// <summary>
    /// Valid range of the constraint angle. Paired with property Range.
    /// </summary>
    [SerializeField]
    private RangeReal m_range = new RangeReal( float.NegativeInfinity, float.PositiveInfinity );

    /// <summary>
    /// Valid range of the constraint angle.
    /// </summary>
    public RangeReal Range
    {
      get { return m_range; }
      set
      {
        m_range = value;
        if ( Native != null )
          agx.RangeController.safeCast( Native ).setRange( m_range.Native );
      }
    }

    /// <summary>
    /// Convenience method to get current force applied by this controller. 0 if not initialized.
    /// </summary>
    public float GetCurrentForce()
    {
      if ( Native != null )
        return (float) agx.RangeController.safeCast( Native ).getCurrentForce( );
      else
        return 0;
    }

    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_range = new RangeReal( agx.RangeController.safeCast( tmpEc ).getRange() );
    }

    protected override void Construct( ElementaryConstraint source )
    {
      base.Construct( source );

      m_range = new RangeReal( ( source as RangeController ).m_range );
    }
  }
}
