using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Target speed controller object, constraining the angle of the constraint to
  /// be driven at a given speed.
  /// </summary>
  [Serializable]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class TargetSpeedControllerNew : ElementaryConstraintControllerNew
  {
    /// <summary>
    /// Desired speed to drive the constraint angle. Paired with property Speed.
    /// </summary>
    [SerializeField]
    private float m_speed = 0f;

    /// <summary>
    /// Desired speed to drive the constraint angle.
    /// </summary>
    public float Speed
    {
      get { return m_speed; }
      set
      {
        m_speed = value;
        if ( Native != null )
          agx.TargetSpeedController.safeCast( Native ).setSpeed( m_speed );
      }
    }

    /// <summary>
    /// State to lock at the current angle when the speed is set to zero.
    /// Paired with property LockAtZeroSpeed.
    /// </summary>
    [SerializeField]
    private bool m_lockAtZeroSpeed = false;

    /// <summary>
    /// State to lock at the current angle when the speed is set to zero.
    /// </summary>
    public bool LockAtZeroSpeed
    {
      get { return m_lockAtZeroSpeed; }
      set
      {
        m_lockAtZeroSpeed = value;
        if ( Native != null )
          agx.TargetSpeedController.safeCast( Native ).setLockedAtZeroSpeed( m_lockAtZeroSpeed );
      }
    }

    /// <summary>
    /// Convenience method to get current force applied by this controller. 0 if not initialized.
    /// </summary>
    public float GetCurrentForce()
    {
      if ( Native != null )
        return (float) agx.TargetSpeedController.safeCast( Native ).getCurrentForce( );
      else
        return 0;
    }

    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_speed = Convert.ToSingle( agx.TargetSpeedController.safeCast( tmpEc ).getSpeed() );
      m_lockAtZeroSpeed = agx.TargetSpeedController.safeCast( tmpEc ).getLockedAtZeroSpeed();
    }

    protected override void Construct( ElementaryConstraintNew source )
    {
      base.Construct( source );

      m_speed = ( source as TargetSpeedControllerNew ).m_speed;
      m_lockAtZeroSpeed = ( source as TargetSpeedControllerNew ).m_lockAtZeroSpeed;
    }
  }
}
