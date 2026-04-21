using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using openplx.Physics3D.Interactions;
using System.Collections.Generic;
using UnityEngine;
using Steering = openplx.Vehicles.Steering;
using Suspensions = openplx.Vehicles.Suspensions;
using TrackSystem = openplx.Vehicles.TrackSystem;
using Wheels = openplx.Vehicles.Wheels;

namespace AGXUnity.IO.OpenPLX
{
  public class VehicleMapper
  {
    private MapperData Data;
    private InteractionMapper m_interactionMapper;

    private Dictionary<Suspensions.SingleMate.Base, WheelJoint> m_mappedWheels = new Dictionary<Suspensions.SingleMate.Base, WheelJoint> ();
    private Dictionary<TrackSystem.Base, TrackSystem.Connections.Base> m_connectionMap = new Dictionary<TrackSystem.Base, TrackSystem.Connections.Base>();

    public VehicleMapper( MapperData cache, InteractionMapper interactionMapper )
    {
      Data = cache;
      m_interactionMapper = interactionMapper;
    }

    public bool HandledInteraction( openplx.Physics.Interactions.Interaction interaction )
    {
      if ( interaction is Steering.Kinematic.Interactions.Base )
        return true;
      if ( interaction.getOwner() is Suspensions.SingleMate.Base susp && ( interaction == susp.mate() || interaction == susp.range() ) )
        return true;
      return false;
    }

    public void MapSystemPass1( openplx.Physics3D.System system )
    {
      if ( system is TrackSystem.Base ts ) {
        if ( ts.tracks().link_description() is TrackSystem.Components.Tracks.LinkDescription.Box boxDesc ) {
          var mat = boxDesc.contact_geometry().material();
          if ( !Data.MaterialCache.ContainsKey( mat ) )
            Data.MaterialCache[ mat ] = m_interactionMapper.MapMaterial( mat );
        }
      }
    }

    public void MapSystemPass2( openplx.Physics3D.System system )
    {
      foreach ( var trackConnection in system.getNonReferenceValues<TrackSystem.Connections.Base>() ) {
        var ts = trackConnection.track_system();

        if ( ts == null )
          Data.ErrorReporter.reportError( new MissingTrackSystemError( trackConnection ) );
        else if ( m_connectionMap.TryGetValue( ts, out var connection ) && connection != null )
          Data.ErrorReporter.reportError( new DuplicateTrackConnectionError( trackConnection, ts ) );
        else
          m_connectionMap[ ts ] = trackConnection;
      }

      // Collect all track systems at this level (for systems that don't have connections)
      foreach ( var ts in system.getNonReferenceValues<TrackSystem.Base>() )
        if ( !m_connectionMap.ContainsKey( ts ) )
          m_connectionMap[ ts ] = null;
    }

    public void MapTrackSystems()
    {
      foreach ( var (ts, connection) in m_connectionMap )
        MapTrackSystem( ts, connection );
    }

    public void MapSystemPass4( openplx.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      if ( system is Suspensions.SingleMate.Base suspension )
        MapSingleMateSuspensionOnto( suspension, s );
    }

    public void MapSystemPass5( openplx.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      if ( system is Steering.Kinematic.Base steering )
        MapSteeringOnto( steering, s );

      foreach ( var wheel in system.getNonReferenceValues<Wheels.ElasticWheel>() )
        MapElasticWheel( wheel );
    }

