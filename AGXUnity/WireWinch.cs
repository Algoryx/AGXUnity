using System;
using System.Linq;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  public class WireWinch : ScriptComponent
  {
    [HideInInspector]
    public agxWire.WireWinchController Native { get; private set; }

    [SerializeField]
    private Wire m_wire = null;
    [HideInInspector]
    public Wire Wire
    {
      get { return m_wire; }
      set { m_wire = value; }
    }

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
      get { return m_pulledInLength; }
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

    protected override bool Initialize()
    {
      if ( Wire == null ) {
        Debug.LogWarning( "Wire not assigned to winch. Winch invalid and ignored.", this );
        return false;
      }

      WireRouteNode winchNode = Wire.Route.FirstOrDefault( node => node.Winch == this );
      if ( winchNode == null ) {
        Debug.LogWarning( "Unable to initialize winch - no winch node assigned.", this );
        return false;
      }

      RigidBody rb = winchNode.Parent != null ? winchNode.Parent.GetInitializedComponentInParent<RigidBody>() : null;
      if ( rb == null )
        Native = new agxWire.WireWinchController( null, winchNode.Position.ToHandedVec3(), ( winchNode.Rotation * Vector3.forward ).ToHandedVec3(), PulledInLength );
      else
        Native = new agxWire.WireWinchController( rb.Native, winchNode.CalculateLocalPosition( rb.gameObject ).ToHandedVec3(), ( winchNode.CalculateLocalRotation( rb.gameObject ) * Vector3.forward ).ToHandedVec3() );

      return true;
    }

    public void OnPostStepForward( Wire wire )
    {
      if ( Native != null )
        m_pulledInLength = Convert.ToSingle( Native.getPulledInWireLength() );
    }

    protected override void OnDestroy()
    {
      Native = null;

      base.OnDestroy();
    }
  }
}
