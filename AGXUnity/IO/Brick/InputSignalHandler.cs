using openplx.DriveTrain;
using openplx.Physics.Signals;
using openplx.Physics1D.Signals;
using openplx.Physics3D.Signals;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static class InputSignalHandler
  {
    #region Real Handling
    public static void HandleRealInputSignal( RealInputSignal signal, BrickRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case TorsionSpringAngleInput tsai: HandleTorsionSpringAngleInput( signal, root, tsai ); break;
        case LinearVelocityMotorVelocityInput lvmvi: HandleLinearVelocityMotorInput( signal, root, lvmvi ); break;
        case RotationalVelocityMotorVelocityInput rvmvi: HandleRotationalVelocityMotorInput( signal, root, rvmvi ); break;
        case RotationalVelocityMotor1DVelocityInput rvm1dvi: HandleRotationalVelocityMotor1DVelocityInput( signal, root, rvm1dvi ); break;
        case FractionInput fi: HandleFractionInput( signal, root, fi ); break;
        case Torque1DInput t1di: HandleTorque1DInput( signal, root, t1di ); break;
        case Force1DInput f1di: HandleForce1DInput( signal, root, f1di ); break;
        case LinearSpringPositionInput lspi: HandleLinearSpringPositionInput( signal, root, lspi ); break;
        default: Debug.LogWarning( $"Unhandled RealInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleLinearSpringPositionInput( RealInputSignal signal, BrickRoot root, LinearSpringPositionInput lspi )
    {
      var prismatic = root.FindMappedObject( lspi.spring().getName() );
      var spring = prismatic.GetComponent<Constraint>().GetController<LockController>();
      spring.Position = (float)signal.value();
    }

    private static void HandleTorque1DInput( RealInputSignal signal, BrickRoot root, Torque1DInput t1di )
    {
      var source = t1di.source();
      if ( source is openplx.Physics3D.Interactions.TorqueMotor tm ) {
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

    private static void HandleForce1DInput( RealInputSignal signal, BrickRoot root, Force1DInput f1di )
    {
      var source = f1di.source();
      if ( source is openplx.Physics3D.Interactions.ForceMotor fm ) {
        var prismatic = root.FindMappedObject(fm.getName());
        var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
        var torque = Mathf.Clamp((float)signal.value(),(float)fm.min_effort(),(float)fm.max_effort());
        motor.ForceRange = new RangeReal( torque, torque );
      }
      else
        Debug.LogWarning( $"Unhandled input source type '{source.GetType().Name}'" );
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
    #endregion

    #region Int Handling
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
    #endregion

    #region Vec3 Handling
    public static void HandleVec3InputSignal( Vec3InputSignal signal, BrickRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case LinearVelocity3DInput lv3di: HandleLinearVelocity3DInput( signal, root, lv3di ); break;
        case AngularVelocity3DInput av3di: HandleAngularVelocity3DInput( signal, root, av3di ); break;
        default: Debug.LogWarning( $"Unhandled IntInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleLinearVelocity3DInput( Vec3InputSignal signal, BrickRoot root, LinearVelocity3DInput lv3di )
    {
      var source = lv3di.source();
      if ( source is openplx.Physics3D.Bodies.RigidBody rb ) {
        var go = root.FindMappedObject(rb.getName());
        var body = go.GetComponent<RigidBody>();
        body.LinearVelocity = signal.value().ToHandedVector3();
      }
      else
        Debug.LogWarning( $"Unhandled LinearVelocity3DInput source type '{source.GetType().Name}'" );
    }

    private static void HandleAngularVelocity3DInput( Vec3InputSignal signal, BrickRoot root, AngularVelocity3DInput av3di )
    {
      var source = av3di.source();
      if ( source is openplx.Physics3D.Bodies.RigidBody rb ) {
        var go = root.FindMappedObject(rb.getName());
        var body = go.GetComponent<RigidBody>();
        body.AngularVelocity = signal.value().ToHandedVector3();
      }
      else
        Debug.LogWarning( $"Unhandled LinearVelocity3DInput source type '{source.GetType().Name}'" );
    }
    #endregion

    #region Bool Handling
    public static void HandleBoolInputSignal( BoolInputSignal signal, BrickRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case BoolInput bi: HandleBoolInput( signal, root, bi ); break;
        default: Debug.LogWarning( $"Unhandled BoolInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleBoolInput( BoolInputSignal signal, BrickRoot Root, BoolInput boolTarget )
    {
      var source = boolTarget.source();
      if ( source is AutomaticClutch clutch ) {
        if ( Root.FindRuntimeMappedObject( clutch.getName() ) is not agxDriveTrain.DryClutch agxClutch )
          Debug.LogError( $"{clutch.getName()} was not mapped to a powerline unit" );
        else
          agxClutch.setEngage( signal.value() );
      }
      else if ( source is openplx.Robotics.EndEffectors.VacuumGripper vg )
        Debug.LogWarning( "Vacuum Grippers are not yet supported by the AGXUnity OpenPLX-bindings" );
      else if ( source is EmpiricalTorqueConverter torqueConverter ) {
        if ( Root.FindRuntimeMappedObject( torqueConverter.getName() ) is not agxDriveTrain.TorqueConverter agxTC )
          Debug.LogError( $"{torqueConverter.getName()} was not mapped to a powerline unit" );
        else
          agxTC.enableLockUp( signal.value() );
      }
      else
        Debug.LogWarning( $"Unhandled BoolInput source type '{source.GetType().Name}'" );
    }
    #endregion
  }
}