    private void MapTrackSystem( TrackSystem.Base system, TrackSystem.Connections.Base connection )
    {
      GameObject s = Data.SystemCache[system];

      var oTracks = system.tracks();

      var track = s.AddComponent<Track>();
      s.AddComponent<TrackRenderer>();

      track.NumberOfNodes = (int)oTracks.link_count();
      // TODO: Uncomment when swigged
      // TODO: Add force tension
      //track.InitialTensionDistance = (float)system.tension_initialization().node_distance();

      var wheels = system.track_wheels();
      if ( wheels.Count < 2 ) {
        Data.ErrorReporter.reportError( new InsufficientTrackWheelsError( system ) );
        return;
      }

      var linkDesc = oTracks.link_description();
      if ( linkDesc.local_cm_transform().position().length() > 0.0001f )
        Data.ErrorReporter.reportError( new LocalOffsetNotSupportedError( linkDesc.local_cm_transform().position() ) );

      if ( linkDesc.width() <= 0 || linkDesc.height() <= 0 ) {
        // TODO: convert to warning?
        Data.ErrorReporter.reportError( new InvalidLinkDescriptionError( linkDesc ) );
      }
      else {
        track.Width = (float)linkDesc.width();
        track.Thickness = (float)linkDesc.height();
      }

      if ( linkDesc.variation() is TrackSystem.Components.Tracks.LinkVariation.Box boxVariation ) {
        track.ThicknessVariation = MapVariation( boxVariation.height_variation() );
        track.WidthVariation = MapVariation( boxVariation.width_variation() );
      }

      if ( linkDesc is TrackSystem.Components.Tracks.LinkDescription.Box boxDesc )
        track.Material = Data.MaterialCache[ boxDesc.contact_geometry().material() ];

      var oChassis = system.chassis_body();
      if ( oChassis == null && connection != null ) {
        var hinge = connection.hinge_1().hinge();
        foreach ( var connector in hinge.connectors() ) {
          if ( connector is RedirectedMateConnector redirected )
            oChassis =  redirected.redirected_parent() as openplx.Physics3D.Bodies.RigidBody;
          else
            oChassis = connector.getOwner() as openplx.Physics3D.Bodies.RigidBody;
          if ( oChassis != null && oChassis != wheels[ 0 ].body() )
            break; // Found the chassis body
        }
      }

      if ( system.TryGetBoolAnnotation( "agx_enable_full_dof", out bool forceFullDoF ) && forceFullDoF )
        track.FullDoF = true;

      if ( !Data.BodyCache.TryGetValue( oChassis, out var chassis ) && !forceFullDoF ) {
        // TODO: Warn that no chassis could be found
        track.FullDoF = true;
      }

      if ( chassis != null )
        track.ReferenceObject = chassis.gameObject;

      MapInternalMergeProperties( system, track );
      MapTrackProperties( system, track );

      foreach ( var wheel in wheels ) {
        if ( !Data.BodyCache.TryGetValue( wheel.body(), out var parent ) ) {
          Data.ErrorReporter.reportError( new MissingTrackWheelBodyError( wheel ) );
          continue;
        }


        var system_relative_transform = new agx.AffineMatrix4x4();
        var to_center_axis_transform = new agx.AffineMatrix4x4();
        to_center_axis_transform.setRotate( new agx.Quat( agx.Vec3.Y_AXIS(), wheel.local_center_axis().ToVec3() ) );
        system_relative_transform.setTranslate( wheel.local_transform().position().ToVec3() );
        system_relative_transform.setRotate( wheel.local_transform().rotation().ToQuat() );

        var position = to_center_axis_transform.getTranslate().ToHandedVector3();
        var rotation = to_center_axis_transform.getRotate().ToHandedQuaternion();

        TrackWheelModel? type = wheel switch
        {
          openplx.Vehicles.TrackSystem.Components.TrackWheels.Sprocket => TrackWheelModel.Sprocket,
          openplx.Vehicles.TrackSystem.Components.TrackWheels.Idler => TrackWheelModel.Idler,
          openplx.Vehicles.TrackSystem.Components.TrackWheels.Roller => TrackWheelModel.Roller,
          openplx.Vehicles.TrackSystem.Components.TrackWheels.RoadWheel => TrackWheelModel.Roller,
          _ => null
        };

        if ( type == null ) {
          Utils.ReportUnimplemented( wheel, Data.ErrorReporter );
          continue;
        }

        track.Add( CreateTrackWheel( type.Value, (float)wheel.radius(), parent.gameObject, position, rotation ) );
      }

      return;
    }

