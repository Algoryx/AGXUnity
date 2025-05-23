using agxROS2;
using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [RequireComponent( typeof( LidarSensor ) )]
  public class LidarROS2Publisher : ScriptComponent
  {
    /// <summary>
    /// QOS Settings for all topics published to by this publisher.
    /// </summary>
    [field: SerializeField]
    [DisableInRuntimeInspector]
    [Tooltip( "QOS Settings for all topics published to by this publisher." )]
    public QOS QOS { get; private set; } = new QOS();

    [SerializeField]
    private string m_pcl24Topic = "lidar/pointcloud";
    [SerializeField]
    private string m_pcl48Topic = "lidar/pointcloud_ex";
    [SerializeField]
    private string m_instanceIdTopic = "lidar/instance_id";

    /// <summary>
    /// Topic name for which to publish 24 byte-per-point pointcloud data with the following format:
    /// - xyz 3 x 32 bit floats
    /// - padding 32 bits
    /// - intensity 32 bit float
    /// - ring_id 16 bit uint
    /// - padding 16 bits
    /// </summary>
    [Tooltip( "Topic name for which to publish 24 byte-per-point pointcloud data." )]
    public string PCL24Topic
    {
      get => m_pcl24Topic;
      set
      {
        m_pcl24Topic = value;
        if ( m_pcl24Pub != null )
          m_pcl24Pub = new PublisherSensorMsgsPointCloud2( m_pcl24Topic, QOS.CreateNative() );
      }
    }

    /// <summary>
    /// Topic name for which to publish 48 byte-per-point pointcloud data with the following format:
    /// - xyz 3 x 32 bit floats
    /// - padding 32 bits
    /// - intensity 32 bit float
    /// - ring_id 16 bit uint
    /// - padding 16 bits
    /// - azimuth 32 bit float
    /// - distance 32 bit float
    /// - return_type 8 bit uint
    /// - padding 46 bits
    /// - time_stamp 64 bit float
    /// </summary>
    [Tooltip( "Topic name for which to publish 48 byte-per-point pointcloud data." )]
    public string PCL48Topic
    {
      get => m_pcl48Topic;
      set
      {
        m_pcl48Topic = value;
        if ( m_pcl48Pub != null )
          m_pcl48Pub = new PublisherSensorMsgsPointCloud2( m_pcl48Topic, QOS.CreateNative() );
      }
    }

    /// <summary>
    /// Topic name for which to publish 20 byte-per-point pointcloud data with the following format:
    /// - xyz 3 x 32 bit floats
    /// - entity_id 32 bit int
    /// - intensity 32 bit float
    /// </summary>
    [Tooltip( "Topic name for which to publish 20 byte-per-point pointcloud data." )]
    public string InstanceIdTopic
    {
      get => m_instanceIdTopic;
      set
      {
        m_instanceIdTopic = value;
        if ( m_instanceIDPub != null )
          m_instanceIDPub = new PublisherSensorMsgsPointCloud2( m_instanceIdTopic, QOS.CreateNative() );
      }
    }

    /// <summary>
    /// The frame ID to include in the message headers
    /// </summary>
    [Tooltip( "The frame ID to include in the message headers" )]
    [field: SerializeField]
    public string FrameID { get; set; } = "world";

    [SerializeField]
    private bool m_publishPCL24 = true;
    [SerializeField]
    private bool m_publishPCL48 = true;
    [SerializeField]
    private bool m_publishInstanceId = false;

    /// <summary>
    ///  When enabled, the publisher will publish 24 byte-per-point pointcloud data to the corresponding topic.
    /// </summary>
    [Tooltip( "When enabled, the publisher will publish 24 byte-per-point pointcloud data to the corresponding topic." )]
    public bool PublishPCL24
    {
      get => m_publishPCL24;
      set
      {
        m_publishPCL24 = value;

        if ( m_lidarSensor == null )
          return;

        if ( m_publishPCL24 && isActiveAndEnabled )
          m_lidarSensor.Add( m_pcl24Output );
        else
          m_lidarSensor.Remove( m_pcl24Output );
      }
    }

    /// <summary>
    ///  When enabled, the publisher will publish 48 byte-per-point pointcloud data to the corresponding topic.
    /// </summary>
    [Tooltip( "When enabled, the publisher will publish 48 byte-per-point pointcloud data to the corresponding topic." )]
    public bool PublishPCL48
    {
      get => m_publishPCL48;
      set
      {
        m_publishPCL48 = value;
        if ( m_lidarSensor == null )
          return;

        if ( m_publishPCL48 && isActiveAndEnabled && m_pcl48Output != null )
          m_lidarSensor.Add( m_pcl48Output );
        else
          m_lidarSensor.Remove( m_pcl48Output );
      }
    }

    /// <summary>
    ///  When enabled, the publisher will publish 20 byte-per-point pointcloud data to the corresponding topic.
    /// </summary>
    [Tooltip( "When enabled, the publisher will publish 20 byte-per-point pointcloud data to the corresponding topic." )]
    public bool PublishInstanceId
    {
      get => m_publishInstanceId;
      set
      {
        m_publishInstanceId = value;
        if ( m_lidarSensor == null )
          return;

        if ( m_publishInstanceId && isActiveAndEnabled && m_instanceIdOutput != null )
          m_lidarSensor.Add( m_instanceIdOutput );
        else
          m_lidarSensor.Remove( m_instanceIdOutput );
      }
    }

    private agxROS2.PublisherSensorMsgsPointCloud2 m_pcl24Pub;
    private agxROS2.PublisherSensorMsgsPointCloud2 m_pcl48Pub;
    private agxROS2.PublisherSensorMsgsPointCloud2 m_instanceIDPub;

    private LidarOutput m_pcl24Output = null;
    private LidarOutput m_pcl48Output = null;
    private LidarOutput m_instanceIdOutput = null;

    private LidarSensor m_lidarSensor;

    protected override bool Initialize()
    {
      m_lidarSensor = GetComponent<LidarSensor>();

      // Setup outputs
      m_pcl24Output = new LidarOutput(
        RtOutput.Field.XYZ_VEC3_F32,
        RtOutput.Field.PADDING_32,
        RtOutput.Field.INTENSITY_F32,
        RtOutput.Field.RING_ID_U16,
        RtOutput.Field.PADDING_16
      );

      m_pcl48Output = new LidarOutput(
        RtOutput.Field.XYZ_VEC3_F32,
        RtOutput.Field.PADDING_32,
        RtOutput.Field.INTENSITY_F32,
        RtOutput.Field.RING_ID_U16,
        RtOutput.Field.PADDING_16,
        RtOutput.Field.AZIMUTH_F32,
        RtOutput.Field.DISTANCE_F32,
        RtOutput.Field.RETURN_TYPE_U8,
        RtOutput.Field.PADDING_8,
        RtOutput.Field.PADDING_16,
        RtOutput.Field.PADDING_32,
        RtOutput.Field.TIME_STAMP_F64
      );

      m_instanceIdOutput = new LidarOutput(
        RtOutput.Field.XYZ_VEC3_F32,
        RtOutput.Field.ENTITY_ID_I32,
        RtOutput.Field.INTENSITY_F32
      );

      if ( PublishPCL24 )
        m_lidarSensor.Add( m_pcl24Output );
      if ( PublishPCL48 )
        m_lidarSensor.Add( m_pcl48Output );
      if ( PublishInstanceId )
        m_lidarSensor.Add( m_instanceIdOutput );

      return base.Initialize();
    }

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PostStepForward += PublishOutputs;

      if ( m_lidarSensor == null )
        return;

      if ( PublishPCL24 )
        m_lidarSensor.Add( m_pcl24Output );
      if ( PublishPCL48 )
        m_lidarSensor.Add( m_pcl48Output );
      if ( PublishInstanceId )
        m_lidarSensor.Add( m_instanceIdOutput );
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= PublishOutputs;

      m_lidarSensor.Remove( m_pcl24Output );
      m_lidarSensor.Remove( m_pcl48Output );
      m_lidarSensor.Remove( m_instanceIdOutput );
    }

    protected override void OnDestroy()
    {
      m_lidarSensor = null;

      m_pcl24Output = null;
      m_pcl24Pub = null;

      m_pcl48Output = null;
      m_pcl48Pub = null;

      m_instanceIdOutput = null;
      m_instanceIDPub = null;

      base.OnDestroy();
    }

    public void PublishOutputs()
    {
      float timestamp = (float)Simulation.Instance.Native.getTimeStamp();

      // Use the convertLidarOutput utility to create messages for enabled publishers
      if ( PublishPCL24 ) {
        if ( m_pcl24Pub == null )
          m_pcl24Pub = new PublisherSensorMsgsPointCloud2( PCL24Topic, QOS.CreateNative() );
        m_pcl24Pub.sendMessage( agxROS2SWIG.convertLidarOutput( m_pcl24Output.Native, timestamp, FrameID, true ) );
      }

      if ( PublishPCL48 ) {
        if ( m_pcl48Pub == null )
          m_pcl48Pub = new PublisherSensorMsgsPointCloud2( PCL48Topic, QOS.CreateNative() );
        m_pcl48Pub.sendMessage( agxROS2SWIG.convertLidarOutput( m_pcl48Output.Native, timestamp, FrameID, true ) );
      }

      if ( PublishInstanceId ) {
        if ( m_instanceIDPub == null )
          m_instanceIDPub = new PublisherSensorMsgsPointCloud2( InstanceIdTopic, QOS.CreateNative() );
        m_instanceIDPub.sendMessage( agxROS2SWIG.convertLidarOutput( m_instanceIdOutput.Native, timestamp, FrameID, true ) );
      }
    }
  }
}
