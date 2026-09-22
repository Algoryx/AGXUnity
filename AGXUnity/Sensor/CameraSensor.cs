using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Sensor
{
  [DisallowMultipleComponent]
  [RequireComponent( typeof( Camera ) )]
  public class CameraSensor : ScriptComponent
  {
    public agxSensor.Camera Native { get; private set; }
    public Camera CameraComponent => GetComponent<Camera>();

    public RenderTexture Output { get; private set; }

    [SerializeField]
    private float m_focalLength;

    [InspectorGroupBegin( Name = "Lens Properties" )]
    public float FocalLength
    {
      get => m_focalLength;
      set
      {
        CameraComponent.focalLength = value * 1000;
        m_focalLength = value;
      }
    }

    [SerializeField]
    private float m_focusDistance;

    public float FocusDistance
    {
      get => m_focusDistance;
      set
      {
        CameraComponent.focusDistance = value;
        m_focusDistance = value;
      }
    }

    [SerializeField]
    private float m_fStop;

    public float fStop
    {
      get => m_fStop;
      set
      {
        CameraComponent.aperture = value;
        m_fStop = value;
      }
    }


    [SerializeField]
    private Vector2 m_sensorSize;

    [InspectorGroupBegin( Name = "Sensor" )]
    public Vector2 SensorSize
    {
      get => m_sensorSize;
      set
      {
        CameraComponent.sensorSize = value * 1000;
        m_sensorSize = value;
      }
    }

    [SerializeField]
    private int m_iso;

    public int ISO
    {
      get => m_iso;
      set
      {
        CameraComponent.iso = value;
        m_iso = value;
      }
    }

    [SerializeField]
    private float m_shutterSpeed;

    public float ShutterSpeed
    {
      get => m_shutterSpeed;
      set
      {
        CameraComponent.shutterSpeed = value;
        m_shutterSpeed = value;
      }
    }

    [SerializeField]
    private Vector2Int m_resolution;

    [DelayedInspector]
    public Vector2Int Resolution
    {
      get => m_resolution;
      set
      {
        if ( value == m_resolution )
          return;
        m_resolution = value;
        RecreateRenderTexture();
      }
    }

    [System.Serializable]
    public class Illuminator
    {
      public agxSensor.ICameraActiveIllumination Native { get; private set; }

      public enum Type
      {
        Spot,
        Point
      };

      [field: SerializeField]
      public Type IlluminatorType { get; set; } = Type.Spot;

      [field: SerializeField]
      public Color Color { get; set; } = Color.white;

      [field: SerializeField]
      public float Intensity { get; set; } = 10;

      private bool HasAngle => IlluminatorType == Type.Spot;

      [field: SerializeField]
      [DynamicallyShowInInspector( nameof( HasAngle ) )]
      public Vector2 ConeAngles { get; set; } = new Vector2( 20, 25 );

      [field: SerializeField]
      public float FlashDuration { get; set; } = Mathf.Infinity;

      public CameraSensor Parent { get; private set; }

      public Light UnityLight { get; internal set; }

      private float LastFlash { get; set; } = float.NegativeInfinity;

      internal void Initialize( CameraSensor parent )
      {
        Parent = parent;
        Native = IlluminatorType switch
        {
          Type.Spot => new agxSensor.CameraSpotlight( new agx.AffineMatrix4x4(), Intensity, new agx.Vec3( Color.r, Color.g, Color.b ), new agx.RangeReal( ConeAngles.x, ConeAngles.y ), FlashDuration ),
          Type.Point => new agxSensor.CameraPointLight( new agx.AffineMatrix4x4(), Intensity, new agx.Vec3( Color.r, Color.g, Color.b ), FlashDuration )
        };
      }

      public void UpdateLightIntensity()
      {
        if ( float.IsFinite( FlashDuration ) && LastFlash < Time.time ) {
          UnityLight.enabled = false;
        }
      }

      public void Flash()
      {
        var baseIntensity = LightUnitUtils.ConvertIntensity( UnityLight, Intensity, LightUnit.Lumen, LightUnitUtils.GetNativeLightUnit( UnityLight.type ) );

        // Scaling based on flash duration throughout camera frame (t0.1, exponential falloff)
        float flashScale = 1.0f;
        if ( float.IsFinite( FlashDuration ) ) {
          float scaledTime = 2.302585093f * Parent.ShutterSpeed / FlashDuration;
          flashScale = ( 1.0f - Mathf.Exp( -scaledTime ) ) / scaledTime;
        }

        UnityLight.intensity = baseIntensity * flashScale;
        UnityLight.enabled = true;
        LastFlash = Time.time;
      }
    }

    public List<Illuminator> Illuminators = new List<Illuminator>();

    [System.Serializable]
    public class ColorOutput
    {
      [NonSerialized]
      public Texture2D OutputTexture;
      private ComputeBuffer OutputBuffer;

      public uint[] m_stagingBuffer;
      private double m_currentDataStamp = -1.0f;
      private double m_lastCapture = -1.0f;
      private bool m_stagingBufferDirty = false;


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
          var clamped = Math.Clamp(value, 1, 4);
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
        OutputTexture = new Texture2D( Resolution.x, Resolution.y, TextureFormat.RGBA32, false );
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

        Parent.OutputConversionCompute.SetInts( "OutputResolution", Resolution.x, Resolution.y );
        Parent.OutputConversionCompute.SetInt( "ChannelCount", (int)ChannelCount );
        Parent.OutputConversionCompute.SetInt( "OutputType", (int)ChannelType );
        Parent.OutputConversionCompute.SetFloats( "IlluminanceCutoff", IlluminanceCutoff.x, IlluminanceCutoff.y );
        Parent.OutputConversionCompute.SetFloat( "GammaInv", 1 / Gamma );
        Parent.OutputConversionCompute.SetMatrix( "ColorMapping", Matrix4x4.identity );
        Parent.OutputConversionCompute.SetTexture( Parent.OutputConversionKernel, "Source", Parent.Output );
        Parent.OutputConversionCompute.SetBuffer( Parent.OutputConversionKernel, "Result", OutputBuffer );

        Parent.OutputConversionCompute.Dispatch( Parent.OutputConversionKernel, Resolution.x, Resolution.y, 1 );

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

    [SerializeField]
    public List<ColorOutput> Outputs = new List<ColorOutput>();

    [field: SerializeField]
    [InspectorGroupEnd]
    public bool SynchronizeUnityChanges { get; set; } = true;

    [field: SerializeField]
    public bool Preview { get; set; } = false;

    internal ComputeShader OutputConversionCompute { get; private set; }
    internal int OutputConversionKernel { get; private set; }

    private int LastRenderedFrame { get; set; } = -1;

    private void RecreateRenderTexture()
    {
      Output = new RenderTexture( Resolution.x, Resolution.y, 8, RenderTextureFormat.Default );
      Output.hideFlags = HideFlags.NotEditable;
      Output.name = name + "_Output";
      CameraComponent.targetTexture = Output;
    }

    private void SynchronizeCamera()
    {
      var cam = CameraComponent;
      if ( !Mathf.Approximately( cam.focalLength / 1000, FocalLength ) )
        FocalLength = cam.focalLength / 1000;
      if ( !Mathf.Approximately( cam.aperture, fStop ) )
        fStop = cam.aperture;
      if ( !Mathf.Approximately( cam.focusDistance, FocusDistance ) )
        FocusDistance = cam.focusDistance;

      if ( !Mathf.Approximately( cam.iso, ISO ) )
        ISO = cam.iso;
      if ( !Mathf.Approximately( cam.shutterSpeed, ShutterSpeed ) )
        ShutterSpeed = cam.shutterSpeed;
      if ( !Mathf.Approximately( cam.sensorSize.x / 1000, SensorSize.x )
        || !Mathf.Approximately( cam.sensorSize.y / 1000, SensorSize.y ) )
        SensorSize = cam.sensorSize / 1000;
    }

    private void SynchronizeIlluminators()
    {
      var illRoot = transform.Find( "Active Illumination" );
      if ( illRoot == null ) {
        illRoot = new GameObject( "Active Illumination" ).transform;
        illRoot.gameObject.hideFlags = HideFlags.DontSave;
        illRoot.parent = transform;
      }
      illRoot.localPosition = Vector3.zero;
      illRoot.localRotation = Quaternion.identity;
      illRoot.localScale = Vector3.one;

      while ( illRoot.childCount < Illuminators.Count ) {
        var light = new GameObject($"Illuminator");
        light.hideFlags = HideFlags.NotEditable;

        light.transform.SetParent( illRoot, false );
      }

      while ( illRoot.childCount > Illuminators.Count ) {
        GameObject.DestroyImmediate( illRoot.GetChild( illRoot.childCount-1 ).gameObject );
      }

      for ( int i = 0; i < Illuminators.Count; i++ ) {
        var light = illRoot.GetChild( i );
        var illum = Illuminators[ i ];
        light.name = $"Illuminator {i} - {illum.IlluminatorType}";
        if ( !light.gameObject.TryGetComponent<Light>( out var lightComp ) )
          lightComp = light.gameObject.AddComponent<Light>();

        lightComp.type = illum.IlluminatorType switch
        {
          Illuminator.Type.Spot => LightType.Spot,
          Illuminator.Type.Point => LightType.Point
        };

        lightComp.color = illum.Color;
        lightComp.range = 100; // TODO: Fine tune range depending on illuminator intensity
        lightComp.spotAngle = illum.ConeAngles.y;
        lightComp.innerSpotAngle = illum.ConeAngles.x;
        illum.UnityLight = lightComp;
        illum.UpdateLightIntensity();
      }
    }

    public override void EditorUpdate()
    {
      if ( SynchronizeUnityChanges )
        SynchronizeCamera();

      SynchronizeIlluminators();
    }

    private void Update()
    {
      SynchronizeIlluminators();
      foreach ( var output in Outputs )
        output.PerformQueuedCapture();
    }

    private void PreStep()
    {
      if ( SynchronizeUnityChanges )
        SynchronizeCamera();
    }

    private void PostStep()
    {
      foreach ( var output in Outputs )
        output.Update();
    }

    protected override bool Initialize()
    {
      OutputConversionCompute = Resources.Load<ComputeShader>( "Shaders/Compute/CameraColorOutputPass" );
      OutputConversionKernel = OutputConversionCompute.FindKernel( "ColorOutputPass" );
      var frame = new agx.Frame();

      var lens = new agxSensor.CameraLensSingleElement();
      lens.setFocalLength( FocalLength );
      lens.setFocusDistance( FocusDistance );
      lens.setFStop( fStop );

      var detector = new agxSensor.CameraCMOSSensor();
      detector.setSize( new agx.Vec2( SensorSize.x, SensorSize.y ) );
      detector.setISO( ISO );
      detector.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
      detector.setShutterSpeed( ShutterSpeed );

      RecreateRenderTexture();

      var illuminators = new agxSensor.ICameraActiveIlluminationRefVector();
      foreach ( var illum in Illuminators ) {
        illum.Initialize( this );
        illuminators.Add( new agxSensor.ICameraActiveIlluminationRef( illum.Native ) );
      }

      var model = new agxSensor.CameraModel(lens, detector, illuminators);

      Native = new agxSensor.Camera( frame, model, CameraBackend.Instance.createBackend() );

      CameraBackend.Instance.MapCamera( Native, this );

      SensorEnvironment.Instance.GetInitialized<SensorEnvironment>().Native.add( Native );

      foreach ( var output in Outputs ) {
        output.Initialize( this );
        Native.getOutputHandler().add( output.Native );
        CameraBackend.Instance.MapColorOutput( output.Native, output );
      }

      Simulation.Instance.StepCallbacks.PreStepForward += PreStep;
      Simulation.Instance.StepCallbacks.PostStepForward += PostStep;

      return base.Initialize();
    }
    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.PreStepForward -= PreStep;
        Simulation.Instance.StepCallbacks.PostStepForward -= PostStep;
      }
      base.OnDestroy();
    }

    internal void EnsureHasOutput()
    {
      if ( Time.frameCount <= LastRenderedFrame )
        return;

      foreach ( var illum in Illuminators ) {
        illum.Flash();
      }

      var request = new RenderPipeline.StandardRequest { destination = Output };
      CameraComponent.SubmitRenderRequest( request );
      LastRenderedFrame = Time.frameCount;
    }

    public void Capture()
    {
      if ( Native != null ) {
        Native.capture();
      }
    }

    private void OnGUI()
    {
      if ( Preview ) {
        GUILayout.Box( new GUIContent( Output ) );
        foreach ( var o in Outputs )
          GUILayout.Box( new GUIContent( o.OutputTexture ) );
      }
    }
  }
}