    private void MapInternalMergeProperties( TrackSystem.Base track_system, Track track )
    {
      var merge_props = ScriptableObject.CreateInstance<TrackInternalMergeProperties>();

      if ( track_system.TryGetBoolAnnotation( "agx_set_enable_merge", out bool enableMerge ) )
        merge_props.MergeEnabled = enableMerge;

      if ( track_system.TryGetStringAnnotation( "agx_set_contact_reduction", out string reduction ) ) {
        if ( reduction == "NONE" )
          merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.None;
        if ( reduction == "MINIMAL" )
          merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Minimal;
        if ( reduction == "MODERATE" )
          merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Moderate;
        if ( reduction == "AGGRESSIVE" )
          merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Aggressive;
      }

      if ( track_system.TryGetBoolAnnotation( "agx_set_enable_lock_to_reach_merge_condition", out bool lockToReachMergeConditionEnabled ) )
        merge_props.LockToReachMergeConditionEnabled = lockToReachMergeConditionEnabled;
      if ( track_system.TryGetRealAnnotation( "agx_set_lock_to_reach_merge_condition_compliance", out float LockToReachMergeConditionCompliance ) )
        merge_props.LockToReachMergeConditionCompliance = LockToReachMergeConditionCompliance;
      if ( track_system.TryGetRealAnnotation( "agx_set_lock_to_reach_merge_condition_relaxation_time", out float LockToReachMergeConditionDamping ) )
        merge_props.LockToReachMergeConditionDamping =LockToReachMergeConditionDamping;
      if ( track_system.TryGetRealAnnotation( "agx_set_num_nodes_per_merge_segment", out float NumNodesPerMergeSegment ) )
        merge_props.NumNodesPerMergeSegment = (int)NumNodesPerMergeSegment;
      if ( track_system.TryGetRealAnnotation( "agx_set_max_angle_merge_condition", out float MaxAngleMergeCondition ) )
        merge_props.MaxAngleMergeCondition = MaxAngleMergeCondition;

      merge_props.name = track_system.getName() + "_MP";
      track.InternalMergeProperties = merge_props;
      Data.MappedTrackInternalMergeProperties.Add( merge_props );
    }

