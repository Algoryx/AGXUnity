using AGXUnity.IO.OpenPLX;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using openplx.Physics3D.Interactions;
using openplx.Vehicles.Tracks;
using System.Collections.Generic;
using UnityEngine;

using Tracks = openplx.Vehicles.Tracks;

namespace AGXUnity.IO.OpenPLX
{
  public class VehicleMapper
  {
    private MapperData Data;

    private Dictionary<openplx.Vehicles.Suspensions.SingleMate.Base, WheelJoint> m_mappedWheels = new Dictionary<openplx.Vehicles.Suspensions.SingleMate.Base, WheelJoint> ();

    public VehicleMapper( MapperData cache )
    {
      Data = cache;
    }

    public bool HandledInteraction( openplx.Physics.Interactions.Interaction interaction )
    {
      if ( interaction is openplx.Vehicles.Steering.Kinematic.Interactions.Base )
        return true;
      if ( interaction.getOwner() is openplx.Vehicles.Suspensions.SingleMate.Base susp && ( interaction == susp.mate() || interaction == susp.range() ) )
        return true;
      return false;
    }

    public void MapElasticWheel( openplx.Vehicles.Wheels.ElasticWheel wheel )
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

    private TrackNodeVariation MapVariation( CyclicVariation variation )
    {
      if ( variation is Tracks::SinusoidalVariation sin )
        return new AGXUnity.Model.SinusoidalVariation( (float)sin.additional_amplitude(), (float)sin.period() );
      if ( variation is Tracks::DiscretePulseVariation disc )
        return new AGXUnity.Model.DiscretePulseVariation( (float)disc.additional_amplitude(), (int)disc.discrete_period() );
      if ( variation.GetType() == typeof( CyclicVariation ) )
        return null;
      return Utils.ReportUnimplemented<TrackNodeVariation>( variation, Data.ErrorReporter );
    }

    public void MapTrackSystem( Tracks.System system )
    {
      GameObject s = Data.SystemCache[system];

      if ( system.belt() is not Tracks.FixedLinkCountBelt belt )
        return;

      var track = s.AddComponent<Track>();
      s.AddComponent<TrackRenderer>();
      track.NumberOfNodes = (int)belt.link_count();
      track.InitialTensionDistance = (float)system.initial_distance_tension();

      foreach ( var wheel in system.road_wheels() ) {
        if ( wheel is not Tracks.CylindricalRoadWheel cylindrical ) {
          Debug.LogError( $"Road wheel {wheel.getName()} not inheriting from CylindricalRoadWheel, not supported." );
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
          CylindricalSprocket => TrackWheelModel.Sprocket,
          CylindricalIdler => TrackWheelModel.Idler,
          CylindricalRoller => TrackWheelModel.Roller,
          _ => null
        };

        if ( type == null ) {
          Debug.LogError( $"Road wheel type {wheel.getType().getName()} is not supported: {cylindrical.body().getName()}" );
          continue;
        }

        if ( !Data.BodyCache.ContainsKey( cylindrical.body() ) ) {
          Debug.LogError( $"Could not find mapped body of Cylindrical {type}: {cylindrical.body().getName()}" );
          continue;
        }

        var parent = Data.BodyCache[cylindrical.body()];
        track.Add( CreateTrackWheel( type.Value, (float)cylindrical.radius(), parent.gameObject, position, rotation ) );
      }

      float default_width = 0.1f;
      float default_height = 0.1f;
      if ( belt.link_description() is BoxLinkDescription box_description ) {
        if ( box_description.contact_geometry().local_transform().position().length() > 0.0001f )
          Data.ErrorReporter.reportError( new LocalOffsetNotSupportedError( box_description.contact_geometry().local_transform().position() ) );
        default_width  = (float)box_description.width();
        default_height = (float)box_description.height();

        track.ThicknessVariation = MapVariation( box_description.variation().height_variation() );
        track.WidthVariation = MapVariation( box_description.variation().width_variation() );

        track.Material = Data.MaterialCache[ box_description.contact_geometry().material() ];
      }

      track.Width = default_width;
      track.Thickness = default_height;

      MapInternalMergeProperties( system, track );
      MapTrackProperties( system, track );

      return;
    }

