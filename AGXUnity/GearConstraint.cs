using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Gear Constraint" )]
  public class GearConstraint : ScriptComponent
  {
    [SerializeField]
    private Constraint m_beginActuatorConstraint = null;

    /// <summary>
    /// Begin actuator constraint. Supported types: Hinge, Prismatic and Distance Joint.
    /// </summary>
    [AllowRecursiveEditing]
    [IgnoreSynchronization]
    public Constraint BeginActuatorConstraint
    {
      set { m_beginActuatorConstraint = value; }
      get { return m_beginActuatorConstraint; }
    }

    [SerializeField]
    private Constraint m_endActuatorConstraint = null;

    /// <summary>
    /// End actuator constraint. Supported types: Hinge, Prismatic and Distance Joint.
    /// </summary>
    [AllowRecursiveEditing]
    [IgnoreSynchronization]
    public Constraint EndActuatorConstraint
    {
      get { return m_endActuatorConstraint; }
      set { m_endActuatorConstraint = value; }
    }

    [SerializeField]
    private float m_gearRatio = 1.0f;

    /// <summary>
    /// Gear ratio where increased value affects the end actuator to
    /// move slower. E.g., end actuator will move at half the speed
    /// when the gear ratio is 2.0. Default: 1.0.
    /// </summary>
    public float GearRatio
    {
      get { return m_gearRatio; }
      set
      {
        m_gearRatio = value;
        if ( Gear != null )
          Gear.setGearRatio( m_gearRatio );
      }
    }

    /// <summary>
    /// When the begin actuator is translational and the end
    /// actuator is rotational, the power line is configured
    /// in reverse, resulting in an inverted gear ratio.
    /// </summary>
    [HideInInspector]
    public bool IsInverted { get; private set; } = false;

    public agxPowerLine.PowerLine PowerLine { get; private set; } = null;
    public agxDriveTrain.Gear Gear { get; private set; } = null;
    public agxPowerLine.Actuator1DOF BeginActuator { get; private set; } = null;
    public agxPowerLine.Actuator1DOF EndActuator { get; private set; } = null;

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXDriveTrain, this ) )
        return false;

      if ( BeginActuatorConstraint?.GetInitialized<Constraint>() == null ) {
        Debug.LogError( "Invalid GearConstraint: Begin actuator constraint is invalid or null.", this );
        return false;
      }
      if ( EndActuatorConstraint?.GetInitialized<Constraint>() == null ) {
        Debug.LogError( "Invalid GearConstraint: End actuator constraint is invalid  or null.", this );
        return false;
      }
      if ( BeginActuatorConstraint == EndActuatorConstraint ) {
        Debug.LogError( "Invalid GearConstraint: Begin actuator constraint is the same instance as the end actuator constraint.", this );
        return false;
      }
      if ( !IsValidConstraintType( BeginActuatorConstraint.Type ) ) {
        Debug.LogError( $"Invalid GearConstraint: Begin actuator constraint type {BeginActuatorConstraint.Type} isn't supported.", this );
        return false;
      }
      if ( !IsValidConstraintType( EndActuatorConstraint.Type ) ) {
        Debug.LogError( $"Invalid GearConstraint: End actuator constraint type {EndActuatorConstraint.Type} isn't supported.", this );
        return false;
      }

      // Rotational -> Rotational:
      //     Begin Actuator -> shaft -> Gear -> shaft -> End Actuator
      // Rotational -> Translational
      //     Begin Actuator -> shaft -> Gear -> shaft -> Converter (Rotational to Translational) -> End Actuator
      // Translational -> Translational
      //     Begin Actuator -> Converter -> shaft -> Gear -> shaft -> Converter -> End Actuator
      // Translational -> Rotational (Inverted)
      //     End Actuator -> shaft -> Gear -> shaft -> Converter -> Begin Actuator

      BeginActuator = CreateActuator( BeginActuatorConstraint );
      EndActuator   = CreateActuator( EndActuatorConstraint );

      IsInverted = BeginActuator.GetType() != EndActuator.GetType() &&
                   BeginActuator is agxPowerLine.TranslationalActuator &&
                   EndActuator is agxPowerLine.RotationalActuator;

      PowerLine = new agxPowerLine.PowerLine();
      Gear      = new agxDriveTrain.Gear( GearRatio );

      PowerLine.add( Gear );
      PowerLine.setName( name );

      var beginShaft = new agxDriveTrain.Shaft();
      var endShaft   = new agxDriveTrain.Shaft();

      Gear.connect( agxPowerLine.Side.INPUT, agxPowerLine.Side.OUTPUT, beginShaft );
      Gear.connect( agxPowerLine.Side.OUTPUT, agxPowerLine.Side.INPUT, endShaft );

      // Translational -> Rotational.
      if ( IsInverted ) {
        EndActuator.connect( endShaft );
        var converter = new agxPowerLine.RotationalTranslationalConnector();
        converter.connect( ( BeginActuator as agxPowerLine.TranslationalActuator ).getInputRod() );

        beginShaft.connect( converter );
      }
      // Rotational -> Translational.
      else if ( BeginActuator is agxPowerLine.RotationalActuator &&
                EndActuator is agxPowerLine.TranslationalActuator ) {
        BeginActuator.connect( beginShaft );
        var converter = new agxPowerLine.RotationalTranslationalConnector();
        converter.connect( ( EndActuator as agxPowerLine.TranslationalActuator ).getInputRod() );

        endShaft.connect( converter );
      }
      // Translational -> Translational.
      else if ( BeginActuator is agxPowerLine.TranslationalActuator &&
                EndActuator is agxPowerLine.TranslationalActuator ) {
        var beginConverter = new agxPowerLine.RotationalTranslationalConnector();
        var endConverter   = new agxPowerLine.RotationalTranslationalConnector();

        beginConverter.connect( ( BeginActuator as agxPowerLine.TranslationalActuator ).getInputRod() );
        endConverter.connect( ( EndActuator as agxPowerLine.TranslationalActuator ).getInputRod() );

        beginShaft.connect( beginConverter );
        endShaft.connect( endConverter );
      }
      // Rotational -> Rotational.
      else {
        BeginActuator.connect( beginShaft );
        EndActuator.connect( endShaft );
      }

      GetSimulation().add( PowerLine );

      return true;
    }

    protected override void OnEnable()
    {
      if ( PowerLine != null && PowerLine.getSimulation() == null )
        GetSimulation().add( PowerLine );
    }

    protected override void OnDisable()
    {
      if ( PowerLine != null && Simulation.HasInstance )
        GetSimulation().remove( PowerLine );
    }

    protected override void OnDestroy()
    {
      PowerLine     = null;
      Gear          = null;
      BeginActuator = null;
      EndActuator   = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      m_beginActuatorConstraint = GetComponent<Constraint>();
    }

    private agxPowerLine.Actuator1DOF CreateActuator( Constraint constraint )
    {
      if ( constraint.Type == ConstraintType.Prismatic )
        return new agxPowerLine.TranslationalActuator( constraint.Native.asPrismatic() );
      else if ( constraint.Type == ConstraintType.DistanceJoint )
        return new agxPowerLine.TranslationalActuator( constraint.Native.asDistanceJoint() );
      else if ( constraint.Type == ConstraintType.Hinge )
        return new agxPowerLine.RotationalActuator( constraint.Native.asHinge() );
      return null;
    }

    private static bool IsValidConstraintType( ConstraintType type )
    {
      return IsValidConstraintType( type,
                                    ConstraintType.Hinge,
                                    ConstraintType.Prismatic,
                                    ConstraintType.DistanceJoint );
    }

    private static bool IsValidConstraintType( ConstraintType type, params ConstraintType[] types )
    {
      return types.Any( t => t == type );
    }
  }
}
