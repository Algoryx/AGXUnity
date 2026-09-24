using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Sensor
{
  [System.Serializable]
  public class ColorOutput
  {
    private ComputeBuffer OutputBuffer;

    private uint[] m_stagingBuffer;
    private double m_currentDataStamp = -1.0f;
    private double m_lastCapture = -1.0f;
    private bool m_stagingBufferDirty = false;

    private static ComputeShader s_conversionCompute;
    private static int s_conversionKernel;
    private static uint s_groupSizeX;
    private static uint s_groupSizeY;

    private uint ChannelTypeSize => ChannelType switch
    {
      agxSensor.CameraColorOutput.ChannelType.U8 or
      agxSensor.CameraColorOutput.ChannelType.I8 => 1,
      agxSensor.CameraColorOutput.ChannelType.U16 or
      agxSensor.CameraColorOutput.ChannelType.I16 => 2,
      agxSensor.CameraColorOutput.ChannelType.U32 or
      agxSensor.CameraColorOutput.ChannelType.I32 or
      agxSensor.CameraColorOutput.ChannelType.F32 => 4,
      agxSensor.CameraColorOutput.ChannelType.U64 or
      agxSensor.CameraColorOutput.ChannelType.I64 or
      agxSensor.CameraColorOutput.ChannelType.F64 => 8,
      _ => 1
    };

    private uint OutputSize => (uint)Resolution.x * (uint)Resolution.y * ChannelCount * ChannelTypeSize;
    private uint NumBytesToNumUints( uint byteCount ) => ( byteCount + ( sizeof( uint )-1 ) ) / sizeof( uint );
    private uint OutputSize32 => NumBytesToNumUints( OutputSize );

    private bool m_hasUnreadData = false;
    private double m_accumulatedCaptureTime = 0.0f;

    [HideInInspector]
    public agxSensor.CameraColorOutput Native { get; internal set; }

    private agxSensor.ByteSpan m_nativeOutputSpan;
    internal agxSensor.ByteSpan NativeOutputSpan
    {
      get => m_nativeOutputSpan;
      set
      {
        m_nativeOutputSpan = value;
        if ( m_stagingBuffer == null || (ulong)m_stagingBuffer.Length != NumBytesToNumUints( (uint)m_nativeOutputSpan.getCount() ) )
          m_stagingBuffer = new uint[ OutputSize32 ];
      }
    }

    [SerializeField]
    private Vector2Int m_resolution = new Vector2Int( 128, 128 );
    public Vector2Int Resolution
    {
      get => m_resolution;
      set
      {
        if ( m_resolution == value ) return;

        m_resolution = value;
        RecreateTexture();
        if ( Native != null )
          Native.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
      }
    }

    [SerializeField]
    private uint m_channelCount = 4;

    public uint ChannelCount
    {
      get => m_channelCount;
      set
      {
        if ( m_channelCount == value ) return;
        var clamped = System.Math.Clamp(value, 1, 4);
        if ( clamped != value )
          Debug.LogWarning( $"Camera color output was passed an invalid channel count {value}, valid values are [1,4]. Clamping provided value to the valid range" );
        m_channelCount = clamped;
        if ( Native != null )
          Native.setChannelCount( clamped );
      }
    }

    [SerializeField]
    private agxSensor.CameraColorOutput.ChannelType m_channelType = agxSensor.CameraColorOutput.ChannelType.U8;

    public agxSensor.CameraColorOutput.ChannelType ChannelType
    {
      get => m_channelType;
      set
      {
        if ( m_channelType == value ) return;
        m_channelType = value;
        if ( Native != null )
          Native.setChannelType( value );
      }
    }

    [SerializeField]
    private float m_gamma = 1.0f;
    public float Gamma
    {
      get => m_gamma;
      set
      {
        if ( m_gamma == value ) return;
        m_gamma = value;
        Native.setGamma( value );
      }
    }

    [SerializeField]
    private Vector2 m_illuminanceCutoff = new Vector2(0,1);

    public Vector2 IlluminanceCutoff
    {
      get => m_illuminanceCutoff;
      set
      {
        if ( m_illuminanceCutoff == value ) return;
        m_illuminanceCutoff = value;
        if ( Native != null )
          Native.setRelativeIlluminanceCutoff( new agx.RangeReal( value.x, value.y ) );
      }
    }

    [SerializeField]
    private bool m_constantCapture = false;
    public bool ConstantCapture
    {
      get => m_constantCapture;
      set
      {
        if ( m_constantCapture == value ) return;
        m_constantCapture = value;
        if ( Native != null ) {
          if ( m_constantCapture )
            Native.setConstantCapture( Framerate );
          else
            Native.setManualCapture();
        }
      }
    }

    [SerializeField]
    private float m_framerate = 50.0f;
    [DynamicallyShowInInspector( nameof( ConstantCapture ) )]
    public float Framerate
    {
      get => m_framerate;
      set
      {
        if ( m_framerate == value ) return;
        if ( value == 0 ) {
          ConstantCapture = false;
          Native?.setManualCapture();
        }
        else {
          m_framerate = value;
          ConstantCapture = true;
          Native?.setConstantCapture( Framerate );
        }
      }
    }

    private void RecreateTexture()
    {
      OutputBuffer = new ComputeBuffer( (int)OutputSize32, sizeof( uint ) );
    }

    [HideInInspector]
    public CameraSensor Parent { get; internal set; }

    internal void Initialize( CameraSensor parent )
    {
      Parent = parent;

      Native = new agxSensor.CameraColorOutput();
      if ( ConstantCapture )
        Native.setConstantCapture( Framerate );
      else
        Native.setManualCapture();
      Native.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
      Native.setChannelCount( ChannelCount );
      Native.setChannelType( ChannelType );
      Native.setRelativeIlluminanceCutoff( new agx.RangeReal( IlluminanceCutoff.x, IlluminanceCutoff.y ) );

      RecreateTexture();
    }

    public void Capture()
    {
      HasQueuedCapture = true;
    }

    private bool HasQueuedCapture { get; set; } = false;

    internal void PerformQueuedCapture()
    {
      if ( !HasQueuedCapture )
        return;

      if ( Parent == null )
        return;

      Parent.EnsureHasOutput();
      HasQueuedCapture = false;

      float requestTime = (float)Simulation.Instance.Native.getTimeStamp();

      if ( requestTime <= m_lastCapture )
        return; // TODO: Notify listeners

      m_lastCapture = requestTime;

      if ( s_conversionCompute == null ) {
        s_conversionCompute = Resources.Load<ComputeShader>( "Shaders/Compute/CameraColorOutputPass" );
        s_conversionKernel = s_conversionCompute.FindKernel( "ColorOutputPass" );
        s_conversionCompute.GetKernelThreadGroupSizes( s_conversionKernel,
                                                         out s_groupSizeX,
                                                         out s_groupSizeY,
                                                         out _ );
      }

      s_conversionCompute.SetInts( "OutputResolution", Resolution.x, Resolution.y );
      s_conversionCompute.SetInt( "ChannelCount", (int)ChannelCount );
      s_conversionCompute.SetInt( "OutputType", (int)ChannelType );
      s_conversionCompute.SetFloats( "IlluminanceCutoff", IlluminanceCutoff.x, IlluminanceCutoff.y );
      s_conversionCompute.SetFloat( "GammaInv", 1 / Gamma );
      s_conversionCompute.SetMatrix( "ColorMapping", Matrix4x4.identity );
      s_conversionCompute.SetTexture( s_conversionKernel, "Source", Parent.Output );
      s_conversionCompute.SetBuffer( s_conversionKernel, "Result", OutputBuffer );

      s_conversionCompute.Dispatch(
        s_conversionKernel,
        Mathf.CeilToInt( Resolution.x / (float)s_groupSizeX ),
        Mathf.CeilToInt( Resolution.y / (float)s_groupSizeY ),
      1 );

      AsyncGPUReadback.Request( OutputBuffer, req => {
        if ( requestTime <= m_currentDataStamp )
          return;

        m_currentDataStamp = requestTime;
        if ( m_stagingBuffer != null && req.layerDataSize == m_stagingBuffer.Length * sizeof( uint ) ) {
          var native = req.GetData<uint>();
          native.CopyTo( m_stagingBuffer );
          m_stagingBufferDirty = true;
        }
      } );
    }

    public bool HasUnreadData( bool markAsRead = false )
    {
      bool unread = m_hasUnreadData;
      if ( markAsRead )
        m_hasUnreadData = false;
      return unread;
    }

    internal void Update()
    {
      if ( Native == null )
        return;
      if ( Native.isConstantlyCapturing() ) {
        m_accumulatedCaptureTime += Simulation.Instance.TimeStep;
        var frameLength = 1/Native.getFramerate();
        while ( m_accumulatedCaptureTime > frameLength ) {
          m_accumulatedCaptureTime -= frameLength;
          Capture();
        }
      }

      if ( m_stagingBufferDirty ) {
        m_stagingBufferDirty = false;
        m_hasUnreadData = true;
        if ( !m_nativeOutputSpan.write( m_stagingBuffer, (uint)m_stagingBuffer.Length ) )
          Debug.LogWarning( "Failed to write camera output data to native buffer" );
      }
    }
  }
}
