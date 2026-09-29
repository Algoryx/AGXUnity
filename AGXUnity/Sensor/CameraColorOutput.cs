using AGXUnity.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Sensor
{
  [System.Serializable]
  public class ColorOutput : Subcomponent<CameraSensor, agxSensor.CameraColorOutput>
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
    private bool m_disposed = false;
    private uint m_allocatedOutputSize32 = 0;

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
    [Tooltip( "Output image resolution." )]
    private Vector2Int m_resolution = new Vector2Int( 128, 128 );
    public Vector2Int Resolution
    {
      get => m_resolution;
      set => PropertyUtil.Assign( ref m_resolution,
                                  new Vector2Int( Mathf.Max( 1, value.x ), Mathf.Max( 1, value.y ) ),
                                  this );
    }

    [SerializeField]
    [Tooltip( "Number of output channels. Values are clamped to the range [1, 4]." )]
    private uint m_channelCount = 4;

    public uint ChannelCount
    {
      get => m_channelCount;
      set
      {
        var clamped = System.Math.Clamp(value, 1, 4);
        if ( clamped != value )
          Debug.LogWarning( $"Camera color output was passed an invalid channel count {value}, valid values are [1,4]. Clamping provided value to the valid range" );
        PropertyUtil.Assign( ref m_channelCount, clamped, this );
      }
    }

    [SerializeField]
    private agxSensor.CameraColorOutput.ChannelType m_channelType = agxSensor.CameraColorOutput.ChannelType.U8;

    public agxSensor.CameraColorOutput.ChannelType ChannelType
    {
      get => m_channelType;
      set => PropertyUtil.Assign( ref m_channelType, value, this );
    }

    [SerializeField]
    [Min( 0.0001f )]
    private float m_gamma = 1.0f;
    public float Gamma
    {
      get => m_gamma;
      set => PropertyUtil.Assign( ref m_gamma, Mathf.Max( 0.0001f, value ), this );
    }

    [SerializeField]
    private Vector2 m_illuminanceCutoff = new Vector2(0,1);

    public Vector2 IlluminanceCutoff
    {
      get => m_illuminanceCutoff;
      set => PropertyUtil.Assign( ref m_illuminanceCutoff, value, this );
    }

    [SerializeField]
    private bool m_constantCapture = false;
    public bool ConstantCapture
    {
      get => m_constantCapture;
      set => PropertyUtil.Assign( ref m_constantCapture, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_framerate = 50.0f;
    public float Framerate
    {
      get => m_framerate;
      set
      {
        value = Mathf.Max( 0.0f, value );
        if ( m_framerate == value )
          return;

        if ( value > 0f )
          m_framerate = value;
        ConstantCapture = value > 0f;
        SynchronizeConfiguration();
      }
    }

    private void RecreateBuffer()
    {
      OutputBuffer?.Release();
      OutputBuffer = new ComputeBuffer( (int)OutputSize32, sizeof( uint ) );
      m_allocatedOutputSize32 = OutputSize32;
    }

    protected override agxSensor.CameraColorOutput InitializeNative()
    {
      var native = new agxSensor.CameraColorOutput();
      RecreateBuffer();
      m_disposed = false;
      CameraBackend.Instance.MapColorOutput( native, this );
      return native;
    }

    internal void SynchronizeConfiguration( bool recreateBuffer = false )
    {
      m_resolution = new Vector2Int( Mathf.Max( 1, m_resolution.x ), Mathf.Max( 1, m_resolution.y ) );
      m_channelCount = System.Math.Clamp( m_channelCount, 1, 4 );
      m_gamma = Mathf.Max( 0.0001f, m_gamma );
      m_framerate = Mathf.Max( 0.0f, m_framerate );
      if ( m_framerate == 0.0f )
        m_constantCapture = false;

      if ( Native != null ) {
        Native.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
        Native.setChannelCount( ChannelCount );
        Native.setChannelType( ChannelType );
        Native.setGamma( Gamma );
        Native.setRelativeIlluminanceCutoff( new agx.RangeReal( IlluminanceCutoff.x, IlluminanceCutoff.y ) );
        if ( ConstantCapture )
          Native.setConstantCapture( Framerate );
        else
          Native.setManualCapture();
      }

      if ( OutputBuffer != null && ( recreateBuffer || m_allocatedOutputSize32 != OutputSize32 ) )
        RecreateBuffer();
    }

    protected override void SynchronizeNative() => SynchronizeConfiguration();

    protected override void DisposeNative()
    {
      m_disposed = true;
      HasQueuedCapture = false;
      OutputBuffer?.Release();
      OutputBuffer = null;
      m_allocatedOutputSize32 = 0;
      if ( Native != null ) {
        if ( Parent?.Native != null )
          Parent.Native.getOutputHandler().removeChild( Native );
        CameraBackend.Instance.UnmapColorOutput( Native );
        Native.Dispose();
      }
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

      if ( Parent == null || OutputBuffer == null ) {
        Debug.LogWarning( "CameraColorOutput attempted to capture after being disposed" );
        return;
      }

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
        if ( requestTime <= m_currentDataStamp || req.hasError || m_disposed )
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