    private void MapInternalMergeProperties( Tracks.System track_system, Track track )
    {
      //var merge_props = track.getInternalMergeProperties();
      var merge_props = ScriptableObject.CreateInstance<TrackInternalMergeProperties>();

      var enable_merge_annots = track_system.findAnnotations("agx_set_enable_merge");
      if ( enable_merge_annots.Count != 0 ) {
        foreach ( var annot in enable_merge_annots ) {
          if ( annot.isTrue() ) {
            merge_props.MergeEnabled = true;
            break;
          }
          if ( annot.isFalse() ) {
            merge_props.MergeEnabled = false;
            break;
          }
        }
      }

      var contact_reduction_annots = track_system.findAnnotations("agx_set_contact_reduction");
      if ( contact_reduction_annots.Count != 0 ) {
        foreach ( var annot in contact_reduction_annots ) {
          if ( annot.isString( "NONE" ) ) {
            merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.None;
            break;
          }
          if ( annot.isString( "MINIMAL" ) ) {
            merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Minimal;
            break;
          }
          if ( annot.isString( "MODERATE" ) ) {
            merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Moderate;
            break;
          }
          if ( annot.isString( "AGGRESSIVE" ) ) {
            merge_props.ContactReduction = TrackInternalMergeProperties.ContactReductionMode.Aggressive;
            break;
          }
        }
      }

      var enable_lock_to_reach_mc_annots = track_system.findAnnotations("agx_set_enable_lock_to_reach_merge_condition");
      if ( enable_lock_to_reach_mc_annots.Count != 0 ) {
        foreach ( var annot in enable_lock_to_reach_mc_annots ) {
          if ( annot.isTrue() ) {
            merge_props.LockToReachMergeConditionEnabled = true;
            break;
          }
          if ( annot.isFalse() ) {
            merge_props.LockToReachMergeConditionEnabled = false;
            break;
          }
        }
      }

      var set_lock_to_reach_mc_compliance_annots = track_system.findAnnotations("agx_set_lock_to_reach_merge_condition_compliance");
      if ( set_lock_to_reach_mc_compliance_annots.Count != 0 ) {
        foreach ( var annot in set_lock_to_reach_mc_compliance_annots ) {
          if ( annot.isNumber() ) {
            merge_props.LockToReachMergeConditionCompliance = (float)annot.asReal();
            break;
          }
        }
      }

      var set_lock_to_reach_mc_relaxation_time = track_system.findAnnotations("agx_set_lock_to_reach_merge_condition_relaxation_time");
      if ( set_lock_to_reach_mc_relaxation_time.Count != 0 ) {
        foreach ( var annot in set_lock_to_reach_mc_relaxation_time ) {
          if ( annot.isNumber() ) {
            merge_props.LockToReachMergeConditionDamping =(float)annot.asReal();
            break;
          }
        }
      }

      var set_num_nodes_per_ms_annots = track_system.findAnnotations("agx_set_num_nodes_per_merge_segment");
      if ( set_num_nodes_per_ms_annots.Count != 0 ) {
        foreach ( var annot in set_num_nodes_per_ms_annots ) {
          if ( annot.isNumber() ) {
            merge_props.NumNodesPerMergeSegment = (int)annot.asReal();
            break;
          }
        }
      }

      var set_max_angle_merge_condition = track_system.findAnnotations("agx_set_max_angle_merge_condition");
      if ( set_max_angle_merge_condition.Count != 0 ) {
        foreach ( var annot in set_max_angle_merge_condition ) {
          if ( annot.isNumber() ) {
            merge_props.MaxAngleMergeCondition = (float)annot.asReal();
            break;
          }
        }
      }

      merge_props.name = track_system.getName() + "_MP";
      track.InternalMergeProperties = merge_props;
      Data.MappedTrackInternalMergeProperties.Add( merge_props );
    }

