using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

using Charges = Brick.Physics3D.Charges;
using Interactions = Brick.Physics3D.Interactions;

namespace AGXUnity.IO.BrickIO
{
  public class InteractionMapper
  {
    private MapperData Data;

    public InteractionMapper( MapperData cache )
    {
      Data = cache;
    }

    IFrame mapMateConnector( Charges.MateConnector mate_connector )
    {
      var frame = new IFrame();

      Brick.Core.Object owner = mate_connector.getOwner();
      if ( mate_connector is Charges.RedirectedMateConnector redirected )
        owner = redirected.redirected_parent();

      if ( Data.FrameCache.ContainsKey( owner ) )
        frame.SetParent( Data.FrameCache[ owner ] );

      frame.LocalPosition = mate_connector.position().ToVec3().ToHandedVector3();

      var normal_n = mate_connector.normal();
      var main_axis_n = mate_connector.main_axis().normal();
      // Orthonormalize
      normal_n = ( normal_n - main_axis_n * ( normal_n * main_axis_n ) ).normal();

      // TODO: Error reporting

      var rotation = Brick.Math.Quat.fromTo(Brick.Math.Vec3.Z_AXIS(), main_axis_n);
      var new_x = rotation.rotate(Brick.Math.Vec3.X_AXIS());
      var angle = Brick.Physics3D.Snap.Utils.angleBetweenVectors(new_x,normal_n,main_axis_n);
      var rotation_2 = Brick.Math.Quat.angleAxis(angle,main_axis_n);
      var q1 = rotation.ToHandedQuaternion().ToHandedQuat();
      var q2 = rotation_2.ToHandedQuaternion().ToHandedQuat();

      frame.LocalRotation = ( q1 * q2 ).ToHandedQuaternion();

      var collapsedFrame = new IFrame();
      collapsedFrame.LocalPosition = frame.Position;
      collapsedFrame.LocalRotation = frame.Rotation;
      return frame;
    }

    HingeClass mapInteraction<HingeClass>( Brick.Physics.Interactions.Interaction interaction,
                                            Brick.Physics3D.System system,
                                            Func<IFrame, IFrame, HingeClass> interactionCreator )
    {
      Brick.Physics.Charges.Charge charge1 = interaction.charges().Count >= 1 ? interaction.charges()[0] : null;
      Brick.Physics.Charges.Charge charge2 = interaction.charges().Count >= 2 ? interaction.charges()[1] : null;

      var mate_connector1 = charge1 == null ? null : charge1 as Charges.MateConnector;
      var mate_connector2 = charge2 == null ? null : charge2 as Charges.MateConnector;

      var frame1 = mate_connector1 == null ? null : mapMateConnector(mate_connector1);
      var frame2 = mate_connector2 == null ? null : mapMateConnector(mate_connector2);

      if ( mate_connector1 is Charges.RedirectedMateConnector redirected_connector1 ) {
        RigidBody rb1 = redirected_connector1.redirected_parent() == null ? null : Data.BodyCache[ redirected_connector1.redirected_parent() ];
        frame1.SetParent( rb1?.gameObject, false );
      }
      if ( mate_connector2 is Charges.RedirectedMateConnector redirected_connector2 ) {
        RigidBody rb2 = redirected_connector2.redirected_parent() == null ? null : Data.BodyCache[ redirected_connector2.redirected_parent() ];
        frame2.SetParent( rb2?.gameObject, false );
      }

      if ( frame1.Parent == null ) {
        var o1 = mate_connector1.getOwner();
        RigidBody rb1 = mate_connector1 == null ? null : Data.BodyCache.GetValueOrDefault(o1);
        frame1.SetParent( rb1?.gameObject, false );
      }
      if ( frame2.Parent == null ) {
        var o2 = mate_connector2.getOwner();
        RigidBody rb2 = mate_connector2 == null ? null : Data.BodyCache.GetValueOrDefault(o2);
        frame2.SetParent( rb2?.gameObject, false );
      }

      // TODO: Error reporting


      if ( frame1.Parent && frame2.Parent )
        return interactionCreator( frame1, frame2 );
      else if ( frame1.Parent )
        return interactionCreator( frame1, frame2 );
      else
        return interactionCreator( frame2, frame1 );
    }

