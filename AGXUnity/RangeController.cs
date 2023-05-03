using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Range controller object, constraining the angle of the constraint to be
  /// within a given range.
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
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
