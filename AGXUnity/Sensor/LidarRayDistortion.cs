using System;
using UnityEngine;

using Axis = agxSensor.LidarRayAngleGaussianNoise.Axis;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// LidarRayAngleGaussianNoise adds noise to the angle at which lidar rays are shot from the sensor.
  /// This class adds an angle around an axis given a gaussian distribution specified in the properties.
  /// </summary>
  [Serializable]
  public class LidarRayAngleGaussianNoise
  {
    public agxSensor.LidarRayAngleGaussianNoise Native { get; private set; }

    private agxSensor.Lidar Parent { get; set; } = null;

    [SerializeField]
    private bool m_enable = false;

    /// <summary>
    /// Enable ray angle noise to vary the angle that rays are shot from the lidar given a gaussian distribution.
    /// </summary>
    [Tooltip( "Enable Ray Angle Noise to vary the angle that rays are shot from the lidar given a gaussian distribution." )]
    public bool Enable
    {
      get => m_enable;
      set
      {
        m_enable = value;
        if ( Native != null ) {
          if ( m_enable )
            Parent.getRayDistortionHandler().add( Native );
          else
            Parent.getRayDistortionHandler().remove( Native );
        }
      }
    }

    [SerializeField]
    private float m_mean = 0.0f;

    /// <summary>
    /// The mean distortion applied by the ray angle noise
    /// </summary>
    [Tooltip( "The mean distortion applied by the Ray Angle noise" )]
    public float Mean
    {
      get => m_mean;
      set
      {
        m_mean = value;
        if ( Native != null )
          Native.setMean( m_mean );
      }
    }

    [SerializeField]
    private float m_standardDeviation = 0.0f;

    /// <summary>
    /// The standard deviation of the distortion applied by the ray angle noise
    /// </summary>
    [Tooltip( "The standard deviation of the distortion applied by the ray angle noise" )]
    public float StandardDeviation
    {
      get => m_standardDeviation;
      set
      {
        m_standardDeviation = value;
        if ( Native != null )
          Native.setStandardDeviation( m_standardDeviation );
      }
    }

    [SerializeField]
    private Axis m_distortionAxis = Axis.AXIS_X;

    /// <summary>
    /// The axis around which to apply the angular distortion.
    /// </summary>
    [Tooltip( "The axis around which to apply the angular distortion." )]
    public Axis DistortionAxis
    {
      get => m_distortionAxis;
      set
      {
        m_distortionAxis = value;
        if ( Native != null )
          Native.setAxis( m_distortionAxis );
      }
    }

    public bool Initialize( agxSensor.Lidar parent )
    {
      Parent = parent;
      Native = new agxSensor.LidarRayAngleGaussianNoise( m_mean, m_standardDeviation, DistortionAxis );
      if ( Enable )
        return parent.getRayDistortionHandler().add( Native );
      return true;
    }
  }

  /// <summary>
  /// LidarDistanceGaussianNoise applies an offset to the distance to points that hit a surface in a lidar simulation.
  /// The offset is drawn from a gaussian distribution based on the specified properties and directly affect
  /// the output positions from the lidar.
  /// The Base and Slope parameters together determine the standard deviation used to calculate the distortion where the 
  /// standard deviation increases with a factor of the slope and distance to the hit point.
  /// </summary>
  [Serializable]
  public class LidarDistanceGaussianNoise
  {
    public agxSensor.RtDistanceGaussianNoise Native { get; private set; }

    private agxSensor.Lidar Parent { get; set; } = null;

    [SerializeField]
    private bool m_enable = false;

    /// <summary>
    /// Enable distance noise to vary the distance of hits given a gaussian distribution.
    /// </summary>
    [Tooltip( "Enable distance noise to vary the distance of hits given a gaussian distribution." )]
    public bool Enable
    {
      get => m_enable;
      set
      {
        m_enable = value;
        if ( Native != null ) {
          if ( m_enable )
            Parent.getOutputHandler().add( Native );
          else
            Parent.getOutputHandler().remove( Native );
        }
      }
    }

    [SerializeField]
    private float m_mean = 0.0f;

    /// <summary>
    /// The mean of the distortion added to the distance of hit points.
    /// </summary>
    [Tooltip( "The mean of the distortion added to the distance of hit points." )]
    public float Mean
    {
      get => m_mean;
      set
      {
        m_mean = value;
        if ( Native != null )
          Native.setMean( m_mean );
      }
    }

    [SerializeField]
    private float m_standardDeviationBase = 0.0f;

    /// <summary>
    /// The base standard deviation of the distortion added to the distance of hit points.
    /// </summary>
    [Tooltip( "The base standard deviation of the distortion added to the distance of hit points." )]
    public float StandardDeviationBase
    {
      get => m_standardDeviationBase;
      set
      {
        m_standardDeviationBase = value;
        if ( Native != null )
          Native.setStdDevBase( m_standardDeviationBase );
      }
    }

    [SerializeField]
    private float m_standardDeviationSlope = 0.0f;

    /// <summary>
    /// The added standard deviation based on the distance to the hit point of the distortion that is added to the distance of the hit points. 
    /// </summary>
    [Tooltip( "The added standard deviation based on the distance to the hit point of the distortion that is added to the distance of the hit points." )]
    public float StandardDeviationSlope
    {
      get => m_standardDeviationSlope;
      set
      {
        m_standardDeviationSlope = value;
        if ( Native != null )
          Native.setStdDevSlope( m_standardDeviationSlope );
      }
    }

    public bool Initialize( agxSensor.Lidar parent )
    {
      Parent = parent;
      Native = new agxSensor.RtDistanceGaussianNoise( m_mean, m_standardDeviationBase, m_standardDeviationSlope );
      if ( Enable )
        return parent.getOutputHandler().add( Native );
      return true;
    }
  }
}
