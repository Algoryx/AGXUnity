using openplx.DriveTrain;
using openplx.Physics.Signals;
using openplx.Physics1D.Interactions;
using openplx.Physics3D.Signals;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public static class InputSignalHandler
  {
    #region Real Handling
    public static void HandleRealInputSignal( RealInputSignal signal, OpenPLXRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case AngleInput ai: HandleAngleInput( signal, root, ai ); break;
        case AngularVelocity1DInput av1di: HandleAngularVelocity1DInput( signal, root, av1di ); break;
        case LinearVelocity1DInput lv1di: HandleLinearVelocity1DInput( signal, root, lv1di ); break;
        case Force1DInput f1di: HandleForce1DInput( signal, root, f1di ); break;
        case Torque1DInput t1di: HandleTorque1DInput( signal, root, t1di ); break;
        case FractionInput fi: HandleFractionInput( signal, root, fi ); break;
        case Position1DInput p1di: HandlePosition1DInput( signal, root, p1di ); break;
        default: Debug.LogWarning( $"Unhandled RealInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleAngleInput( RealInputSignal signal, OpenPLXRoot root, AngleInput ai )
    {
      var source = ai.source();
      if ( source is openplx.Physics3D.Interactions.TorsionSpring ts ) {
        var hinge = root.FindMappedObject( ts.getName() );
        var spring = hinge.GetComponent<Constraint>().GetController<LockController>();
        spring.Position = (float)signal.value();
      }
      else
        Debug.LogWarning( $"Unhandled input source type '{source.GetType().Name}'" );
    }

    private static void HandleAngularVelocity1DInput( RealInputSignal signal, OpenPLXRoot root, AngularVelocity1DInput av1di )
    {
      var source = av1di.source();
      if ( source is RotationalVelocityMotor rvm1d ) {
        var motor = root.FindRuntimeMappedObject( rvm1d.getName() );
        if ( motor is agxDriveTrain.VelocityConstraint vc )
          vc.setTargetVelocity( (float)signal.value() );
        else
          Debug.LogError( $"Could not find runtime mapped VelocityConstraint for signal target '{rvm1d.getName()}'" );
      }
      else if ( source is openplx.Physics3D.Interactions.RotationalVelocityMotor rvm3d ) {
        var hinge = root.FindMappedObject( rvm3d.getName() );
        var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
        motor.Speed = (float)signal.value();
      }
      else
        Debug.LogWarning( $"Unhandled input source type '{source.GetType().Name}'" );
    }

    private static void HandleLinearVelocity1DInput( RealInputSignal signal, OpenPLXRoot root, LinearVelocity1DInput lv1di )
    {
      var source = lv1di.source();
      if ( source is openplx.Physics3D.Interactions.LinearVelocityMotor lvm ) {
        var prismatic = root.FindMappedObject( lvm.getName() );
        var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
        motor.Speed = (float)signal.value();
      }
      else
        Debug.LogWarning( $"Unhandled input source type '{source.GetType().Name}'" );
    }

    private static void HandleForce1DInput( RealInputSignal signal, OpenPLXRoot root, Force1DInput f1di )
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

    private static void HandleTorque1DInput( RealInputSignal signal, OpenPLXRoot root, Torque1DInput t1di )
    {
      var source = t1di.source();
      if ( source is openplx.Physics3D.Interactions.TorqueMotor tm ) {
        var hinge = root.FindMappedObject(tm.getName());
        var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
        var torque = Mathf.Clamp((float)signal.value(),(float)tm.min_effort(),(float)tm.max_effort());
        motor.ForceRange = new RangeReal( torque, torque );
      }
      else if ( source is TorqueMotor tm_dt ) {
        var torque = Mathf.Clamp((float)signal.value(),(float)tm_dt.min_effort(),(float)tm_dt.max_effort());
        tm_dt.setDynamic( "___the__last__motor__torque___", new openplx.Core.Any( torque ) );
        foreach ( var charge in tm_dt.charges() ) {
          var unit = (agxPowerLine.Unit)root.FindRuntimeMappedObject(charge.getOwner().getName());
          var rot_unit = unit.asRotationalUnit();
          if ( rot_unit != null )
            rot_unit.getRotationalDimension().addLoad( torque );
        }
      }
    }

    private static void HandleFractionInput( RealInputSignal signal, OpenPLXRoot root, FractionInput fi )
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

    private static void HandlePosition1DInput( RealInputSignal signal, OpenPLXRoot root, Position1DInput p1di )
    {
      var source = p1di.source();
      if ( source is openplx.Physics3D.Interactions.LinearSpring ls ) {
        var prismatic = root.FindMappedObject( ls.getName() );
        var spring = prismatic.GetComponent<Constraint>().GetController<LockController>();
        spring.Position = (float)signal.value();
      }
      else
        Debug.LogWarning( $"Unhandled input source type '{source.GetType().Name}'" );
    }

    #endregion

    #region Int Handling
    public static void HandleIntInputSignal( IntInputSignal signal, OpenPLXRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case IntInput ii: HandleIntInput( signal, root, ii ); break;
        default: Debug.LogWarning( $"Unhandled IntInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleIntInput( IntInputSignal signal, OpenPLXRoot root, IntInput intTarget )
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
    public static void HandleVec3InputSignal( Vec3InputSignal signal, OpenPLXRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case LinearVelocity3DInput lv3di: HandleLinearVelocity3DInput( signal, root, lv3di ); break;
        case AngularVelocity3DInput av3di: HandleAngularVelocity3DInput( signal, root, av3di ); break;
        default: Debug.LogWarning( $"Unhandled IntInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleLinearVelocity3DInput( Vec3InputSignal signal, OpenPLXRoot root, LinearVelocity3DInput lv3di )
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

    private static void HandleAngularVelocity3DInput( Vec3InputSignal signal, OpenPLXRoot root, AngularVelocity3DInput av3di )
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
    public static void HandleBoolInputSignal( BoolInputSignal signal, OpenPLXRoot root )
    {
      var target = signal.target();
      switch ( target ) {
        case BoolInput bi: HandleBoolInput( signal, root, bi ); break;
        default: Debug.LogWarning( $"Unhandled BoolInputSignal target type '{target.GetType().Name}'" ); break;
      }
    }

    private static void HandleBoolInput( BoolInputSignal signal, OpenPLXRoot Root, BoolInput boolTarget )
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
