using AGXUnity.Utils;
using Brick.Physics.Charges;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Charges = Brick.Physics3D.Charges;
using Interactions = Brick.Physics3D.Interactions;

namespace AGXUnity.IO.BrickIO
{
  public class InteractionMapper
  {
    private MapperData Data;

    enum MappedConstraintType
    {
      Ordinary,
      RotationalTargetSpeed,
      RotationalRange,
      RotationalLock,
      TranslationalTargetSpeed,
      TranslationalRange,
      TranslationalLock,
    };

    public struct ChargeKey
    {
      public Charge[] m_charges;

      public static implicit operator ChargeKey( Charge[] charges )
      {
        return new ChargeKey() { m_charges = charges };
      }

      public static implicit operator ChargeKey( std.PhysicsChargesChargeVector charges )
      {
        return new ChargeKey() { m_charges = charges.ToArray() };
      }
    }

    public class CKEquality : IEqualityComparer<ChargeKey>
    {
      public bool Equals( ChargeKey x, ChargeKey y )
      {
        if ( x.m_charges.Length != y.m_charges.Length )
          return false;
        for ( int i = 0; i < x.m_charges.Length; i++ )
          if ( x.m_charges[ i ].getName() != y.m_charges[ i ].getName() )
            return false;
        return true;
      }

      public int GetHashCode( ChargeKey obj )
      {
        int hash = 0;
        foreach ( var charge in obj.m_charges )
          hash ^= charge.GetHashCode();
        return hash;
      }
    }

    private Dictionary<ChargeKey,List<Constraint>> ChargeConstraintsMap = new Dictionary<ChargeKey,List<Constraint>>(new CKEquality());
    private HashSet<Tuple<Constraint,MappedConstraintType>> UsedConstraintDofs = new HashSet<Tuple<Constraint,MappedConstraintType>>();

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
      var angle = Brick.Math.Vec3.angleBetweenVectors(new_x,normal_n,main_axis_n);
      var rotation_2 = Brick.Math.Quat.angleAxis(angle,main_axis_n);
      frame.LocalRotation = ( rotation_2 * rotation ).ToHandedQuaternion();

      var collapsedFrame = new IFrame();
      collapsedFrame.LocalPosition = frame.Position;
      collapsedFrame.LocalRotation = frame.Rotation;
      return frame;
    }

    HingeClass mapInteraction<HingeClass>( Brick.Physics.Interactions.Interaction interaction,
                                            Func<IFrame, IFrame, HingeClass> interactionCreator )
      where HingeClass : class
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

      Constraint agxConstraint = getOrCreateConstraintForInteraction(mate);
      if ( agxConstraint == null ) {
        Debug.LogWarning( $"Mate type '{mate.GetType()}' is not supported" );
        return null;
      }

      BrickObject.RegisterGameObject( mate.getName(), agxConstraint.gameObject, true );

      mapMateDamping( mate.damping(), mate.deformation(), agxConstraint );
      mapMateDeformation( mate.deformation(), agxConstraint );

      agxConstraint.SetForceRange( new RangeReal( float.NegativeInfinity, float.PositiveInfinity ) );

      return agxConstraint.gameObject;
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
      if ( spring is Interactions.TorsionSpring ts )
        agxLock.Position = (float)ts.angle();
      else if ( spring is Interactions.LinearSpring ls )
        agxLock.Position = (float)ls.position();
      else
        Utils.ReportUnimplemented<System.Object>( spring, Data.ErrorReporter );

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
      agxTarSpeed.Compliance = (float)( motor.gain() > 0.0f ? ( 1.0f / motor.gain() ) : float.MaxValue );
      agxTarSpeed.ForceRange = new RangeReal( (float)motor.min_effort(), (float)motor.max_effort() );

      agxTarSpeed.LockAtZeroSpeed = motor.zero_speed_as_spring();
      agxTarSpeed.Speed = (float)motor.desired_speed();

      mapControllerDamping( motor.zero_speed_spring_damping(), motor.zero_speed_spring_deformation(), agxTarSpeed );
      mapControllerDeformation( motor.zero_speed_spring_deformation(), agxTarSpeed );
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

    Constraint getOrCreateConstraintForInteraction( Brick.Physics.Interactions.Interaction interaction )
    {
      ConstraintType ?type = interaction switch
      {
        // Lock
        Interactions.Lock => ConstraintType.LockJoint,
        // Hinge
        Interactions.Hinge => ConstraintType.Hinge,
        Interactions.RotationalRange => ConstraintType.Hinge,
        Interactions.TorsionSpring => ConstraintType.Hinge,
        Interactions.RotationalVelocityMotor => ConstraintType.Hinge,
        Interactions.TorqueMotor => ConstraintType.Hinge,
        // Prismatic
        Interactions.Prismatic => ConstraintType.Prismatic,
        Interactions.LinearRange => ConstraintType.Prismatic,
        Interactions.LinearSpring => ConstraintType.Prismatic,
        Interactions.LinearVelocityMotor => ConstraintType.Prismatic,
        // Cylindrical
        Interactions.Cylindrical => ConstraintType.CylindricalJoint,
        // Unknown
        _ => Utils.ReportUnimplementedS<ConstraintType>( interaction, Data.ErrorReporter )
      };

      if ( type == null )
        return null;

      MappedConstraintType ct = interaction switch
      {
        Interactions.Lock => MappedConstraintType.Ordinary,
        Interactions.Hinge => MappedConstraintType.Ordinary,
        Interactions.Prismatic => MappedConstraintType.Ordinary,
        Interactions.Cylindrical => MappedConstraintType.Ordinary,
        Interactions.RotationalRange => MappedConstraintType.RotationalRange,
        Interactions.TorsionSpring => MappedConstraintType.RotationalLock,
        Interactions.RotationalVelocityMotor => MappedConstraintType.RotationalTargetSpeed,
        Interactions.TorqueMotor => MappedConstraintType.RotationalTargetSpeed,
        Interactions.LinearRange => MappedConstraintType.TranslationalRange,
        Interactions.LinearSpring => MappedConstraintType.TranslationalLock,
        Interactions.LinearVelocityMotor => MappedConstraintType.TranslationalTargetSpeed,
      };

      if ( !ChargeConstraintsMap.ContainsKey( interaction.charges() ) )
        ChargeConstraintsMap[ interaction.charges() ] = new List<Constraint>();

      var availableConstraint = ChargeConstraintsMap[ interaction.charges() ]
        .Where(c => c.Type == type.Value)
        .Where(c => !UsedConstraintDofs.Contains(Tuple.Create(c,ct)))
        .FirstOrDefault();

      if ( availableConstraint == null ) {
        availableConstraint = mapInteraction( interaction, ( f1, f2 ) => createConstraint( f1, f2, type.Value ) );
        availableConstraint.SetForceRange( new RangeReal( 0.0f, 0.0f ) );
        ChargeConstraintsMap[ interaction.charges() ].Add( availableConstraint );
      }
      UsedConstraintDofs.Add( Tuple.Create( availableConstraint, ct ) );

      return availableConstraint;
    }

