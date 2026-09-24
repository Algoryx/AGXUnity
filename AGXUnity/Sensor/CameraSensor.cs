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
      if ( Preview )
        GUILayout.Box( new GUIContent( Output ) );
    }
  }
}
