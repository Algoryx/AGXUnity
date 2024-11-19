using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Lock controller object, constraining the angle of the constraint to
  /// a given value.
  /// </summary>
  [Serializable]
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

    /// <summary>
    /// Convenience method to get current force applied by this controller. 0 if not initialized.
    /// </summary>
    public float GetCurrentForce()
    {
      if ( Native != null )
        return (float)agx.LockController.safeCast( Native ).getCurrentForce();
      else
        return 0;
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
