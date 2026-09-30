using AGXUnity.Rendering.PostProcessing;
using AGXUnity.Util;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AGXUnity.Sensor
{
  public interface ILensDistortion { }

  [Serializable]
  public class LensDistortionBrownConrady : Subcomponent<CameraLens, agxSensor.LensDistortionBrownConrady>, ILensDistortion
  {
    [SerializeField]
    private Vector3 m_radialCoefficients = Vector3.zero;
    public Vector3 RadialCoefficients
    {
      get => m_radialCoefficients;
      set => PropertyUtil.Assign( ref m_radialCoefficients, value, this );
    }

    [SerializeField]
    private Vector2 m_tangentialCoefficients = Vector2.zero;
    public Vector2 TangentialCoefficients
    {
      get => m_tangentialCoefficients;
      set => PropertyUtil.Assign( ref m_tangentialCoefficients, value, this );
    }

    protected override agxSensor.LensDistortionBrownConrady InitializeNative()
    {
      return new agxSensor.LensDistortionBrownConrady(
        RadialCoefficients.x, RadialCoefficients.y, RadialCoefficients.z,
        TangentialCoefficients.x, TangentialCoefficients.y
      );
    }

    protected override void NativeSync()
    {
      Native?.setCoefficients(
        RadialCoefficients.x, RadialCoefficients.y, RadialCoefficients.z,
        TangentialCoefficients.x, TangentialCoefficients.y );
    }
  }

  [Serializable]
  public class CameraLens : Subcomponent<CameraSensor, agxSensor.CameraLens>
  {
    [SerializeField]
    [Min( 0.0f )]
    [Tooltip( "Physical focal length in meters." )]
    private float m_focalLength = 0.01f;
    public float FocalLength
    {
      get => m_focalLength;
      set => PropertyUtil.Assign( ref m_focalLength, value, this );
    }

    [SerializeField]
    private bool m_autofocus = true;
    public bool Autofocus
    {
      get => m_autofocus;
      set => PropertyUtil.Assign( ref m_autofocus, value, this );
    }

    [SerializeField]
    [DynamicallyShowInInspector(nameof(Autofocus))]
    private CameraAutofocuser m_autofocuser = new CameraAutofocuser();
    public CameraAutofocuser Autofocuser => m_autofocuser;

    public float MinimumFocusDistance
    {
      get => m_autofocuser.MinimumFocusDistance;
      set => m_autofocuser.MinimumFocusDistance = value;
    }

    [SerializeField]
    [Min( 0.0f )]
    [DynamicallyShowInInspector(nameof(Autofocus), invert: true)]
    [Tooltip( "Manual focus distance. Used when autofocus is disabled." )]
    private float m_focusDistance = 10.0f;

    public float FocusDistance
    {
      get => m_focusDistance;
      set => PropertyUtil.Assign( ref m_focusDistance, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_fStop = 2f;
    public float FStop
    {
      get => m_fStop;
      set => PropertyUtil.Assign( ref m_fStop, value, this );
    }

    [SerializeReference]
    private ILensDistortion m_lensDistortion = null;
    public ILensDistortion LensDistortion
    {
      get => m_lensDistortion;
      set => PropertyUtil.Assign( ref m_lensDistortion, value, this );
    }

    protected override agxSensor.CameraLens InitializeNative()
    {
      return new agxSensor.CameraLensSingleElement();
    }

    protected override void NativeSync()
    {
      m_focalLength = Mathf.Max( 0.0f, m_focalLength );
      MinimumFocusDistance = Mathf.Max( 0.0f, MinimumFocusDistance );
      m_focusDistance = Mathf.Max( 0.0f, m_focusDistance );
      m_fStop = Mathf.Max( 0.0f, m_fStop );

      var camera = Parent?.CameraComponent;
      if ( camera != null ) {
        camera.focalLength = FocalLength * 1000.0f;
        camera.aperture = FStop;
        camera.focusDistance = FocusDistance;
      }

      if ( !Autofocus )
        Autofocuser.Dispose();

      if ( Native is not agxSensor.CameraLensSingleElement lens )
        return;

      lens.setFocalLength( FocalLength );
      lens.setFStop( FStop );
      if ( Autofocus ) {
        Autofocuser.Activate( camera, FocusDistance );
        lens.setAutofocus( MinimumFocusDistance );
      }
      else {
        lens.setFocusDistance( FocusDistance );
      }

      if ( LensDistortion is LensDistortionBrownConrady brownConrady ) {
        brownConrady.Initialize( this );
        lens.setLensDistortion( brownConrady.Native );
      }
      else
        lens.setLensDistortion( null );
    }

    protected override void DisposeNative()
    {
      Autofocuser?.Dispose();
      ( m_lensDistortion as LensDistortionBrownConrady )?.Disconnect();
    }

    internal void Update()
    {
      if ( Autofocus ) {
        Autofocuser.MinimumFocusDistance = MinimumFocusDistance;
        Autofocuser.Update();
        m_focusDistance = Autofocuser.FocusDistance;
      }
    }
  }

  [Serializable]
  public class CameraPhotodetector : Subcomponent<CameraSensor, agxSensor.CameraPhotodetector>
  {
    [SerializeField]
    [Tooltip( "Physical sensor size in meters." )]
    private Vector2 m_sensorSize;
    public Vector2 SensorSize
    {
      get => m_sensorSize;
      set => PropertyUtil.Assign( ref m_sensorSize, value, this );
    }

    [SerializeField]
    [Min( 0 )]
    private int m_iso = 200;
    public int ISO
    {
      get => m_iso;
      set => PropertyUtil.Assign( ref m_iso, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_shutterSpeed = 0.005f;
    public float ShutterSpeed
    {
      get => m_shutterSpeed;
      set => PropertyUtil.Assign( ref m_shutterSpeed, value, this );
    }

    [SerializeField]
    private Vector2Int m_resolution = new Vector2Int(128,128);
    public Vector2Int Resolution
    {
      get => m_resolution;
      set => PropertyUtil.Assign( ref m_resolution, value, this );
    }

    protected override agxSensor.CameraPhotodetector InitializeNative()
    {
      return new agxSensor.CameraCMOSSensor();
    }

    protected override void NativeSync()
    {
      m_sensorSize = Vector2.Max( Vector2.zero, m_sensorSize );
      m_iso = Mathf.Max( 0, m_iso );
      m_shutterSpeed = Mathf.Max( 0.0f, m_shutterSpeed );
      m_resolution = new Vector2Int( Mathf.Max( 1, m_resolution.x ),
                                     Mathf.Max( 1, m_resolution.y ) );

      var camera = Parent?.CameraComponent;
      if ( camera != null ) {
        camera.sensorSize = SensorSize * 1000.0f;
        camera.iso = ISO;
        camera.shutterSpeed = ShutterSpeed;
      }

      if ( Native is agxSensor.CameraCMOSSensor cmos ) {
        cmos.setSize( new agx.Vec2( SensorSize.x, SensorSize.y ) );
        cmos.setISO( ISO );
        cmos.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
        cmos.setShutterSpeed( ShutterSpeed );
      }
    }
  }

  [DisallowMultipleComponent]
  [DoNotGenerateCustomEditor]
  [RequireComponent( typeof( Camera ) )]
  [RequireComponent( typeof( Volume ) )]
  [RequireComponent( typeof( SphereCollider ) )]
  public class CameraSensor : ScriptComponent, INativeSynchronizer
  {
    public enum LensDistortionModel
    {
      None = 0,
      BrownConrady = 1
    }

    [Header("Lens")]
    [SerializeField]
    private CameraLens m_lens = new CameraLens();
    public CameraLens Lens => m_lens;

    [Header( "Photodetector" )]
    [SerializeField]
    private CameraPhotodetector m_photoDetector = new CameraPhotodetector();
    public CameraPhotodetector Photodetector => m_photoDetector;

    [Header( "Active Illumination" )]
    [SerializeField]
    private SubcomponentList<Illuminator, CameraSensor> m_illuminators = new();

    [Header( "Outputs" )]
    [SerializeField]
    private SubcomponentList<ColorOutput, CameraSensor> m_outputs = new();

    [Header( "Synchronization" )]
    [SerializeField]
    [Tooltip( "Pull physical camera changes made on Unity's Camera component into this sensor." )]
    public bool SynchronizeUnityChanges = true;

    [SerializeField]
    public bool Preview = false;

    [NonSerialized]
    private bool m_configurationDirty = true;

    [NonSerialized]
    private bool m_isSynchronizingConfiguration = false;

    [NonSerialized]
    private List<Illuminator> m_configuredIlluminators = new List<Illuminator>();

    [NonSerialized]
    private List<ColorOutput> m_configuredOutputs = new List<ColorOutput>();

    public agxSensor.Camera Native { get; private set; }
    public agxSensor.CameraLens NativeLens => Lens.Native;
    public agxSensor.CameraPhotodetector NativePhotodetector => Photodetector.Native;

    public Camera CameraComponent => GetComponent<Camera>();

    public RenderTexture Output { get; private set; }

    public SubcomponentList<Illuminator, CameraSensor> Illuminators => m_illuminators;

    public SubcomponentList<ColorOutput, CameraSensor> Outputs => m_outputs;

    public void SynchronizeNative()
    {
      if ( m_isSynchronizingConfiguration )
        return;

      m_isSynchronizingConfiguration = true;
      try {
        m_configurationDirty = false;
        Photodetector.SynchronizeNative();
        Lens.SynchronizeNative();
        EnsureRenderTexture();
        ReconcileIlluminators();
        ReconcileOutputs();
        SynchronizeIlluminatorVisuals();
        SynchronizeVolume();
      }
      finally {
        m_isSynchronizingConfiguration = false;
      }
    }

    private void EnsureRenderTexture()
    {
      if ( Output != null && Output.width == Photodetector.Resolution.x && Output.height == Photodetector.Resolution.y ) {
        if ( CameraComponent.targetTexture != Output )
          CameraComponent.targetTexture = Output;
        return;
      }

      if ( Output != null ) {
        Output.Release();
        if ( Application.isPlaying )
          Destroy( Output );
        else
          DestroyImmediate( Output );
      }

      Output = new RenderTexture( Photodetector.Resolution.x, Photodetector.Resolution.y, 8, RenderTextureFormat.Default )
      {
        hideFlags = HideFlags.NotEditable,
        name = name + "_Output"
      };
      CameraComponent.targetTexture = Output;
    }

    private void SynchronizeCameraFromUnity()
    {
      var camera = CameraComponent;

      if ( !Mathf.Approximately( Lens.FocalLength, camera.focalLength / 1000.0f ) )
        Lens.FocalLength = camera.focalLength / 1000.0f;
      if ( !Mathf.Approximately( Lens.FStop, camera.aperture ) )
        Lens.FStop = camera.aperture;
      if ( !Lens.Autofocus && !Mathf.Approximately( Lens.FocusDistance, camera.focusDistance ) )
        Lens.FocusDistance = camera.focusDistance;

      if ( !Mathf.Approximately( Photodetector.ISO, camera.iso ) )
        Photodetector.ISO = camera.iso;
      if ( !Mathf.Approximately( Photodetector.ShutterSpeed, camera.shutterSpeed ) )
        Photodetector.ShutterSpeed = camera.shutterSpeed;

      var cameraSensorSize = camera.sensorSize / 1000.0f;
      if ( !Mathf.Approximately( Photodetector.SensorSize.x, cameraSensorSize.x ) ||
           !Mathf.Approximately( Photodetector.SensorSize.y, cameraSensorSize.y ) )
        Photodetector.SensorSize = cameraSensorSize;
    }

    private void ReconcileIlluminators()
    {
      var current = m_illuminators.GetAttachedItems( this, "illuminator", this );
      var nativeIlluminators = Native?.getModel().getIlluminators();
      nativeIlluminators?.Clear();

      foreach ( var previous in m_configuredIlluminators )
        if ( !current.Contains( previous ) || !previous.NativeMatchesConfiguration )
          previous.Disconnect();

      if ( Native != null ) {
        foreach ( var illuminator in current ) {
          if ( !illuminator.Initialize( this ) )
            continue;
          nativeIlluminators.Add( new agxSensor.ICameraActiveIlluminationRef( illuminator.Native ) );
        }
      }
      else {
        foreach ( var illuminator in current )
          illuminator.SynchronizeConfiguration();
      }

      m_configuredIlluminators = current;
    }

    private void ReconcileOutputs()
    {
      var current = m_outputs.GetAttachedItems( this, "color output", this );
      bool orderChanged = !SameOrder( m_configuredOutputs, current );

      foreach ( var previous in m_configuredOutputs )
        if ( !current.Contains( previous ) )
          previous.Disconnect();

      if ( Native != null ) {
        bool rebuildHandlerOrder = orderChanged;
        foreach ( var output in current )
          rebuildHandlerOrder |= output.Native == null;

        if ( rebuildHandlerOrder ) {
          foreach ( var output in current )
            if ( output.Native != null )
              Native.getOutputHandler().removeChild( output.Native );
        }

        foreach ( var output in current ) {
          if ( output.Native == null ) {
            if ( !output.Initialize( this ) )
              continue;
          }
          else
            output.SynchronizeConfiguration();

          if ( rebuildHandlerOrder )
            Native.getOutputHandler().add( output.Native );
        }
      }
      else {
        foreach ( var output in current )
          output.SynchronizeConfiguration();
      }

      m_configuredOutputs = current;
    }

    private static bool SameOrder<T>( List<T> lhs, List<T> rhs )
    {
      if ( lhs.Count != rhs.Count )
        return false;
      for ( int i = 0; i < lhs.Count; ++i )
        if ( !ReferenceEquals( lhs[ i ], rhs[ i ] ) )
          return false;
      return true;
    }

    private void SynchronizeIlluminatorVisuals()
    {
      var illuminationRoot = transform.Find( "Active Illumination" );
      if ( illuminationRoot == null ) {
        illuminationRoot = new GameObject( "Active Illumination" ).transform;
        illuminationRoot.gameObject.hideFlags = HideFlags.DontSave;
        illuminationRoot.SetParent( transform, false );
      }
      illuminationRoot.localPosition = Vector3.zero;
      illuminationRoot.localRotation = Quaternion.identity;
      illuminationRoot.localScale = Vector3.one;

      while ( illuminationRoot.childCount < m_configuredIlluminators.Count ) {
        var light = new GameObject( "Illuminator" );
        light.hideFlags = HideFlags.NotEditable;
        light.transform.SetParent( illuminationRoot, false );
      }

      while ( illuminationRoot.childCount > m_configuredIlluminators.Count ) {
        var child = illuminationRoot.GetChild( illuminationRoot.childCount - 1 );
        child.SetParent( null );
        if ( Application.isPlaying )
          Destroy( child.gameObject );
        else
          DestroyImmediate( child.gameObject );
      }

      for ( int i = 0; i < m_configuredIlluminators.Count; ++i ) {
        var illuminator = m_configuredIlluminators[ i ];
        var light = illuminationRoot.GetChild( i );
        light.name = $"Illuminator {i} - {illuminator.IlluminatorType}";
        if ( !light.TryGetComponent<Light>( out var lightComponent ) )
          lightComponent = light.gameObject.AddComponent<Light>();
        illuminator.UnityLight = lightComponent;
        illuminator.SynchronizeUnityLight();
      }
    }

    private void SynchronizeVolume()
    {
      var profile = GetComponent<Volume>()?.sharedProfile;
      if ( profile == null )
        return;

      if ( !profile.TryGet( out DepthOfField depthOfField ) )
        depthOfField = profile.Add<DepthOfField>();
      depthOfField.active = true;
      depthOfField.mode.Override( DepthOfFieldMode.Bokeh );
      depthOfField.focusDistance.Override( Lens.FocusDistance );
      depthOfField.focalLength.Override( Lens.FocalLength * 1000.0f );
      depthOfField.aperture.Override( Lens.FStop );

      if ( !profile.TryGet( out AGXLensDistortion distortion ) )
        distortion = profile.Add<AGXLensDistortion>();
      distortion.active = true;
      distortion.hideFlags = HideFlags.NotEditable;
      if ( Lens.LensDistortion is LensDistortionBrownConrady brownConrady ) {
        distortion.type.Override( LensDistortionModel.BrownConrady );
        distortion.radialCoefficients.Override( brownConrady.RadialCoefficients );
        distortion.tangentialCoefficients.Override( brownConrady.TangentialCoefficients );
      }
      else {
        distortion.type.Override( LensDistortionModel.None );
      }
    }

    private void OnValidate()
    {
      m_configurationDirty = true;
    }

    public override void EditorUpdate()
    {
      Lens.Bind( this );
      Photodetector.Bind( this );

      if ( SynchronizeUnityChanges )
        SynchronizeCameraFromUnity();

      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.UpdateLightIntensity();
    }

    private void Update()
    {
      foreach ( var output in m_configuredOutputs )
        output.PerformQueuedCapture();

      Lens.Update();

      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.UpdateLightIntensity();
    }

    private void PreStep()
    {
      if ( SynchronizeUnityChanges )
        SynchronizeCameraFromUnity();
      if ( m_configurationDirty )
        SynchronizeNative();
    }

    private void PostStep()
    {
      foreach ( var output in m_configuredOutputs )
        output.Update();
    }

    protected override bool Initialize()
    {
      m_illuminators.OnChange += SynchronizeNative;
      m_outputs.OnChange += SynchronizeNative;

      Lens.Initialize( this );
      Photodetector.Initialize( this );

      var model = new agxSensor.CameraModel( NativeLens,
                                             NativePhotodetector,
                                             new agxSensor.ICameraActiveIlluminationRefVector() );
      Native = new agxSensor.Camera( new agx.Frame(), model, CameraBackend.Instance.createBackend() );
      CameraBackend.Instance.MapCamera( Native, this );
      SensorEnvironment.Instance.GetInitialized<SensorEnvironment>().Native.add( Native );

      m_configurationDirty = true;
      SynchronizeNative();

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

      if ( SensorEnvironment.HasInstance )
        SensorEnvironment.Instance.Native?.remove( Native );

      foreach ( var output in m_configuredOutputs )
        output.Disconnect();
      m_configuredOutputs.Clear();

      Native?.getModel().getIlluminators().Clear();
      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.Disconnect();
      m_configuredIlluminators.Clear();

      Lens.Disconnect();
      Photodetector.Disconnect();

      if ( Output != null ) {
        Output.Release();
        if ( Application.isPlaying )
          Destroy( Output );
        else
          DestroyImmediate( Output );
        Output = null;
      }

      CameraBackend.Instance.UnmapCamera( Native );
      Native?.Dispose();
      Native = null;
      base.OnDestroy();
    }

    internal void EnsureHasOutput()
    {
      if ( Time.frameCount <= LastRenderedFrame )
        return;

      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.Flash();

      SynchronizeVolume();
      CameraComponent.Render();
      LastRenderedFrame = Time.frameCount;
    }

    public void Capture()
    {
      Native?.capture();
    }

    private int LastRenderedFrame { get; set; } = -1;

    private void OnGUI()
    {
      if ( Preview && Output != null )
        GUILayout.Box( new GUIContent( Output ) );
    }
  }
}