    private void MapTrackProperties( TrackSystem.Base track_system, Track track )
    {
      var track_props = ScriptableObject.CreateInstance<TrackProperties>();

      var oProps = track_system.properties();

      Vector3 rotationalStiffness = track_props.HingeStiffnessRotational;
      Vector3 translationalStiffness = track_props.HingeStiffnessTranslational;

      if ( oProps.flexibility().bending_vertical() is openplx.Physics.Interactions.Flexibility.LinearElastic bending_verticalLE )
        rotationalStiffness.x = (float)bending_verticalLE.stiffness();

      if ( oProps.flexibility().bending_lateral() is openplx.Physics.Interactions.Flexibility.LinearElastic bending_lateralLE )
        rotationalStiffness.z = (float)bending_lateralLE.stiffness();

      if ( oProps.flexibility().torsional() is openplx.Physics.Interactions.Flexibility.LinearElastic torsionalLE )
        rotationalStiffness.y = (float)torsionalLE.stiffness();

      if ( oProps.flexibility().tensile() is openplx.Physics.Interactions.Flexibility.LinearElastic tensileLE )
        translationalStiffness.y = (float)tensileLE.stiffness();

      if ( oProps.flexibility().shear_vertical() is openplx.Physics.Interactions.Flexibility.LinearElastic shear_verticalLE )
        translationalStiffness.z = (float)shear_verticalLE.stiffness();

      if ( oProps.flexibility().shear_lateral() is openplx.Physics.Interactions.Flexibility.LinearElastic shear_lateralLE )
        translationalStiffness.x = (float)shear_lateralLE.stiffness();

      track_props.HingeStiffnessRotational = rotationalStiffness;
      track_props.HingeStiffnessTranslational = translationalStiffness;

      Vector3 rotationalAttenuation = track_props.HingeAttenuationRotational;
      Vector3 translationalAttenuation = track_props.HingeAttenuationTranslational;

      if ( oProps.damping().bending_vertical() is TrackSystem.Interactions.Dissipation.Attenuation bending_verticalAttenuation )
        rotationalAttenuation.x = (float)bending_verticalAttenuation.value();

      if ( oProps.damping().bending_lateral() is TrackSystem.Interactions.Dissipation.Attenuation bending_lateralAttenuation )
        rotationalAttenuation.z = (float)bending_lateralAttenuation.value();

      if ( oProps.damping().torsional() is TrackSystem.Interactions.Dissipation.Attenuation torsionalAttenuation )
        rotationalAttenuation.y = (float)torsionalAttenuation.value();

      if ( oProps.damping().tensile() is TrackSystem.Interactions.Dissipation.Attenuation tensileAttenuation )
        translationalAttenuation.y = (float)tensileAttenuation.value();

      if ( oProps.damping().shear_vertical() is TrackSystem.Interactions.Dissipation.Attenuation shear_verticalAttenuation )
        translationalAttenuation.z = (float)shear_verticalAttenuation.value();

      if ( oProps.damping().shear_lateral() is TrackSystem.Interactions.Dissipation.Attenuation shear_lateralAttenuation )
        translationalAttenuation.x = (float)shear_lateralAttenuation.value();

      track_props.HingeAttenuationRotational = rotationalAttenuation;
      track_props.HingeAttenuationTranslational = translationalAttenuation;

      if ( track_system.TryGetRealAnnotation( "agx_track_node_wheel_overlap", out float overlap ) )
        track_props.TransformNodesToWheelsOverlap = overlap;

      if ( track_system.TryGetBoolAnnotation( "agx_track_on_initialize_merge_nodes_to_wheels", out bool merge ) )
        track_props.OnInitializeMergeNodesToWheelsEnabled = merge;

      if ( track_system.TryGetBoolAnnotation( "agx_track_on_initialize_transform_nodes_to_wheels", out bool transform ) )
        track_props.OnInitializeTransformNodesToWheelsEnabled = transform;

      bool hasRange = false;
      float min = -Mathf.Infinity;
      float max = Mathf.Infinity;
      if ( track_system.TryGetRealAnnotation( "agx_track_hinge_range_lower", out float lower ) ) {
        min = lower;
        hasRange = true;
      }
      if ( track_system.TryGetRealAnnotation( "agx_track_hinge_range_upper", out float upper ) ) {
        max = upper;
        hasRange = true;
      }
      if ( hasRange ) {
        track_props.HingeRangeEnabled = true;
        track_props.HingeRangeRange = new AGXUnity.RangeReal( min * 180.0f/Mathf.PI, max * 180.0f/Mathf.PI );
      }

      // TODO: Map new stiffness/attenuation parameters
      //var track_hinge_compliance_annots = track_system.findAnnotations("agx_track_hinge_compliance");
      //if ( track_hinge_compliance_annots.Count != 0 ) {
      //  if ( track_hinge_compliance_annots[ 0 ].isNumber() ) {
      //    track_props.HingeStiffnessRotational = Vector2.one * (float)track_hinge_compliance_annots[ 0 ].asReal();
      //    track_props.HingeComplianceTranslational = Vector2.one * (float)track_hinge_compliance_annots[ 0 ].asReal();
      //  }
      //}

      //var track_hinge_relaxation_time = track_system.findAnnotations("agx_track_hinge_relaxation_time");
      //if ( track_hinge_relaxation_time.Count != 0 ) {
      //  if ( track_hinge_relaxation_time[ 0 ].isNumber() ) {
      //    track_props.HingeDampingRotational = Vector2.one * (float)track_hinge_relaxation_time[ 0 ].asReal();
      //    track_props.HingeAttenuationTranslational = Vector2.one * (float)track_hinge_relaxation_time[ 0 ].asReal();
      //  }
      //}

      if ( track_system.TryGetRealAnnotation( "agx_track_stabilizing_friction_parameter", out float StabilizingHingeFrictionParameter ) )
        track_props.StabilizingHingeFrictionParameter = StabilizingHingeFrictionParameter;
      if ( track_system.TryGetRealAnnotation( "agx_track_min_stabilizing_normal_force", out float MinStabilizingHingeNormalForce ) )
        track_props.MinStabilizingHingeNormalForce = MinStabilizingHingeNormalForce;
      if ( track_system.TryGetRealAnnotation( "agx_track_node_wheel_merge_threshold", out float NodesToWheelsMergeThreshold ) )
        track_props.NodesToWheelsMergeThreshold = NodesToWheelsMergeThreshold;
      if ( track_system.TryGetRealAnnotation( "agx_track_node_wheel_split_threshold", out float NodesToWheelsSplitThreshold ) )
        track_props.NodesToWheelsSplitThreshold = NodesToWheelsSplitThreshold;
      if ( track_system.TryGetRealAnnotation( "agx_track_num_nodes_in_average_direction", out float NumNodesIncludedInAverageDirection ) )
        track_props.NumNodesIncludedInAverageDirection = (int)NumNodesIncludedInAverageDirection;

      // TODO: Map reduced order annotations

      track_props.name = track_system.getName() + "_TP";
      track.Properties = track_props;
      Data.MappedTrackProperties.Add( track_props );
    }

