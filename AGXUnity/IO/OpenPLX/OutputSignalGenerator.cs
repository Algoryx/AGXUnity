using AGXUnity.Utils;
using openplx.DriveTrain;
using openplx.Physics.Signals;
using openplx.Physics1D.Bodies;
using openplx.Physics3D.Interactions;
using openplx.Physics3D.Signals;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public static class OutputSignalGenerator
  {
    public static OutputSignal GenerateSignalFrom( Output output, OpenPLXRoot root )
    {
      var signal = output switch
      {
        BoolOutput bo => GenerateBoolOutputSignal( bo, root ),
        IntOutput io => GenerateIntOutputSignal( io, root ),
        Force1DOutput f1do => GenerateForce1DOutputSignal( f1do, root ),
        AngleOutput ao => GenerateAngleOutputSignal( ao, root ),
        Torque1DOutput t1do => GenerateTorque1DOutputSignal( t1do, root ),
        Position1DOutput p1do => GeneratePosition1DOutputSignal(root, p1do ),
        AngularVelocity1DOutput av1do => GenerateAngularVelocity1DOutputSignal( root, av1do ),
        LinearVelocity1DOutput lv1do => GenerateLinearVelocity1DOutputSignal( root, lv1do ),
        FractionOutput fo => GenerateFractionOutputSignal( root, fo ),
        RelativeVelocity1DOutput rv1do => GenerateRelativeVelocity1DOutputSignal( root, rv1do ),
        Position3DOutput p3do => GeneratePosition3DOutputSignal(root, p3do ),
        RPYOutput rpyo => GenerateRPYOutputSignal( root, rpyo ),
        LinearVelocity3DOutput lv3do => GenerateLinearVelocity3DOutputSignal( root, lv3do ),
        AngularVelocity3DOutput av3do => GenerateAngularVelocity3DOutputSignal( root, av3do ),
        _ => null
      } ;

      if ( signal == null )
        Debug.LogWarning( $"Unhandled output of type {output.getType().getName()}" );

      return signal;
    }

    private static ValueOutputSignal GenerateBoolOutputSignal( BoolOutput bo, OpenPLXRoot root )
    {
        default: Debug.LogWarning( $"Unhandled BoolOutput source type '{bo.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateIntOutputSignal( IntOutput io, OpenPLXRoot root )
    {
      switch ( io.source() ) {
        case GearBox gearbox: {
            if ( root.FindRuntimeMappedObject( gearbox.getName() ) is not agxDriveTrain.GearBox agxGearbox ) {
              Debug.LogError( $"{gearbox.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else {
              var num_reverse = gearbox.reverse_gears().Count;
              return ValueOutputSignal.from_int( agxGearbox.getGear() - num_reverse, io );
            }
          }
        default: Debug.LogWarning( $"Unhandled IntOutput source type '{io.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateForce1DOutputSignal( Force1DOutput f1do, OpenPLXRoot root )
    {
      switch ( f1do.source() ) {
        case ForceMotor fm: {
            var prismatic = root.FindMappedObject(fm.getName());
            var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
            return ValueOutputSignal.from_force_1d( motor.Native.getCurrentForce(), f1do );
          }
        default: Debug.LogWarning( $"Unhandled Force1DOutput source type '{f1do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateAngleOutputSignal( AngleOutput ao, OpenPLXRoot root )
    {
      switch ( ao.source() ) {
        case Hinge hinge: {
            var agxHinge = root.FindMappedObject( hinge.getName() );
            var constraint = agxHinge.GetComponent<Constraint>();
            return ValueOutputSignal.from_angle( constraint.GetCurrentAngle(), ao );
          }
        case RotationalBody rotBod: {
            if ( root.FindRuntimeMappedObject( rotBod.getName() ) is not agxPowerLine.Unit agxRotBod || agxRotBod.asRotationalUnit() == null ) {
              Debug.LogError( $"{rotBod.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_angle( agxRotBod.asRotationalUnit().getAngle(), ao );
          }
        case TorsionSpring ts: {
            var hinge = root.FindMappedObject( ts.getName() );
            var spring = hinge.GetComponent<Constraint>().GetController<LockController>();
            return ValueOutputSignal.from_angle( spring.Position, ao );
          }
        default: Debug.LogWarning( $"Unhandled AngleOutput source type '{ao.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateTorque1DOutputSignal( Torque1DOutput t1do, OpenPLXRoot root )
    {
      switch ( t1do.source() ) {
        case openplx.Physics3D.Interactions.TorqueMotor:
        case openplx.Physics3D.Interactions.RotationalVelocityMotor: {
            var hinge = root.FindMappedObject((t1do.source() as openplx.Core.Object).getName());
            var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
            return ValueOutputSignal.from_torque_1d( motor.Native.getCurrentForce(), t1do );
          }
        case openplx.Physics3D.Interactions.TorsionSpring ts: {
            var hinge = root.FindMappedObject(ts.getName());
            var spring = hinge.GetComponent<Constraint>().GetController<LockController>();
            return ValueOutputSignal.from_torque_1d( spring.Native.getCurrentForce(), t1do );
          }
        case openplx.Physics3D.Interactions.RotationalRange rr: {
            var hinge = root.FindMappedObject(rr.getName());
            var range = hinge.GetComponent<Constraint>().GetController<RangeController>();
            return ValueOutputSignal.from_torque_1d( range.Native.getCurrentForce(), t1do );
          }
        case openplx.Physics1D.Interactions.RotationalVelocityMotor rvm1d: {
            if ( root.FindRuntimeMappedObject( rvm1d.getName() ) is not agxDriveTrain.VelocityConstraint agxVc ) {
              Debug.LogError( $"{rvm1d.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_torque_1d( agxVc.getElementaryConstraint( 0 ).getCurrentForce(), t1do );
          }
        case Gear:
        case ManualClutch:
        case GearBox: {
            if ( root.FindRuntimeMappedObject( ( t1do.source() as openplx.Core.Object ).getName() ) is not agxPowerLine.Connector agxConnector ) {
              Debug.LogError( $"{( t1do.source() as openplx.Core.Object ).getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_torque_1d( agxConnector.getElementaryConstraint().getCurrentForce(), t1do );
          }
        case CombustionEngine ce: {
            if ( root.FindRuntimeMappedObject( ce.getName() ) is not agxDriveTrain.CombustionEngine agxCe ) {
              Debug.LogError( $"{ce.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_torque_1d( agxCe.getOutputTorque(), t1do );
          }
        case openplx.DriveTrain.TorqueMotor dttm: {
            var dynValue = dttm.getDynamic("___the__last__motor__torque___");
            return ValueOutputSignal.from_torque_1d( dynValue.isReal() ? dynValue.asReal() : 0.0, t1do );
          }
        default: Debug.LogWarning( $"Unhandled Torque1DOutput source type '{t1do.source().GetType().FullName}'" ); return null;
      }
    }


    private static ValueOutputSignal GeneratePosition1DOutputSignal( OpenPLXRoot root, Position1DOutput p1do )
    {
      switch ( p1do.source() ) {
        case openplx.Physics3D.Interactions.Prismatic prismatic: {
            var agxPrismatic = root.FindMappedObject( prismatic.getName() );
            var constraint = agxPrismatic.GetComponent<Constraint>();
            return ValueOutputSignal.from_distance( constraint.GetCurrentAngle(), p1do );
          }
        case LinearSpring ls: {
            var agxPrismatic = root.FindMappedObject( ls.getName() );
            var spring = agxPrismatic.GetComponent<Constraint>().GetController<LockController>();
            return ValueOutputSignal.from_distance( spring.Position, p1do );
          }
        default: Debug.LogWarning( $"Unhandled Position1DOutput source type '{p1do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateAngularVelocity1DOutputSignal( OpenPLXRoot root, AngularVelocity1DOutput av1do )
    {
      switch ( av1do.source() ) {
        case openplx.Physics3D.Interactions.Hinge hinge: {
            var agxHinge = root.FindMappedObject( hinge.getName() );
            var constraint = agxHinge.GetComponent<Constraint>();
            return ValueOutputSignal.from_angular_velocity_1d( constraint.GetCurrentSpeed(), av1do );
          }
        case RotationalBody rotBod: {
            if ( root.FindRuntimeMappedObject( rotBod.getName() ) is not agxPowerLine.Unit agxRotBod || agxRotBod.asRotationalUnit() == null ) {
              Debug.LogError( $"{rotBod.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_angular_velocity_1d( agxRotBod.asRotationalUnit().getAngularVelocity(), av1do );
          }
        case openplx.Physics1D.Interactions.RotationalVelocityMotor rvm1d: {
            if ( root.FindRuntimeMappedObject( rvm1d.getName() ) is not agxDriveTrain.VelocityConstraint agxVc ) {
              Debug.LogError( $"{rvm1d.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_angular_velocity_1d( agxVc.getTargetVelocity(), av1do );
          }
        default: Debug.LogWarning( $"Unhandled AngularVelocity1DOutput source type '{av1do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateLinearVelocity1DOutputSignal( OpenPLXRoot root, LinearVelocity1DOutput lv1do )
    {
      switch ( lv1do.source() ) {
        case openplx.Physics3D.Interactions.Prismatic prismatic: {
            var agxPrismatic = root.FindMappedObject( prismatic.getName() );
            var constraint = agxPrismatic.GetComponent<Constraint>();
            return ValueOutputSignal.from_velocity_1d( constraint.GetCurrentSpeed(), lv1do );
          }
        default: Debug.LogWarning( $"Unhandled LinearVelocity1DOutput source type '{lv1do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateFractionOutputSignal( OpenPLXRoot root, FractionOutput fo )
    {
      switch ( fo.source() ) {
        case ManualClutch clutch: {
            if ( root.FindRuntimeMappedObject( clutch.getName() ) is not agxDriveTrain.DryClutch agxClutch ) {
              Debug.LogError( $"{clutch.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_fraction( agxClutch.getFraction(), fo );
          }
        case Gear gear: {
            if ( root.FindRuntimeMappedObject( gear.getName() ) is not agxDriveTrain.Gear agxGear ) {
              Debug.LogError( $"{gear.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_fraction( agxGear.getGearRatio(), fo );
          }
        default: Debug.LogWarning( $"Unhandled FractionOutput source type '{fo.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateRelativeVelocity1DOutputSignal( OpenPLXRoot root, RelativeVelocity1DOutput rv1do )
    {
      switch ( rv1do.source() ) {
        case EmpiricalTorqueConverter etc: {
            if ( root.FindRuntimeMappedObject( etc.getName() ) is not agxDriveTrain.TorqueConverter agxTc ) {
              Debug.LogError( $"{etc.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else {
              var inputs = new agxPowerLine.UnitPtrSetVector();
              var outputs = new agxPowerLine.UnitPtrSetVector();
              bool foundInputs = agxTc.getInputUnits(inputs);
              bool foundOutputs = agxTc.getOutputUnits(outputs);
              if ( !foundInputs || !foundOutputs ) {
                Debug.LogError( $"Failed to find units for TorqueConverter mapped from '{etc.getName()}'" );
                return null;
              }
              var rotInput = inputs[0].asRotationalUnit();
              var rotOutput = outputs[0].asRotationalUnit();
              if ( rotInput == null || rotOutput == null ) {
                Debug.LogError( $"Invalid units in TorqueConverter mapped from '{etc.getName()}'" );
                return null;
              }
              var slipVelocity = rotInput.getAngularVelocity() == 0 ? 0 : rotOutput.getAngularVelocity() / rotInput.getAngularVelocity();
              return ValueOutputSignal.from_angular_velocity_1d( slipVelocity, rv1do );
            }
          }
        case ManualClutch clutch: {
            if ( root.FindRuntimeMappedObject( clutch.getName() ) is not agxDriveTrain.DryClutch agxClutch ) {
              Debug.LogError( $"{clutch.getName()} was not mapped to a powerline unit" );
              return null;
            }
            else
              return ValueOutputSignal.from_angular_velocity_1d( agxClutch.getSlip(), rv1do );
          }
        default: Debug.LogWarning( $"Unhandled RelativeVelocity1DOutput source type '{rv1do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GeneratePosition3DOutputSignal( OpenPLXRoot root, Position3DOutput p3do )
    {
      switch ( p3do.source() ) {
        case openplx.Physics3D.Bodies.RigidBody rb: {
            var agxRBObj = root.FindMappedObject( rb.getName() );
            var agxRB= agxRBObj.GetComponent<RigidBody>();
            return ValueOutputSignal.from_position_3d( agxRB.Native.getPosition().ToOpenPLXVec3(), p3do );
          }
        default: Debug.LogWarning( $"Unhandled Position3DOutput source type '{p3do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateRPYOutputSignal( OpenPLXRoot root, RPYOutput rpyo )
    {
      switch ( rpyo.source() ) {
        case openplx.Physics3D.Bodies.RigidBody rb: {
            var go = root.FindMappedObject(rb.getName());
            var agxrb = go.GetComponent<RigidBody>();
            var vel = agxrb.Native.getRotation().getAsEulerAngles();
            return ValueOutputSignal.from_rpy( vel.ToOpenPLXVec3(), rpyo );
          }
        default: Debug.LogWarning( $"Unhandled RPYOutput source type '{rpyo.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateLinearVelocity3DOutputSignal( OpenPLXRoot root, LinearVelocity3DOutput lv3do )
    {
      switch ( lv3do.source() ) {
        case openplx.Physics3D.Bodies.RigidBody rb: {
            var go = root.FindMappedObject(rb.getName());
            var agxRb = go.GetComponent<RigidBody>();
            var vel = agxRb.LinearVelocity.ToLeftHanded();
            return ValueOutputSignal.from_velocity_3d( vel.ToOpenPLXVec3(), lv3do );
          }
        default: Debug.LogWarning( $"Unhandled LinearVelocity3DOutput source type '{lv3do.source().GetType().FullName}'" ); return null;
      }
    }

    private static ValueOutputSignal GenerateAngularVelocity3DOutputSignal( OpenPLXRoot root, AngularVelocity3DOutput av3do )
    {
      switch ( av3do.source() ) {
        case openplx.Physics3D.Bodies.RigidBody rb: {
            var go = root.FindMappedObject(rb.getName());
            var agxRb = go.GetComponent<RigidBody>();
            var vel = agxRb.AngularVelocity.ToLeftHanded();
            return ValueOutputSignal.from_velocity_3d( vel.ToOpenPLXVec3(), av3do );
          }
        default: Debug.LogWarning( $"Unhandled AngularVelocity3DOutput source type '{av3do.source().GetType().FullName}'" ); return null;
      }
    }
  }
}
