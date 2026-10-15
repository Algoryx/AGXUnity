using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Rendering.URP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Exception = System.Exception;

namespace AGXUnityEditor.Rendering
{
  /// <summary>Batch-mode integration checks; run in an isolated project with a graphics device.</summary>
  public static class SoilSurfaceValidation
  {
    private static readonly List<string> s_report = new List<string>();
    private static string s_output;
    private static int s_errors;
    private static TerrainParticleSurfaceRenderer s_primaryRenderer;

    public static void Run()
    {
      InitializeOutput();
      Record( $"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; {SystemInfo.graphicsDeviceName}" );
      CheckSelection();
      if ( string.IsNullOrEmpty( UnityEngine.SceneManagement.SceneManager.GetActiveScene().path ) && Application.isBatchMode )
        EditorSceneManager.SaveScene( UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/__SoilValidationEmpty.unity" );
      if ( !File.Exists( SoilSurfaceExampleBuilder.Destination + "/SoilSurfaceGrading.unity" ) ) SoilSurfaceExampleBuilder.CreateExample();
      EditorSceneManager.OpenScene( SoilSurfaceExampleBuilder.Destination + "/SoilSurfaceGrading.unity" );
      File.WriteAllLines( Path.Combine( s_output, "validation.txt" ), s_report );
      SessionState.SetBool( "AGXSoilValidation.Pending", true );
      EditorApplication.isPlaying = true;
      ResumeAfterReload();
    }

    public static void BuildWindows()
    {
      InitializeOutput();
      BenchmarkLargeBridgeSet();
      var report = BuildPipeline.BuildPlayer( new BuildPlayerOptions {
        scenes = new[] { SoilSurfaceExampleBuilder.Destination + "/SoilSurfaceGrading.unity" },
        locationPathName = Path.Combine( s_output, "Player", "SoilSurface.exe" ),
        target = BuildTarget.StandaloneWindows64,
        options = BuildOptions.Development
      } );
      File.WriteAllText( Path.Combine( s_output, "player-build.txt" ),
        $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n" );
      EditorApplication.Exit( report.summary.result == BuildResult.Succeeded ? 0 : 1 );
    }

    private static void BenchmarkLargeBridgeSet()
    {
      var particles = new TerrainParticleSnapshotProvider.Particle[8192];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( (i%32)*0.095f, ((i/32)%8)*0.095f, (i/256)*0.095f );
        particles[i].Radius = 0.04f+0.04f*(i%7)/6;
        particles[i].RenderingEligible = SoilParticleSelection.IsIndividualParticle( (uint)i, 0.05 ) ? SoilParticleBridges.CenterAnchor : 1;
      }
      using var managed = new SoilParticleBridges( false );
      using var accelerated = new SoilParticleBridges();
      double managedTime = Measure( () => managed.Build( particles, particles.Length, 1.5f ), 30, out _ );
      double acceleratedTime = Measure( () => accelerated.Build( particles, particles.Length, 1.5f ), 100, out _ );
      var expected = new HashSet<ulong>();
      for ( int i = 0; i < managed.Count; ++i ) expected.Add( ((ulong)managed.Pairs[i].A << 32) | managed.Pairs[i].B );
      if ( expected.Count != accelerated.Count ) {
        var actual = new HashSet<ulong>();
        for ( int i = 0; i < accelerated.Count; ++i ) actual.Add( ((ulong)accelerated.Pairs[i].A << 32) | accelerated.Pairs[i].B );
        var diagnostics = new List<string> { $"managed={expected.Count}; accelerated={actual.Count}; candidates={managed.CandidateCount}/{accelerated.CandidateCount}" };
        foreach ( ulong pair in expected.Except( actual ).Concat( actual.Except( expected ) ).Take( 20 ) ) {
          var a = particles[(int)(pair >> 32)]; var b = particles[(int)(pair & 0xffffffff)];
          float dx = a.Position.x-b.Position.x, dy = a.Position.y-b.Position.y, dz = a.Position.z-b.Position.z;
          float reach = 1.5f*(a.Radius+b.Radius);
          diagnostics.Add( $"{pair >> 32}/{pair & 0xffffffff}: managed={expected.Contains( pair )}; d2={dx*dx+dy*dy+dz*dz:R}; r2={reach*reach:R}; A={a.Position}; B={b.Position}; radii={a.Radius:R}/{b.Radius:R}" );
        }
        File.WriteAllLines( Path.Combine( s_output, "large-mismatch.txt" ), diagnostics );
        throw new Exception( "Large bridge count mismatch; see large-mismatch.txt" );
      }
      for ( int i = 0; i < accelerated.Count; ++i ) RequireSilent( expected.Remove( ((ulong)accelerated.Pairs[i].A << 32) | accelerated.Pairs[i].B ), "Large bridge pair mismatch or duplicate" );
      RequireSilent( expected.Count == 0, "Large bridge set incomplete" );
      File.WriteAllText( Path.Combine( s_output, "large-bridges.txt" ),
        $"Particles: {particles.Length}\nPairs: {accelerated.Count}\nBackend: {accelerated.Backend}\nManaged ms: {managedTime:F4}\nAccelerated ms: {acceleratedTime:F4}\nSpeedup: {managedTime/Math.Max( acceleratedTime, 1e-6 ):F2}x\nExact pair equality: True\n" );
    }

    public static void UpdateExampleAssets()
    {
      var texture = AssetDatabase.LoadAssetAtPath<Texture2D>( SoilSurfaceExampleBuilder.Destination + "/SoilTexture.asset" );
      var replacement = SoilSurfaceExampleBuilder.CreateSoilTexture();
      EditorUtility.CopySerialized( replacement, texture ); Object.DestroyImmediate( replacement );
      EditorUtility.SetDirty( texture );
      var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>( SoilSurfaceExampleBuilder.Destination + "/SoilRenderer.asset" );
      if ( !data.rendererFeatures.Any( f => f is ScreenSpaceAmbientOcclusion ) ) {
        var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        ao.name = "Screen Space Ambient Occlusion";
        data.rendererFeatures.Add( ao ); AssetDatabase.AddObjectToAsset( ao, data );
      }
      for ( int i = 0; i < data.rendererFeatures.Count; ++i ) {
        var feature = data.rendererFeatures[i];
        if ( AssetDatabase.GetAssetPath( feature ) == AssetDatabase.GetAssetPath( data ) ) continue;
        var clone = Object.Instantiate( feature );
        data.rendererFeatures[i] = clone; AssetDatabase.AddObjectToAsset( clone, data );
      }
      EditorUtility.SetDirty( data ); AssetDatabase.SaveAssets();
      var scene = EditorSceneManager.OpenScene( SoilSurfaceExampleBuilder.Destination + "/SoilSurfaceGrading.unity", OpenSceneMode.Additive );
      var renderer = scene.GetRootGameObjects().SelectMany( root => root.GetComponentsInChildren<TerrainParticleSurfaceRenderer>() ).First();
      renderer.SupportRadiusScale = 1.5f; renderer.Smoothing = 1;
      EditorUtility.SetDirty( renderer ); EditorSceneManager.SaveScene( scene ); EditorSceneManager.CloseScene( scene, true );
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload()
    {
      if ( !SessionState.GetBool( "AGXSoilValidation.Pending", false ) ) return;
      EditorApplication.update -= AwaitPlay;
      EditorApplication.update += AwaitPlay;
    }

    private static int s_frames;
    private static void AwaitPlay()
    {
      if ( !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ) return;
      if ( ++s_frames < 10 ) return;
      EditorApplication.update -= AwaitPlay;
      SessionState.SetBool( "AGXSoilValidation.Pending", false );
      InitializeOutput();
      s_report.Clear();
      s_report.AddRange( File.ReadAllLines( Path.Combine( s_output, "validation.txt" ) ) );
      Execute();
    }

    private static void InitializeOutput()
    {
      s_output = Environment.GetEnvironmentVariable( "AGX_SOIL_VALIDATION_OUTPUT" );
      if ( string.IsNullOrEmpty( s_output ) ) s_output = Path.Combine( Path.GetTempPath(), "AGXSoilValidationResults" );
      Directory.CreateDirectory( s_output );
    }

    private static void Execute()
    {
      Application.logMessageReceived += Log;
      try {
        var shader = Resources.Load<Shader>( "Shaders/URP/TerrainParticleSurface" );
        Require( shader != null && shader.isSupported, "Surface shader available and supported" );
        foreach ( var message in ShaderUtil.GetShaderMessages( shader ) ) Record( "Shader: " + message.message );
        var example = Object.FindFirstObjectByType<SoilSurfaceExample>();
        GraphicsSettings.defaultRenderPipeline = example.ExamplePipeline;
        QualitySettings.renderPipeline = example.ExamplePipeline;
        var renderer = example.Renderer;
        s_primaryRenderer = renderer;
        var camera = Object.FindFirstObjectByType<Camera>();
        var simulation = Simulation.Instance.GetInitialized<Simulation>();
        Require( simulation != null, "Native simulation initialization" );
        simulation.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;
        NativeHandler.Instance.ValidateLicense();
        Require( NativeHandler.Instance.HasValidLicense, "Valid native AGX license" );
        foreach ( var component in Object.FindObjectsByType<ScriptComponent>( FindObjectsSortMode.None ) )
          Require( component.GetInitialized() != null, "Initialize " + component.GetType().Name );
        uint captureBeforeSteps = renderer.Snapshot.CaptureCount, packBeforeSteps = renderer.PackCount;
        for ( int i = 0; i < 120; ++i ) simulation.DoStep();
        Require( renderer.Snapshot.CaptureCount == captureBeforeSteps && renderer.PackCount == packBeforeSteps, "120 physics steps perform no snapshot readback, connection search or upload" );
        renderer.Snapshot.RefreshIfNeeded();
        Require( renderer.Snapshot.CaptureCount == captureBeforeSteps+1, "Completed steps coalesce into one latest snapshot" );
        renderer.Snapshot.RefreshIfNeeded();
        Require( renderer.Snapshot.CaptureCount == captureBeforeSteps+1, "Repeated snapshot request reuses readback" );
        Record( $"Simulation time: {simulation.Native.getTimeStamp():F3}s; native soil particles: {renderer.Snapshot.Terrain.GetParticles().size()}" );
        Require( renderer.Snapshot.Count > 0, "Grading creates dynamic soil particles" );
        Record( $"Eligible particles: {renderer.Snapshot.Count}" );
        CompareReadbackModes( renderer.Snapshot, "initial dense state" );
        renderer.IndividualParticleFraction = float.NaN;
        Require( renderer.IndividualParticleFraction == 0.05f, "Nonfinite fraction is rejected" );
        float previousSupport = renderer.SupportRadiusScale;
        renderer.SupportRadiusScale = 0;
        Require( renderer.SupportRadiusScale == previousSupport, "Nonpositive support scale is rejected" );
        var target = new RenderTexture( 960, 640, 24, RenderTextureFormat.ARGB32 );
        target.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };

        example.Ordinary(); Capture( camera, request, target, "ordinary" );
        CheckPhysicalScale( renderer );
        example.FullSurface(); Capture( camera, request, target, "sphere-full", TerrainParticleSurfaceRenderer.SurfaceInputMode.Sphere );
        renderer.TextureMapping = TerrainParticleSurfaceRenderer.TextureMappingMode.World;
        Capture( camera, request, target, "world-texture" );
        renderer.TextureMapping = TerrainParticleSurfaceRenderer.TextureMappingMode.Particle;
        Capture( camera, request, target, "particle-texture" );
        Record( $"Physical tangent bridges: {renderer.BridgeCount}; bridge buffer capacity: {renderer.BridgeBuffer?.count ?? 0}" );
        Require( renderer.FaceCount > 0 && renderer.FaceBuffer != null, "Native soil contains filled local faces" );
        Record( $"Local tangent faces: {renderer.FaceCount}; backend={renderer.FaceBackend}; buffer capacity={renderer.FaceBuffer.count}" );
        Require( renderer.IndividualCount == 0, "Fraction zero excludes no particles" );
        Record( $"Boundary adhesion: colliders={renderer.Boundaries.ColliderCount}; attached={renderer.Boundaries.AttachedParticleCount}; patches={renderer.Boundaries.PatchCount}; queries={renderer.Boundaries.QueryCount}" );
        Require( renderer.Boundaries.AttachedParticleCount > 0 && renderer.Boundaries.PatchCount > 0, "Soil conforms to nearby collision terrain" );
        renderer.BoundaryAdhesion = false; Capture( camera, request, target, "boundary-disabled" );
        Require( renderer.Boundaries.PatchCount == 0 && !renderer.Boundaries.Enabled, "Boundary adhesion can be disabled at runtime" );
        renderer.BoundaryAdhesion = true; Capture( camera, request, target, "boundary-restored" );
        Require( renderer.Boundaries.PatchCount > 0, "Boundary adhesion restores at runtime" );
        float hysteresis = renderer.ConnectionHysteresis;
        renderer.ConnectionHysteresis = 0;
        Capture( camera, request, target, "hysteresis-disabled" );
        renderer.ConnectionHysteresis = hysteresis;
        Capture( camera, request, target, "hysteresis-restored" );
        Require( renderer.ConnectionHysteresis == hysteresis && renderer.FaceCount > 0, "Runtime hysteresis settings rebuild connected faces" );
        example.Mixed(); Capture( camera, request, target, "sphere-mixed" );
        Require( renderer.SurfaceCount + renderer.IndividualCount == renderer.Snapshot.Count, "Disjoint complete partition" );
        Capture( camera, request, target, "mesh-mixed", TerrainParticleSurfaceRenderer.SurfaceInputMode.Mesh );
        renderer.IndividualParticleFraction = 1; Capture( camera, request, target, "all-rocks" );
        Require( renderer.SurfaceCount == 0, "Fraction one leaves empty surface" );
        example.Mixed(); camera.orthographic = true; camera.orthographicSize = 6;
        Capture( camera, request, target, "orthographic-mixed" );
        camera.orthographic = false;
        renderer.ResolutionScale = 0.5f; Capture( camera, request, target, "half-resolution" );
        renderer.ResolutionScale = 1;
        renderer.Smoothing = 0; Capture( camera, request, target, "unsmoothed" ); renderer.Smoothing = 0.6f;
        var pane = GameObject.CreatePrimitive( PrimitiveType.Cube );
        pane.name = "Transparent pane behind soil";
        pane.transform.position = new Vector3( 6, 2.3f, 8.5f );
        pane.transform.localScale = new Vector3( 3, 0.8f, 0.04f );
        var glass = SoilSurfaceExampleBuilder.Lit( "Transparent validation", new Color( 0.1f, 0.8f, 1, 0.4f ) );
        AGXUnity.Utils.RenderingUtils.SetTransparencyEnabled( glass, true );
        pane.GetComponent<MeshRenderer>().sharedMaterial = glass;
        Capture( camera, request, target, "transparent-behind" );
        Object.Destroy( pane ); Object.Destroy( glass );
        var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation;
        float oldNear = camera.nearClipPlane;
        var particle = renderer.Snapshot.Particles.Take( renderer.Snapshot.Count ).OrderByDescending( p => p.Position.y ).First();
        camera.transform.position = particle.Position + Vector3.up * (particle.Radius * 0.5f);
        camera.transform.rotation = Quaternion.LookRotation( Vector3.down, Vector3.forward );
        camera.nearClipPlane = 0.01f;
        renderer.SurfaceInput = TerrainParticleSurfaceRenderer.SurfaceInputMode.Sphere;
        Capture( camera, request, target, "near-plane-intersection" );
        camera.transform.SetPositionAndRotation( oldPosition, oldRotation ); camera.nearClipPlane = oldNear;
        renderer.SurfaceInput = TerrainParticleSurfaceRenderer.SurfaceInputMode.Mesh;
        uint beforeCamera = renderer.Snapshot.Version;
        var secondCamera = Object.Instantiate( camera );
        secondCamera.transform.position += new Vector3( 0.7f, 0.2f, 0 );
        Capture( secondCamera, request, target, "second-camera" );
        Require( renderer.Snapshot.Version == beforeCamera, "Cameras share a snapshot without native reacquisition" );
        Object.Destroy( secondCamera.gameObject );
        target.Release(); target.width = 640; target.height = 480; target.Create();
        Capture( camera, request, target, "resized" );
        target.Release(); target.width = 960; target.height = 640; target.Create();
        CheckSecondTerrain( renderer, camera, request, target, simulation );
        var pipeline = example.ExamplePipeline;
        pipeline.msaaSampleCount = 2;
        Require( !TerrainParticleSurfaceFeature.SupportsCamera( camera ), "Unsupported MSAA uses particle fallback" );
        Capture( camera, request, target, "msaa-fallback" );
        pipeline.msaaSampleCount = 1;
        renderer.Snapshot.enabled = false;
        Capture( camera, request, target, "empty-particles" );
        Require( renderer.SurfaceCount == 0 && renderer.IndividualCount == 0, "Empty snapshot leaves no stale surface" );
        renderer.Snapshot.enabled = true; simulation.DoStep();
        renderer.enabled = false;
        Require( renderer.SurfaceBuffer == null && renderer.BridgeBuffer == null && renderer.FaceBuffer == null &&
          renderer.BridgeCount == 0 && renderer.FaceCount == 0 && renderer.Boundaries.ContactBuffer == null && renderer.Boundaries.NodeBuffer == null &&
          renderer.Boundaries.ShapeBuffer == null && renderer.Boundaries.TriangleBuffer == null && renderer.Boundaries.PatchBuffer == null, "Disable releases particle, bridge, face and boundary buffers" );
        renderer.enabled = true;
        Capture( camera, request, target, "reenabled" );
        Require( renderer.SurfaceCount > 0 && renderer.SurfaceBuffer != null, "Re-enable recreates resources" );
        for ( int i = 0; i < 40; ++i ) { simulation.DoStep(); RenderPipeline.SubmitRenderRequest( camera, request ); }
        // Profile acquisition and packing independently, excluding physics and draws.
        var capture = (Action)typeof( TerrainParticleSnapshotProvider ).GetMethod( "Capture", BindingFlags.Instance | BindingFlags.NonPublic ).CreateDelegate( typeof( Action ), renderer.Snapshot );
        var pack = (Action)typeof( TerrainParticleSurfaceRenderer ).GetMethod( "Pack", BindingFlags.Instance | BindingFlags.NonPublic ).CreateDelegate( typeof( Action ), renderer );
        double packMilliseconds = Measure( pack, 100, out long packBytes );
        double captureMilliseconds = Measure( capture, 100, out long captureBytes );
        CompareReadbackModes( renderer.Snapshot, "later state" );
        double scalarMilliseconds = new SoilAcquisitionBenchmark().Measure( out int scalarRecords, out int scalarProviders );
        Record( $"Acquisition on identical native state: scalar={scalarMilliseconds:F4} ms; optimized={captureMilliseconds:F4} ms; providers={scalarProviders}; records={scalarRecords}; speedup={scalarMilliseconds/Math.Max( captureMilliseconds, 1e-6 ):F2}x. All records match." );
        using var bridges = new SoilParticleBridges();
        var packed = (TerrainParticleSnapshotProvider.Particle[])typeof( TerrainParticleSurfaceRenderer ).GetField( "m_packed", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue( renderer );
        using ( var managedBoundaries = new SoilSurfaceBoundaries( false ) ) using ( var burstBoundaries = new SoilSurfaceBoundaries() ) {
          var region = new Bounds( packed[0].Position, Vector3.one*100 );
          managedBoundaries.Refresh( renderer.BoundaryShapes, region, true ); burstBoundaries.Refresh( renderer.BoundaryShapes, region, true );
          double managedBoundaryTime = Measure( () => managedBoundaries.Build( packed, renderer.SurfaceCount, renderer.UploadedParticleCount, true,
            renderer.BoundaryGap, renderer.BoundarySpread, renderer.SupportRadiusScale, false ), 30, out _ );
          double burstBoundaryTime = Measure( () => burstBoundaries.Build( packed, renderer.SurfaceCount, renderer.UploadedParticleCount, true,
            renderer.BoundaryGap, renderer.BoundarySpread, renderer.SupportRadiusScale, false ), 30, out _ );
          Require( managedBoundaries.AttachedParticleCount == burstBoundaries.AttachedParticleCount, "Managed/Burst boundary queries attach the same native particles" );
          Record( $"Boundary CPU on identical native snapshot: managed={managedBoundaryTime:F4} ms; {burstBoundaries.QueryBackend}={burstBoundaryTime:F4} ms; attached={burstBoundaries.AttachedParticleCount}; patches={burstBoundaries.PatchCount}; queries={burstBoundaries.QueryCount}; excludes upload/draw." );
        }
        double bridgeMilliseconds = Measure( () => bridges.Build( packed, renderer.UploadedParticleCount, renderer.SupportRadiusScale ), 100, out long bridgeBytes );
        using var managedBridges = new SoilParticleBridges( false );
        double managedMilliseconds = Measure( () => managedBridges.Build( packed, renderer.UploadedParticleCount, renderer.SupportRadiusScale ), 30, out _ );
        var acceleratedPairs = new HashSet<ulong>();
        for ( int i = 0; i < bridges.Count; ++i ) acceleratedPairs.Add( ((ulong)bridges.Pairs[i].A << 32) | bridges.Pairs[i].B );
        Require( acceleratedPairs.Count == managedBridges.Count && Enumerable.Range( 0, managedBridges.Count ).All( i => acceleratedPairs.Contains( ((ulong)managedBridges.Pairs[i].A << 32) | managedBridges.Pairs[i].B ) ), "Accelerated and managed searches produce exactly the same pairs on the native snapshot" );
        Record( $"Bridge backend: {bridges.Backend}; accelerated={bridgeMilliseconds:F4} ms; managed={managedMilliseconds:F4} ms; speedup={managedMilliseconds/Math.Max( bridgeMilliseconds, 1e-6 ):F2}x on identical input." );
        float minimumRadius = float.PositiveInfinity, maximumRadius = 0;
        for ( int i = 0; i < renderer.SurfaceCount; ++i ) { minimumRadius = Mathf.Min( minimumRadius, packed[i].Radius ); maximumRadius = Mathf.Max( maximumRadius, packed[i].Radius ); }
        Record( $"CPU bridge search: {bridgeMilliseconds:F4} ms; candidates={bridges.CandidateCount}; radii={minimumRadius:G5}..{maximumRadius:G5} m; packed particles={renderer.SurfaceCount}." );
        Record( $"CPU pack/upload: {packMilliseconds:F4} ms; native acquisition/dispatch: {captureMilliseconds:F4} ms; sum={captureMilliseconds+packMilliseconds:F4} ms. Acquisition and GPU preparation are separate markers." );
        long probeBefore = GC.GetAllocatedBytesForCurrentThread();
        var allocationProbe = new byte[4096]; GC.KeepAlive( allocationProbe );
        bool allocationCounterWorks = GC.GetAllocatedBytesForCurrentThread() > probeBefore;
        Record( allocationCounterWorks ? $"Managed allocations per update: pack={packBytes/100.0:F1} B; acquisition={captureBytes/100.0:F1} B (current thread)." :
          "Managed allocation counter returns zero even for a known allocation on this Unity runtime; steady-state allocation totals are unverified. The native array wrapper is reacquired once per update." );
        Record( $"GPU particle/anchor buffer: {renderer.SurfaceBuffer?.count * 64} B; uploaded={renderer.UploadedParticleCount * 64} B/update." );
        Record( $"GPU bridge buffer: {renderer.BridgeBuffer?.count * 8} B; bridges={renderer.BridgeCount}; uploaded={renderer.BridgeCount * 8} B/update." );
        Record( $"GPU face buffer: {renderer.FaceBuffer?.count * 12} B; triangles={renderer.FaceCount}; uploaded={renderer.FaceCount * 12} B/update." );
        Record( $"Boundary preparation: backend={renderer.Boundaries.QueryBackend}; colliders={renderer.Boundaries.ColliderCount}; attached={renderer.Boundaries.AttachedParticleCount}; patches={renderer.Boundaries.PatchCount}; queries={renderer.Boundaries.QueryCount}; patch buffer={renderer.Boundaries.PatchBuffer?.count*48} B." );
        Record( "960x640 reconstruction attachment working set: approximately 105.7 MB/terrain (3 attribute sets including half-precision texture color, plus input depth); half resolution approximately 26.4 MB. Render Graph may alias transient allocations. Full-precision texture-color fallback uses 120.4 MB / 30.1 MB." );
        foreach ( var message in ShaderUtil.GetShaderMessages( shader ) ) Record( "Shader after draws: " + message.message );
        Require( s_errors == 0, "No runtime or Render Graph errors" );
        s_perfCamera = camera; s_perfTarget = target; s_perfRequest = request; s_perfRenderer = renderer;
        // Use normal player frames for GPU timing; offscreen manual requests
        // alone do not always advance Unity's GPU recorder in batch mode.
        s_perfCamera.targetTexture = target; s_perfCamera.enabled = true;
        foreach ( var sampler in TerrainParticleSurfaceFeature.ProfileSamplers ) sampler.enableRecording = true;
        EditorApplication.update += ProfileFrame;
        return;
      }
      catch ( Exception exception ) {
        ++s_errors; Record( exception.ToString() );
      }
      Finish();
    }

    private static Camera s_perfCamera;
    private static RenderTexture s_perfTarget;
    private static UniversalRenderPipeline.SingleCameraRequest s_perfRequest;
    private static TerrainParticleSurfaceRenderer s_perfRenderer;
    private static int s_profileFrames;
    private static readonly double[] s_gpuTimes = new double[5];
    private static readonly int[] s_gpuSamples = new int[5];
    private static void ProfileFrame()
    {
      try {
        var samplers = TerrainParticleSurfaceFeature.ProfileSamplers;
        if ( s_profileFrames > 5 )
          for ( int i = 0; i < samplers.Length; ++i ) {
            s_gpuTimes[i] += samplers[i].gpuElapsedTime;
            s_gpuSamples[i] += samplers[i].gpuSampleCount;
          }
        if ( ++s_profileFrames < 40 ) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        EditorApplication.update -= ProfileFrame;
        for ( int i = 0; i < samplers.Length; ++i ) {
          Record( s_gpuSamples[i] > 0 ? $"GPU {samplers[i].name}: {s_gpuTimes[i]/s_gpuSamples[i]:F4} ms/draw ({s_gpuSamples[i]} samples)." :
            $"GPU {samplers[i].name}: timing unavailable on this capture." );
          samplers[i].enableRecording = false;
        }
        s_perfCamera.targetTexture = null;
        s_perfTarget.Release(); Object.DestroyImmediate( s_perfTarget );
        s_perfRenderer.enabled = false;
        Require( s_perfRenderer.SurfaceBuffer == null && s_perfRenderer.BridgeBuffer == null && s_perfRenderer.FaceBuffer == null &&
          s_perfRenderer.Boundaries.ContactBuffer == null && s_perfRenderer.Boundaries.NodeBuffer == null && s_perfRenderer.Boundaries.ShapeBuffer == null &&
          s_perfRenderer.Boundaries.TriangleBuffer == null && s_perfRenderer.Boundaries.PatchBuffer == null &&
          !TerrainParticleSurfaceRenderer.Active.Contains( s_perfRenderer ), "Disable releases GPU buffers and registry entry" );
        Require( s_errors == 0, "No errors during profiling" );
        Record( "Integration checks completed." );
      }
      catch ( Exception exception ) { ++s_errors; Record( exception.ToString() ); }
      Finish();
    }

    private static void Finish()
    {
      File.WriteAllLines( Path.Combine( s_output, "validation.txt" ), s_report );
      Application.logMessageReceived -= Log;
      EditorApplication.Exit( s_errors == 0 ? 0 : 1 );
    }

    private static void Capture( Camera camera, UniversalRenderPipeline.SingleCameraRequest request, RenderTexture target, string name,
                                 TerrainParticleSurfaceRenderer.SurfaceInputMode? mode = null, TerrainParticleSurfaceRenderer targetRenderer = null )
    {
      var renderer = targetRenderer != null ? targetRenderer : s_primaryRenderer;
      if ( mode.HasValue ) renderer.SurfaceInput = mode.Value;
      // Rendering callback packs dirty fractions once, shared by every pass.
      RenderPipeline.SubmitRenderRequest( camera, request );
      RenderPipeline.SubmitRenderRequest( camera, request );
      var previous = RenderTexture.active;
      RenderTexture.active = target;
      var texture = new Texture2D( target.width, target.height, TextureFormat.RGB24, false );
      texture.ReadPixels( new Rect( 0, 0, target.width, target.height ), 0, 0 ); texture.Apply();
      File.WriteAllBytes( Path.Combine( s_output, name + ".png" ), texture.EncodeToPNG() );
      var pixels = texture.GetPixels32();
      Require( pixels.Any( p => p.r != pixels[0].r || p.g != pixels[0].g || p.b != pixels[0].b ), name + " contains rendered geometry" );
      Object.DestroyImmediate( texture ); RenderTexture.active = previous;
      Record( name + $": surface={renderer.SurfaceCount}, individual={renderer.IndividualCount}" );
    }

    private static void CheckSecondTerrain( TerrainParticleSurfaceRenderer first, Camera camera,
      UniversalRenderPipeline.SingleCameraRequest request, RenderTexture target, Simulation simulation )
    {
      var source = AssetDatabase.LoadAssetAtPath<TerrainData>( SoilSurfaceExampleBuilder.Destination + "/GradingTerrain.asset" );
      var ground = Terrain.CreateTerrainGameObject( Object.Instantiate( source ) );
      ground.name = "Validation second terrain";
      ground.transform.position = new Vector3( 13, 0, 0 );
      ground.GetComponent<Terrain>().materialTemplate = first.Snapshot.Terrain.GetComponent<Terrain>().materialTemplate;
      var terrain = ground.AddComponent<DeformableTerrain>(); terrain.MaximumDepth = 1;
      var second = ground.AddComponent<TerrainParticleSurfaceRenderer>();
      second.ParticleMesh = first.ParticleMesh; second.ParticleMaterial = first.ParticleMaterial;
      second.SoilTexture = first.SoilTexture; second.IndividualParticleFraction = 0.05f;
      second.SoilTint = new Color( 0.3f, 0.4f, 0.2f );
      second.GetComponent<TerrainParticleSnapshotProvider>().GetInitialized();
      var release = new GameObject( "Second terrain release" );
      release.transform.position = new Vector3( 19, 2.4f, 8.5f );
      var shape = release.AddComponent<AGXUnity.Collide.Box>(); shape.HalfExtents = new Vector3( 2.2f, 0.7f, 1.6f );
      shape.CollisionsEnabled = false;
      var failure = release.AddComponent<DeformableTerrainFailureVolume>(); failure.AddAllTerrainsOnStart = false; failure.Add( terrain );
      failure.GetInitialized();
      for ( int i = 0; i < 60; ++i ) simulation.DoStep();
      var snapshot = second.GetComponent<TerrainParticleSnapshotProvider>();
      uint traversals = TerrainParticleSnapshotProvider.NativeTraversalCount;
      first.Snapshot.RefreshIfNeeded(); snapshot.RefreshIfNeeded();
      Require( TerrainParticleSnapshotProvider.NativeTraversalCount == traversals+1, "Two terrain providers share one native particle traversal" );
      Require( snapshot.Count > 0, "Second terrain produces its own snapshot" );
      var identities = new HashSet<uint>();
      for ( int i = 0; i < first.Snapshot.Count; ++i ) identities.Add( first.Snapshot.Particles[i].Id );
      for ( int i = 0; i < snapshot.Count; ++i ) RequireSilent( !identities.Contains( snapshot.Particles[i].Id ), "Terrain ownership overlap" );
      Require( true, "Terrain snapshots have disjoint native ownership" );
      var position = camera.transform.position;
      camera.transform.position += Vector3.right * 13;
      Capture( camera, request, target, "second-terrain", targetRenderer: second );
      camera.transform.position = position;
      Object.Destroy( release ); Object.Destroy( ground );
    }

    private static double Measure( Action action, int count, out long allocations )
    {
      for ( int i = 0; i < 10; ++i ) action();
      long before = GC.GetAllocatedBytesForCurrentThread();
      var watch = Stopwatch.StartNew();
      for ( int i = 0; i < count; ++i ) action();
      watch.Stop(); allocations = GC.GetAllocatedBytesForCurrentThread() - before;
      return watch.Elapsed.TotalMilliseconds / count;
    }

    private static void CompareReadbackModes( TerrainParticleSnapshotProvider provider, string state )
    {
      var capture = (Action)typeof( TerrainParticleSnapshotProvider ).GetMethod( "Capture", BindingFlags.Instance | BindingFlags.NonPublic ).CreateDelegate( typeof( Action ), provider );
      var mode = typeof( TerrainParticleSnapshotProvider ).GetField( "s_readbackMode", BindingFlags.Static | BindingFlags.NonPublic );
      int previous = (int)mode.GetValue( null );
      try {
        mode.SetValue( null, 0 ); double scalar = Measure( capture, 50, out _ );
        mode.SetValue( null, 1 ); double partial = Measure( capture, 50, out _ );
        mode.SetValue( null, 3 ); double full = Measure( capture, 50, out _ );
        Record( $"Readback path comparison ({state}): coordinated scalar={scalar:F4} ms; bulk position/radius={partial:F4} ms; full bulk={full:F4} ms; native array={provider.NativeParticleCount}; visible owned={provider.Count}." );
      }
      finally { mode.SetValue( null, previous ); capture(); }
      Record( $"Selected readback ({state}): {provider.ReadbackPath}." );
    }

    private static void CheckPhysicalScale( TerrainParticleSurfaceRenderer renderer )
    {
      using var particles = renderer.Snapshot.Terrain.GetParticles();
      var lookup = renderer.Snapshot.Particles.Take( renderer.Snapshot.Count ).ToDictionary( p => p.Id );
      int checkedRotations = 0;
      for ( uint index = 0; index < particles.size(); ++index ) {
        var native = particles.at( index );
        try {
          if ( !lookup.TryGetValue( native.getId(), out var p ) ) continue;
          var position = native.position(); var rotation = native.rotation();
          RequireSilent( Vector3.Distance( p.Position, new Vector3( -(float)position.x, (float)position.y, (float)position.z ) ) < 1e-4f, "Bulk position vs scalar" );
          RequireSilent( Mathf.Abs( p.Radius-(float)native.getRadius() ) < 1e-6f, "Bulk radius vs scalar" );
          RequireSilent( Mathf.Abs( Quaternion.Dot( p.Rotation, new Quaternion( -(float)rotation.x, (float)rotation.y, (float)rotation.z, -(float)rotation.w ) ) ) > 0.99999f, "Bulk rotation vs scalar" );
          ++checkedRotations;
        }
        finally { native.ReturnToPool(); }
      }
      Require( checkedRotations == renderer.Snapshot.Count, $"Snapshot position/radius/rotation match scalar API for {checkedRotations} particles" );
      var expected = renderer.Snapshot.Particles[0];
      for ( uint i = 0; i < particles.size(); ++i ) {
        var native = particles.at( i );
        try {
          if ( native.getId() != expected.Id ) continue;
          var position = native.position();
          Require( Vector3.Distance( expected.Position, new Vector3( -(float)position.x, (float)position.y, (float)position.z ) ) < 1e-4f,
            "Native position has the expected handedness" );
          Require( Mathf.Abs( expected.Radius - (float)native.getRadius() ) < 1e-6f, "Snapshot preserves physical radius" );
          break;
        }
        finally { native.ReturnToPool(); }
      }
      var matrices = (Matrix4x4[])typeof( TerrainParticleSurfaceRenderer ).GetField( "m_individualMatrices", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue( renderer );
      for ( int i = 0; i < renderer.Snapshot.Count; ++i ) {
        var p = renderer.Snapshot.Particles[i];
        if ( !SoilParticleSelection.IsIndividualParticle( p.Id, renderer.IndividualParticleFraction ) ) continue;
        Require( Mathf.Abs( matrices[0].MultiplyVector( Vector3.right * 0.5f ).magnitude - p.Radius ) < 1e-5f,
          "Unit-diameter rock transforms preserve physical radius" );
        break;
      }
      Require( renderer.ParticleMesh.vertices.Max( p => p.magnitude ) <= 0.50001f, "Example rock is normalized to unit diameter" );
    }

    private static void CheckSelection()
    {
      int selected = 0;
      for ( uint id = 0; id < 100000; ++id ) {
        RequireSilent( !SoilParticleSelection.IsIndividualParticle( id, 0 ), "Fraction zero" );
        RequireSilent( SoilParticleSelection.IsIndividualParticle( id, 1 ), "Fraction one" );
        if ( SoilParticleSelection.IsIndividualParticle( id, 0.05 ) ) ++selected;
      }
      Require( selected > 4500 && selected < 5500, $"5% hash distribution ({selected}/100000)" );
      var identities = Enumerable.Range( 0, 1000 ).Select( x => (uint)x * 13 ).ToArray();
      var original = identities.ToDictionary( id => id, id => SoilParticleSelection.IsIndividualParticle( id, 0.05 ) );
      foreach ( uint id in identities.Reverse().Skip( 100 ) )
        RequireSilent( original[id] == SoilParticleSelection.IsIndividualParticle( id, 0.05 ), "Reorder/removal stability" );
      Require( true, "Stable chunk identity under reorder and removal" );
    }
    private static void RequireSilent( bool condition, string description ) { if ( !condition ) throw new Exception( "FAIL: " + description ); }
    private static void Require( bool condition, string description ) { RequireSilent( condition, description ); Record( "PASS: " + description ); }
    private static void Record( string line ) { s_report.Add( line ); Debug.Log( line ); }
    private static void Log( string message, string stack, LogType type )
    {
      if ( type == LogType.Error || type == LogType.Exception || type == LogType.Assert ) { ++s_errors; s_report.Add( "ERROR: " + message + "\n" + stack ); }
    }
  }
}
