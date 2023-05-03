using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Lock controller object, constraining the angle of the constraint to
  /// a given value.
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class LockController : ElementaryConstraintController
  {
    /// <summary>
    /// Desired position/angle to lock the angle to. Paired with property Position.
    /// </summary>
    [SerializeField]
    private float m_position = 0f;

    /// <summary>
    /// Desired position/angle to lock the angle to.
    /// </summary>
    public float Position
    {
      get { return m_position; }
      set
      {
        m_position = value;
        if ( Native != null )
          agx.LockController.safeCast( Native ).setPosition( m_position );
      }
    }

    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_position = Convert.ToSingle( agx.LockController.safeCast( tmpEc ).getPosition() );
    }

    protected override void Construct( ElementaryConstraint source )
    {
      base.Construct( source );

      m_position = ( source as LockController ).m_position;
    }
  }
}
