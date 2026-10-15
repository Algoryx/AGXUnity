using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.IO;

namespace AGXUnity.Rendering.URP
{
  /// <summary>Comparison controls and a kinematic grading blade for the example.</summary>
  public sealed class SoilSurfaceExample : MonoBehaviour
  {
    public TerrainParticleSurfaceRenderer Renderer;
    public AGXUnity.RigidBody Blade;
    [Tooltip( "Example-only copy of the same URP pipeline, with the soil renderer feature. Restored on disable." )]
    public UniversalRenderPipelineAsset ExamplePipeline;
    private RenderPipelineAsset m_previousDefault, m_previousQuality;
    private bool m_appliedPipeline;
    private Vector3 m_origin;
    private string m_smokeOutput;
    private Camera m_smokeCamera;
    private RenderTexture m_smokeTarget;
    private UniversalRenderPipeline.SingleCameraRequest m_smokeRequest;
    private int m_smokeFrames;

    private void Start()
    {
      var arguments = Environment.GetCommandLineArgs();
      for ( int i = 0; i + 1 < arguments.Length; ++i )
        if ( arguments[i] == "-agxSoilSmokeOutput" ) m_smokeOutput = arguments[i+1];
      if ( string.IsNullOrEmpty( m_smokeOutput ) ) return;
      Application.runInBackground = true;
      Directory.CreateDirectory( m_smokeOutput );
      m_smokeCamera = Camera.main;
      m_smokeTarget = new RenderTexture( 960, 640, 24, RenderTextureFormat.ARGB32 );
      m_smokeTarget.Create(); m_smokeCamera.targetTexture = m_smokeTarget;
      m_smokeRequest = new UniversalRenderPipeline.SingleCameraRequest { destination = m_smokeTarget };
    }

    private void LateUpdate()
    {
      if ( m_smokeTarget == null ) return;
      if ( RenderPipeline.SupportsRenderRequest( m_smokeCamera, m_smokeRequest ) ) RenderPipeline.SubmitRenderRequest( m_smokeCamera, m_smokeRequest );
      if ( ++m_smokeFrames < 150 || Time.timeSinceLevelLoad < 3 ) return;
      var previous = RenderTexture.active; RenderTexture.active = m_smokeTarget;
      var image = new Texture2D( 960, 640, TextureFormat.RGB24, false );
      image.ReadPixels( new Rect( 0, 0, 960, 640 ), 0, 0 ); image.Apply();
      File.WriteAllBytes( Path.Combine( m_smokeOutput, "player.png" ), image.EncodeToPNG() );
      RenderTexture.active = previous;
      bool supported = TerrainParticleSurfaceFeature.SupportsCamera( m_smokeCamera );
      var pixels = image.GetPixels32(); bool visible = false;
      for ( int i = 1; i < pixels.Length; ++i )
        if ( pixels[i].r != pixels[0].r || pixels[i].g != pixels[0].g || pixels[i].b != pixels[0].b ) { visible = true; break; }
      File.WriteAllText( Path.Combine( m_smokeOutput, "player-smoke.txt" ),
        $"Unity: {Application.unityVersion}\nAPI: {SystemInfo.graphicsDeviceType}\nSurface supported: {supported}\nRendered geometry: {visible}\nEligible: {Renderer.Snapshot.Count}\nSurface: {Renderer.SurfaceCount}\nIndividual: {Renderer.IndividualCount}\nBridge backend: {Renderer.BridgeBackend}\nSnapshot captures: {Renderer.Snapshot.CaptureCount}\nPack/upload calls: {Renderer.PackCount}\nNative traversals: {TerrainParticleSnapshotProvider.NativeTraversalCount}\n" );
      Destroy( image ); m_smokeCamera.targetTexture = null; m_smokeTarget.Release(); Destroy( m_smokeTarget );
      m_smokeTarget = null;
      Application.Quit( supported && visible && Renderer.Snapshot.Count > 0 ? 0 : 1 );
    }

    private void OnEnable()
    {
      if ( !Application.isPlaying ) return;
      m_previousDefault = GraphicsSettings.defaultRenderPipeline;
      m_previousQuality = QualitySettings.renderPipeline;
      if ( ExamplePipeline != null ) {
        GraphicsSettings.defaultRenderPipeline = ExamplePipeline;
        QualitySettings.renderPipeline = ExamplePipeline;
        m_appliedPipeline = true;
      }
      if ( Blade != null ) m_origin = Blade.transform.position;
      Simulation.Instance.StepCallbacks.PreStepForward += MoveBlade;
    }

    private void OnDisable()
    {
      if ( Simulation.HasInstance ) Simulation.Instance.StepCallbacks.PreStepForward -= MoveBlade;
      if ( m_appliedPipeline ) {
        GraphicsSettings.defaultRenderPipeline = m_previousDefault;
        QualitySettings.renderPipeline = m_previousQuality;
        m_appliedPipeline = false;
      }
    }

    private void MoveBlade()
    {
      if ( Blade?.Native == null ) return;
      // Drive via AGX velocity, rather than teleporting the simulated shape.
      float elapsed = (float)Simulation.Instance.Native.getTimeStamp();
      var target = m_origin + Vector3.forward * (1.5f * Mathf.Sin( elapsed * 0.6f ));
      Blade.LinearVelocity = (target - Blade.transform.position) * 3;
    }

    public void Ordinary() => Renderer.Display = TerrainParticleSurfaceRenderer.DisplayMode.OrdinaryParticles;
    public void FullSurface() { Renderer.Display = TerrainParticleSurfaceRenderer.DisplayMode.Surface; Renderer.IndividualParticleFraction = 0; }
    public void Mixed() { Renderer.Display = TerrainParticleSurfaceRenderer.DisplayMode.Surface; Renderer.IndividualParticleFraction = 0.05f; }

    private void OnGUI()
    {
      GUILayout.BeginArea( new Rect( 12, 12, 300, 170 ), GUI.skin.box );
      GUILayout.Label( "AGX rolling soil — URP Forward+" );
      if ( GUILayout.Button( "Ordinary particles" ) ) Ordinary();
      if ( GUILayout.Button( "Full surface" ) ) FullSurface();
      if ( GUILayout.Button( "Surface + 5% rocks" ) ) Mixed();
      if ( GUILayout.Button( "Switch sphere / mesh input" ) ) Renderer.SurfaceInput =
        Renderer.SurfaceInput == TerrainParticleSurfaceRenderer.SurfaceInputMode.Sphere ?
          TerrainParticleSurfaceRenderer.SurfaceInputMode.Mesh : TerrainParticleSurfaceRenderer.SurfaceInputMode.Sphere;
      GUILayout.Label( $"Surface: {Renderer.SurfaceCount}    Rocks: {Renderer.IndividualCount}" );
      GUILayout.EndArea();
    }
  }
}
