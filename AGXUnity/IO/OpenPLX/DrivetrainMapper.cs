using AGXUnity.Utils;
using openplx.Physics1D.Charges;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public class DrivetrainMapper
  {
    private RuntimeMapper.Data MapperData { get; set; }

    public DrivetrainMapper( RuntimeMapper.Data data )
    {
      MapperData = data;
    }

    public agxPowerLine.RotationalUnit MapRotationalBody( openplx.Physics1D.Bodies.RotationalBody body )
    {
      if ( body is openplx.DriveTrain.Shaft shaft ) {
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

    public void ConnectDrivetrainInteraction( openplx.Physics.Interactions.Interaction interaction, agxPowerLine.Connector connector )
    {
      openplx.Physics.Charges.Charge charge1 = interaction.charges().Count >= 1 ? interaction.charges()[0] : null;
      openplx.Physics.Charges.Charge charge2 = interaction.charges().Count >= 2 ? interaction.charges()[1] : null;

      var input_connector = charge1 as openplx.Physics1D.Charges.MateConnector;
      var output_connector = charge2 as openplx.Physics1D.Charges.MateConnector;

      var pump_shaft = input_connector.getOwner() as openplx.DriveTrain.Shaft;
      var turbine_shaft = output_connector.getOwner() as openplx.DriveTrain.Shaft;

      agxPowerLine.Unit pump_unit = pump_shaft == null ? null : MapperData.UnitCache[pump_shaft];
      agxPowerLine.Unit turbine_unit = turbine_shaft == null ? null : MapperData.UnitCache[turbine_shaft];

      if ( pump_unit == null || turbine_unit == null ) {
        // TODO: Error reporting
        //reportErrorFromKey( torque_converter_key, MissingConnectedBody, system );
      }
      else {
        agxPowerLine.Side pump_side = pump_shaft.input() == input_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        connector.connect( agxPowerLine.Side.INPUT, pump_side, pump_unit );
        agxPowerLine.Side turbine_side = turbine_shaft.input() == output_connector ? agxPowerLine.Side.INPUT : agxPowerLine.Side.OUTPUT;
        connector.connect( agxPowerLine.Side.OUTPUT, turbine_side, turbine_unit );
      }
    }

    public agxPowerLine.RotationalConnector MapGear( openplx.DriveTrain.Gear gear )
    {
      agxDriveTrain.Gear agx_gear = null;
      if ( gear is openplx.DriveTrain.ViscousGear viscGear ) {
        var slipGear = new agxDriveTrain.SlipGear();

        if ( viscGear.dissipation().viscosity() > double.MaxValue )
          slipGear.setViscousCompliance( 0 );
        else if ( viscGear.dissipation().viscosity() == 0.0 )
          slipGear.setViscousCompliance( double.MaxValue );
        else
          slipGear.setViscousCompliance( 1.0/viscGear.dissipation().viscosity() );

        agx_gear = slipGear;
      }
      else if ( gear is openplx.DriveTrain.FlexibleGear flexGear ) {
        var holonomicGear = new agxDriveTrain.HolonomicGear();
        var damping = InteractionMapper.MapDissipation( flexGear.dissipation(), flexGear.flexibility() );
        if ( damping != null )
          holonomicGear.setViscousDamping( damping.Value );
        var flexibility = InteractionMapper.MapFlexibility( flexGear.flexibility() );
        if ( flexibility != null )
          holonomicGear.setViscousCompliance( flexibility.Value );

        agx_gear = holonomicGear;
      }
      else {
        // TODO: Error reporting
        return null;
      }

      agx_gear.setGearRatio( gear.ratio() );
      ConnectDrivetrainInteraction( gear, agx_gear );
      agx_gear.setName( gear.getName() );

      return agx_gear;
    }

    public agxDriveTrain.GearBox MapGearBox( openplx.DriveTrain.GearBox gear_box )
    {
      agxDriveTrain.GearBox agx_gear_box = new agxDriveTrain.GearBox();
      agx.RealVector gears = new agx.RealVector();

      foreach ( var ratio in gear_box.reverse_gears().AsEnumerable().Reverse() )
        gears.Add( -agx.agxMath.Abs( ratio ) );

      gears.Add( 0 );

      foreach ( var ratio in gear_box.forward_gears() )
        gears.Add( agx.agxMath.Abs( ratio ) );

      agx_gear_box.setGearRatios( gears );

      var relaxation_time = InteractionMapper.MapDissipation(gear_box.dissipation(), gear_box.flexibility());

      if ( relaxation_time != null ) {
        agx_gear_box.setDamping( (double)relaxation_time );
      }

      ConnectDrivetrainInteraction( gear_box, agx_gear_box );

      agx_gear_box.setName( gear_box.getName() );

      return agx_gear_box;
    }

    public agxDriveTrain.Differential MapDifferential( openplx.DriveTrain.Differential differential )
    {
      agxDriveTrain.Differential agx_differential = new agxDriveTrain.Differential();

      if ( differential is openplx.DriveTrain.TorqueLimitedSlipDifferential lsd ) {
        agx_differential.setLock( true );
        agx_differential.setLimitedSlipTorque( lsd.breakaway_torque() );
      }

      agx_differential.setGearRatio( differential.gear_ratio() );

      openplx.Physics.Charges.Charge charge1 = differential.charges().Count >= 1 ? differential.charges()[0] : null;
      openplx.Physics.Charges.Charge charge2 = differential.charges().Count >= 2 ? differential.charges()[1] : null;
      openplx.Physics.Charges.Charge charge3 = differential.charges().Count >= 3 ? differential.charges()[2] : null;

      var drive_connector = charge1 as openplx.Physics1D.Charges.MateConnector;
      var left_connector  = charge2 as openplx.Physics1D.Charges.MateConnector;
      var right_connector = charge3 as openplx.Physics1D.Charges.MateConnector;

      var drive_shaft = drive_connector?.getOwner() as openplx.DriveTrain.Shaft;
      var left_shaft  = left_connector?.getOwner() as openplx.DriveTrain.Shaft;
      var right_shaft = right_connector?.getOwner() as openplx.DriveTrain.Shaft;
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

    public agxDriveTrain.TorqueConverter MapTorqueConverter( openplx.DriveTrain.EmpiricalTorqueConverter tc )
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

      ConnectDrivetrainInteraction( tc, agx_torque_converter );

      agx_torque_converter.setName( tc.getName() );

      return agx_torque_converter;
    }

    public agxDriveTrain.VelocityConstraint Map1dRotationalVelocityMotor( openplx.Physics1D.Interactions.RotationalVelocityMotor motor )
    {
      agxDriveTrain.VelocityConstraint constraint = null;

      var charge = motor.charges()[0];
      if ( charge != null ) {
        agxPowerLine.Unit unit = MapperData.UnitCache[charge.getOwner()];
        if ( unit is agxPowerLine.RotationalUnit rot_unit ) {
          constraint = new agxDriveTrain.VelocityConstraint( rot_unit );
          constraint.setForceRange( new agx.RangeReal( motor.min_effort(), motor.max_effort() ) );
          constraint.setName( motor.getName() );
          constraint.setTargetVelocity( motor.target_speed() );
        }
      }
      return constraint;
    }

    public agxDriveTrain.CombustionEngine MapCombustionEngine( openplx.DriveTrain.CombustionEngine engine )
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
      openplx.Physics.Charges.Charge charge = engine.charges().Count >= 1 ? engine.charges()[0] : null;

      var stiff_internal_gear = new agxDriveTrain.Gear(1.0);
      stiff_internal_gear.connect( agxPowerLine.Side.INPUT, agxPowerLine.Side.OUTPUT, agxEngine );


      if ( charge is not MateConnector connector ) {
        // Error reporting
        //reportErrorFromKey( torque_converter_key, MissingConnectedBody, system );
      }
      else {
        var shaft = connector.getOwner() as openplx.DriveTrain.Shaft;

        agxPowerLine.Unit shaft_unit = shaft == null ? null : MapperData.UnitCache[ shaft ];

        if ( shaft_unit == null ) {
          // Error reporting
          // reportErrorFromKey( torque_converter_key, MissingConnectedBody, system );
        }
        else {
          stiff_internal_gear.connect( agxPowerLine.Side.OUTPUT, agxPowerLine.Side.INPUT, shaft_unit );
        }
      }
      agxEngine.setName( engine.getName() );

      return agxEngine;

    }

    public void MapActuator( openplx.DriveTrain.Actuator actuator )
    {
      openplx.Physics1D.Charges.MateConnector connector_1d = actuator.connector_1d();
      openplx.Core.Object mate = actuator.mate_3d();

      var agx_constraint = MapperData.Root.FindMappedObject( mate.getName() )?.GetInitializedComponent<Constraint>();
      if ( agx_constraint == null ) {
        Debug.LogError( $"Missing interaction for actuator: {actuator.getName()} (interaction: {mate.getName()})" );
        return;
      }

      var internal_inertia = 1E-4;
      var internal_inertia_annotation = actuator.findAnnotations("agx_actuator_internal_inertia");
      if ( internal_inertia_annotation.Count == 1 && internal_inertia_annotation[ 0 ].isNumber() )
        internal_inertia = internal_inertia_annotation[ 0 ].asReal();

      // Create agx actuator for this (OpenPLX) actuator
      agxPowerLine.Actuator axis = null;
      if ( actuator is openplx.DriveTrain.HingeActuator ha ) {
        var rotational_actuator = new agxPowerLine.RotationalActuator(agx_constraint.Native.asConstraint1DOF());
        rotational_actuator.getInputShaft().setInertia( internal_inertia );
        axis = rotational_actuator;
      }
      else if ( actuator is openplx.DriveTrain.PrismaticActuator pa ) {
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
