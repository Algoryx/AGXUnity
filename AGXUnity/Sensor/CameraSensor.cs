using AGXUnity.Rendering.PostProcessing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.RenderPipeline;

namespace AGXUnity.Sensor
{
  internal class CameraAutofocuser
  {
    private RenderTexture Depth { get; set; }
    private ComputeShader DepthSampler;
    private int DepthSamplerKernel;
    private ComputeBuffer DepthSamplerBuffer;
    private float m_targetFocusDistance;
    private float m_autofocusPeriod = 0.2f;
    private float m_lastFocus = float.NegativeInfinity;
    private Camera m_camera;

    public float MinimumFocusDistance { get; set; } = 0.1f;
    public float FocusDistance { get; private set; }

    internal CameraAutofocuser( Camera cam, float initialFocusDistance = 10 )
    {
      m_camera = cam;
      FocusDistance = initialFocusDistance;

      var desc = new RenderTextureDescriptor(128, 128)
      {
        graphicsFormat = GraphicsFormat.None,
        depthStencilFormat = GraphicsFormat.D32_SFloat,
        msaaSamples = 1
      };
      Depth = new RenderTexture( desc );
      Depth.Create();

      DepthSampler ??= Resources.Load<ComputeShader>( "Shaders/Compute/CameraAutofocusDistance" );
      DepthSamplerKernel = DepthSampler.FindKernel( "SampleDepth" );
      DepthSamplerBuffer ??= new ComputeBuffer( 1, sizeof( float ) );

      DepthSampler.SetBuffer( DepthSamplerKernel, "Result", DepthSamplerBuffer );
      DepthSampler.SetTexture( DepthSamplerKernel, "DepthTexture", Depth, 0, RenderTextureSubElement.Depth );
    }

    internal void Update()
    {
      if ( Time.time > m_lastFocus + m_autofocusPeriod ) {
        var urpData = m_camera.GetUniversalAdditionalCameraData();

        bool prePP = urpData.renderPostProcessing;
        bool preShadows = urpData.renderShadows;
        bool preReqColor = urpData.requiresColorTexture;
        bool preReqDepth = urpData.requiresDepthTexture;
        CameraClearFlags preClear = m_camera.clearFlags;
        DepthTextureMode preDepthMode = m_camera.depthTextureMode;

        try {
          urpData.renderPostProcessing = false;
          urpData.renderShadows = false;
          urpData.requiresColorTexture = false;
          urpData.requiresDepthTexture = false;

          m_camera.clearFlags = CameraClearFlags.SolidColor;
          m_camera.depthTextureMode = DepthTextureMode.None;

          var request = new StandardRequest() {destination = Depth};
          m_camera.SubmitRenderRequest( request );
        }
        finally {
          urpData.renderPostProcessing = prePP;
          urpData.renderShadows = preShadows;
          urpData.requiresColorTexture = preReqColor;
          urpData.requiresDepthTexture = preReqDepth;

          m_camera.clearFlags = preClear;
          m_camera.depthTextureMode = preDepthMode;
        }

        Vector4 zBufferParams;

        float n = m_camera.nearClipPlane;
        float f = m_camera.farClipPlane;

        if ( SystemInfo.usesReversedZBuffer ) {
          float x = -1.0f + f / n;
          zBufferParams = new Vector4( x, 1.0f, x / f, 1.0f / f );
        }
        else {
          float x = 1.0f - f / n;
          float y = f / n;

          zBufferParams = new Vector4( x, y, x / f, y / f );
        }

        DepthSampler.SetVector( "ZBufferParams", zBufferParams );
        DepthSampler.Dispatch( DepthSamplerKernel, 1, 1, 1 );

        AsyncGPUReadback.Request( DepthSamplerBuffer, req => {
          m_targetFocusDistance = req.GetData<float>()[ 0 ];
        } );
        m_lastFocus = Time.time;
      }
      FocusDistance = Mathf.Max( MinimumFocusDistance, Mathf.Lerp( FocusDistance, m_targetFocusDistance, 0.05f ) );
    }
  }

  [DisallowMultipleComponent]
  [RequireComponent( typeof( Camera ) )]
  [RequireComponent( typeof( Volume ) )]
  [RequireComponent( typeof( SphereCollider ) )]
  public class CameraSensor : ScriptComponent
  {
    public agxSensor.Camera Native { get; private set; }
    public agxSensor.CameraLens NativeLens { get; private set; }
    public agxSensor.CameraPhotodetector NativePhotodetector { get; private set; }

    public Camera CameraComponent => GetComponent<Camera>();

    public RenderTexture Output { get; private set; }

    private CameraAutofocuser m_autofocuser;

