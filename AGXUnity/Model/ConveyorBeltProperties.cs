using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  public class ConveyorBeltProperties : ScriptAsset
  {
    [SerializeField]
    private Vector3 m_translationalCompliance = 1.0E-8f * Vector3.one;

    public Vector3 TranslationalCompliance
    {
      get { return m_translationalCompliance; }
      set
      {
        m_translationalCompliance = value;
        Propagate( SetCompliance( m_translationalCompliance ) );
      }
    }

    [SerializeField]
    private Vector2 m_rotationalCompliance = 1.0E-8f * Vector2.one;

    public Vector2 RotationalCompliance
    {
      get { return m_rotationalCompliance; }
      set
      {
        m_rotationalCompliance = value;
        Propagate( SetCompliance( m_rotationalCompliance ) );
      }
    }

    public void Register( ConveyorBelt belt )
    {
      if ( !m_belts.Contains( belt ) ) {
        m_belts.Add( belt );

        Utils.PropertySynchronizer.Synchronize( this );
      }
    }

    public void Unregister( ConveyorBelt belt )
    {
      m_belts.Remove( belt );
    }

    public override void Destroy()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    private void Propagate( Action<agx.Constraint> constraintAction )
    {
      if ( constraintAction == null )
        return;

      foreach ( var belt in m_belts ) {
        if ( belt.ConnectingConstraints == null )
          continue;
        foreach ( var constraint in belt.ConnectingConstraints )
          constraintAction.Invoke( constraint );
      }
    }

    private Action<agx.Constraint> SetCompliance( Vector3 translationalCompliance )
    {
      Action<agx.Constraint> setCompliance = constraint =>
      {
        if ( constraint is agx.Hinge hinge ) {
          hinge.setCompliance( translationalCompliance.x, (long)agx.Hinge.DOF.TRANSLATIONAL_1 );
          hinge.setCompliance( translationalCompliance.y, (long)agx.Hinge.DOF.TRANSLATIONAL_2 );
          hinge.setCompliance( translationalCompliance.z, (long)agx.Hinge.DOF.TRANSLATIONAL_3 );
        }
        else if ( constraint is agx.BallJoint ballJoint ) {
          ballJoint.setCompliance( translationalCompliance.x, (long)agx.BallJoint.DOF.TRANSLATIONAL_1 );
          ballJoint.setCompliance( translationalCompliance.y, (long)agx.BallJoint.DOF.TRANSLATIONAL_2 );
          ballJoint.setCompliance( translationalCompliance.z, (long)agx.BallJoint.DOF.TRANSLATIONAL_3 );
        }
      };
      return setCompliance;
    }

    private Action<agx.Constraint> SetCompliance( Vector2 rotationalCompliance )
    {
      Action<agx.Constraint> setCompliance = constraint =>
      {
        if ( constraint is agx.Hinge hinge ) {
          hinge.setCompliance( rotationalCompliance.x, (long)agx.Hinge.DOF.ROTATIONAL_1 );
          hinge.setCompliance( rotationalCompliance.y, (long)agx.Hinge.DOF.ROTATIONAL_2 );
        }
      };
      return setCompliance;
    }

    [NonSerialized]
    private List<ConveyorBelt> m_belts = new List<ConveyorBelt>();
  }
}
