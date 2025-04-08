using openplx.Physics.Charges;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChargeKey = std.PhysicsChargesChargeVector;
using Charges = openplx.Physics3D.Charges;
using Interactions = openplx.Physics3D.Interactions;

namespace AGXUnity.IO.OpenPLX
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

    public class CKEquality : IEqualityComparer<ChargeKey>
    {
      public bool Equals( ChargeKey x, ChargeKey y )
      {
        if ( x.Count != y.Count )
          return false;
        for ( int i = 0; i < x.Count; i++ )
          if ( x[ i ].getName() != y[ i ].getName() )
            return false;
        return true;
      }

      public int GetHashCode( ChargeKey obj )
      {
        int hash = 0;
        foreach ( var charge in obj )
          hash ^= charge.GetHashCode();
        return hash;
      }
    }

    private Dictionary<ChargeKey,List<Constraint>> ChargeConstraintsMap = new Dictionary<ChargeKey,List<Constraint>>(new CKEquality());
    private HashSet<Tuple<Constraint,MappedConstraintType>> UsedConstraintDofs = new HashSet<Tuple<Constraint,MappedConstraintType>>();
    private Dictionary<Constraint, openplx.Core.Object> ConstraintParents = new Dictionary<Constraint, openplx.Core.Object>();

    public InteractionMapper( MapperData cache )
    {
      Data = cache;
    }

    public void MapMateConnectorInitial( openplx.Physics3D.Charges.MateConnector mc, GameObject parent )
    {
      if ( Data.MateConnectorCache.ContainsKey( mc ) )
        return;

      var mcObject = OpenPLXObject.CreateGameObject( mc.getName() );
      openplx.Core.Object owner = mc.getOwner();
      if ( mc is Charges.RedirectedMateConnector redirected )
        owner = redirected.redirected_parent();

      if ( Data.FrameCache.ContainsKey( owner ) )
        mcObject.transform.SetParent( Data.FrameCache[ owner ].transform );
      else
        mcObject.transform.SetParent( parent.transform );

      mcObject.transform.localPosition = mc.position().ToHandedVector3();

      var normal_n = mc.normal();
      var main_axis_n = mc.main_axis().normal();
      // Orthonormalize
      normal_n = ( normal_n - main_axis_n * ( normal_n * main_axis_n ) ).normal();

      // TODO: Error reporting

      var rotation = openplx.Math.Quat.from_to(openplx.Math.Vec3.Z_AXIS(), main_axis_n);
      var new_x = rotation.rotate(openplx.Math.Vec3.X_AXIS());
      var angle = openplx.Math.Vec3.angle_between_vectors(new_x,normal_n,main_axis_n);
      var rotation_2 = openplx.Math.Quat.angle_axis(angle,main_axis_n);
      mcObject.transform.localRotation = ( rotation_2 * rotation ).ToHandedQuaternion();

      Data.MateConnectorCache[ mc ] = mcObject;
    }

    IFrame MapMateConnector( Charges.MateConnector mate_connector )
    {
      var frame = new IFrame();

      if ( Data.MateConnectorCache.TryGetValue( mate_connector, out GameObject mapped ) ) {
        frame.SetParent( mapped, false );
        return frame;
      }
      else {
        // TODO: Remove Warning
        Debug.LogWarning( "Mapping MC -> Frame encountered a new MC" );
        return null;
      }
    }

    HingeClass MapInteraction<HingeClass>( openplx.Physics.Interactions.Interaction interaction,
                                            Func<IFrame, IFrame, HingeClass> interactionCreator )
      where HingeClass : class
    {
      Charge charge1 = interaction.charges().Count >= 1 ? interaction.charges()[0] : null;
      Charge charge2 = interaction.charges().Count >= 2 ? interaction.charges()[1] : null;

      var mate_connector1 = charge1 == null ? null : charge1 as Charges.MateConnector;
      var mate_connector2 = charge2 == null ? null : charge2 as Charges.MateConnector;

      var frame1 = mate_connector1 == null ? null : MapMateConnector(mate_connector1);
      var frame2 = mate_connector2 == null ? null : MapMateConnector(mate_connector2);

      if ( mate_connector1 is Charges.RedirectedMateConnector redirected_connector1 ) {
        RigidBody rb1 = redirected_connector1.redirected_parent() == null ? null : Data.BodyCache[ redirected_connector1.redirected_parent() ];
        frame1.SetParent( rb1?.gameObject, true );
      }
      if ( mate_connector2 is Charges.RedirectedMateConnector redirected_connector2 ) {
        RigidBody rb2 = redirected_connector2.redirected_parent() == null ? null : Data.BodyCache[ redirected_connector2.redirected_parent() ];
        frame2.SetParent( rb2?.gameObject, true );
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

    Constraint CreateConstraint( IFrame f1, IFrame f2, ConstraintType type )
    {
      var at1 = ConstraintFrame.CreateLocal(f1.Parent ?? Data.RootNode,f1.LocalPosition,f1.LocalRotation);
      var at2 = ConstraintFrame.CreateLocal(f2.Parent ?? Data.RootNode,f2.LocalPosition,f2.LocalRotation);
      var c = Constraint.Create(type);
      c.AttachmentPair.Synchronized = false;
      c.AttachmentPair.ReferenceFrame = at1;
      c.AttachmentPair.ConnectedFrame = at2;
      c.CollisionsState = Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;
      return c;
    }

    public static float? MapFlexibility( openplx.Physics.Interactions.Flexibility.DefaultFlexibility flexibility )
    {
      if ( flexibility is openplx.Physics.Interactions.Flexibility.Rigid )
        return float.Epsilon;
      else if ( flexibility is openplx.Physics.Interactions.Flexibility.LinearElastic elastic )
        return (float)( 1.0 / elastic.stiffness() );
      return null;
    }

    public static float? MapDissipation( openplx.Physics.Interactions.Dissipation.DefaultDissipation dissipation, openplx.Physics.Interactions.Flexibility.DefaultFlexibility deformation )
    {
      if ( dissipation is openplx.Physics.Interactions.Dissipation.ConstraintRelaxationTimeDamping crtd )
        return (float)crtd.relaxation_time();

      else if ( dissipation is openplx.Physics.Interactions.Dissipation.MechanicalDamping mechanical ) {
        if ( deformation is openplx.Physics.Interactions.Flexibility.LinearElastic elastic && elastic.stiffness() != 0.0 )
          return (float)( mechanical.damping_constant() / elastic.stiffness() );
        return null;
      }

      var agx_relaxation_time_annotations = dissipation.findAnnotations("agx_relaxation_time");
      if ( agx_relaxation_time_annotations.Count == 1 && agx_relaxation_time_annotations[ 0 ].isNumber() )
        return (float)agx_relaxation_time_annotations[ 0 ].asReal();

      return null;
    }

    Constraint.RotationalDof MapRotationalDOF( string axisName )
    {
      return axisName switch
      {
        "main" => Constraint.RotationalDof.Z,
        "normal" => Constraint.RotationalDof.X,
        "cross" => Constraint.RotationalDof.Y,
        _ => throw new ArgumentException( $"'{axisName}' is not a valid axis" )
      };
    }

    Constraint.TranslationalDof MapTranslationalDOF( string axisName )
    {
      return axisName switch
      {
        "main" => Constraint.TranslationalDof.Z,
        "normal" => Constraint.TranslationalDof.X,
        "cross" => Constraint.TranslationalDof.Y,
        _ => throw new ArgumentException( $"'{axisName}' is not a valid axis" )
      };
    }

    void MapMateDissipation( Interactions.Dissipation.DefaultMateDissipation damping, Interactions.Flexibility.DefaultMateFlexibility deformation, Constraint target )
    {
      foreach ( var (key, damp) in damping.getEntries<openplx.Physics.Interactions.Dissipation.DefaultDissipation>() ) {
        var def = deformation.getDynamic( key ).asObject() as openplx.Physics.Interactions.Flexibility.DefaultFlexibility;
        float? mapped = MapDissipation(damp, def);
        if ( mapped == null )
          continue;
        if ( key.StartsWith( "along_" ) )
          target.SetDamping( mapped.Value, MapRotationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
        else if ( key.StartsWith( "around_" ) )
          target.SetDamping( mapped.Value, MapTranslationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
      }
    }

    void MapMateFlexibility( Interactions.Flexibility.DefaultMateFlexibility deformation, Constraint target )
    {
      foreach ( var (key, def) in deformation.getEntries<openplx.Physics.Interactions.Flexibility.DefaultFlexibility>() ) {
        float? mapped = MapFlexibility(def);
        if ( mapped == null )
          continue;
        if ( key.StartsWith( "along_" ) )
          target.SetCompliance( mapped.Value, MapTranslationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
        else if ( key.StartsWith( "around_" ) )
          target.SetCompliance( mapped.Value, MapRotationalDOF( key.Substring( key.LastIndexOf( '_' ) + 1 ) ) );
      }
    }

    void MapControllerDissipation( openplx.Physics.Interactions.Dissipation.DefaultDissipation damping, openplx.Physics.Interactions.Flexibility.DefaultFlexibility deformation, ElementaryConstraintController target )
    {
      float? mapped = MapDissipation(damping, deformation);
      if ( mapped == null )
        return;
      target.Damping = mapped.Value;
    }

    void MapControllerFlexibility( openplx.Physics.Interactions.Flexibility.DefaultFlexibility deformation, ElementaryConstraintController target )
    {
      float? mapped = MapFlexibility(deformation);
      if ( mapped == null )
        return;
      target.Compliance = mapped.Value;
    }

    public GameObject MapMate( Interactions.Mate mate, openplx.Physics3D.System system )
    {

      Constraint agxConstraint = getOrCreateConstraintForInteraction(mate);
      if ( agxConstraint == null ) {
        Debug.LogWarning( $"Mate type '{mate.GetType()}' is not supported" );
        return null;
      }

      OpenPLXObject.RegisterGameObject( mate.getName(), agxConstraint.gameObject, true );

      MapMateDissipation( mate.dissipation(), mate.flexibility(), agxConstraint );
      MapMateFlexibility( mate.flexibility(), agxConstraint );

      agxConstraint.SetForceRange( new RangeReal( float.NegativeInfinity, float.PositiveInfinity ) );

      return agxConstraint.gameObject;
    }

    void EnableRangeInteraction( RangeController agxRange, Interactions.RangeInteraction1DOF range )
    {
      agxRange.Enable = range.enabled();
      agxRange.Range = new RangeReal( (float)range.start(), (float)range.end() );
      agxRange.ForceRange = new RangeReal( (float)range.min_effort(), (float)range.max_effort() );

      MapControllerDissipation( range.dissipation(), range.flexibility(), agxRange );
      MapControllerFlexibility( range.flexibility(), agxRange );
    }

    void EnableSpringInteraction( LockController agxLock, Interactions.SpringInteraction1DOF spring )
    {
      agxLock.Enable = spring.enabled();
      agxLock.ForceRange = new RangeReal( (float)spring.min_effort(), (float)spring.max_effort() );
      if ( spring is Interactions.TorsionSpring ts )
        agxLock.Position = (float)ts.angle();
      else if ( spring is Interactions.LinearSpring ls )
        agxLock.Position = (float)ls.position();
      else
        Utils.ReportUnimplemented<System.Object>( spring, Data.ErrorReporter );

      MapControllerDissipation( spring.dissipation(), spring.flexibility(), agxLock );
      MapControllerFlexibility( spring.flexibility(), agxLock );
    }

    void EnableTorqueMotorInteraction( TargetSpeedController agxTarSpeed, Interactions.TorqueMotor motor )
    {
      agxTarSpeed.Compliance = 1e-16f;
      agxTarSpeed.Enable = motor.enabled();
      agxTarSpeed.Speed = 0;

      var torque = Mathf.Clamp((float)motor.default_torque(), (float)motor.min_effort(), (float)motor.max_effort());
      agxTarSpeed.ForceRange = new RangeReal( torque, torque );
    }

    void EnableForceMotorInteraction( TargetSpeedController agxTarSpeed, Interactions.ForceMotor motor )
    {
      agxTarSpeed.Compliance = 1e-16f;
      agxTarSpeed.Enable = motor.enabled();
      agxTarSpeed.Speed = 0;

      var force = Mathf.Clamp((float)motor.default_force(), (float)motor.min_effort(), (float)motor.max_effort());
      agxTarSpeed.ForceRange = new RangeReal( force, force );
    }

    void EnableVelocityMotorInteraction( TargetSpeedController agxTarSpeed, Interactions.VelocityMotor motor )
    {
      agxTarSpeed.Enable = motor.enabled();
      agxTarSpeed.Compliance = (float)( motor.gain() > 0.0f ? ( 1.0f / motor.gain() ) : float.MaxValue );
      agxTarSpeed.ForceRange = new RangeReal( (float)motor.min_effort(), (float)motor.max_effort() );

      agxTarSpeed.LockAtZeroSpeed = motor.zero_speed_as_spring();
      agxTarSpeed.Speed = (float)motor.target_speed();

      MapControllerDissipation( motor.zero_speed_spring_dissipation(), motor.zero_speed_spring_flexibility(), agxTarSpeed );
      MapControllerFlexibility( motor.zero_speed_spring_flexibility(), agxTarSpeed );
    }

    //GameObject mapRotationalVelocityMotor(openplx.Physics1D.Interactions.RotationalVelocityMotor motor,  openplx.Physics3D.System system )
    //{
    //  var motor_hinge = mapInteraction( motor, system, ( f1, f2 ) => createConstraint( f1, f2, ConstraintType.Hinge ) );
    //  motor_hinge.SetForceRange( new RangeReal( 0, 0 ) );

    //  var motor_tarSpeed = motor_hinge.GetController<TargetSpeedController>();
    //  enableMotorInteraction( motor_tarSpeed, motor );

    //  GameObject cGO = motor_hinge.gameObject;
    //  OpenPLXObject.RegisterGameObject( motor.getName(), cGO );

    //  return cGO;
    //}

    Constraint getOrCreateConstraintForInteraction( openplx.Physics.Interactions.Interaction interaction )
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
        Interactions.ForceMotor => ConstraintType.Prismatic,
        // Cylindrical
        Interactions.Cylindrical => ConstraintType.CylindricalJoint,
        // Ball
        Interactions.Ball => ConstraintType.BallJoint,
        // Unknown
        _ => Utils.ReportUnimplementedS<ConstraintType>( interaction, Data.ErrorReporter )
      };

      if ( type == null )
        return null;

      MappedConstraintType? ct = interaction switch
      {
        Interactions.Lock => MappedConstraintType.Ordinary,
        Interactions.Hinge => MappedConstraintType.Ordinary,
        Interactions.Prismatic => MappedConstraintType.Ordinary,
        Interactions.Cylindrical => MappedConstraintType.Ordinary,
        Interactions.Ball => MappedConstraintType.Ordinary,
        Interactions.RotationalRange => MappedConstraintType.RotationalRange,
        Interactions.TorsionSpring => MappedConstraintType.RotationalLock,
        Interactions.RotationalVelocityMotor => MappedConstraintType.RotationalTargetSpeed,
        Interactions.TorqueMotor => MappedConstraintType.RotationalTargetSpeed,
        Interactions.LinearRange => MappedConstraintType.TranslationalRange,
        Interactions.LinearSpring => MappedConstraintType.TranslationalLock,
        Interactions.LinearVelocityMotor => MappedConstraintType.TranslationalTargetSpeed,
        Interactions.ForceMotor => MappedConstraintType.TranslationalTargetSpeed,
        _ => Utils.ReportUnimplementedS<MappedConstraintType>( interaction, Data.ErrorReporter )
      };

      if ( ct == null )
        return null;

      if ( !ChargeConstraintsMap.ContainsKey( interaction.charges() ) )
        ChargeConstraintsMap[ interaction.charges() ] = new List<Constraint>();

      var availableConstraint = ChargeConstraintsMap[ interaction.charges() ]
        .Where(c => c.Type == type.Value)
        .Where(c => ConstraintParents.ContainsKey(c) && ConstraintParents[c] == interaction.getOwner())
        .Where(c => !UsedConstraintDofs.Contains(Tuple.Create(c,ct.Value)))
        .FirstOrDefault();

      if ( availableConstraint == null ) {
        availableConstraint = MapInteraction( interaction, ( f1, f2 ) => CreateConstraint( f1, f2, type.Value ) );
        availableConstraint.SetForceRange( new RangeReal( 0.0f, 0.0f ) );
        ChargeConstraintsMap[ interaction.charges() ].Add( availableConstraint );
        ConstraintParents.Add( availableConstraint, interaction.getOwner() );
      }
      UsedConstraintDofs.Add( Tuple.Create( availableConstraint, ct.Value ) );

      return availableConstraint;
    }

    public GameObject MapInteraction( openplx.Physics.Interactions.Interaction interaction, openplx.Physics3D.System system )
    {
      if ( interaction is Interactions.Mate mate )
        return MapMate( mate, system );

      var constraint = getOrCreateConstraintForInteraction(interaction);

      switch ( interaction ) {
        case Interactions.RangeInteraction1DOF range:
          EnableRangeInteraction( constraint.GetController<RangeController>(), range );
          break;
        case Interactions.SpringInteraction1DOF spring:
          EnableSpringInteraction( constraint.GetController<LockController>(), spring );
          break;
        case Interactions.TorqueMotor tm:
          EnableTorqueMotorInteraction( constraint.GetController<TargetSpeedController>(), tm );
          break;
        case Interactions.VelocityMotor vm:
          EnableVelocityMotorInteraction( constraint.GetController<TargetSpeedController>(), vm );
          break;
        case Interactions.ForceMotor fm:
          EnableForceMotorInteraction( constraint.GetController<TargetSpeedController>(), fm );
          break;
        default:
          return Utils.ReportUnimplemented<GameObject>( interaction, Data.ErrorReporter );
      };

      GameObject cGO = constraint.gameObject;
      OpenPLXObject.RegisterGameObject( interaction.getName(), cGO );
      return cGO;
    }

    public void MapFrictionModel( ContactMaterial cm, openplx.Physics.Interactions.Dissipation.DefaultFriction friction )
    {
      // TODO: Map friction model
      if ( friction is not openplx.Physics.Interactions.Dissipation.DefaultDryFriction dryFriction ) {
        Data.ErrorReporter.Report( friction, AgxUnityOpenPLXErrors.UnsupportedFrictionModel );
        return;
      }

      // TODO: Map Solve type
      //auto friction_solve_type = agx::FrictionModel::DIRECT;
      //auto friction_solve_type_annotation = friction->findAnnotations("agx_friction_solve_type");
      //auto approximate_cone_annotation = friction->findAnnotations("agx_approximate_cone_friction");
      //bool approximate_cone = approximate_cone_annotation.size() == 1 &&  approximate_cone_annotation.front()->isTrue() ? true : false;
      //if ( friction_solve_type_annotation.size() == 1 && friction_solve_type_annotation.front()->isString() ) {
      //  if ( friction_solve_type_annotation.front()->isString( "SPLIT" ) ) {
      //    friction_solve_type = agx::FrictionModel::SPLIT;
      //  }
      //  else if ( friction_solve_type_annotation.front()->isString( "DIRECT_AND_ITERATIVE" ) ) {
      //    friction_solve_type = agx::FrictionModel::DIRECT_AND_ITERATIVE;
      //  }
      //  else if ( friction_solve_type_annotation.front()->isString( "ITERATIVE" ) ) {
      //    friction_solve_type = agx::FrictionModel::ITERATIVE;
      //  }
      //  else if ( !friction_solve_type_annotation.front()->isString( "DIRECT" ) ) {
      //    SPDLOG_WARN( "AGX friction solve type annotation defaults to DIRECT, {} is not supported", friction_solve_type_annotation.front()->asString() );
      //  }
      //}

      //var cone = std.dynamic_pointer_cast<Physics.Interactions.Dissipation.DryConeFriction>(friction);
      //var box = std.dynamic_pointer_cast<Physics.Interactions.Dissipation.DryBoxFriction>(friction);
      //var scale_box = std.dynamic_pointer_cast<Physics.Interactions.Dissipation.DryScaleBoxFriction>(friction);
      //var constant_normal = std.dynamic_pointer_cast<Physics.Interactions.Dissipation.DryConstantNormalForceFriction>(friction);

      cm.FrictionCoefficients = new Vector2( (float)dryFriction.coefficient(), (float)dryFriction.coefficient() );
      if ( Data.FrictionModelCache.TryGetValue( friction, out FrictionModel fm ) ) {
        cm.FrictionModel = fm;
        return;
      }

      // TODO: Map oriented friction models
      // Figure out if a oriented friction trait is being used
      //var reference_body = friction.getObject("reference_body");
      //var reference_geometry = friction.getObject("reference_geometry");
      //agx.FrameRef agx_reference_frame = nullptr;
      //if ( reference_body != nullptr ) {
      //  var it = m_rigidbody_map.find(reference_body);
      //  if ( it == m_rigidbody_map.end() ) {
      //    var token = friction.getType().getNameToken();
      //    m_error_reporter.reportError( Error.create( openplx.agxerror.MissingConnectedBody, token.line, token.column, m_source_id ) );
      //  }
      //  else {
      //    agx_reference_frame = it.second.getFrame();
      //  }
      //}
      //else if ( reference_geometry != nullptr ) {
      //  var it = m_geometry_map.find(reference_geometry);
      //  if ( it == m_geometry_map.end() ) {
      //    var token = friction.getType().getNameToken();
      //    m_error_reporter.reportError( Error.create( openplx.agxerror.MissingConnectedGeometry, token.line, token.column, m_source_id ) );
      //  }
      //  else {
      //    agx_reference_frame = it.second.getFrame();
      //  }
      //}

      //var primary_direction = friction.getObject("primary_direction") as openplx.Math.Vec3;
      if ( friction is openplx.Physics.Interactions.Dissipation.DryConeFriction cone ) {
        //agx.IterativeProjectedConeFrictionRef ipcf = nullptr;
        //if ( primary_direction != nullptr && agx_reference_frame != nullptr ) {
        //  ipcf = new agx.OrientedIterativeProjectedConeFrictionModel( agx_reference_frame,
        //                                                              mapVec3( primary_direction ),
        //                                                              agx.FrictionModel.DIRECT );
        //}
        //else {
        //  ipcf = new agx.IterativeProjectedConeFriction( agx.FrictionModel.DIRECT );
        //}
        //ipcf.setEnableDirectExactConeProjection( !approximate_cone );
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.IterativeProjectedFriction;
        Data.MappedFrictionModels.Add( fm );
      }
      else if ( friction is openplx.Physics.Interactions.Dissipation.DryBoxFriction box ) {
        //agx.BoxFrictionModelRef bfm = nullptr;
        //if ( primary_direction != nullptr && agx_reference_frame != nullptr ) {
        //  bfm = new agx.OrientedBoxFrictionModel( agx_reference_frame,
        //                                                 mapVec3( primary_direction ),
        //                                                 agx.FrictionModel.DIRECT );
        //}
        //else {
        //  bfm = new agx.BoxFrictionModel( agx.FrictionModel.DIRECT );
        //}
        //fm = bfm;
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.BoxFriction;
        Data.MappedFrictionModels.Add( fm );
      }
      else if ( friction is openplx.Physics.Interactions.Dissipation.DryScaleBoxFriction scale_box ) {
        //agx.ScaleBoxFrictionModelRef sbfm = nullptr;
        //if ( primary_direction != nullptr && agx_reference_frame != nullptr ) {
        //  sbfm = new agx.OrientedScaleBoxFrictionModel( agx_reference_frame,
        //                                                 mapVec3( primary_direction ),
        //                                                 agx.FrictionModel.DIRECT );
        //}
        //else {
        //  sbfm = new agx.ScaleBoxFrictionModel( agx.FrictionModel.DIRECT );
        //}
        //fm = sbfm;
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.ScaleBoxFriction;
        Data.MappedFrictionModels.Add( fm );
      }
      else if ( friction is openplx.Physics.Interactions.Dissipation.DryConstantNormalForceFriction constant_normal ) {
        //agx.BoxFrictionModelRef cnfm = nullptr;
        var depth_factor = 1.0;
        var scale_with_depth = false;
        if ( constant_normal is openplx.Physics.Interactions.Dissipation.DryDepthScaledConstantNormalForceFriction constant_normal_scaled_depth ) {
          scale_with_depth = true;
          depth_factor = constant_normal_scaled_depth.depth_factor();
        }
        //if ( primary_direction != nullptr && agx_reference_frame != nullptr ) {
        //  cnfm = new agx.ConstantNormalForceOrientedBoxFrictionModel( constant_normal.normal_force() * depth_factor,
        //                                                 agx_reference_frame,
        //                                                 mapVec3( primary_direction ),
        //                                                 agx.FrictionModel.DIRECT,
        //                                                 scale_with_depth );
        //}
        //else {
        //  var default_dir = new agx.Vec3(1,0,0);
        //  cnfm = new agx.ConstantNormalForceOrientedBoxFrictionModel( constant_normal.normal_force() * depth_factor,
        //                                                 nullptr,
        //                                                 *default_dir,
        //                                                 agx.FrictionModel.DIRECT,
        //                                                 scale_with_depth );
        //}
        //fm = cnfm;
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.ConstantNormalForceBoxFriction;
        fm.ScaleNormalForceWithDepth = scale_with_depth;
        fm.NormalForceMagnitude = (float)( constant_normal.normal_force() * depth_factor );
        Data.MappedFrictionModels.Add( fm );
      }
      else {
        // Here we choose the agx-openplx DefaultFriction to be a Split solve with the IterativeProjectedConeFriction
        if ( Data.DefaultFriction == null ) {
          fm = FrictionModel.CreateInstance<FrictionModel>();
          fm.name = friction.getName();
          fm.SolveType = FrictionModel.ESolveType.Split;
          fm.Type = FrictionModel.EType.IterativeProjectedFriction;
          Data.DefaultFriction = fm;
          Data.MappedFrictionModels.Add( fm );
        }
        fm = Data.DefaultFriction;
      }
      Data.FrictionModelCache[ friction ] = fm;
      cm.FrictionModel = fm;
    }

    public void MapContactModel( openplx.Physics.Interactions.SurfaceContact.Model contactModel )
    {
      var mat1 = contactModel.material_1();
      var mat2 = contactModel.material_2();

      ShapeMaterial sm1 = mat1 != null ? Data.MaterialCache[mat1] : null;
      ShapeMaterial sm2 = mat2 != null ? Data.MaterialCache[mat2] : null;

      if ( sm1 == null )
        Data.ErrorReporter.Report( mat1, AgxUnityOpenPLXErrors.MissingMaterial );

      if ( sm2 == null )
        Data.ErrorReporter.Report( mat2, AgxUnityOpenPLXErrors.MissingMaterial );

      if ( sm1  == null || sm2 == null )
        return;

      if ( Data.PrefabLocalData.ContactMaterials.Any( cm => ( cm.Material1 == sm1 && cm.Material2 == sm2 ) || ( cm.Material1 == sm2 && cm.Material2 == sm1 ) ) ) {
        Data.ErrorReporter.Report( contactModel, AgxUnityOpenPLXErrors.DuplicateMaterialPairForSurfaceContactModelDefinition );
        return;
      }

      ContactMaterial cm = ContactMaterial.CreateInstance<ContactMaterial>();
      cm.name = contactModel.getName();
      cm.Material1 = sm1;
      cm.Material2 = sm2;

      // TODO: set the damping
      //var mechanical_damping = std.dynamic_pointer_cast<Physics.Interactions.Damping.MechanicalDamping>(contactModel.damping());

      // Set the deformation
      if ( contactModel.normal_flexibility() is openplx.Physics.Interactions.Flexibility.Rigid rigid ) {
        // Set the contact as stiff as agx handle
        cm.YoungsModulus = 1e16f;
        // Set the damping to two times the time step, which is the recommended minimum.
        // Will override any other damping defined
        // TODO: We dont know the timestep at import time so this needs to be revised
        cm.Damping = ( 1.0f/60.0f ) * 2.0f;
      }
      else if ( contactModel.normal_flexibility() is openplx.Physics.Interactions.Flexibility.LinearElastic elastic ) {
        cm.YoungsModulus = (float)elastic.stiffness();
        var time = MapDissipation(contactModel.dissipation(), contactModel.normal_flexibility());
        if ( time.HasValue )
          cm.Damping = time.Value;
        if ( elastic is openplx.Physics.Interactions.SurfaceContact.PatchElasticity )
          cm.UseContactArea = true;
      }

      MapFrictionModel( cm, contactModel.friction() );

      if ( contactModel.adhesion() is openplx.Physics.Interactions.Adhesion.ConstantForceAdhesion constant_adhesive_force )
        cm.AdhesiveForce = (float)constant_adhesive_force.force();
      if ( contactModel.clearance() is openplx.Physics.Interactions.Clearance.ConstantDistanceClearance constant_slack_distance )
        cm.AdhesiveOverlap = (float)constant_slack_distance.distance();

      // Restitution
      cm.Restitution = (float)contactModel.normal_restitution();
      // TODO: Tangential restitution

      Data.PrefabLocalData.AddContactMaterial( cm );
      Data.MappedContactMaterials.Add( cm );
    }
  }
}
