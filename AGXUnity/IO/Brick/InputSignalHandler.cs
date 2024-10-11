using Brick.DriveTrain;
using Brick.Physics.Signals;
using Brick.Physics1D.Signals;
using Brick.Physics3D.Signals;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static class InputSignalHandler
  {
    public static void HandleRealInputSignal( RealInputSignal signal, BrickRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case TorsionSpringAngleInput tsai: HandleTorsionSpringAngleInput( signal, root, tsai ); break;
        case LinearVelocityMotorVelocityInput lvmvi: HandleLinearVelocityMotorInput( signal, root, lvmvi ); break;
        case RotationalVelocityMotorVelocityInput rvmvi: HandleRotationalVelocityMotorInput( signal, root, rvmvi ); break;
        case RotationalVelocityMotor1DVelocityInput rvm1dvi: HandleRotationalVelocityMotor1DVelocityInput( signal, root, rvm1dvi ); break;
        case ForceMotorForceInput fmfi: HandleForceMotorInput( signal, root, fmfi ); break;
        case FractionInput fi: HandleFractionInput( signal, root, fi ); break;
        case Torque1DInput t1di: HandleTorque1DInput( signal, root, t1di ); break;
        default: Debug.LogWarning( $"Unhandled RealInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleTorque1DInput( RealInputSignal signal, BrickRoot root, Torque1DInput t1di )
    {
      var source = t1di.source();
      if ( source is Brick.Physics3D.Interactions.TorqueMotor tm ) {
        var hinge = root.FindMappedObject(tm.getName());
        var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
        var torque = Mathf.Clamp((float)signal.value(),(float)tm.min_effort(),(float)tm.max_effort());
        motor.ForceRange = new RangeReal( torque, torque );
      }
      else if ( source is TorqueMotor tm_dt ) {
        foreach ( var charge in tm_dt.charges() ) {
          var unit = (agxPowerLine.Unit)root.FindRuntimeMappedObject(charge.getOwner().getName());
          var rot_unit = unit.asRotationalUnit();
          if ( rot_unit != null ) {
            var torque = Mathf.Clamp((float)signal.value(),(float)tm_dt.min_effort(),(float)tm_dt.max_effort());
            rot_unit.getRotationalDimension().addLoad( torque );
          }
        }
      }
    }

    private static void HandleFractionInput( RealInputSignal signal, BrickRoot root, FractionInput fi )
    {
      var source = fi.source();
      if ( source is CombustionEngine ce ) {
        var engine = root.FindRuntimeMappedObject( ce.getName() );
        if ( engine is agxDriveTrain.CombustionEngine mappedCe ) {
          mappedCe.setThrottle( signal.value() );
        }
        else
          Debug.LogError( $"Could not find runtime mapped CombustionEngine for signal target '{ce.getName()}'" );
      }
    }

    private static void HandleForceMotorInput( RealInputSignal signal, BrickRoot root, ForceMotorForceInput fmfi )
    {
      var prismatic = root.FindMappedObject(fmfi.motor().getName());
      var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
      var torque = Mathf.Clamp((float)signal.value(),(float)fmfi.motor().min_effort(),(float)fmfi.motor().max_effort());
      motor.ForceRange = new RangeReal( torque, torque );
    }

    private static void HandleRotationalVelocityMotor1DVelocityInput( RealInputSignal signal, BrickRoot root, RotationalVelocityMotor1DVelocityInput rvm1dvi )
    {
      var motor = root.FindRuntimeMappedObject( rvm1dvi.motor().getName() );
      if ( motor is agxDriveTrain.VelocityConstraint vc )
        vc.setTargetVelocity( (float)signal.value() );
      else
        Debug.LogError( $"Could not find runtime mapped VelocityConstraint for signal target '{rvm1dvi.motor().getName()}'" );
    }

    private static void HandleRotationalVelocityMotorInput( RealInputSignal signal, BrickRoot root, RotationalVelocityMotorVelocityInput rvmvi )
    {
      var hinge = root.FindMappedObject( rvmvi.motor().getName() );
      var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
      motor.Speed = (float)signal.value();
    }

    private static void HandleLinearVelocityMotorInput( RealInputSignal signal, BrickRoot root, LinearVelocityMotorVelocityInput lvmvi )
    {
      var prismatic = root.FindMappedObject( lvmvi.motor().getName() );
      var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
      motor.Speed = (float)signal.value();
    }

    private static void HandleTorsionSpringAngleInput( RealInputSignal signal, BrickRoot root, TorsionSpringAngleInput tsai )
    {
      var hinge = root.FindMappedObject( tsai.spring().getName() );
      var spring = hinge.GetComponent<Constraint>().GetController<LockController>();
      spring.Position = (float)signal.value();
    }

    public static void HandleIntInputSignal( IntInputSignal signal, BrickRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case IntInput ii: HandleIntInput( signal, root, ii ); break;
        default: Debug.LogWarning( $"Unhandled IntInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleIntInput( IntInputSignal signal, BrickRoot root, IntInput intTarget )
    {
      var source = intTarget.source();
      if ( source is GearBox gearbox ) {
        if ( root.FindRuntimeMappedObject( gearbox.getName() ) is not agxDriveTrain.GearBox agxGearbox )
          Debug.LogError( $"{gearbox.getName()} was not mapped to a powerline unit" );
        else {
          var numReverse = gearbox.reverse_gears().Count;
          var numForward = gearbox.forward_gears().Count;
          int gear = (int)signal.value();
          if ( gear < 0 && Mathf.Abs( gear ) > numReverse )
            gear = -numReverse;
          if ( gear > 0 && Mathf.Abs( gear ) > numForward )
            gear = numForward;

          var adjustedGear = gear + numReverse;
          if ( adjustedGear >= agxGearbox.getNumGears() || adjustedGear < 0 )
            Debug.LogError( $"Signal had gear {adjustedGear} which is out of range 0 - {agxGearbox.getNumGears()} for agxDriveTrain.GearBox" );
          agxGearbox.setGear( adjustedGear );
        }
      }
    }
  }
}