    private TrackNodeVariation MapVariation( openplx.Math.Distributions.Cyclic.Base variation )
    {
      if ( variation is openplx.Math.Distributions.Cyclic.Sinusoidal sin )
        return new AGXUnity.Model.SinusoidalVariation( (float)sin.amplitude(), (float)sin.period() );
      if ( variation is openplx.Math.Distributions.Cyclic.DiscretePulse disc )
        return new AGXUnity.Model.DiscretePulseVariation( (float)disc.amplitude(), (int)disc.period() );
      if ( variation.GetType() == typeof( openplx.Math.Distributions.Cyclic.Base ) )
        return null;
      return Utils.ReportUnimplemented<TrackNodeVariation>( variation, Data.ErrorReporter );
    }

    private void MapElasticWheel( Wheels.ElasticWheel wheel )
    {
      if ( !Data.BodyCache.TryGetValue( wheel.tire(), out RigidBody tireBody ) ) {
        Data.ErrorReporter.reportError( new InternalMapperError( wheel.tire(), "Failed to find mapped body for tire" ) );
        return;
      }

      double outerRadius = wheel.tire().getNumber("outer_radius");

      if ( !Data.BodyCache.TryGetValue( wheel.rim(), out RigidBody rimBody ) ) {
        Data.ErrorReporter.reportError( new InternalMapperError( wheel.rim(), "Failed to find mapped body for rim" ) );
        return;
      }
      double innerRadius = wheel.rim().getNumber("radius");

      var tireGO = Data.SystemCache[wheel];

      var twoBodyTire = tireGO.AddComponent<TwoBodyTire>();

      twoBodyTire.TireRigidBody = tireBody;
      twoBodyTire.TireRadius = (float)outerRadius;
      twoBodyTire.RimRigidBody = rimBody;
      twoBodyTire.RimRadius  = (float)innerRadius;

      twoBodyTire.TireRimConstraint = Data.MateCache[ wheel.tire_mate() ];

      //// Tire settings
      ///
      TwoBodyTireProperties properties;
      if ( !Data.TirePropertyCache.TryGetValue( wheel, out properties ) ) {
        properties = ScriptAsset.CreateInstance<TwoBodyTireProperties>();
        properties.name = wheel.getName();

        properties.TorsionalStiffness = (float)wheel.flexibility().around_radial().stiffness();
        properties.RadialStiffness    = (float)wheel.flexibility().along_radial().stiffness();
        properties.LateralStiffness   = (float)wheel.flexibility().along_axial().stiffness();
        properties.BendingStiffness   = (float)wheel.flexibility().around_axial().stiffness();

        properties.TorsionalDampingCoefficient = (float)wheel.dissipation().around_radial().damping_constant();
        properties.RadialDampingCoefficient    = (float)wheel.dissipation().along_radial().damping_constant();
        properties.LateralDampingCoefficient   = (float)wheel.dissipation().along_axial().damping_constant();
        properties.BendingDampingCoefficient   = (float)wheel.dissipation().around_axial().damping_constant();

        Data.TirePropertyCache[ wheel ] = properties;
      }

      twoBodyTire.Properties = properties;
    }

