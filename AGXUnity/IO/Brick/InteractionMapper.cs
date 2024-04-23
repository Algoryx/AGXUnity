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

    public void MapContactModel( Brick.Physics.Interactions.SurfaceContact.Model contactModel )
    {
      var material_1 = contactModel.material_1();
      var material_2 = contactModel.material_2();

      ShapeMaterial sm1 = material_1 != null ? Data.MaterialCache[material_1] : null;
      ShapeMaterial sm2 = material_2 != null ? Data.MaterialCache[material_2] : null;

      if ( sm1 == null )
        Data.ErrorReporter.Report( material_1, AgxUnityBrickErrors.MissingMaterial );

      if ( sm2 == null )
        Data.ErrorReporter.Report( material_2, AgxUnityBrickErrors.MissingMaterial );

      if ( sm1  == null || sm2 == null )
        return;

      if ( Data.PrefabLocalData.ContactMaterials.Any( cm => ( cm.Material1 == sm1 && cm.Material2 == sm2 ) || ( cm.Material1 == sm2 && cm.Material2 == sm1 ) ) ) {
        Data.ErrorReporter.Report( contactModel, AgxUnityBrickErrors.DuplicateMaterialPairForSurfaceContactModelDefinition );
        return;
      }

      ContactMaterial cm = new ContactMaterial();
      cm.Material1 = sm1;
      cm.Material2 = sm2;

      // TODO: set the damping
      //var mechanical_damping = std.dynamic_pointer_cast<Physics.Interactions.Damping.MechanicalDamping>(contactModel.damping());

      // Set the deformation
      if ( contactModel.normal_deformation() is Brick.Physics.Interactions.Deformation.RigidDeformation rigid ) {
        // Set the contact as stiff as agx handle
        cm.YoungsModulus = 1e16f;
        // Set the damping to two times the time step, which is the recommended minimum.
        // Will override any other damping defined
        // TODO: We dont know the timestep at import time so this needs to be revised
        cm.Damping = (1.0f/50.0f) * 2.0f;
      }
      else if ( contactModel.normal_deformation() is Brick.Physics.Interactions.Deformation.ElasticDeformation elastic ) {
        cm.YoungsModulus = (float)elastic.stiffness();
        var time = mapDamping(contactModel.damping(), contactModel.normal_deformation());
        if ( time.HasValue )
          cm.Damping = time.Value;
        if ( elastic is Brick.Physics.Interactions.SurfaceContact.PatchElasticity)
          cm.UseContactArea = true ;
      }
      if ( contactModel.normal_deformation() is Brick.Physics.Interactions.Deformation.ElastoPlasticDeformation elastoplastic ) {
        Data.ErrorReporter.Report( elastoplastic, AgxUnityBrickErrors.InvalidDefomationType );
        return;
      }

      //// Set the friction
      //var dry_friction = std.dynamic_pointer_cast<Physics.Interactions.Friction.DefaultDryFriction>(contactModel.friction());
      //agx.FrictionModelRef fm = null;
      //if ( dry_friction == null ) {
      //  var member = contactModel.getType().findFirstMember("friction");
      //  var token = member.isVarDeclaration() ? member.asVarDeclaration().getNameToken() : member.asVarAssignment().getTargetSegments().back();
      //  m_error_reporter.reportError( Error.create( AGXBrickError.InvalidFrictionModel, token.line, token.column, m_source_id ) );
      //  SPDLOG_ERROR( "AGXBrick only supports DryFriction." );
      //  return;
      //}
      //else {
      //  var body_oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.BodyOrientedDryConeFriction>(contactModel.friction());
      //  var geometry_oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.GeometryOrientedDryConeFriction>(contactModel.friction());
      //  var body_oriented_sb = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.BodyOrientedDryScaleBoxFriction>(contactModel.friction());
      //  var geometry_oriented_sb = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.GeometryOrientedDryScaleBoxFriction>(contactModel.friction());
      //  var cone = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryConeFriction>(contactModel.friction());
      //  var approx_cone = std.dynamic_pointer_cast<Physics.Interactions.Friction.ApproximateDryConeFriction>(contactModel.friction());
      //  var box = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryBoxFriction>(contactModel.friction());
      //  var scale_box = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryScaleBoxFriction>(contactModel.friction());
      //  cm.setFrictionCoefficient( dry_friction.coefficient(), agx.ContactMaterial.FrictionDirection.BOTH_PRIMARY_AND_SECONDARY );

      //  if ( body_oriented_cone != null ) {
      //    agx.FrameRef ref_frame = null;
      //    var it = m_rigidbody_map.find(body_oriented_cone.reference_body());
      //    if ( it == m_rigidbody_map.end() ) {
      //      var token = body_oriented_cone.getType().getNameToken();
      //      m_error_reporter.reportError( Error.create( AGXBrickError.MissingConnectedGeometry, token.line, token.column, m_source_id ) );
      //    }
      //    else {
      //      ref_frame = it.second.getFrame();
      //    }
      //    var boc = new agx.OrientedIterativeProjectedConeFrictionModel(ref_frame,
      //                                                                      mapVec3(body_oriented_cone.primary_direction()),
      //                                                                      agx.FrictionModel.DIRECT);
      //    boc.setEnableDirectExactConeProjection( true );
      //    fm = boc;
      //  }
      //  else if ( geometry_oriented_cone != null ) {
      //    agx.FrameRef ref_frame = null;
      //    var it = m_geometry_map.find(geometry_oriented_cone.reference_geometry());
      //    if ( it == m_geometry_map.end() ) {
      //      var token = geometry_oriented_cone.getType().getNameToken();
      //      m_error_reporter.reportError( Error.create( AGXBrickError.MissingConnectedGeometry, token.line, token.column, m_source_id ) );
      //    }
      //    else {
      //      ref_frame = it.second.getFrame();
      //    }
      //    var goc = new agx.OrientedIterativeProjectedConeFrictionModel(ref_frame,
      //                                                                      mapVec3(geometry_oriented_cone.primary_direction()),
      //                                                                      agx.FrictionModel.DIRECT);
      //    goc.setEnableDirectExactConeProjection( true );
      //    fm = goc;
      //  }
      //  else if ( body_oriented_sb != null ) {
      //    agx.FrameRef ref_frame = null;
      //    var it = m_rigidbody_map.find(body_oriented_sb.reference_body());
      //    if ( it == m_rigidbody_map.end() ) {
      //      var token = body_oriented_sb.getType().getNameToken();
      //      m_error_reporter.reportError( Error.create( AGXBrickError.MissingConnectedGeometry, token.line, token.column, m_source_id ) );
      //    }
      //    else {
      //      ref_frame = it.second.getFrame();
      //    }
      //    var bosb = new agx.OrientedScaleBoxFrictionModel(ref_frame,
      //                                                         mapVec3(body_oriented_sb.primary_direction()),
      //                                                         agx.FrictionModel.DIRECT);
      //    fm = bosb;
      //  }
      //  else if ( geometry_oriented_sb != null ) {
      //    agx.FrameRef ref_frame = null;
      //    var it = m_geometry_map.find(geometry_oriented_sb.reference_geometry());
      //    if ( it == m_geometry_map.end() ) {
      //      var token = geometry_oriented_sb.getType().getNameToken();
      //      m_error_reporter.reportError( Error.create( AGXBrickError.MissingConnectedGeometry, token.line, token.column, m_source_id ) );
      //    }
      //    else {
      //      ref_frame = it.second.getFrame();
      //    }
      //    var gosb = new agx.OrientedScaleBoxFrictionModel(ref_frame,
      //                                                         mapVec3(geometry_oriented_sb.primary_direction()),
      //                                                         agx.FrictionModel.DIRECT);
      //    fm = gosb;
      //  }
      //  else if ( cone != null ) {
      //    var pc = new agx.IterativeProjectedConeFriction(agx.FrictionModel.DIRECT);
      //    pc.setEnableDirectExactConeProjection( true );
      //    fm = pc;
      //  }
      //  else if ( approx_cone != null ) {
      //    var pc = new agx.IterativeProjectedConeFriction(agx.FrictionModel.DIRECT);
      //    pc.setEnableDirectExactConeProjection( false );
      //    fm = pc;
      //  }
      //  else if ( box != null ) {
      //    var bfm = new agx.BoxFrictionModel(agx.FrictionModel.DIRECT);
      //    fm = bfm;
      //  }
      //  else if ( scale_box != null ) {
      //    var sbfm = new agx.ScaleBoxFrictionModel(agx.FrictionModel.DIRECT);
      //    fm = sbfm;
      //  }
      //  else {
      //    // Here we choose the AGXBrick DefaultFriction to be a Split solve with the IterativeProjectedConeFriction
      //    var ipc = new agx.IterativeProjectedConeFriction(agx.FrictionModel.SPLIT);
      //    fm = ipc;
      //  }
      //}

      //var oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.DefaultOrientedDryFriction>(contactModel.friction());
      //if ( oriented_cone != null ) {
      //  cm.setFrictionCoefficient( oriented_cone.secondary_coefficient(), agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION );
      //}
      //cm.setFrictionModel( fm );

      //double adhesive_force = 0.0;
      //double slack_distance = 0.0;
      //var constant_adhesive_force = std.dynamic_pointer_cast<Physics.Interactions.Adhesion.ConstantForceAdhesion>(contactModel.adhesion());
      //if ( constant_adhesive_force != null ) {
      //  adhesive_force = constant_adhesive_force.force();
      //}
      //var constant_slack_distance = std.dynamic_pointer_cast<Physics.Interactions.Slack.ConstantDistanceSlack>(contactModel.slack());
      //if ( constant_slack_distance != null ) {
      //  slack_distance = constant_slack_distance.distance();
      //}
      //if ( adhesive_force > 0.0 || slack_distance > 0.0 ) {
      //  cm.setAdhesion( adhesive_force, slack_distance );
      //}

      //// Restitution
      //cm.setTangentialRestitution( contactModel.tangential_restitution() );
      //cm.setRestitution( contactModel.normal_restitution() );
      Data.PrefabLocalData.AddContactMaterial( cm );
      Data.ContactMaterials.Add( cm );
    }
  }
}