    public GameObject MapInteraction( Brick.Physics.Interactions.Interaction interaction, Brick.Physics3D.System system )
    {
      if ( interaction is Interactions.Mate mate )
        return MapMate( mate, system );

      var constraint = getOrCreateConstraintForInteraction(interaction);

      switch ( interaction ) {
        case Interactions.RangeInteraction1DOF range:
          enableRangeInteraction( constraint.GetController<RangeController>(), range );
          break;
        case Interactions.SpringInteraction1DOF spring:
          enableSpringInteraction( constraint.GetController<LockController>(), spring );
          break;
        case Interactions.TorqueMotor tm:
          enableTorqueMotorInteraction( constraint.GetController<TargetSpeedController>(), tm );
          break;
        case Interactions.VelocityMotor vm:
          enableVelocityMotorInteraction( constraint.GetController<TargetSpeedController>(), vm );
          break;
        default:
          Utils.ReportUnimplemented<GameObject>( interaction, Data.ErrorReporter );
          break;
      };

      GameObject cGO = constraint.gameObject;
      BrickObject.RegisterGameObject( interaction.getName(), cGO );
      return cGO;
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

      ContactMaterial cm = ContactMaterial.CreateInstance<ContactMaterial>();
      cm.name = contactModel.getName();
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
        cm.Damping = ( 1.0f/50.0f ) * 2.0f;
      }
      else if ( contactModel.normal_deformation() is Brick.Physics.Interactions.Deformation.ElasticDeformation elastic ) {
        cm.YoungsModulus = (float)elastic.stiffness();
        var time = mapDamping(contactModel.damping(), contactModel.normal_deformation());
        if ( time.HasValue )
          cm.Damping = time.Value;
        if ( elastic is Brick.Physics.Interactions.SurfaceContact.PatchElasticity )
          cm.UseContactArea = true;
      }
      if ( contactModel.normal_deformation() is Brick.Physics.Interactions.Deformation.ElastoPlasticDeformation elastoplastic ) {
        Data.ErrorReporter.Report( elastoplastic, AgxUnityBrickErrors.InvalidDefomationType );
        return;
      }

      // Set the friction
      if ( contactModel.friction() is not Brick.Physics.Interactions.Friction.DefaultDryFriction dryFriction ) {
        Data.ErrorReporter.Report( contactModel.friction(), AgxUnityBrickErrors.UnsupportedFrictionModel );
        return;
      }
      else {
        cm.FrictionCoefficients = new Vector2( (float)dryFriction.coefficient(), (float)dryFriction.coefficient() );

        // TODO: Map friction model
        //  var body_oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.BodyOrientedDryConeFriction>(contactModel.friction());
        //  var geometry_oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.GeometryOrientedDryConeFriction>(contactModel.friction());
        //  var body_oriented_sb = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.BodyOrientedDryScaleBoxFriction>(contactModel.friction());
        //  var geometry_oriented_sb = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.GeometryOrientedDryScaleBoxFriction>(contactModel.friction());
        //  var cone = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryConeFriction>(contactModel.friction());
        //  var approx_cone = std.dynamic_pointer_cast<Physics.Interactions.Friction.ApproximateDryConeFriction>(contactModel.friction());
        //  var box = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryBoxFriction>(contactModel.friction());
        //  var scale_box = std.dynamic_pointer_cast<Physics.Interactions.Friction.DryScaleBoxFriction>(contactModel.friction());

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
      }

      //var oriented_cone = std.dynamic_pointer_cast<Physics3D.Interactions.Friction.DefaultOrientedDryFriction>(contactModel.friction());
      //if ( oriented_cone != null ) {
      //  cm.setFrictionCoefficient( oriented_cone.secondary_coefficient(), agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION );
      //}
      //cm.setFrictionModel( fm );

      if ( contactModel.adhesion() is Brick.Physics.Interactions.Adhesion.ConstantForceAdhesion constant_adhesive_force )
        cm.AdhesiveForce = (float)constant_adhesive_force.force();
      if ( contactModel.slack() is Brick.Physics.Interactions.Slack.ConstantDistanceSlack constant_slack_distance )
        cm.AdhesiveOverlap = (float)constant_slack_distance.distance();

      // Restitution
      cm.Restitution = (float)contactModel.normal_restitution();
      // TODO: Tangential restitution

      Data.PrefabLocalData.AddContactMaterial( cm );
      Data.ContactMaterials.Add( cm );
    }
  }
}