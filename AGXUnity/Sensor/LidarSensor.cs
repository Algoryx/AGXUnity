using agx;
using agxModel;
using agxSensor;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Sensor
{
  public interface IModelData { }

  [Serializable]
  public class OusterData : IModelData
  {
    public LidarModelOusterOS.ChannelCount ChannelCount = LidarModelOusterOS.ChannelCount.ch_32;
    public LidarModelOusterOS.BeamSpacing BeamSpacing = LidarModelOusterOS.BeamSpacing.Uniform;
    public LidarModelOusterOS.LidarMode LidarMode = LidarModelOusterOS.LidarMode.Mode_512x20;
  }

  [Serializable]
  public class GenericSweepData : IModelData
  {
    public float Frequency = 10.0f;
    public float HorizontalFoV = 360.0f;
    public float VerticalFoV = 35.0f;
    public float HorizontalResolution = 0.5f;
    public float VerticalResolution = 0.5f;
  }

  public enum LidarModelPreset
  {
    NONE,
    LidarModelGeneric360HorizontalSweep,
    LidarModelOusterOS0,
    LidarModelOusterOS1,
    LidarModelOusterOS2
  }

  /// <summary>
  /// WIP component for lidar sensor
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/Lidar Sensor" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSensor : ScriptComponent
  {
    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public Lidar Native { get; private set; } = null;

    /**
    * The Model, or preset, of this Lidar.
    * Changing this will assign Model specific properties to this Lidar.
    */
    [SerializeField]
    private LidarModelPreset m_lidarModelPreset = LidarModelPreset.LidarModelOusterOS1;

    public LidarModelPreset LidarModelPreset
    {
      get => m_lidarModelPreset;
      set
      {
        m_lidarModelPreset = value;
        ModelData = value switch
        {
          LidarModelPreset.LidarModelOusterOS0 => new OusterData(),
          LidarModelPreset.LidarModelOusterOS1 => new OusterData(),
          LidarModelPreset.LidarModelOusterOS2 => new OusterData(),
          LidarModelPreset.LidarModelGeneric360HorizontalSweep => new GenericSweepData(),
          LidarModelPreset.NONE => null,
        };
      }
    }


    [field: SerializeReference]
    public IModelData ModelData { get; private set; } = new OusterData();

    /**
    * The minimum and maximum range of the Lidar Sensor [m].
    * Objects outside this range will not be detected by this Lidar Sensor.
    */
    public RangeReal LidarRange = new RangeReal(0.1f, float.MaxValue);

    /**
    * Divergence of the lidar laser light beam [deg].
    * This the total "cone angle", i.e. the angle between a perfectly parallel beam of the same
    * exit dimater to the cone surface is half this angle.
    * This property affects the calculated intensity.
    */
    [ClampAboveZeroInInspector]
    public float BeamDivergence = 0.001f * 180f / Mathf.PI;

    /**
    * The diameter of the lidar laser light beam as it exits the lidar [m].
    * This property affects the calculated intensity.
    */
    public float BeamExitRadius = 0.005f;

    /**
    * Determines the number of maximum raytrace steps.
    * The number of steps is one more than the number of bounces a ray will make,
    * however, this number will generally not affect the size of the output data.
    * It should be noted that the time and memory complexity of the raytrace will grow
    * exponentially with the maximum number of raytrace steps.
    */
    [Min(1)]
    public int RayTraceDepth = 1;

    /**
	  * Enables or disables distance gaussian noise, adding an individual distance error to each
	  * measurements of Position.
	  */
    public bool DistanceGaussianNoiseEnabled = false;

    /**
	   * Determines the distance noise characteristics. The standard deviation is calculated as
	  * s = stdDev + d * stdDevSlope where d is the distance in centimeters.
	  */
    public GaussianFunctionSettings DistanceGaussianNoiseSettings = new GaussianFunctionSettings();

    /**
    * Enables or disables angle ray gaussian noise, adding an individual angle error to each lidar
    * ray.
    */
    public bool RayAngleGaussianNoiseEnabled = false;

    /**
	  * Determines the lidar ray noise characteristics.
	  */
    public GaussianFunctionSettings RayAngleGaussianNoiseSettings = new GaussianFunctionSettings();
    // TODO check type of above, custom editor usw

    /**
	  * Discard rays reaching max range or not.
	  */
    private bool m_setEnableRemoveRayMisses = true;

    public bool SetEnableRemoveRayMisses
    {
      get => m_setEnableRemoveRayMisses;
      set
      {
        m_setEnableRemoveRayMisses = value;
        if ( Native != null )
          Native.getOutputHandler().setEnableRemoveRayMisses( value );
      }
    }

    [SerializeField]
    private List<LidarOutput> m_outputs = new List<LidarOutput>();

    public void UpdateTransform()
    {
      Native.setFrame( new agx.Frame(
                          new AffineMatrix4x4(
                            ( transform.rotation ).ToHandedQuat(),
                            transform.position.ToHandedVec3() ) ) );
    }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      var model = CreateLidarModel(LidarModelPreset);
      if ( model == null )
        return false;

      Native = new Lidar( null, model ); // Note: Use default position in order to have the rays be created 
      Native.getOutputHandler().setEnableRemoveRayMisses( SetEnableRemoveRayMisses );

      foreach ( var output in m_outputs )
        output.Initialize( this );

      Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += UpdateTransform;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    protected override void OnEnable()
    {
      Native?.setEnable( true );
    }

    protected override void OnDisable()
    {
      Native?.setEnable( false );
    }

    public void Add( LidarOutput output )
    {
      if ( !m_outputs.Contains( output ) )
        m_outputs.Add( output );
      if ( Native != null )
        output.Initialize( this );
    }

    public void Remove( LidarOutput output )
    {
      if ( m_outputs.Contains( output ) )
        m_outputs.Remove( output );
      if ( Native != null )
        Native.getOutputHandler().removeChild( output.Native );
    }
    protected override void OnDestroy()
    {
      if ( SensorEnvironment.HasInstance ) {
        SensorEnvironment.Instance.Native?.remove( Native );
      }

      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= UpdateTransform;
      }

      base.OnDestroy();
    }

    private LidarModel CreateLidarModel( LidarModelPreset preset )
    {
      LidarModel lidarModel = null;

      switch ( preset ) {
        case LidarModelPreset.LidarModelGeneric360HorizontalSweep:
          GenericSweepData sweepData = ModelData as GenericSweepData;
          lidarModel = new LidarModelHorizontalSweep(
            Mathf.Deg2Rad * new agx.Vec2( sweepData.HorizontalFoV, sweepData.VerticalFoV ),
            Mathf.Deg2Rad * new agx.Vec2( sweepData.HorizontalResolution, sweepData.VerticalResolution ),
            sweepData.Frequency
          ); // TODO Default frequency for now, implement lidar settings
          break;

        case LidarModelPreset.LidarModelOusterOS0:
          OusterData ousterData = ModelData as OusterData;
          lidarModel = new LidarModelOusterOS0( ousterData.ChannelCount, ousterData.BeamSpacing, ousterData.LidarMode );
          break;

        case LidarModelPreset.LidarModelOusterOS1:
          ousterData = ModelData as OusterData;
          lidarModel = new LidarModelOusterOS1( ousterData.ChannelCount, ousterData.BeamSpacing, ousterData.LidarMode );
          break;

        case LidarModelPreset.LidarModelOusterOS2:
          ousterData = ModelData as OusterData;
          lidarModel = new LidarModelOusterOS2( ousterData.ChannelCount, ousterData.BeamSpacing, ousterData.LidarMode );
          break;

        case LidarModelPreset.NONE:
        default:
          Debug.LogWarning( "No valid LidarModelPreset selected!" );
          break;
      }

      UpdateLidarProperties();

      return lidarModel;
    }
    private void UpdateLidarProperties()
    {
      // TODO only update stuff that we want overridable with this lidar... But have to decide on that interface


    }
  }
}