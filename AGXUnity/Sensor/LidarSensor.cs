using agx;
using agxSensor;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// IModelData is an empty interface to allow for a lidar sensor to hold generic data dependent on the
  /// underlying lidar model used.
  /// </summary>
  public interface IModelData { }

  [Serializable]
  public class OusterData : IModelData
  {
    /// <summary>
    /// The vertical resolution of the Ouster lidar.
    /// </summary>
    [Tooltip("The vertical resolution of the Ouster lidar.")]
    public LidarModelOusterOS.ChannelCount ChannelCount = LidarModelOusterOS.ChannelCount.ch_32;

    /// <summary>
    /// The spacing of the beams shot by the Ouster lidar.
    /// </summary>
    [Tooltip("The spacing of the beams shot by the Ouster lidar.")]
    public LidarModelOusterOS.BeamSpacing BeamSpacing = LidarModelOusterOS.BeamSpacing.Uniform;

    /// <summary>
    /// The operational mode of the Ouster lidar where (RxF) denotes a resolution of R points and a frequency of F Hz.
    /// </summary>
    [Tooltip("The operational mode of the Ouster lidar where (RxF) denotes a resolution of R points and a frequency of F Hz.")]
    public LidarModelOusterOS.LidarMode LidarMode = LidarModelOusterOS.LidarMode.Mode_512x20;
  }

  [Serializable]
  public class GenericSweepData : IModelData
  {
    /// <summary>
    /// The frequency [Hz] of the lidar sweep
    /// </summary>
    [Tooltip("The frequency [Hz] of the lidar sweep")]
    public float Frequency = 10.0f;

    /// <summary>
    /// The horizontal FoV [deg] of the lidar sweep
    /// </summary>
    [Tooltip("The horizontal FoV [deg] of the lidar sweep")]
    public float HorizontalFoV = 360.0f;

    /// <summary>
    /// The vertical FoV [deg] of the lidar sweep
    /// </summary>
    [Tooltip("The vertical FoV [deg] of the lidar sweep")]
    public float VerticalFoV = 35.0f;

    /// <summary>
    /// The horizontal resolution [deg per point] of the lidar sweep 
    /// </summary>
    [Tooltip("The Horizontal resolution [deg per point] of the lidar sweep ")]
    public float HorizontalResolution = 0.5f;

    /// <summary>
    /// The vertical resolution [deg per point] of the lidar sweep 
    /// </summary>
    [Tooltip("The vertical resolution [deg per point] of the lidar sweep ")]
    public float VerticalResolution = 0.5f;
  }


  [Serializable]
  public class LivoxData : IModelData
  {
    /// <summary>
    /// Optionally downsample the amount of points generated. 1 is default, 2 is sample every other point in the pattern, 3 every 3 points etc.
    /// </summary>
    [Tooltip("The vertical resolution of the Ouster lidar.")]
    [Min(1)]
    public uint Downsample = 1;
  }


  public enum LidarModelPreset
  {
    NONE,
    LidarModelGenericHorizontalSweep,
    LidarModelOusterOS0,
    LidarModelOusterOS1,
    LidarModelOusterOS2,
    LidarModelLivoxAvia,
    LidarModelLivoxHap,
  }

  /// <summary>
  /// WIP component for lidar sensor
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/Lidar Sensor" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#simulating-lidar-sensors" )]
  public class LidarSensor : ScriptComponent
  {
    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public Lidar Native { get; private set; } = null;
    public LidarModel m_nativeModel = null;

    [SerializeField]
    private LidarModelPreset m_lidarModelPreset = LidarModelPreset.LidarModelOusterOS1;

    /// <summary>
    /// The Model, or preset, of this Lidar.
    /// Changing this will assign Model specific properties to this Lidar.
    /// </summary>
    [Tooltip( "The Model, or preset, of this Lidar. " +
              "Changing this will assign Model specific properties to this Lidar." )]
    [DisableInRuntimeInspector]
    public LidarModelPreset LidarModelPreset
    {
      get => m_lidarModelPreset;
      set
      {
        if ( value != m_lidarModelPreset ) {
          ModelData = value switch
          {
            LidarModelPreset.LidarModelOusterOS0 => new OusterData(),
            LidarModelPreset.LidarModelOusterOS1 => new OusterData(),
            LidarModelPreset.LidarModelOusterOS2 => new OusterData(),
            LidarModelPreset.LidarModelGenericHorizontalSweep => new GenericSweepData(),
            LidarModelPreset.LidarModelLivoxAvia => new LivoxData(),
            LidarModelPreset.LidarModelLivoxHap => new LivoxData(),
            _ => null,
          };
        }
        m_lidarModelPreset = value;
      }
    }

    [field: SerializeReference]
    [DisableInRuntimeInspector]
    public IModelData ModelData { get; private set; } = new OusterData();

    /// <summary>
    /// Local sensor rotation relative to the parent GameObject transform.
    /// </summary>
    [Tooltip("Local sensor rotation relative to the parent GameObject transform.")]
    public Vector3 LocalRotation = Vector3.zero;

    /// <summary>
    /// Local sensor offset relative to the parent GameObject transform.
    /// </summary>
    [Tooltip("Local sensor offset relative to the parent GameObject transform.")]
    public Vector3 LocalPosition = Vector3.zero;

    /// <summary>
    /// The local transformation matrix from the sensor frame to the parent GameObject frame
    /// </summary>
    public UnityEngine.Matrix4x4 LocalTransform => UnityEngine.Matrix4x4.TRS( LocalPosition, Quaternion.Euler( LocalRotation ), Vector3.one );

    /// <summary>
    /// The global transformation matrix from the sensor frame to the world frame. 
    /// </summary>
    public UnityEngine.Matrix4x4 GlobalTransform => transform.localToWorldMatrix * LocalTransform;

    [SerializeField]
    private RangeReal m_lidarRange = new RangeReal(0.1f, float.MaxValue);

    /// <summary>
    /// The minimum and maximum range of the Lidar Sensor [m].
    /// Objects outside this range will not be detected by this Lidar Sensor.
    /// </summary>
    [Tooltip( "The minimum and maximum range of the Lidar Sensor [m]. " +
              "Objects outside this range will not be detected by this Lidar Sensor." )]
    public RangeReal LidarRange
    {
      get => m_lidarRange;
      set
      {
        m_lidarRange = value;
        if ( Native != null )
          Native.getModel().getRayRange().setRange( new RangeReal32( m_lidarRange.Min, m_lidarRange.Max ) );
      }
    }

    [SerializeField]
    private float m_beamDivergence = 0.001f * Mathf.Rad2Deg;

    /// <summary>
    /// Divergence of the lidar laser light beam [deg].
    /// This the total "cone angle", i.e. the angle between a perfectly parallel beam of the same
    /// exit dimater to the cone surface is half this angle.
    /// This property affects the calculated intensity.
    /// </summary>
    [Tooltip( "Divergence of the lidar laser light beam [deg]. " +
              "This the total \"cone angle\", i.e. the angle between a perfectly parallel beam of the same " +
              "exit dimater to the cone surface is half this angle. " +
              "This property affects the calculated intensity." )]
    [ClampAboveZeroInInspector]
    public float BeamDivergence
    {
      get => m_beamDivergence;
      set
      {
        m_beamDivergence = Mathf.Max( value, 1e-10f );
        if ( Native != null )
          Native.getModel().getProperties().setBeamDivergence( m_beamDivergence * Mathf.Deg2Rad );
      }
    }

    [SerializeField]
    private float m_beamExitRadius = 0.005f;

    /// <summary>
    /// The diameter of the lidar laser light beam as it exits the lidar [m].
    /// This property affects the calculated intensity.
    /// </summary>
    [Tooltip( "The diameter of the lidar laser light beam as it exits the lidar [m]. " +
              "This property affects the calculated intensity." )]
    [ClampAboveZeroInInspector]
    public float BeamExitRadius
    {
      get => m_beamExitRadius;
      set
      {
        m_beamExitRadius = Mathf.Max( value, 1e-10f );
        if ( Native != null )
          Native.getModel().getProperties().setBeamExitRadius( m_beamExitRadius );
      }
    }

    private uint m_rayTraceDepth = 1;

    /// <summary>
    /// Determines the number of maximum raytrace steps.
    /// The number of steps is one more than the number of bounces a ray will make,
    /// however, this number will generally not affect the size of the output data.
    /// It should be noted that the time and memory complexity of the raytrace will grow
    /// exponentially with the maximum number of raytrace steps.
    /// </summary>
    [Tooltip( "Determines the number of maximum raytrace steps. " +
              "The number of steps is one more than the number of bounces a ray will make, " +
              "however, this number will generally not affect the size of the output data. " +
              "It should be noted that the time and memory complexity of the raytrace will grow " +
              "exponentially with the maximum number of raytrace steps." )]
    [ClampAboveZeroInInspector()]
    public uint RayTraceDepth
    {
      get => m_rayTraceDepth;
      set
      {
        m_rayTraceDepth = value;
        if ( Native != null )
          Native.getOutputHandler().setRaytraceDepth( m_rayTraceDepth );
      }
    }

    [SerializeField]
    private bool m_setEnableRemoveRayMisses = true;

    /// <summary>
    /// When enabled, discard rays that miss all objects.
    /// </summary>
    [Tooltip( "When enabled, discard rays that miss all objects." )]
    public bool RemoveRayMisses
    {
      get => m_setEnableRemoveRayMisses;
      set
      {
        m_setEnableRemoveRayMisses = value;
        if ( Native != null )
          Native.getOutputHandler().setEnableRemoveRayMisses( value );
      }
    }

    /// <summary>
    /// Settings controlling the gaussian noise applied to the distance output along rays for hits.
    /// </summary>
    [field: SerializeField]
    public LidarDistanceGaussianNoise DistanceGaussianNoise { get; private set; } = new LidarDistanceGaussianNoise();

    /// <summary>
    /// Settings controlling the gaussian noise applied to the ray angles before rays are shot.
    /// </summary>
    [field: SerializeField]
    public List<LidarRayAngleGaussianNoise> RayAngleGaussianNoises { get; set; } = new List<LidarRayAngleGaussianNoise>();

    private readonly List<LidarRayAngleGaussianNoise> m_initializedNoises = new List<LidarRayAngleGaussianNoise>();

    [SerializeField]
    private List<LidarOutput> m_outputs = new List<LidarOutput>();

    /// <summary>
    /// An array of the outputs registered to this LidarSensor instance.
    /// </summary>
    public LidarOutput[] Outputs => m_outputs.ToArray();

    private void Sync()
    {
      // Sync ray angle noises
      foreach ( var noise in m_initializedNoises )
        if ( !RayAngleGaussianNoises.Contains( noise ) )
          noise.Disconnect();

      foreach ( var noise in RayAngleGaussianNoises )
        if ( !m_initializedNoises.Contains( noise ) )
          if ( noise.Initialize( Native ) )
            m_initializedNoises.Add( noise );

      var xform = GlobalTransform;

      Native.setFrame( new agx.Frame(
                          new AffineMatrix4x4(
                            ( xform.rotation ).ToHandedQuat(),
                            xform.GetPosition().ToHandedVec3() ) ) );
    }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      m_nativeModel = CreateLidarModel( LidarModelPreset );
      if ( m_nativeModel == null )
        return false;
      Native = new Lidar( null, m_nativeModel );
      Native.getOutputHandler().setEnableRemoveRayMisses( RemoveRayMisses );

      foreach ( var output in m_outputs ) {
        if ( !output.Initialize( this ) ) {
          Debug.LogError( $"Lidar '{name}' failed to initialize outputs" );
          return false;
        }
      }

      Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += Sync;

      DistanceGaussianNoise?.Initialize( Native );
      foreach ( var noise in RayAngleGaussianNoises )
        noise?.Initialize( Native );

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

    /// <summary>
    /// Adds a new <see cref="LidarOutput"/> to this LidarSensor. 
    /// Note that adding the same output to multiple sensors is not supported.
    /// </summary>
    /// <param name="output">The output to add to this sensor.</param>
    /// <returns>True if the output was successfully added.</returns>
    public bool Add( LidarOutput output )
    {
      if ( m_outputs.Contains( output ) )
        return false;

      m_outputs.Add( output );
      if ( Native != null ) {
        bool ok = output.Initialize( this );
        if ( ok )
          return true;

        m_outputs.Remove( output );
        return false;
      }
      return true;
    }

    /// <summary>
    /// Removes a <see cref="LidarOutput"/> from this LidarSensor. 
    /// </summary>
    /// <param name="output">The output to remove from this sensor.</param>
    /// <returns>True if the output was successfully removed.</returns>
    public bool Remove( LidarOutput output )
    {
      if ( !m_outputs.Contains( output ) )
        return false;

      m_outputs.Remove( output );
      if ( Native != null )
        output.Disconnect();
      return true;
    }

    protected override void OnDestroy()
    {
      if ( SensorEnvironment.HasInstance )
        SensorEnvironment.Instance.Native?.remove( Native );

      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= Sync;

      while ( m_outputs.Count > 0 ) {
        var output = m_outputs.Last();
        Remove( output );
      }

      DistanceGaussianNoise?.Disconnect();
      foreach ( var noise in m_initializedNoises )
        noise.Disconnect();

      Native?.Dispose();
      Native = null;
      m_nativeModel?.Dispose();
      m_nativeModel = null;

      base.OnDestroy();
    }

    private LidarModel CreateLidarModel( LidarModelPreset preset )
    {
      LidarModel lidarModel = null;

      switch ( preset ) {
        case LidarModelPreset.LidarModelGenericHorizontalSweep:
          GenericSweepData sweepData = ModelData as GenericSweepData;
          lidarModel = new LidarModelHorizontalSweep(
            Mathf.Deg2Rad * new agx.Vec2( sweepData.HorizontalFoV, sweepData.VerticalFoV ),
            Mathf.Deg2Rad * new agx.Vec2( sweepData.HorizontalResolution, sweepData.VerticalResolution ),
            sweepData.Frequency
          );
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

        case LidarModelPreset.LidarModelLivoxAvia:
          LivoxData livoxData = ModelData as LivoxData;
          lidarModel = new LidarModelLivoxAvia(livoxData.Downsample);
          break;

        case LidarModelPreset.LidarModelLivoxHap:
          livoxData = ModelData as LivoxData;
          lidarModel = new LidarModelLivoxHap(livoxData.Downsample);
          break;

        case LidarModelPreset.NONE:
        default:
          Debug.LogWarning( "No valid LidarModelPreset selected!" );
          break;
      }

      PropertySynchronizer.Synchronize( this );

      return lidarModel;
    }
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
      var xform = GlobalTransform;

      var pos = xform.GetPosition();
      var scale = UnityEditor.HandleUtility.GetHandleSize(pos) * 1.5f;
      Gizmos.DrawLine( pos, xform.MultiplyPoint( Vector3.forward * scale ) );

      int numPoints = 25;
      Vector3[] disc = new Vector3[numPoints];

      Vector3 x = xform.MultiplyVector(Vector3.right * scale);
      Vector3 y = xform.MultiplyVector(Vector3.up * scale);

      for ( int i = 0; i < numPoints; i++ ) {
        float ang = Mathf.PI * 2 * i / numPoints;
        disc[ i ] = pos + x * Mathf.Cos( ang ) + y * Mathf.Sin( ang );
      }
      Gizmos.DrawLineStrip( disc, true );
#endif
    }
  }
}
