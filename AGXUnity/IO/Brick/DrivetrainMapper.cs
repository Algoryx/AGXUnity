using AGXUnity.Utils;
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
      agx_gear.setViscousDamping( gear.damping() / gear.stiffness() );
      agx_gear.setViscousCompliance( 1.0 / gear.stiffness() );

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

    public agxDriveTrain.Differential MapDifferential(Brick.DriveTrain.Differential differential )
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

    public void MapActuator( Brick.DriveTrain.Actuator actuator )
    {
      Brick.Physics1D.Charges.MateConnector connector_1d = actuator.connector_1d();
      Brick.Core.Object mate = actuator.mate_3d();

      var agx_constraint = MapperData.Root.FindMappedObject( mate.getName() ).GetInitializedComponent<Constraint>();
      if ( agx_constraint == null ) {
        Debug.LogError( $"Missing interaction for actuator: {actuator.getName()}" );
        return;
      }

      var internal_inertia = 1E-4;
      var internal_inertia_annotation = actuator.getType().findAnnotations("agx_actuator_internal_inertia");
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
