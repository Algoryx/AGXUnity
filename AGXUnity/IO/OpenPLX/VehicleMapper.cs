using AGXUnity.IO.OpenPLX;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using openplx.Vehicles.Tracks;
using System.Collections.Generic;
using UnityEngine;

using Tracks = openplx.Vehicles.Tracks;

namespace AGXUnity.IO.OpenPLX
{
  public class VehicleMapper
  {
    private MapperData Data;

    private Dictionary<openplx.Vehicles.Suspensions.Interactions.Mate, WheelJoint> m_mappedWheels = new Dictionary<openplx.Vehicles.Suspensions.Interactions.Mate, WheelJoint> ();

    public VehicleMapper( MapperData cache )
    {
      Data = cache;
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

      var track_hinge_compliance_annots = track_system.findAnnotations("agx_track_hinge_compliance");
      if ( track_hinge_compliance_annots.Count != 0 ) {
        if ( track_hinge_compliance_annots[ 0 ].isNumber() ) {
          track_props.HingeComplianceRotational = Vector2.one * (float)track_hinge_compliance_annots[ 0 ].asReal();
          track_props.HingeComplianceTranslational = Vector2.one * (float)track_hinge_compliance_annots[ 0 ].asReal();
        }
      }

      var track_hinge_relaxation_time = track_system.findAnnotations("agx_track_hinge_relaxation_time");
      if ( track_hinge_relaxation_time.Count != 0 ) {
        if ( track_hinge_relaxation_time[ 0 ].isNumber() ) {
          track_props.HingeDampingRotational = Vector2.one * (float)track_hinge_relaxation_time[ 0 ].asReal();
          track_props.HingeDampingTranslational = Vector2.one * (float)track_hinge_relaxation_time[ 0 ].asReal();
        }
      }

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

    public GameObject MapSuspension( openplx.Vehicles.Suspensions.Interactions.Mate suspension )
    {
      return suspension switch
      {
        openplx.Vehicles.Suspensions.Interactions.LinearSpringDamperMate linSpring => MapLinearSpringDamperMate( linSpring ),
        _ => Utils.ReportUnimplemented<GameObject>( suspension, Data.ErrorReporter )
      };
    }

    public GameObject MapLinearSpringDamperMate( openplx.Vehicles.Suspensions.Interactions.LinearSpringDamperMate linearSpring )
    {
      RigidBody chassis = null;
      RigidBody wheel = null;
      var parent = linearSpring.chassis_connector().redirected_parent();

      if ( parent == null )
        return null;

      if ( Data.BodyCache.ContainsKey( parent ) )
        chassis = Data.BodyCache[ parent ];
      else {
        Data.ErrorReporter.reportError( new InvalidWheelChassisError( linearSpring ) );
        return null;
      }

      var wheelParent = linearSpring.attachment_connector().getOwner();

      if ( wheelParent is openplx.Physics3D.Bodies.RigidBody wheelBody )
        wheel = Data.BodyCache[ wheelBody ];
      else if ( wheelParent is openplx.Vehicles.Wheels.Wheel wheelModel ) {
        wheel = Data.BodyCache.GetValueOrDefault( wheelModel.rim() );
      }

      if ( wheel == null ) {
        Data.ErrorReporter.reportError( new MissingWheelBodyError( linearSpring ) );
        return null;
      }

      // OpenPLX assumes axes N = Wheel and U = Steering, in agx N = Steering, V = Steering is used. Apply a local rotation to correct for this from the precalculated frame.
      Quaternion frameCorrection = Quaternion.Euler( 0, -90, 90);
      var chassisFrame    = new ConstraintFrame( Data.MateConnectorCache[ linearSpring.chassis_connector() ], Vector3.zero, frameCorrection );
      var wheelFrame      = new ConstraintFrame( Data.MateConnectorCache[ linearSpring.attachment_connector() ], Vector3.zero, frameCorrection );

      var wheelJoint = WheelJoint.Create(wheelFrame, chassisFrame);
      if ( !linearSpring.getOwner().hasTrait( "Vehicles.Suspensions.Traits.Steering" ) )
        wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Steering ).Enable = true;

      if ( linearSpring.hasTrait( "Vehicles.Suspensions.Traits.SpringDamper" ) ) {
        var damping = InteractionMapper.MapDissipation( linearSpring.spring_damping(), linearSpring.spring_constant() );
        if ( damping.HasValue )
          wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Suspension ).Damping = damping.Value;
        var compliance = InteractionMapper.MapFlexibility(linearSpring.spring_constant());
        if ( compliance.HasValue )
          wheelJoint.GetController<LockController>( WheelJoint.WheelDimension.Suspension ).Compliance = compliance.Value;
      }

      wheelJoint.enabled = linearSpring.enabled();

      Data.RegisterOpenPLXObject( linearSpring.getName(), wheelJoint.gameObject );

      m_mappedWheels[ linearSpring ] = wheelJoint;

      return wheelJoint.gameObject;
    }

    public GameObject MapSteering( openplx.Vehicles.Steering.Interactions.DualSuspensionSteering steering )
    {
      Steering.SteeringMechanism? mechanism = steering switch
      {
        openplx.Vehicles.Steering.Interactions.Ackermann => Steering.SteeringMechanism.Ackermann,
        openplx.Vehicles.Steering.Interactions.BellCrank => Steering.SteeringMechanism.BellCrank,
        openplx.Vehicles.Steering.Interactions.RackAndPinion => Steering.SteeringMechanism.RackPinion,
        _ => null
      };

      if ( !mechanism.HasValue )
        return Utils.ReportUnimplemented<GameObject>( steering, Data.ErrorReporter );

      var steeringGO = Data.CreateOpenPLXObject(steering.getName());
      var steeringComp = steeringGO.AddComponent<Steering>();

      WheelJoint rightWheel, leftWheel;

      if ( !m_mappedWheels.TryGetValue( steering.right_suspension().mate(), out rightWheel ) ) {
        Data.ErrorReporter.reportError( new UnmappedWheelError( steering.right_suspension() ) );
        return null;
      }

      if ( !m_mappedWheels.TryGetValue( steering.left_suspension().mate(), out leftWheel ) ) {
        Data.ErrorReporter.reportError( new UnmappedWheelError( steering.left_suspension() ) );
        return null;
      }

      steeringComp.RightWheel = rightWheel;
      steeringComp.LeftWheel = leftWheel;

      steeringComp.Mechanism = mechanism.Value;

      steeringComp.Phi0 = (float)steering.knuckle_angle();
      steeringComp.L = (float)steering.knuckle_length();

      if ( steering is openplx.Vehicles.Steering.Interactions.BellCrank bellCrank ) {
        steeringComp.Lc = (float)bellCrank.steering_column_distance();
        steeringComp.Gear = (float)bellCrank.gear();
        steeringComp.Alpha0 = (float)bellCrank.initial_angle_right_tie_rod();
      }
      else if ( steering is openplx.Vehicles.Steering.Interactions.RackAndPinion rackPinion ) {
        steeringComp.Lc = (float)rackPinion.steering_column_distance();
        steeringComp.Lr = (float)rackPinion.rack_length();
        steeringComp.Gear = (float)rackPinion.gear();
        steeringComp.Alpha0 = (float)rackPinion.initial_angle_right_tie_rod();
      }

      return steeringGO;
    }
  }
}
