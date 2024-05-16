using AGXUnity.IO.BrickIO;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using Brick.Vehicles.Tracks;
using UnityEngine;

using Tracks = Brick.Vehicles.Tracks;

public class TrackMapper
{
  private MapperData Data;

  public TrackMapper( MapperData cache )
  {
    Data = cache;
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

    Debug.LogWarning( $"Unknown variation type {variation.getType().getName()}: {variation.getName()}" );
    return null;
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

      var position = wheel.local_transform().position().ToHandedVector3();
      var rotation = wheel.local_transform().rotation().ToHandedQuaternion();

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
        Data.ErrorReporter.Report( box_description.contact_geometry().local_transform().position(), AgxUnityBrickErrors.LocalOffsetNotSupported );
      default_width  = (float)box_description.width();
      default_height = (float)box_description.height();

      track.ThicknessVariation = MapVariation( box_description.variation().height_variation() );
      track.WidthVariation = MapVariation( box_description.variation().width_variation() );
    }

    track.Width = default_width;
    track.Thickness = default_height;

    //// Internal merge properties
    //auto merge_props = track->getInternalMergeProperties();
    //// TODO: These should come from an AGX bundle not annotations
    //auto model_decl = track_system->getType();
    //auto enable_merge_annots = model_decl->findAnnotations("agx_set_enable_merge");
    //if ( !enable_merge_annots.empty() ) {
    //  for ( auto& annot : enable_merge_annots) {
    //    if ( annot->isTrue() ) {
    //      merge_props->setEnableMerge( true );
    //      break;
    //    }
    //    if ( annot->isFalse() ) {
    //      merge_props->setEnableMerge( false );
    //      break;
    //    }
    //  }
    //}

    //auto contact_reduction_annots = model_decl->findAnnotations("agx_set_contact_reduction");
    //if ( !contact_reduction_annots.empty() ) {
    //  for ( auto& annot : contact_reduction_annots) {
    //    if ( annot->isString( "NONE" ) ) {
    //      merge_props->setContactReduction( agxVehicle::TrackInternalMergeProperties::ContactReduction::NONE );
    //      break;
    //    }
    //    if ( annot->isString( "MINIMAL" ) ) {
    //      merge_props->setContactReduction( agxVehicle::TrackInternalMergeProperties::ContactReduction::MINIMAL );
    //      break;
    //    }
    //    if ( annot->isString( "MODERATE" ) ) {
    //      merge_props->setContactReduction( agxVehicle::TrackInternalMergeProperties::ContactReduction::MODERATE );
    //      break;
    //    }
    //    if ( annot->isString( "AGGRESSIVE" ) ) {
    //      merge_props->setContactReduction( agxVehicle::TrackInternalMergeProperties::ContactReduction::AGGRESSIVE );
    //      break;
    //    }
    //  }
    //}

    //auto enable_lock_to_reach_mc_annots = model_decl->findAnnotations("agx_set_enable_lock_to_reach_merge_condition");
    //if ( !enable_lock_to_reach_mc_annots.empty() ) {
    //  for ( auto& annot : enable_lock_to_reach_mc_annots) {
    //    if ( annot->isTrue() ) {
    //      merge_props->setEnableLockToReachMergeCondition( true );
    //      break;
    //    }
    //    if ( annot->isFalse() ) {
    //      merge_props->setEnableLockToReachMergeCondition( false );
    //      break;
    //    }
    //  }
    //}

    //auto set_lock_to_reach_mc_compliance_annots = model_decl->findAnnotations("agx_set_lock_to_reach_merge_condition_compliance");
    //if ( !set_lock_to_reach_mc_compliance_annots.empty() ) {
    //  for ( auto& annot : set_lock_to_reach_mc_compliance_annots) {
    //    if ( annot->isNumber() ) {
    //      merge_props->setLockToReachMergeConditionCompliance( annot->asReal() );
    //      break;
    //    }
    //  }
    //}

    //auto set_lock_to_reach_mc_damping_annots = model_decl->findAnnotations("agx_set_lock_to_reach_merge_condition_damping");
    //if ( !set_lock_to_reach_mc_damping_annots.empty() ) {
    //  for ( auto& annot : set_lock_to_reach_mc_damping_annots) {
    //    if ( annot->isNumber() ) {
    //      merge_props->setLockToReachMergeConditionDamping( annot->asReal() );
    //      break;
    //    }
    //  }
    //}

    //auto set_num_nodes_per_ms_annots = model_decl->findAnnotations("agx_set_num_nodes_per_merge_segment");
    //if ( !set_num_nodes_per_ms_annots.empty() ) {
    //  for ( auto& annot : set_num_nodes_per_ms_annots) {
    //    if ( annot->isNumber() ) {
    //      merge_props->setNumNodesPerMergeSegment( static_cast<agx::UInt>( annot->asReal() ) );
    //      break;
    //    }
    //  }
    //}

    //return track;

    return;
  }
}