    private TrackWheel CreateTrackWheel( TrackWheelModel model, float radius, GameObject parent, Vector3 position, Quaternion rotation )
    {
      var wheel = parent.AddComponent<TrackWheel>();
      wheel.Radius = radius;
      wheel.Model = model;

      wheel.Frame.SetParent( parent );
      wheel.Frame.LocalPosition = position;
      wheel.Frame.LocalRotation = rotation;

      return wheel;
    }

    private bool MapSingleMateSuspensionOnto( Suspensions.SingleMate.Base suspension, GameObject onto )
    {
      RigidBody chassis = null;
      RigidBody wheel = null;
      var parent = suspension.chassis_connector().getOwner();

      if ( parent == null )
        return false;

      openplx.Physics3D.Bodies.RigidBody chassisBody = null;

      if ( suspension.chassis_connector() is RedirectedMateConnector crmc )
        chassisBody = crmc.redirected_parent() as openplx.Physics3D.Bodies.RigidBody;
      else if ( parent is openplx.Physics3D.System chassisSystem )
        chassisBody = suspension.attachment_connector().getOwner() as openplx.Physics3D.Bodies.RigidBody;
      else
        chassisBody = parent as openplx.Physics3D.Bodies.RigidBody;

      if ( chassisBody != null && Data.BodyCache.ContainsKey( chassisBody ) )
        chassis = Data.BodyCache[ chassisBody ];
      else {
        Data.ErrorReporter.reportError( new InvalidWheelChassisError( suspension ) );
        return false;
      }

      var wheelParent = suspension.attachment_connector().getOwner();
      openplx.Physics3D.Bodies.RigidBody attachmentBody = null;

      if ( suspension.attachment_connector() is RedirectedMateConnector armc )
        attachmentBody = armc.redirected_parent() as openplx.Physics3D.Bodies.RigidBody;
      else if ( wheelParent is openplx.Physics3D.Bodies.RigidBody ab )
        attachmentBody = ab;
      else if ( wheelParent is Wheels.Base wheelModel )
        attachmentBody = wheelModel.rim();

      if ( attachmentBody != null && Data.BodyCache.ContainsKey( attachmentBody ) )
        wheel = Data.BodyCache[ attachmentBody ];
      else {
        Data.ErrorReporter.reportError( new MissingWheelBodyError( suspension ) );
        return false;
      }

      // OpenPLX assumes axes N = Wheel and U = Suspension, in agx N = Suspension, V = Wheel is used. Apply a local rotation to correct for this from the precalculated frame.
      Quaternion frameCorrection = Quaternion.Euler( 0, -90, -90 );
      var chassisXForm  = chassis.transform.worldToLocalMatrix * Data.MateConnectorCache[ suspension.chassis_connector() ].transform.localToWorldMatrix;
      var wheelXForm    = wheel.transform.worldToLocalMatrix * Data.MateConnectorCache[ suspension.attachment_connector() ].transform.localToWorldMatrix;

      var chassisFrame    = new ConstraintFrame( chassis.gameObject, chassisXForm.GetTranslate(), chassisXForm.GetRotation() * frameCorrection);
      var wheelFrame      = new ConstraintFrame( wheel.gameObject, wheelXForm.GetTranslate(), wheelXForm.GetRotation() * frameCorrection );

      var wheelJoint = WheelJoint.Create(wheelFrame, chassisFrame, onto);
      if ( !suspension.HasTrait<Suspensions.Properties.Steering>() )
        wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Steering ).Enable = true;

      if ( suspension is Suspensions.SingleMate.LinearSpringDamper lsd ) {
        var damping = InteractionMapper.MapDissipation( lsd.mate().spring_damping(), lsd.mate().spring_constant() );
        if ( damping.HasValue )
          wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Suspension ).Damping = damping.Value;
        var compliance = InteractionMapper.MapFlexibility(lsd.mate().spring_constant());
        if ( compliance.HasValue )
          wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Suspension ).Compliance = compliance.Value;

