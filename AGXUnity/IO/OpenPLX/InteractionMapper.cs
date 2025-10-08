using agxopenplx;
using openplx.Physics.Charges;
using openplx.Physics3D.Interactions;
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

    public InteractionMapper( MapperData cache )
    {
      Data = cache;
    }

    public void MapMateConnector( openplx.Physics3D.Charges.MateConnector mc )
    {
      if ( Data.MateConnectorCache.ContainsKey( mc ) )
        return;

      var mcObject = Data.CreateOpenPLXObject( mc.getName() + (mc is Charges.RedirectedMateConnector ? "_redirected" : "") );
      mcObject.AddComponent<ObserverFrame>();
      openplx.Core.Object owner = mc.getOwner();
      if ( mc is Charges.RedirectedMateConnector redirected )
        owner = redirected.redirected_parent();

      openplx.Core.Object current = owner;

      while ( current != null && !Data.FrameCache.ContainsKey( current ) )
        current = current.getOwner();

      if ( current == null )
        Debug.LogError( $"MateConnector '{mc.getName()}' has no valid parent GameObject in the hierarchy to parent to." );

      var parent = Data.FrameCache[ current ];

      mcObject.transform.SetParent( parent.transform );

      if ( current != owner )
        mcObject.name = mc.getName();

      mcObject.transform.localPosition = mc.position().ToHandedVector3();

      var normal_n = mc.normal();
      var main_axis_n = mc.main_axis().normal();
      // Orthonormalize
      normal_n = ( normal_n - main_axis_n * ( normal_n * main_axis_n ) ).normal();

      if ( Math.Abs( mc.normal().normal() * main_axis_n - 1.0 ) < float.Epsilon ) {
        var errorData = BaseError.CreateErrorData(mc);
        Data.ErrorReporter.reportError( new InvalidMateConnectorAxis( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, mc ) );
      }

      var rotation = openplx.Math.Quat.from_to(openplx.Math.Vec3.Z_AXIS(), main_axis_n);
      var new_x = rotation.rotate(openplx.Math.Vec3.X_AXIS());
      var angle = openplx.Math.Vec3.angle_between_vectors(new_x,normal_n,main_axis_n);
      var rotation_2 = openplx.Math.Quat.angle_axis(angle,main_axis_n);
      mcObject.transform.localRotation = ( rotation_2 * rotation ).ToHandedQuaternion();

      Data.MateConnectorCache[ mc ] = mcObject;
    }

    GameObject GetMappedMC( Charges.MateConnector mc )
    {
      if ( !Data.MateConnectorCache.ContainsKey( mc ) )
        MapMateConnector( mc );
      return Data.MateConnectorCache[ mc ];
    }

    IFrame GOToIFrame( GameObject mappedMC )
    {
      var frame = new IFrame();

      frame.SetParent( mappedMC, false );
      return frame;
    }

    HingeClass MapInteraction<HingeClass>( openplx.Physics.Interactions.Interaction interaction,
                                            Func<IFrame, IFrame, HingeClass> interactionCreator )
      where HingeClass : class
    {
      Charge charge1 = interaction.charges().Count >= 1 ? interaction.charges()[0] : null;
      Charge charge2 = interaction.charges().Count >= 2 ? interaction.charges()[1] : null;

      var mate_connector1 = charge1 == null ? null : charge1 as Charges.MateConnector;
      var mate_connector2 = charge2 == null ? null : charge2 as Charges.MateConnector;

      var mappedMC1 = GetMappedMC(mate_connector1);
      var mappedMC2 = GetMappedMC(mate_connector2);

      var frame1 = mate_connector1 == null ? null : GOToIFrame(mappedMC1);
      var frame2 = mate_connector2 == null ? null : GOToIFrame(mappedMC2);

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

      if ( frame1.Parent == null && frame2.Parent == null
        && mate_connector1 is not Charges.RedirectedMateConnector
        && mate_connector2 is not Charges.RedirectedMateConnector ) {
        var errorData = BaseError.CreateErrorData( interaction );
        Data.ErrorReporter.reportError( new MissingConnectedBody( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, interaction ) );
      }

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

      Constraint agxConstraint = CreateConstraintForInteraction(mate);
      if ( agxConstraint == null ) {
        Debug.LogWarning( $"Mate type '{mate.GetType()}' is not supported" );
        return null;
      }

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

    Constraint CreateConstraintForInteraction( openplx.Physics.Interactions.Interaction interaction )
    {
      ConstraintType ?type = interaction switch
      {
        // Ordinary
        Interactions.Lock => ConstraintType.LockJoint,
        Interactions.Hinge => ConstraintType.Hinge,
        Interactions.Prismatic => ConstraintType.Prismatic,
        Interactions.Cylindrical => ConstraintType.CylindricalJoint,
        Interactions.Ball => ConstraintType.BallJoint,
        // Generic 1DOF
        Interactions.RotationalRange => ConstraintType.GenericConstraint1DOF,
        Interactions.TorsionSpring => ConstraintType.GenericConstraint1DOF,
        Interactions.RotationalVelocityMotor => ConstraintType.GenericConstraint1DOF,
        Interactions.TorqueMotor => ConstraintType.GenericConstraint1DOF,
        Interactions.LinearRange => ConstraintType.GenericConstraint1DOF,
        Interactions.LinearSpring => ConstraintType.GenericConstraint1DOF,
        Interactions.LinearVelocityMotor => ConstraintType.GenericConstraint1DOF,
        Interactions.ForceMotor => ConstraintType.GenericConstraint1DOF,
        // Unknown
        _ => Utils.ReportUnimplementedS<ConstraintType>( interaction, Data.ErrorReporter )
      };

      if ( type == null )
        return null;

      var availableConstraint = MapInteraction( interaction, ( f1, f2 ) => CreateConstraint( f1, f2, type.Value ) );
      Data.RegisterOpenPLXObject( interaction.getName(), availableConstraint.gameObject );
      availableConstraint.SetForceRange( new RangeReal( 0.0f, 0.0f ) );

      return availableConstraint;
    }

    public GameObject MapInteraction( openplx.Physics.Interactions.Interaction interaction, openplx.Physics3D.System system )
    {
      if ( interaction is Interactions.Mate mate )
        return MapMate( mate, system );

      // TODO: Robotics joints can have null actuators maybe?
      if ( interaction.GetType() == typeof( openplx.Physics.Interactions.Interaction1DOF ) && system is openplx.Robotics.Joints.Joint )
        return null;

      var constraint = CreateConstraintForInteraction(interaction);

      if ( constraint is not Generic1DOFControlledConstraint g1dof )
        return null;

      agx.Angle.Type? angleType = interaction switch
      {
        LinearRange => agx.Angle.Type.TRANSLATIONAL,
        LinearSpring => agx.Angle.Type.TRANSLATIONAL,
        LinearVelocityMotor => agx.Angle.Type.TRANSLATIONAL,
        ForceMotor => agx.Angle.Type.TRANSLATIONAL,
        RotationalRange => agx.Angle.Type.ROTATIONAL,
        TorsionSpring => agx.Angle.Type.ROTATIONAL,
        RotationalVelocityMotor => agx.Angle.Type.ROTATIONAL,
        TorqueMotor => agx.Angle.Type.ROTATIONAL,
        _ => null
      };

      if ( angleType == null )
        return Utils.ReportUnimplemented<GameObject>( interaction, Data.ErrorReporter );

      g1dof.DOFType = angleType.Value;

      switch ( interaction ) {
        case Interactions.RangeInteraction1DOF range:
          g1dof.Controller = Generic1DOFControlledConstraint.SecondaryType.Range;
          EnableRangeInteraction( constraint.GetController<RangeController>(), range );
          break;
        case Interactions.SpringInteraction1DOF spring:
          g1dof.Controller = Generic1DOFControlledConstraint.SecondaryType.Lock;
          EnableSpringInteraction( constraint.GetController<LockController>(), spring );
          break;
        case Interactions.TorqueMotor tm:
          g1dof.Controller = Generic1DOFControlledConstraint.SecondaryType.TargetSpeed;
          EnableTorqueMotorInteraction( constraint.GetController<TargetSpeedController>(), tm );
          break;
        case Interactions.VelocityMotor vm:
          g1dof.Controller = Generic1DOFControlledConstraint.SecondaryType.TargetSpeed;
          EnableVelocityMotorInteraction( constraint.GetController<TargetSpeedController>(), vm );
          break;
        case Interactions.ForceMotor fm:
          g1dof.Controller = Generic1DOFControlledConstraint.SecondaryType.TargetSpeed;
          EnableForceMotorInteraction( constraint.GetController<TargetSpeedController>(), fm );
          break;
        default:
          return Utils.ReportUnimplemented<GameObject>( interaction, Data.ErrorReporter );
      }

      return constraint.gameObject;
    }

    public void MapFrictionModel( ContactMaterial cm, openplx.Physics.Interactions.Dissipation.DefaultFriction friction )
    {
      // TODO: Map more friction models
      if ( friction is not openplx.Physics.Interactions.Dissipation.DefaultDryFriction dryFriction ) {
        Data.ErrorReporter.reportError( new UnsupportedFrictionModelError( friction ) );
        return;
      }

      cm.FrictionCoefficients = new Vector2( (float)dryFriction.coefficient(), (float)dryFriction.coefficient() );
      if ( Data.FrictionModelCache.TryGetValue( friction, out FrictionModel fm ) ) {
        cm.FrictionModel = fm;
        return;
      }

      // TODO: Map oriented friction models
      // TODO: Find Oriented friction reference frames

      //var primary_direction = friction.getObject("primary_direction") as openplx.Math.Vec3;
      if ( friction is openplx.Physics.Interactions.Dissipation.DryConeFriction cone ) {
        //TODO: Handle oriented model
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.IterativeProjectedFriction;
        Data.MappedFrictionModels.Add( fm );
      }
      else if ( friction is openplx.Physics.Interactions.Dissipation.DryBoxFriction box ) {
        //TODO: Handle oriented model
        fm = FrictionModel.CreateInstance<FrictionModel>();
        fm.name = friction.getName();
        fm.SolveType = FrictionModel.ESolveType.Direct;
        fm.Type = FrictionModel.EType.BoxFriction;
        Data.MappedFrictionModels.Add( fm );
      }
      else if ( friction is openplx.Physics.Interactions.Dissipation.DryScaleBoxFriction scale_box ) {
        //TODO: Handle oriented model
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
        //TODO: Handle oriented model
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

      var solveType = FrictionModel.ESolveType.Direct;
      var solveTypeAnnotation = friction.findAnnotations("agx_friction_solve_type");
      var approximateConeAnnotation = friction.findAnnotations("agx_approximate_cone_friction");
      bool approximate_cone = approximateConeAnnotation.Count == 1 &&  approximateConeAnnotation[0].isTrue() ? true : false;
      if ( solveTypeAnnotation.Count == 1 && solveTypeAnnotation[ 0 ].isString() ) {
        if ( solveTypeAnnotation[ 0 ].isString( "SPLIT" ) )
          solveType = FrictionModel.ESolveType.Split;
        else if ( solveTypeAnnotation[ 0 ].isString( "DIRECT_AND_ITERATIVE" ) )
          solveType = FrictionModel.ESolveType.DirectAndIterative;
        else if ( solveTypeAnnotation[ 0 ].isString( "ITERATIVE" ) )
          solveType = FrictionModel.ESolveType.Iterative;
        else if ( !solveTypeAnnotation[ 0 ].isString( "DIRECT" ) ) {
          // TODO: Add warning
          //SPDLOG_WARN( "AGX friction solve type annotation defaults to DIRECT, {} is not supported", friction_solve_type_annotation.front().asString() );
        }
      }
      fm.SolveType = solveType;

      Data.FrictionModelCache[ friction ] = fm;
      cm.FrictionModel = fm;
    }

    public ShapeMaterial MapMaterial( openplx.Physics.Charges.Material material )
    {
      var sm = ShapeMaterial.CreateInstance<ShapeMaterial>();
      sm.name = material.getName();

      sm.Density = (float)material.density();
      // TODO: AGXUnity does not expose Young's modulus in ShapeMaterial

      return sm;
    }

    public void MapContactModel( openplx.Physics.Interactions.SurfaceContact.Model contactModel )
    {
      var mat1 = contactModel.material_1();
      var mat2 = contactModel.material_2();

      ShapeMaterial sm1 = mat1 != null && Data.MaterialCache.ContainsKey(mat1) ? Data.MaterialCache[mat1] : null;
      ShapeMaterial sm2 = mat2 != null && Data.MaterialCache.ContainsKey(mat2) ? Data.MaterialCache[mat2] : null;

      if ( sm1 == null ) {
        sm1 = MapMaterial( mat1 );
        Data.MaterialCache.Add( mat1, sm1 );
      }

      if ( mat1 == mat2 )
        sm2 = sm1;

      if ( sm2 == null ) {
        sm2 = MapMaterial( mat2 );
        Data.MaterialCache.Add( mat2, sm2 );
      }

      if ( sm1  == null || sm2 == null )
        return;

      if ( Data.PrefabLocalData.ContactMaterials.Any( cm => ( cm.Material1 == sm1 && cm.Material2 == sm2 ) || ( cm.Material1 == sm2 && cm.Material2 == sm1 ) ) ) {
        Data.ErrorReporter.reportError( new DuplicateMaterialPairForSurfaceContactModelDefinitionError( contactModel ) );
        return;
      }

      ContactMaterial cm = ContactMaterial.CreateInstance<ContactMaterial>();
      cm.name = contactModel.getName();
      cm.Material1 = sm1;
      cm.Material2 = sm2;

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

      if ( contactModel.hasTrait( "AGX.ContactReductionBinResolution" ) ) {
        var binResolution = contactModel.getNumber( "bin_resolution" );
        cm.ContactReductionLevel = binResolution switch
        {
          1 => ContactMaterial.ContactReductionLevelType.Aggressive,
          2 => ContactMaterial.ContactReductionLevelType.Moderate,
          _ => ContactMaterial.ContactReductionLevelType.Minimal
        };
      }

      // Restitution
      cm.Restitution = (float)contactModel.normal_restitution();
      // TODO: Tangential restitution

      Data.PrefabLocalData.AddContactMaterial( cm );
      Data.MappedContactMaterials.Add( cm );
    }
  }
}