    Constraint createConstraint( IFrame f1, IFrame f2, ConstraintType type )
    {
      var at1 = ConstraintFrame.CreateLocal(f1.Parent ?? Data.RootNode,f1.LocalPosition,f1.LocalRotation);
      var at2 = ConstraintFrame.CreateLocal(f2.Parent ?? Data.RootNode,f2.LocalPosition,f2.LocalRotation);
      var c = Constraint.Create(type);
      c.AttachmentPair.Synchronized = false;
      c.AttachmentPair.ReferenceFrame = at1;
      c.AttachmentPair.ConnectedFrame = at2;
      return c;
    }

    float? mapDeformation( Brick.Physics.Interactions.Deformation.DefaultDeformation deformation )
    {
      if ( deformation is Brick.Physics.Interactions.Deformation.RigidDeformation )
        return float.Epsilon;
      else if ( deformation is Brick.Physics.Interactions.Deformation.ElasticDeformation elastic )
        return (float)( 1.0 / elastic.stiffness() );
      return null;
    }

    float? mapDamping( Brick.Physics.Interactions.Damping.DefaultDamping damping, Brick.Physics.Interactions.Deformation.DefaultDeformation deformation )
    {
      if ( damping is Brick.Physics.Interactions.Damping.ConstraintRelaxationTimeDamping crtd )
        return (float)crtd.time();

      else if ( damping is Brick.Physics.Interactions.Damping.MechanicalDamping mechanical ) {
        if ( deformation is Brick.Physics.Interactions.Deformation.ElasticDeformation elastic && elastic.stiffness() != 0.0 )
          return (float)( mechanical.damping() / elastic.stiffness() );
        return null;
      }

      var agx_relaxation_time_annotations = damping.getType().findAnnotations("agx_relaxation_time");
      if ( agx_relaxation_time_annotations.Count == 1 && agx_relaxation_time_annotations[ 0 ].isNumber() )
        return (float)agx_relaxation_time_annotations[ 0 ].asReal();

      return null;
    }

    Constraint.RotationalDof mapRotationalDOF( string axisName )
    {
      return axisName switch
      {
        "main" => Constraint.RotationalDof.Z,
        "normal" => Constraint.RotationalDof.X,
        "cross" => Constraint.RotationalDof.Y,
        _ => throw new ArgumentException( $"'{axisName}' is not a valid axis" )
      };
    }

    Constraint.TranslationalDof mapTranslationalDOF( string axisName )
    {
      return axisName switch
      {
        "main" => Constraint.TranslationalDof.Z,
        "normal" => Constraint.TranslationalDof.X,
        "cross" => Constraint.TranslationalDof.Y,
        _ => throw new ArgumentException( $"'{axisName}' is not a valid axis" )
      };
    }