    private void MapTrackProperties( Tracks.System track_system, Track track )
    {
      //var track_props = track.getProperties();
      var track_props = ScriptableObject.CreateInstance<TrackProperties>();

      var node_wheel_overlap_annots = track_system.findAnnotations("agx_track_node_wheel_overlap");
      if ( node_wheel_overlap_annots.Count != 0 ) {
        if ( node_wheel_overlap_annots[ 0 ].isNumber() ) {
          track_props.TransformNodesToWheelsOverlap = (float)node_wheel_overlap_annots[ 0 ].asReal();
        }
      }

      var track_on_initialize_merge_nodes_to_wheels = track_system.findAnnotations("agx_track_on_initialize_merge_nodes_to_wheels");
      if ( track_on_initialize_merge_nodes_to_wheels.Count != 0 ) {
        foreach ( var annot in track_on_initialize_merge_nodes_to_wheels ) {
          if ( annot.isTrue() ) {
            track_props.OnInitializeMergeNodesToWheelsEnabled = true;
            break;
          }
          if ( annot.isFalse() ) {
            track_props.OnInitializeMergeNodesToWheelsEnabled = false;
            break;
          }
        }
      }

      var track_on_initialize_transform_nodes_to_wheels = track_system.findAnnotations("agx_track_on_initialize_transform_nodes_to_wheels");
      if ( track_on_initialize_transform_nodes_to_wheels.Count != 0 ) {
        foreach ( var annot in track_on_initialize_transform_nodes_to_wheels ) {
          if ( annot.isTrue() ) {
            track_props.OnInitializeTransformNodesToWheelsEnabled = true;
            break;
          }
          if ( annot.isFalse() ) {
            track_props.OnInitializeTransformNodesToWheelsEnabled = false;
            break;
          }
        }
      }

      var track_hinge_range_lower_annots = track_system.findAnnotations("agx_track_hinge_range_lower");
      var track_hinge_range_upper_annots = track_system.findAnnotations("agx_track_hinge_range_upper");
      if ( track_hinge_range_lower_annots.Count != 0 || track_hinge_range_upper_annots.Count != 0 ) {
        track_props.HingeRangeEnabled = true;
        double min = -Mathf.Infinity;
        double max = Mathf.Infinity;
        if ( track_hinge_range_lower_annots[ 0 ].isNumber() ) {
          min = track_hinge_range_lower_annots[ 0 ].asReal() * 180.0/Mathf.PI;
        }
        if ( track_hinge_range_upper_annots[ 0 ].isNumber() ) {
          max = track_hinge_range_upper_annots[ 0 ].asReal() * 180.0/Mathf.PI;
        }
        track_props.HingeRangeRange = new AGXUnity.RangeReal( (float)min, (float)max );
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

      var track_stabilizing_friction_parameter = track_system.findAnnotations("agx_track_stabilizing_friction_parameter");
      if ( track_stabilizing_friction_parameter.Count != 0 ) {
        if ( track_stabilizing_friction_parameter[ 0 ].isNumber() ) {
          track_props.StabilizingHingeFrictionParameter = (float)track_stabilizing_friction_parameter[ 0 ].asReal();
        }
      }

      var track_min_stabilizing_normal_force = track_system.findAnnotations("agx_track_min_stabilizing_normal_force");
      if ( track_min_stabilizing_normal_force.Count != 0 ) {
        if ( track_min_stabilizing_normal_force[ 0 ].isNumber() ) {
          track_props.MinStabilizingHingeNormalForce = (float)track_min_stabilizing_normal_force[ 0 ].asReal();
        }
      }

      var track_node_wheel_merge_threshold = track_system.findAnnotations("agx_track_node_wheel_merge_threshold");
      if ( track_node_wheel_merge_threshold.Count != 0 ) {
        if ( track_node_wheel_merge_threshold[ 0 ].isNumber() ) {
          track_props.NodesToWheelsMergeThreshold = (float)track_node_wheel_merge_threshold[ 0 ].asReal();
        }
      }
      var track_node_wheel_split_threshold = track_system.findAnnotations("agx_track_node_wheel_split_threshold");
      if ( track_node_wheel_split_threshold.Count != 0 ) {
        if ( track_node_wheel_split_threshold[ 0 ].isNumber() ) {
          track_props.NodesToWheelsSplitThreshold = (float)track_node_wheel_split_threshold[ 0 ].asReal();
        }
      }

      var track_num_nodes_in_average_direction = track_system.findAnnotations("agx_track_num_nodes_in_average_direction");
      if ( track_num_nodes_in_average_direction.Count != 0 ) {
        if ( track_num_nodes_in_average_direction[ 0 ].isNumber() ) {
          track_props.NumNodesIncludedInAverageDirection = (int)track_num_nodes_in_average_direction[ 0 ].asReal();
        }
      }

      track_props.name = track_system.getName() + "_TP";
      track.Properties = track_props;
      Data.MappedTrackProperties.Add( track_props );
    }

    public bool MapSingleMateSuspensionOnto( openplx.Vehicles.Suspensions.SingleMate.Base suspension, GameObject onto )
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
      else if ( wheelParent is openplx.Vehicles.Wheels.Base wheelModel )
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
      if ( !suspension.HasTrait<openplx.Vehicles.Suspensions.Properties.Steering>() )
        wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Steering ).Enable = true;

      if ( suspension is openplx.Vehicles.Suspensions.SingleMate.LinearSpringDamper lsd ) {
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

    public bool MapSteeringOnto( openplx.Vehicles.Steering.Kinematic.Base steering, GameObject onto )
    {
      Steering.SteeringMechanism? mechanism = steering switch
      {
        openplx.Vehicles.Steering.Kinematic.Ackermann => Steering.SteeringMechanism.Ackermann,
        openplx.Vehicles.Steering.Kinematic.BellCrank => Steering.SteeringMechanism.BellCrank,
        openplx.Vehicles.Steering.Kinematic.RackAndPinion => Steering.SteeringMechanism.RackPinion,
        _ => null
      };

      if ( !mechanism.HasValue ) {
        Data.ErrorReporter.reportError( new UnimplementedError( steering ) );
        return false;
      }

      var steeringComp = onto.AddComponent<Steering>();
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

      if ( steering is openplx.Vehicles.Steering.Kinematic.Ackermann ackermann ) {
        steeringComp.Phi0 = (float)ackermann.knuckle_angle();
        steeringComp.L    = (float)ackermann.knuckle_length();
      }
      else if ( steering is openplx.Vehicles.Steering.Kinematic.BellCrank bellCrank ) {
        steeringComp.Phi0   = (float)bellCrank.knuckle_angle();
        steeringComp.L      = (float)bellCrank.knuckle_length();
        steeringComp.Lc     = (float)bellCrank.steering_column_distance();
        steeringComp.Gear   = (float)bellCrank.gear();
        steeringComp.Alpha0 = (float)bellCrank.initial_angle_right_tie_rod();
      }
      else if ( steering is openplx.Vehicles.Steering.Kinematic.RackAndPinion rackPinion ) {
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
