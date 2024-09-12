using AGXUnity.Utils;
using System;
using UnityEngine;

namespace AGXUnity
{
  [Serializable]
  public class WireWinch : IPropertySynchronizable
  {
    [HideInInspector]
    public agxWire.WireWinchController Native { get; private set; }

    [SerializeField]
    private float m_speed = 0.0f;
    public float Speed
    {
      get { return m_speed; }
      set
      {
        m_speed = value;
        if ( Native != null )
          Native.setSpeed( m_speed );
      }
    }

    [SerializeField]
    private float m_pulledInLength = 0.0f;
    [ClampAboveZeroInInspector( true )]
    public float PulledInLength
    {
      get
      {
        if ( Native != null )
          m_pulledInLength = Convert.ToSingle( Native.getPulledInWireLength() );
        return m_pulledInLength;
      }
      set
      {
        m_pulledInLength = Mathf.Max( value, 0.0f );
        if ( Native != null )
          Native.setPulledInWireLength( m_pulledInLength );
      }
    }

    [SerializeField]
    private RangeReal m_forceRange = new RangeReal( float.NegativeInfinity, float.PositiveInfinity );
    public RangeReal ForceRange
    {
      get { return m_forceRange; }
      set
      {
        m_forceRange = value;
        if ( Native != null )
          Native.setForceRange( m_forceRange.Native );
      }
    }

    [SerializeField]
    private RangeReal m_brakeForceRange = new RangeReal() { Min = 0f, Max = 0f };
    public RangeReal BrakeForceRange
    {
      get { return m_brakeForceRange; }
      set
      {
        m_brakeForceRange = value;
        if ( Native != null )
          Native.setBrakeForceRange( m_brakeForceRange.Native );
      }
    }

    public float CurrentForce
    {
      get
      {
        if ( Native != null )
          return Convert.ToSingle( Native.getCurrentForce() );
        return 0.0f;
      }
    }

    public void RestoreLocalDataFrom( agxWire.WireWinchController native ) 
    {
      if ( native == null )
        return;

      Speed           = Convert.ToSingle( native.getSpeed() );
      PulledInLength  = Convert.ToSingle( native.getPulledInWireLength() );
      ForceRange      = new RangeReal( native.getForceRange() );
      BrakeForceRange = new RangeReal( native.getBrakeForceRange() );
    }

    public bool Initialize( WireRouteNode winchNode )
    {
      if ( winchNode == null ) {
        Debug.LogWarning( "Unable to initialize winch - no winch node assigned." );
        return false;
      }

      RigidBody rb = winchNode.Parent != null ? winchNode.Parent.GetInitializedComponentInParent<RigidBody>() : null;
      if ( rb == null )
        Native = new agxWire.WireWinchController( null, winchNode.Position.ToHandedVec3(), ( winchNode.Rotation * Vector3.forward ).ToHandedVec3(), PulledInLength );
      else
        Native = new agxWire.WireWinchController( rb.Native, winchNode.CalculateLocalPosition( rb.gameObject ).ToHandedVec3(), ( winchNode.CalculateLocalRotation( rb.gameObject ) * Vector3.forward ).ToHandedVec3() );

      PropertySynchronizer.Synchronize( this );

      return true;
    }
  }
}
