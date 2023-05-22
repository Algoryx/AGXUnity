using System;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class ElectricMotorController : ElementaryConstraintController
  {
    [SerializeField]
    private float m_voltage = 0f;
    public float Voltage
    {
      get { return m_voltage; }
      set
      {
        m_voltage = value;
        if ( Native != null )
          agx.ElectricMotorController.safeCast( Native ).setVoltage( m_voltage );
      }
    }

    [SerializeField]
    private float m_armatureResistance = 0f;
    public float ArmatureResistance
    {
      get { return m_armatureResistance; }
      set
      {
        m_armatureResistance = value;
        if ( Native != null )
          agx.ElectricMotorController.safeCast( Native ).setArmatureResistance( m_armatureResistance );
      }
    }

    [SerializeField]
    private float m_torqueConstant = 0f;
    public float TorqueConstant
    {
      get { return m_torqueConstant; }
      set
      {
        m_torqueConstant = value;
        if ( Native != null )
          agx.ElectricMotorController.safeCast( Native ).setTorqueConstant( m_torqueConstant );
      }
    }
    
    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_voltage = Convert.ToSingle( agx.ElectricMotorController.safeCast( tmpEc ).getVoltage() );
      m_armatureResistance = Convert.ToSingle( agx.ElectricMotorController.safeCast( tmpEc ).getArmatureResistance() );
      m_torqueConstant = Convert.ToSingle( agx.ElectricMotorController.safeCast( tmpEc ).getTorqueConstant() );
    }

    protected override void Construct( ElementaryConstraint source )
    {
      base.Construct( source );

      m_voltage = ( source as ElectricMotorController ).m_voltage;
      m_armatureResistance = ( source as ElectricMotorController ).m_armatureResistance;
      m_torqueConstant = ( source as ElectricMotorController ).m_torqueConstant;
    }
  }
}