    [SerializeField]
    private float m_focalLength;

    [InspectorGroupBegin( Name = "Lens Properties" )]
    public float FocalLength
    {
      get => m_focalLength;
      set
      {
        if ( m_focalLength == value ) return;
        CameraComponent.focalLength = value * 1000;
        m_focalLength = value;
        if ( NativeLens is agxSensor.CameraLensSingleElement lens )
          lens.setFocalLength( m_focalLength );
      }
    }

    [SerializeField]
    private bool m_autofocus = true;
    public bool Autofocus
    {
      get => m_autofocus;
      set
      {
        if ( m_autofocus == value )
          return;
        m_autofocus = value;

        if ( !m_autofocus )
          m_autofocuser = null;

        if ( NativeLens is agxSensor.CameraLensSingleElement lens ) {
          if ( m_autofocus )
            lens.setAutofocus( MinimumFocusDistance );
          else
            lens.setFocusDistance( FocusDistance );
        }
      }
    }

    [SerializeField]
    private float m_minimumFocusDistance;

    [DynamicallyShowInInspector( nameof( Autofocus ) )]
    public float MinimumFocusDistance
    {
      get => m_minimumFocusDistance;
      set
      {
        if ( m_minimumFocusDistance == value ) return;

        m_minimumFocusDistance = value;
        if ( Autofocus && NativeLens is agxSensor.CameraLensSingleElement lens )
          lens.setAutofocus( m_minimumFocusDistance );
      }
    }

    [SerializeField]
    private float m_focusDistance;

    [DynamicallyShowInInspector( nameof( Autofocus ), invert: false )]
    public float FocusDistance
    {
      get => m_focusDistance;
      set
      {
        if ( m_focusDistance == value && !Autofocus ) return;

        CameraComponent.focusDistance = value;
        m_focusDistance = value;
        Autofocus = false;
        if ( NativeLens is agxSensor.CameraLensSingleElement lens )
          lens.setFocusDistance( m_minimumFocusDistance );
      }
    }

    [SerializeField]
    private float m_fStop;

    public float fStop
    {
      get => m_fStop;
      set
      {
        if ( m_fStop == value ) return;
        CameraComponent.aperture = value;
        m_fStop = value;
        if ( NativeLens is agxSensor.CameraLensSingleElement lens )
          lens.setFStop( m_fStop );
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

    [SerializeField]
    public List<Illuminator> Illuminators = new List<Illuminator>();

    [SerializeField]
    public List<ColorOutput> Outputs = new List<ColorOutput>();

    [field: SerializeField]
    [InspectorGroupEnd]
    public bool SynchronizeUnityChanges { get; set; } = true;

    [field: SerializeField]
    public bool Preview { get; set; } = false;

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

      while ( illRoot.childCount > Illuminators.Count )
        GameObject.DestroyImmediate( illRoot.GetChild( illRoot.childCount-1 ).gameObject );

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

    private void SynchronizeVolume()
    {
      var volume = GetComponent<Volume>();
      var profile = volume.sharedProfile;
      if ( !profile.TryGet( out DepthOfField dof ) )
        dof = profile.Add<DepthOfField>();
      dof.active = true;
      dof.mode.Override( DepthOfFieldMode.Bokeh );
      dof.focusDistance.Override( FocusDistance );
      dof.focalLength.Override( FocalLength * 1000 );
      dof.aperture.Override( fStop );

      if ( !profile.TryGet( out AGXLensDistortion distortion ) )
        distortion = profile.Add<AGXLensDistortion>();
      distortion.active = true;
    }

    private void Update()
    {
      SynchronizeIlluminators();
      foreach ( var output in Outputs )
        output.PerformQueuedCapture();

      if ( Autofocus ) {
        m_autofocuser ??= new CameraAutofocuser( CameraComponent, FocusDistance );
        m_autofocuser.MinimumFocusDistance = MinimumFocusDistance;
        m_autofocuser.Update();
        m_focusDistance = m_autofocuser.FocusDistance;
      }
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
      var frame = new agx.Frame();

      var lens = new agxSensor.CameraLensSingleElement();
      NativeLens = lens;
      lens.setFocalLength( FocalLength );
      if ( Autofocus )
        lens.setAutofocus( MinimumFocusDistance );
      else
        lens.setFocusDistance( FocusDistance );
      lens.setFStop( fStop );

      var detector = new agxSensor.CameraCMOSSensor();
      NativePhotodetector = detector;
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

      var model = new agxSensor.CameraModel(NativeLens, NativePhotodetector, illuminators);

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

      SynchronizeVolume();

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
      if ( Preview )
        GUILayout.Box( new GUIContent( Output ) );
    }
  }
}
