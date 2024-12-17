using System.Collections;
using UnityEngine;
using AGXUnity.Utils;
using System.Linq;
using System.Collections.Generic;
using agxSensor;
using agx;
using agxCollide;
using UnityEngine.Rendering;
using agxModel;
using UnityEditor;
using AGXUnity.Rendering;

namespace AGXUnity.Sensor
{
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
    public LidarModelPreset LidarModelPreset = LidarModelPreset.LidarModelOusterOS1;

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

    private RtOutputVec4f m_rtOutput = null;

    private SensorEnvironment m_sensorEnvironment = null;

    private uint m_outputID = 0; // Must be greater than 0 to be valid

    private static Quaternion m_agxToUnityRotation = Quaternion.Euler(90, 0, 0);

    public void UpdateTransform()
    {
      Native.setFrame(new agx.Frame(
                          new AffineMatrix4x4(
                            (m_agxToUnityRotation * transform.rotation).ToHandedQuat(),
                            transform.position.ToHandedVec3())));
    }

    LidarPointCloudRenderer m_pointCloudRenderer = null;
    protected override bool Initialize()
    {
      return true;
    }

    public void RegisterRenderer()
    {
      if (m_pointCloudRenderer == null)
        m_pointCloudRenderer = GetComponent<LidarPointCloudRenderer>();
    }

    public bool InitializeLidar(SensorEnvironment sensorEnvironment, uint uniqueId)
    {
      m_sensorEnvironment = sensorEnvironment;
      m_outputID = uniqueId;

      if (m_outputID < 1)
        Debug.LogError("Output ID can't be 0");

      var model = CreateLidarModel(LidarModelPreset);
      if (model == null)
        return false;

      Native = new Lidar(null, model); // Note: Use default position in order to have the rays be created 
      UpdateTransform();
      //Native.getOutputHandler().setEnableRemoveRayMisses(true);

      // TODO Temp way of defining output
      m_rtOutput = new RtOutputVec4f();
      m_rtOutput.add(RtOutput.Field.XYZ_VEC3_F32);
      m_rtOutput.add(RtOutput.Field.INTENSITY_F32);

      Native.getOutputHandler().add(m_outputID, m_rtOutput);

      Simulation.Instance.StepCallbacks.PostStepForward += ProcessOutput;

      return true;
    }

    protected override void OnDestroy()
    {
      if (Simulation.HasInstance)
      {
        Simulation.Instance.StepCallbacks.PostStepForward -= ProcessOutput;
      }

      m_rtOutput.Dispose();
      m_rtOutput = null;

      Native = null;

      base.OnDestroy();
    }

    private void ProcessOutput()
    {
      if (Native == null)
        return;

      if (m_rtOutput != null && m_rtOutput.hasUnreadData())
      {
        var view = m_rtOutput.view();
        if (m_pointCloudRenderer != null)
          m_pointCloudRenderer.SetData(view);
      }
    }

    private LidarModel CreateLidarModel(LidarModelPreset preset)
    {
      LidarModel lidarModel = null;

      switch (preset)
      {
        case LidarModelPreset.LidarModelGeneric360HorizontalSweep:
          lidarModel = new LidarModelGeneric360HorizontalSweep(10f); // TODO Default frequency for now, implement lidar settings
          break;

        case LidarModelPreset.LidarModelOusterOS0:
          lidarModel = new LidarModelOusterOS0();
          break;

        case LidarModelPreset.LidarModelOusterOS1:
          lidarModel = new LidarModelOusterOS1();
          break;

        case LidarModelPreset.LidarModelOusterOS2:
          lidarModel = new LidarModelOusterOS2();
          break;

        case LidarModelPreset.NONE:
        default:
          Debug.LogWarning("No valid LidarModelPreset selected!");
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