        wheelJoint.enabled = lsd.mate().enabled();
      }

      var range = wheelJoint.GetController<RangeController>( WheelJoint.WheelDimension.Suspension );
      range.Enable = suspension.range().enabled();
      var rangeCompliance = InteractionMapper.MapFlexibility(suspension.range().flexibility());
      if ( rangeCompliance.HasValue )
        range.Compliance = rangeCompliance.Value;
      var rangeDamping = InteractionMapper.MapDissipation(suspension.range().dissipation(),suspension.range().flexibility());
      if ( rangeDamping.HasValue )
        range.Damping = rangeDamping.Value;
      range.Range = new RangeReal( (float)suspension.range().start(), (float)suspension.range().end() );
      range.ForceRange = new RangeReal( (float)suspension.range().min_effort(), (float)suspension.range().max_effort() );

      Data.RegisterOpenPLXObject( suspension.getName(), wheelJoint.gameObject );
      Data.RegisterOpenPLXObject( suspension.range().getName(), wheelJoint.gameObject );
      Data.RegisterOpenPLXObject( suspension.mate().getName(), wheelJoint.gameObject );

      m_mappedWheels[ suspension ] = wheelJoint;

      return true;
    }

    private bool MapSteeringOnto( Steering.Kinematic.Base steering, GameObject onto )
    {
      Model.Steering.SteeringMechanism? mechanism = steering switch
      {
        Steering.Kinematic.Ackermann => Model.Steering.SteeringMechanism.Ackermann,
        Steering.Kinematic.BellCrank => Model.Steering.SteeringMechanism.BellCrank,
        Steering.Kinematic.RackAndPinion => Model.Steering.SteeringMechanism.RackPinion,
        _ => null
      };

      if ( !mechanism.HasValue ) {
        Data.ErrorReporter.reportError( new UnimplementedError( steering ) );
        return false;
      }

      var steeringComp = onto.AddComponent<Model.Steering>();
      Data.RegisterOpenPLXObject( steering.getName(), onto );
      Data.RegisterOpenPLXObject( steering.interaction().getName(), onto );

      WheelJoint rightWheel, leftWheel;

      if ( !m_mappedWheels.TryGetValue( steering.right_suspension(), out rightWheel ) ) {
        Data.ErrorReporter.reportError( new UnmappedWheelError( steering.right_suspension() ) );
        return false;
      }

      if ( !m_mappedWheels.TryGetValue( steering.left_suspension(), out leftWheel ) ) {
        Data.ErrorReporter.reportError( new UnmappedWheelError( steering.left_suspension() ) );
        return false;
      }

      steeringComp.RightWheel = rightWheel;
      steeringComp.LeftWheel = leftWheel;

      steeringComp.Mechanism = mechanism.Value;

      if ( steering is Steering.Kinematic.Ackermann ackermann ) {
        steeringComp.Phi0 = (float)ackermann.knuckle_angle();
        steeringComp.L    = (float)ackermann.knuckle_length();
      }
      else if ( steering is Steering.Kinematic.BellCrank bellCrank ) {
        steeringComp.Phi0   = (float)bellCrank.knuckle_angle();
        steeringComp.L      = (float)bellCrank.knuckle_length();
        steeringComp.Lc     = (float)bellCrank.steering_column_distance();
        steeringComp.Gear   = (float)bellCrank.gear();
        steeringComp.Alpha0 = (float)bellCrank.initial_angle_right_tie_rod();
      }
      else if ( steering is Steering.Kinematic.RackAndPinion rackPinion ) {
        steeringComp.Phi0   = (float)rackPinion.knuckle_angle();
        steeringComp.L      = (float)rackPinion.knuckle_length();
        steeringComp.Lc     = (float)rackPinion.steering_column_distance();
        steeringComp.Lr     = (float)rackPinion.rack_length();
        steeringComp.Gear   = (float)rackPinion.gear();
        steeringComp.Alpha0 = (float)rackPinion.initial_angle_right_tie_rod();
      }

      return true;
    }
  }
}