    void mapMateDamping( Interactions.Damping.DefaultMateDamping damping, Interactions.Deformation.DefaultMateDeformation deformation, Constraint target )
    {
      foreach ( var (key, damp) in damping.getEntries<Brick.Physics.Interactions.Damping.DefaultDamping>() ) {
        var def = deformation.getDynamic( key ).asObject() as Brick.Physics.Interactions.Deformation.DefaultDeformation;
        float? mapped = mapDamping(damp, def);
        if ( mapped == null )
          continue;
        if ( key.StartsWith( "along_" ) )
          target.SetDamping( mapped.Value, mapRotationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
        else if ( key.StartsWith( "around_" ) )
          target.SetDamping( mapped.Value, mapTranslationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
      }
    }

    void mapMateDeformation( Interactions.Deformation.DefaultMateDeformation deformation, Constraint target )
    {
      foreach ( var (key, def) in deformation.getEntries<Brick.Physics.Interactions.Deformation.DefaultDeformation>() ) {
        float? mapped = mapDeformation(def);
        if ( mapped == null )
          continue;
        if ( key.StartsWith( "along_" ) )
          target.SetCompliance( mapped.Value, mapRotationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
        else if ( key.StartsWith( "around_" ) )
          target.SetCompliance( mapped.Value, mapTranslationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
      }
    }

    void mapControllerDamping( Brick.Physics.Interactions.Damping.DefaultDamping damping, Brick.Physics.Interactions.Deformation.DefaultDeformation deformation, ElementaryConstraintController target )
    {
      float? mapped = mapDamping(damping, deformation);
      if ( mapped == null )
        return;
      target.Damping = mapped.Value;
    }

    void mapControllerDeformation( Brick.Physics.Interactions.Deformation.DefaultDeformation deformation, ElementaryConstraintController target )
    {
      float? mapped = mapDeformation(deformation);
      if ( mapped == null )
        return;
      target.Compliance = mapped.Value;
    }

    public GameObject MapMate( Interactions.Mate mate, Brick.Physics3D.System system )
    {
      ConstraintType? t = mate switch
      {
        Interactions.Cylindrical => ConstraintType.CylindricalJoint,
        Interactions.Hinge => ConstraintType.Hinge,
        Interactions.Lock => ConstraintType.LockJoint,
        Interactions.Prismatic => ConstraintType.Prismatic,
        _ => null,
      };
      if ( t == null ) {
        Debug.LogWarning( $"Mate type '{mate.GetType()}' is not supported" );
        return null;
      }

      Constraint agxConstraint = mapInteraction( mate, system, ( f1, f2 ) => createConstraint( f1, f2, t.Value ) );
      GameObject cGO = agxConstraint.gameObject;
      BrickObject.RegisterGameObject( mate.getName(), cGO );

      mapMateDamping( mate.damping(), mate.deformation(), agxConstraint );
      mapMateDeformation( mate.deformation(), agxConstraint );

      return cGO;
    }

    void enableRangeInteraction( RangeController agxRange, Interactions.RangeInteraction1DOF range )
    {
      agxRange.Enable = true;
      agxRange.Range = new RangeReal( (float)range.start(), (float)range.end() );
      agxRange.ForceRange = new RangeReal( (float)range.min_effort(), (float)range.max_effort() );

      mapControllerDamping( range.damping(), range.deformation(), agxRange );
      mapControllerDeformation( range.deformation(), agxRange );
    }

    void enableSpringInteraction( LockController agxLock, Interactions.SpringInteraction1DOF spring )
    {
      agxLock.Enable = true;
      agxLock.ForceRange = new RangeReal( (float)spring.min_effort(), (float)spring.max_effort() );

      mapControllerDamping( spring.damping(), spring.deformation(), agxLock );
      mapControllerDeformation( spring.deformation(), agxLock );
    }

    void enableTorqueMotorInteraction( TargetSpeedController agxTarSpeed, Interactions.TorqueMotor motor )
    {
      agxTarSpeed.Compliance = 1e-16f;
      agxTarSpeed.enabled = true;
      agxTarSpeed.Speed = 0;

      var torque = Mathf.Clamp((float)motor.default_torque(), (float)motor.min_effort(), (float)motor.max_effort());
      agxTarSpeed.ForceRange = new RangeReal( torque, torque );
    }

    void enableVelocityMotorInteraction( TargetSpeedController agxTarSpeed, Interactions.VelocityMotor motor )
    {
      agxTarSpeed.Enable = true;
      agxTarSpeed.Compliance = (float)(motor.gain() > 0.0f ? ( 1.0f / motor.gain() ) : float.MaxValue);
      agxTarSpeed.ForceRange = new RangeReal( (float)motor.min_effort(), (float)motor.max_effort() );

      agxTarSpeed.LockAtZeroSpeed = motor.zero_speed_as_spring();
      agxTarSpeed.Speed = (float)motor.desired_speed();

      mapControllerDamping( motor.zero_speed_spring_damping(), motor.zero_speed_spring_deformation(), agxTarSpeed );
      mapControllerDeformation( motor.zero_speed_spring_deformation(), agxTarSpeed );
    }

    GameObject mapLinearRange( Interactions.LinearRange range, Brick.Physics3D.System system )
    {
      var range_prismatic = mapInteraction( range, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Prismatic ) );
      range_prismatic.SetForceRange( new RangeReal( 0, 0 ) );

      enableRangeInteraction( range_prismatic.GetController<RangeController>(), range );

      GameObject cGO = range_prismatic.gameObject;
      BrickObject.RegisterGameObject( range.getName(), cGO );

      return cGO;
    }

    GameObject mapLinearSpring( Interactions.LinearSpring spring, Brick.Physics3D.System system )
    {
      var spring_prismatic = mapInteraction( spring, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Prismatic ) );
      spring_prismatic.SetForceRange( new RangeReal( 0, 0 ) );

      var spring_lock = spring_prismatic.GetController<LockController>();
      enableSpringInteraction( spring_lock, spring );
      spring_lock.Position = (float)spring.position();

      GameObject cGO = spring_prismatic.gameObject;
      BrickObject.RegisterGameObject( spring.getName(), cGO );

      return cGO;
    }

    GameObject mapRotationalRange( Interactions.RotationalRange range, Brick.Physics3D.System system )
    {
      var range_hinge = mapInteraction( range, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
      range_hinge.SetForceRange( new RangeReal( 0, 0 ) );

      enableRangeInteraction( range_hinge.GetController<RangeController>(), range );

      GameObject cGO = range_hinge.gameObject;
      BrickObject.RegisterGameObject( range.getName(), cGO );

      return cGO;
    }

    GameObject mapTorsionSpring( Interactions.TorsionSpring spring, Brick.Physics3D.System system )
    {
      var spring_hinge = mapInteraction( spring, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
      spring_hinge.SetForceRange( new RangeReal( 0, 0 ) );

      var spring_lock = spring_hinge.GetController<LockController>();
      enableSpringInteraction( spring_lock, spring );
      spring_lock.Position = -(float)spring.angle();

      GameObject cGO = spring_hinge.gameObject;
      BrickObject.RegisterGameObject( spring.getName(), cGO );

      return cGO;
    }

    //GameObject mapRotationalVelocityMotor(Brick.Physics1D.Interactions.RotationalVelocityMotor motor,  Brick.Physics3D.System system )
    //{
    //  var motor_hinge = mapInteraction( motor, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
    //  motor_hinge.SetForceRange( new RangeReal( 0, 0 ) );

    //  var motor_tarSpeed = motor_hinge.GetController<TargetSpeedController>();
    //  enableMotorInteraction( motor_tarSpeed, motor );

    //  GameObject cGO = motor_hinge.gameObject;
    //  BrickObject.RegisterGameObject( motor.getName(), cGO );

    //  return cGO;
    //}

    GameObject mapTorqueMotor( Interactions.TorqueMotor motor, Brick.Physics3D.System system )
    {
      var motor_hinge = mapInteraction( motor, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
      motor_hinge.SetForceRange( new RangeReal( 0, 0 ) );

      var motor_tarSpeed = motor_hinge.GetController<TargetSpeedController>();
      enableTorqueMotorInteraction( motor_tarSpeed, motor );

      GameObject cGO = motor_hinge.gameObject;
      BrickObject.RegisterGameObject( motor.getName(), cGO );

      return cGO;
    }

    GameObject mapRotationalVelocityMotor( Interactions.RotationalVelocityMotor motor, Brick.Physics3D.System system )
    {
      var motor_hinge = mapInteraction( motor, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
      motor_hinge.SetForceRange( new RangeReal( 0, 0 ) );

      var motor_tarSpeed = motor_hinge.GetController<TargetSpeedController>();
      enableVelocityMotorInteraction( motor_tarSpeed, motor );

      GameObject cGO = motor_hinge.gameObject;
      BrickObject.RegisterGameObject( motor.getName(), cGO );

      return cGO;
    }

    GameObject mapLinearVelocityMotor( Interactions.LinearVelocityMotor motor, Brick.Physics3D.System system )
    {
      var motor_prismatic = mapInteraction( motor, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Prismatic ) );
      motor_prismatic.SetForceRange( new RangeReal( 0, 0 ) );

      var motor_tarSpeed = motor_prismatic.GetController<TargetSpeedController>();
      enableVelocityMotorInteraction( motor_tarSpeed, motor );

      GameObject cGO = motor_prismatic.gameObject;
      BrickObject.RegisterGameObject( motor.getName(), cGO );

      return cGO;
    }

    public GameObject MapInteraction( Brick.Physics.Interactions.Interaction interaction, Brick.Physics3D.System system )
    {
      return interaction switch
      {
        Interactions.Mate mate => MapMate( mate, system ),
        Interactions.LinearRange lr => mapLinearRange( lr, system ),
        Interactions.LinearSpring ls => mapLinearSpring( ls, system ),
        Interactions.RotationalRange rs => mapRotationalRange( rs, system ),
        Interactions.TorsionSpring ts => mapTorsionSpring( ts, system ),
        Interactions.TorqueMotor tm => mapTorqueMotor( tm, system ),
        Interactions.RotationalVelocityMotor rvm => mapRotationalVelocityMotor( rvm, system ),
        Interactions.LinearVelocityMotor lvm => mapLinearVelocityMotor( lvm, system ),
        _ => Utils.ReportUnimplemented<GameObject>( interaction, Data.ErrorReporter )
      };
    }
  }
}