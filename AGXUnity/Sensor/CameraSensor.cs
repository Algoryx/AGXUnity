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
  public class LensDistortionBrownConrady : Subcomponent<CameraSensor, agxSensor.LensDistortionBrownConrady>, ILensDistortion
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

    protected override void SynchronizeNative()
    {
      Native?.setCoefficients(
        RadialCoefficients.x, RadialCoefficients.y, RadialCoefficients.z,
        TangentialCoefficients.x, TangentialCoefficients.y );
      Parent?.SynchronizeConfiguration();
    }
  }

  [Serializable]
  public class CameraLens : Subcomponent<CameraSensor, agxSensor.CameraLens>
  {
    [SerializeField]
    [Min( 0.0f )]
    [Tooltip( "Physical focal length in meters." )]
    private float m_focalLength;
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

    public float MinimumFocusDistance
    {
      get => m_autofocuser.MinimumFocusDistance;
      set => m_autofocuser.MinimumFocusDistance = value;
    }

    [SerializeField]
    [Min( 0.0f )]
    [DynamicallyShowInInspector(nameof(Autofocus), invert: true)]
    [Tooltip( "Manual focus distance. Used when autofocus is disabled." )]
    private float m_focusDistance;

    public float FocusDistance
    {
      get => m_focusDistance;
      set => PropertyUtil.Assign( ref m_focusDistance, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_fStop;
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
      throw new NotImplementedException();
    }

    protected override void SynchronizeNative()
    {
      throw new NotImplementedException();
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

    [Header( "Lens" )]
    [SerializeField]
    [Min( 0.0f )]
    [Tooltip( "Physical focal length in meters." )]
    private float m_focalLength;
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

    public float MinimumFocusDistance
    {
      get => m_autofocuser.MinimumFocusDistance;
      set => m_autofocuser.MinimumFocusDistance = value;
    }

    [SerializeField]
    [Min( 0.0f )]
    [DynamicallyShowInInspector(nameof(Autofocus), invert: true)]
    [Tooltip( "Manual focus distance. Used when autofocus is disabled." )]
    private float m_focusDistance;

    public float FocusDistance
    {
      get => m_focusDistance;
      set => PropertyUtil.Assign( ref m_focusDistance, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_fStop;
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

    [Header( "Photodetector" )]
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
    private int m_iso;
    public int ISO
    {
      get => m_iso;
      set => PropertyUtil.Assign( ref m_iso, value, this );
    }

    [SerializeField]
    [Min( 0.0f )]
    private float m_shutterSpeed;
    public float ShutterSpeed
    {
      get => m_shutterSpeed;
      set => PropertyUtil.Assign( ref m_shutterSpeed, value, this );
    }

    [SerializeField]
    private Vector2Int m_resolution;
    public Vector2Int Resolution
    {
      get => m_resolution;
      set => PropertyUtil.Assign( ref m_resolution, value, this );
    }

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
    public agxSensor.CameraLens NativeLens { get; private set; }
    public agxSensor.CameraPhotodetector NativePhotodetector { get; private set; }

    public Camera CameraComponent => GetComponent<Camera>();

    public RenderTexture Output { get; private set; }

    public CameraAutofocuser Autofocuser => m_autofocuser;

    public SubcomponentList<Illuminator, CameraSensor> Illuminators => m_illuminators;

    public SubcomponentList<ColorOutput, CameraSensor> Outputs => m_outputs;

    public void SynchronizeConfiguration()
    {
      if ( m_isSynchronizingConfiguration )
        return;

      m_isSynchronizingConfiguration = true;
      try {
        m_configurationDirty = false;
        NormalizeConfiguration();
        SynchronizeLens();
        SynchronizePhotodetector();
        ReconcileIlluminators();
        ReconcileOutputs();
        SynchronizeIlluminatorVisuals();
        SynchronizeVolume();
      }
      finally {
        m_isSynchronizingConfiguration = false;
      }
    }

    void INativeSynchronizer.SynchronizeNative() => SynchronizeConfiguration();

    private void NormalizeConfiguration()
    {
      m_focalLength = Mathf.Max( 0.0f, m_focalLength );
      MinimumFocusDistance = Mathf.Max( 0.0f, MinimumFocusDistance );
      m_focusDistance = Mathf.Max( 0.0f, m_focusDistance );
      m_fStop = Mathf.Max( 0.0f, m_fStop );
      m_sensorSize = Vector2.Max( Vector2.zero, m_sensorSize );
      m_iso = Mathf.Max( 0, m_iso );
      m_shutterSpeed = Mathf.Max( 0.0f, m_shutterSpeed );
      m_resolution = new Vector2Int( Mathf.Max( 1, m_resolution.x ),
                                     Mathf.Max( 1, m_resolution.y ) );
    }

    private void SynchronizeLens()
    {
      var camera = CameraComponent;
      camera.focalLength = FocalLength * 1000.0f;
      camera.aperture = FStop;
      camera.focusDistance = FocusDistance;

      if ( !Autofocus )
        Autofocuser.Dispose();

      if ( NativeLens is not agxSensor.CameraLensSingleElement lens )
        return;

      lens.setFocalLength( FocalLength );
      lens.setFStop( FStop );
      if ( Autofocus ) {
        Autofocuser.Activate( camera, FocusDistance );
        lens.setAutofocus( MinimumFocusDistance );
      }
      else
        lens.setFocusDistance( FocusDistance );

      if ( LensDistortion is LensDistortionBrownConrady brownConrady ) {
        brownConrady.Attach( this );
        lens.setLensDistortion( brownConrady.Native );
      }
      else {
        lens.setLensDistortion( null );
      }
    }

    private void SynchronizePhotodetector()
    {
      var camera = CameraComponent;
      camera.sensorSize = SensorSize * 1000.0f;
      camera.iso = ISO;
      camera.shutterSpeed = ShutterSpeed;

      if ( NativePhotodetector is agxSensor.CameraCMOSSensor detector ) {
        detector.setSize( new agx.Vec2( SensorSize.x, SensorSize.y ) );
        detector.setISO( ISO );
        detector.setResolution( new agx.Vec2i( Resolution.x, Resolution.y ) );
        detector.setShutterSpeed( ShutterSpeed );
      }

      EnsureRenderTexture();
    }

    private void EnsureRenderTexture()
    {
      if ( Output != null && Output.width == Resolution.x && Output.height == Resolution.y ) {
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

      Output = new RenderTexture( Resolution.x, Resolution.y, 8, RenderTextureFormat.Default )
      {
        hideFlags = HideFlags.NotEditable,
        name = name + "_Output"
      };
      CameraComponent.targetTexture = Output;
    }

    private void SynchronizeCameraFromUnity()
    {
      var camera = CameraComponent;
      bool changed = false;

      changed |= AssignIfDifferent( ref m_focalLength, camera.focalLength / 1000.0f );
      changed |= AssignIfDifferent( ref m_fStop, camera.aperture );
      if ( !Autofocus )
        changed |= AssignIfDifferent( ref m_focusDistance, camera.focusDistance );
      changed |= AssignIfDifferent( ref m_iso, camera.iso );
      changed |= AssignIfDifferent( ref m_shutterSpeed, camera.shutterSpeed );

      var sensorSize = camera.sensorSize / 1000.0f;
      if ( !Approximately( m_sensorSize, sensorSize ) ) {
        m_sensorSize = sensorSize;
        changed = true;
      }

      if ( changed )
        SynchronizeConfiguration();
    }

    private static bool AssignIfDifferent( ref float destination, float value )
    {
      if ( Mathf.Approximately( destination, value ) )
        return false;
      destination = value;
      return true;
    }

    private static bool AssignIfDifferent( ref int destination, int value )
    {
      if ( destination == value )
        return false;
      destination = value;
      return true;
    }

    private static bool Approximately( Vector2 lhs, Vector2 rhs ) =>
      Mathf.Approximately( lhs.x, rhs.x ) && Mathf.Approximately( lhs.y, rhs.y );

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
          if ( !illuminator.Attach( this ) )
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
            if ( !output.Attach( this ) )
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
      depthOfField.focusDistance.Override( FocusDistance );
      depthOfField.focalLength.Override( FocalLength * 1000.0f );
      depthOfField.aperture.Override( FStop );

      if ( !profile.TryGet( out AGXLensDistortion distortion ) )
        distortion = profile.Add<AGXLensDistortion>();
      distortion.active = true;
      distortion.hideFlags = HideFlags.NotEditable;
      if ( LensDistortion is LensDistortionBrownConrady brownConrady ) {
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
      if ( m_configurationDirty )
        SynchronizeConfiguration();
      else if ( SynchronizeUnityChanges )
        SynchronizeCameraFromUnity();

      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.UpdateLightIntensity();
    }

    private void Update()
    {
      if ( m_configurationDirty )
        SynchronizeConfiguration();

      foreach ( var output in m_configuredOutputs )
        output.PerformQueuedCapture();

      if ( Autofocus ) {
        Autofocuser.MinimumFocusDistance = MinimumFocusDistance;
        Autofocuser.Update();
        m_focusDistance = Autofocuser.FocusDistance;
      }

      foreach ( var illuminator in m_configuredIlluminators )
        illuminator.UpdateLightIntensity();
    }

    private void PreStep()
    {
      if ( m_configurationDirty )
        SynchronizeConfiguration();
      if ( SynchronizeUnityChanges )
        SynchronizeCameraFromUnity();
    }

    private void PostStep()
    {
      foreach ( var output in m_configuredOutputs )
        output.Update();
    }

    protected override bool Initialize()
    {
      m_illuminators.OnChange += SynchronizeConfiguration;
      m_outputs.OnChange += SynchronizeConfiguration;

      NativeLens = new agxSensor.CameraLensSingleElement();
      NativePhotodetector = new agxSensor.CameraCMOSSensor();

      var model = new agxSensor.CameraModel( NativeLens,
                                             NativePhotodetector,
                                             new agxSensor.ICameraActiveIlluminationRefVector() );
      Native = new agxSensor.Camera( new agx.Frame(), model, CameraBackend.Instance.createBackend() );
      CameraBackend.Instance.MapCamera( Native, this );
      SensorEnvironment.Instance.GetInitialized<SensorEnvironment>().Native.add( Native );

      m_configurationDirty = true;
      SynchronizeConfiguration();

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

      Autofocuser?.Dispose();
      if ( NativeLens is agxSensor.CameraLensSingleElement lens )
        lens.setLensDistortion( null );
      ( m_lensDistortion as LensDistortionBrownConrady )?.Disconnect();

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
      NativeLens = null;
      NativePhotodetector = null;
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
