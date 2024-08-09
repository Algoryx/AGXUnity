using AGXUnity.Utils;
using Brick.Physics1D.Charges;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public class DrivetrainMapper
  {
    private RuntimeMapper.Data MapperData { get; set; }

    public DrivetrainMapper( RuntimeMapper.Data data )
    {
      MapperData = data;
    }

    public agxPowerLine.RotationalUnit MapRotationalBody( Brick.Physics1D.Bodies.RotationalBody body )
    {
      if ( body is Brick.DriveTrain.Shaft shaft ) {
        agxDriveTrain.Shaft agx_shaft = new agxDriveTrain.Shaft();
        agx_shaft.setName( body.getName() );
        agx_shaft.setInertia( body.inertia().inertia() );
        return agx_shaft;
      }

      agxPowerLine.RotationalUnit rotational_unit = new agxPowerLine.RotationalUnit();
      rotational_unit.setName( body.getName() );
      if ( body.inertia() != null )
        rotational_unit.setInertia( body.inertia().inertia() );
      return rotational_unit;
    }

    public agxPowerLine.RotationalConnector MapGear( Brick.DriveTrain.Gear gear )
    {
      agxDriveTrain.HolonomicGear agx_gear = new agxDriveTrain.HolonomicGear();

      agx_gear.setGearRatio( gear.ratio() );
      var damping = InteractionMapper.MapDissipation( gear.dissipation(), gear.flexibility() );
      if( damping != null )
        agx_gear.setViscousDamping( damping.Value );
      var flexibility = InteractionMapper.MapFlexibility( gear.flexibility() );
      if( flexibility != null ) 
        agx_gear.setViscousCompliance( flexibility.Value );

      Brick.Physics.Charges.Charge charge1 = gear.charges().Count >= 1 ? gear.charges()[0] : null;
      Brick.Physics.Charges.Charge charge2 = gear.charges().Count >= 2 ? gear.charges()[1] : null;

      var mc1 = charge1 == null ? null : (charge1 as Brick.Physics1D.Charges.MateConnector);
      var mc2 = charge2 == null ? null : (charge2 as Brick.Physics1D.Charges.MateConnector);

      agxPowerLine.Unit unit1 = mc1 == null ? null : MapperData.UnitCache[mc1.getOwner()];
      agxPowerLine.Unit unit2 = mc2 == null ? null : MapperData.UnitCache[mc2.getOwner()];

      if ( unit1 == null && unit2 == null ) {
        // Todo: Error reporting
        //reportErrorFromKey( hinge_key, AGXBrickError::MissingConnectedBody, system );
      }

      if ( unit1 != null && unit2 != null ) {
        unit1.connect( agx_gear );
        unit2.connect( agx_gear );
        agx_gear.connect( unit1 );
        agx_gear.connect( unit2 );
      }
      else if ( unit1 != null ) {
        unit1.connect( agx_gear );
        agx_gear.connect( unit1 );
      }
      else if ( unit2 != null ) {
        unit2.connect( agx_gear );
        agx_gear.connect( unit2 );
      }

      agx_gear.setName( gear.getName() );

      return agx_gear;
    }

    public agxDriveTrain.Differential MapDifferential( Brick.DriveTrain.Differential differential )
    {
      agxDriveTrain.Differential agx_differential = new agxDriveTrain.Differential();

      if ( differential is Brick.DriveTrain.TorqueLimitedSlipDifferential lsd ) {
        agx_differential.setLock( true );
        agx_differential.setLimitedSlipTorque( lsd.breakaway_torque() );
      }

      agx_differential.setGearRatio( differential.gear_ratio() );

      Brick.Physics.Charges.Charge charge1 = differential.charges().Count >= 1 ? differential.charges()[0] : null;
      Brick.Physics.Charges.Charge charge2 = differential.charges().Count >= 2 ? differential.charges()[1] : null;
      Brick.Physics.Charges.Charge charge3 = differential.charges().Count >= 3 ? differential.charges()[2] : null;

      var drive_connector = charge1 as Brick.Physics1D.Charges.MateConnector;
      var left_connector  = charge2 as Brick.Physics1D.Charges.MateConnector;
      var right_connector = charge3 as Brick.Physics1D.Charges.MateConnector;

      var drive_shaft = drive_connector?.getOwner() as Brick.DriveTrain.Shaft;
      var left_shaft  = left_connector?.getOwner() as Brick.DriveTrain.Shaft;
      var right_shaft = right_connector?.getOwner() as Brick.DriveTrain.Shaft;
      agxPowerLine.Unit drive_unit = MapperData.UnitCache.GetValueOrDefault(drive_shaft,null);
      agxPowerLine.Unit left_unit  = MapperData.UnitCache.GetValueOrDefault(left_shaft,null);
      agxPowerLine.Unit right_unit = MapperData.UnitCache.GetValueOrDefault(right_shaft,null);

      if ( drive_unit == null || left_unit == null || right_unit == null ) {
        // TODO: Error reporting
      }
      else {
        agxPowerLine.Side drive_side = drive_shaft.input() == drive_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        agx_differential.connect( agxPowerLine.Side.INPUT, drive_side, drive_unit );
        agxPowerLine.Side left_side = left_shaft.input() == left_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        agx_differential.connect( agxPowerLine.Side.OUTPUT, left_side, left_unit );
        agxPowerLine.Side right_side = right_shaft.input() == right_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        agx_differential.connect( agxPowerLine.Side.OUTPUT, right_side, right_unit );
      }

      agx_differential.setName( differential.getName() );

      return agx_differential;

    }

    public agxDriveTrain.TorqueConverter MapTorqueConverter( Brick.DriveTrain.EmpiricalTorqueConverter tc )
    {
      agxDriveTrain.TorqueConverter agx_torque_converter = new agxDriveTrain.TorqueConverter();

      agx_torque_converter.setLockUpTime( tc.lock_up_time() );
      agx_torque_converter.setOilDensity( tc.oil_density() );
      agx_torque_converter.setPumpDiameter( tc.diameter() );

      agx_torque_converter.clearEfficiencyTable();
      agx_torque_converter.clearGeometryFactorTable();

      foreach ( var pair in tc.velocity_ratio_geometry_factor_list() ) {
        agx_torque_converter.insertGeometryFactorLookupValue( pair.velocity_ratio(), pair.geometry_factor() );
      }

      foreach ( var pair in tc.velocity_ratio_torque_multiplier_list() ) {
        // AGX has wrong naming. It is multiplier, not efficiency.
        agx_torque_converter.insertEfficiencyLookupValue( pair.velocity_ratio(), pair.multiplier() );
      }

      Brick.Physics.Charges.Charge charge1 = tc.charges().Count >= 1 ? tc.charges()[0] : null;
      Brick.Physics.Charges.Charge charge2 = tc.charges().Count >= 2 ? tc.charges()[1] : null;

      var pump_connector = charge1 as Brick.Physics1D.Charges.MateConnector;
      var turbine_connector = charge2 as Brick.Physics1D.Charges.MateConnector;

      var pump_shaft = pump_connector.getOwner() as Brick.DriveTrain.Shaft;
      var turbine_shaft = turbine_connector.getOwner() as Brick.DriveTrain.Shaft;

      agxPowerLine.Unit pump_unit = pump_shaft == null ? null : MapperData.UnitCache[pump_shaft];
      agxPowerLine.Unit turbine_unit = turbine_shaft == null ? null : MapperData.UnitCache[turbine_shaft];

      if ( pump_unit == null || turbine_unit == null ) {
        // TODO: Error reporting
        //reportErrorFromKey( torque_converter_key, AGXBrickError.MissingConnectedBody, system );
      }
      else {
        agxPowerLine.Side pump_side = pump_shaft.input() == pump_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        agx_torque_converter.connect( agxPowerLine.Side.INPUT, pump_side, pump_unit );
        agxPowerLine.Side turbine_side = turbine_shaft.input() == turbine_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        agx_torque_converter.connect( agxPowerLine.Side.OUTPUT, turbine_side, turbine_unit );
      }

      agx_torque_converter.setName( tc.getName() );

      return agx_torque_converter;
    }

    public agxDriveTrain.VelocityConstraint Map1dRotationalVelocityMotor( Brick.Physics1D.Interactions.RotationalVelocityMotor motor )
    {
      agxDriveTrain.VelocityConstraint constraint = null;

      var charge = motor.charges()[0];
      if ( charge != null ) {
        agxPowerLine.Unit unit = MapperData.UnitCache[charge.getOwner()];
        if ( unit is agxPowerLine.RotationalUnit rot_unit ) {
          constraint = new agxDriveTrain.VelocityConstraint( rot_unit );
          constraint.setForceRange( new agx.RangeReal( motor.min_effort(), motor.max_effort() ) );
          constraint.setName( motor.getName() );
          constraint.setTargetVelocity( motor.desired_speed() );
        }
      }
      return constraint;
    }

    public agxDriveTrain.CombustionEngine MapCombustionEngine( Brick.DriveTrain.CombustionEngine engine )
    {
      var parameters = new agxDriveTrain.CombustionEngineParameters();
      parameters.displacementVolume = engine.displacement_volume();
      parameters.maxTorque = engine.max_torque();
      parameters.maxTorqueRPM = engine.max_torque_RPM();
      //parameters.maxPower = engine.max_power();
      parameters.maxPowerRPM = engine.max_power_RPM();
      parameters.idleRPM = engine.idle_RPM();
      parameters.crankShaftInertia = engine.crank_shaft_inertia();
      agxDriveTrain.CombustionEngine agxEngine = new agxDriveTrain.CombustionEngine(parameters);

      agxEngine.setEnable( true );
      agxEngine.setThrottle( engine.initial_throttle() );
      Brick.Physics.Charges.Charge charge = engine.charges().Count >= 1 ? engine.charges()[0] : null;

      var stiff_internal_gear = new agxDriveTrain.Gear(1.0);
      stiff_internal_gear.connect( agxPowerLine.Side.INPUT, agxPowerLine.Side.OUTPUT, agxEngine );


      if ( charge is not MateConnector connector ) {
        // Error reporting
        //reportErrorFromKey( torque_converter_key, AGXBrickError.MissingConnectedBody, system );
      }
      else {
        var shaft = connector.getOwner() as Brick.DriveTrain.Shaft;

        agxPowerLine.Unit shaft_unit = shaft == null ? null : MapperData.UnitCache[ shaft ];

        if ( shaft_unit == null ) {
          // Error reporting
          // reportErrorFromKey( torque_converter_key, AGXBrickError.MissingConnectedBody, system );
        }
        else {
          stiff_internal_gear.connect( agxPowerLine.Side.OUTPUT, agxPowerLine.Side.INPUT, shaft_unit );
        }
      }
      agxEngine.setName( engine.getName() );

      return agxEngine;

    }

    public void MapActuator( Brick.DriveTrain.Actuator actuator )
    {
      Brick.Physics1D.Charges.MateConnector connector_1d = actuator.connector_1d();
      Brick.Core.Object mate = actuator.mate_3d();

      var agx_constraint = MapperData.Root.FindMappedObject( mate.getName() )?.GetInitializedComponent<Constraint>();
      if ( agx_constraint == null ) {
        Debug.LogError( $"Missing interaction for actuator: {actuator.getName()} (interaction: {mate.getName()})" );
        return;
      }

      var internal_inertia = 1E-4;
      var internal_inertia_annotation = actuator.findAnnotations("agx_actuator_internal_inertia");
      if ( internal_inertia_annotation.Count == 1 && internal_inertia_annotation[ 0 ].isNumber() )
        internal_inertia = internal_inertia_annotation[ 0 ].asReal();

      // Create agx actuator for this (BRICK) actuator
      agxPowerLine.Actuator axis = null;
      if ( actuator is Brick.DriveTrain.HingeActuator ha ) {
        var rotational_actuator = new agxPowerLine.RotationalActuator(agx_constraint.Native.asConstraint1DOF());
        rotational_actuator.getInputShaft().setInertia( internal_inertia );
        axis = rotational_actuator;
      }
      else if ( actuator is Brick.DriveTrain.PrismaticActuator pa ) {
        var translational_actuator = new agxPowerLine.TranslationalActuator (agx_constraint.Native.asConstraint1DOF());
        translational_actuator.getInputRod().setMass( internal_inertia );
        axis = translational_actuator;
      }

      // Connect newly created agx actuator to agx power line unit from m_drivetrain_unit_map
      axis.setName( actuator.getName() );
      MapperData.PowerLine.add( axis );
      MapperData.RuntimeMap[ actuator.getName() ] = axis;
      if ( connector_1d != null ) {
        agxPowerLine.Unit unit = MapperData.UnitCache[connector_1d.getOwner()];
        if ( unit != null )
          unit.connect( axis );
        else
          Debug.LogError( $"Missing interaction for actuator: {connector_1d.getOwner().getName()}" );
      }
    }
  }
}
