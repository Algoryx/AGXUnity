using AGXUnity.Utils;
using Brick.DriveTrain;
using Brick.Physics.Signals;
using Brick.Physics1D.Signals;
using Brick.Physics3D.Signals;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static class OutputSignalGenerator
  {
    public static OutputSignal GenerateSignalFrom( Output output, BrickRoot root )
    {
      var signal = output switch
      {
        IntOutput io => GenerateIntOutputSignal(io,root),
        HingeAngleOutput hao => GenerateHingeAngleOutputSignal( root, hao ),
        HingeAngularVelocityOutput havo => GenerateHingeAngularVelocityOutputSignal( root, havo ),
        Position1DOutput p1do => GeneratePosition1DOutputSignal(root, p1do ),
        RigidBodyPositionOutput rbpo => GenerateRigidBodyPositionOutputSignal( root, rbpo ),
        RigidBodyRPYOutput rbrpy => GenerateRigidBodyRPYOutputSignal( root, rbrpy ),
        RotationalBodyAngularVelocityOutput rbavo => GenerateRotationalBodyAngularVelocityOutputSignal( root, rbavo ),
        LinearVelocity1DOutput lv1do => GenerateLinearVelocity1DOutputSignal( root, lv1do ),
        LinearVelocity3DOutput lv3do => GenerateLinearVelocity3DOutputSignal( root, lv3do ),
        _ => null
      };

      if(signal == null)
        Debug.LogWarning( $"Unhandled output of type {output.getType().getName()}" );

      return signal;
    }

    private static ValueOutputSignal GenerateLinearVelocity3DOutputSignal( BrickRoot root, LinearVelocity3DOutput lv3do )
    {
      if ( lv3do.source() is Brick.Physics3D.Bodies.RigidBody sourceRB ) {
        var go = root.FindMappedObject(sourceRB.getName());
        var rb = go.GetComponent<RigidBody>();
        var vel = rb.LinearVelocity.ToLeftHanded();
        return ValueOutputSignal.from_velocity_3d( vel.ToBrickVec3(), lv3do );
      }
      else {
        Debug.LogWarning( $"Unhandled LinearVelocity3DOutput source type '{lv3do.source().GetType().Name}'" );
        return null;
      }

    }

    private static ValueOutputSignal GenerateLinearVelocity1DOutputSignal( BrickRoot root, LinearVelocity1DOutput lv1do )
    {
      if ( lv1do.source() is Brick.Physics3D.Interactions.Prismatic sourcePrismatic ) {
        var prismatic = root.FindMappedObject( sourcePrismatic.getName() );
        var constraint = prismatic.GetComponent<Constraint>();
        return ValueOutputSignal.from_velocity_1d( constraint.GetCurrentSpeed(), lv1do );
      }
      else {
        Debug.LogWarning( $"Unhandled LinearVelocity1DOutput source type '{lv1do.source().GetType().Name}'" );
        return null;
      }

    }

    private static ValueOutputSignal GenerateRotationalBodyAngularVelocityOutputSignal( BrickRoot root, RotationalBodyAngularVelocityOutput rbavo )
    {
      if ( root.FindRuntimeMappedObject( rbavo.body().getName() ) is not agxPowerLine.Unit rotBod || rotBod.asRotationalUnit() == null ) {
        Debug.LogError( $"{rbavo.body().getName()} was not mapped to a powerline unit" );
        return null;
      }
      else
        return ValueOutputSignal.from_angular_velocity_1d( rotBod.asRotationalUnit().getAngularVelocity(), rbavo );
    }

    private static ValueOutputSignal GenerateRigidBodyRPYOutputSignal( BrickRoot root, RigidBodyRPYOutput rbrpy )
    {
      var go = root.FindMappedObject(rbrpy.rigid_body().getName());
      var rb = go.GetComponent<RigidBody>();
      var vel = rb.Native.getRotation().getAsEulerAngles();
      return ValueOutputSignal.from_rpy( vel.ToBrickVec3(), rbrpy );
    }

    private static ValueOutputSignal GenerateRigidBodyPositionOutputSignal( BrickRoot root, RigidBodyPositionOutput rbpo )
    {
      var go = root.FindMappedObject(rbpo.rigid_body().getName());
      var rb = go.GetComponent<RigidBody>();
      var pos = rb.Native.getPosition();
      return ValueOutputSignal.from_position_3d( pos.ToBrickVec3(), rbpo );
    }

    private static ValueOutputSignal GeneratePosition1DOutputSignal( BrickRoot root, Position1DOutput p1do )
    {
      if ( p1do.source() is Brick.Physics3D.Interactions.Prismatic sourcePrismatic ) {
        var prismatic = root.FindMappedObject( sourcePrismatic.getName() );
        var constraint = prismatic.GetComponent<Constraint>();
        return ValueOutputSignal.from_distance( constraint.GetCurrentAngle(), p1do );
      }
      else {
        Debug.LogWarning( $"Unhandled Position1DOutput source type '{p1do.source().GetType().Name}'" );
        return null;
      }
    }

    private static ValueOutputSignal GenerateHingeAngularVelocityOutputSignal( BrickRoot root, HingeAngularVelocityOutput havo )
    {
      var hinge = root.FindMappedObject( havo.hinge().getName() );
      var constraint = hinge.GetComponent<Constraint>();
      return ValueOutputSignal.from_angular_velocity_1d( constraint.GetCurrentSpeed(), havo );
    }

    private static ValueOutputSignal GenerateHingeAngleOutputSignal( BrickRoot root, HingeAngleOutput hao )
    {
      var hinge = root.FindMappedObject( hao.hinge().getName() );
      var constraint = hinge.GetComponent<Constraint>();
      return ValueOutputSignal.from_angle( constraint.GetCurrentAngle(), hao );
    }

    private static ValueOutputSignal GenerateIntOutputSignal( IntOutput io, BrickRoot root )
    {
      switch ( io.source() ) {
        case GearBox gearbox:
          if ( root.FindRuntimeMappedObject( gearbox.getName() ) is not agxDriveTrain.GearBox agxGearbox ) {
            Debug.LogError( $"{gearbox.getName()} was not mapped to a powerline unit" );
            return null;
          }
          else {
            var num_reverse = gearbox.reverse_gears().Count;
            return ValueOutputSignal.from_int( agxGearbox.getGear() - num_reverse, io );
          }
        default: Debug.LogWarning( $"Unhandled IntOutput source type '{io.source().GetType().Name}'" ); return null;
      }
    }
  }
}