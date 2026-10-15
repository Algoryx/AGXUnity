using System;
using System.Collections.Generic;
using System.IO;
using AGXUnity.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Rendering
{
  /// <summary>Isolated GPU regression: no native simulation or license required.</summary>
  public static class SoilBridgeValidation
  {
    private const int Size = 128;
    private static readonly List<string> s_results = new List<string>();

    public static void Run()
    {
      int exit = 0;
      s_results.Clear();
      try {
        s_results.Add( $"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}" );
        var shader = Resources.Load<Shader>( "Shaders/URP/TerrainParticleSurface" );
        if ( shader == null || !shader.isSupported ) throw new Exception( "Surface shader unsupported" );
        var one = new[] { Particle( Vector3.zero, 1 ) };
        var baseline = Capture( shader, one, 1, false, false, "isolated-default" );
        var increased = Capture( shader, one, 3, false, false, "isolated-support3" );
        for ( int i = 0; i < baseline.Length; ++i )
          if ( baseline[i] != increased[i] ) throw new Exception( "Support scale changed isolated particle geometry" );
        s_results.Add( "PASS isolated particle is pixel-identical at support scales 1 and 3" );
        var equal = new[] { Particle( new Vector3( -1.2f, 0, 0 ), 1 ), Particle( new Vector3( 1.2f, 0, 0 ), 1 ) };
        Capture( shader, equal, 1, false, false, "unconnected" );
        Capture( shader, equal, 1.5f, false, false, "tangent-bridge", true );
        Capture( shader, equal, 1.5f, false, true, "tangent-smoothed", true );
        Capture( shader, equal, 1.5f, true, true, "tangent-perspective", true );
        var unequal = new[] { Particle( new Vector3( -0.9f, 0, 0 ), 0.6f ), Particle( new Vector3( 0.9f, 0, 0 ), 1 ) };
        Capture( shader, unequal, 1.5f, false, true, "unequal-tangent", true );
        Capture( shader, unequal, 1.5f, true, true, "unequal-perspective", true );
        var anchored = new[] { Particle( new Vector3( -1.2f, 0, 0 ), 1 ), Particle( new Vector3( 1.2f, 0, 0 ), 1 ) };
        anchored[1].RenderingEligible = SoilParticleBridges.CenterAnchor;
        Capture( shader, anchored, 1.5f, false, true, "rock-center-anchor", true );
        Capture( shader, anchored, 1.5f, true, true, "rock-center-perspective", true );
        Capture( shader, equal, 1.5f, true, false, "near-plane", false, true );
        var triangle = new[] { Particle( new Vector3( -1.2f, -0.8f, 0 ), 0.2f ),
                               Particle( new Vector3( 1.2f, -0.8f, 0 ), 0.2f ),
                               Particle( new Vector3( 0, 1.6f, 0 ), 0.2f ) };
        CheckFlatCenter( Capture( shader, triangle, 8, false, false, "triangle-flat-face", true ), 4.8f );
        CheckFlatCenter( Capture( shader, triangle, 8, false, true, "triangle-flat-smoothed", true ), 4.8f );
        var tetrahedron = new[] { triangle[0], triangle[1], triangle[2], Particle( new Vector3( 0, 0, 2 ), 0.2f ) };
        CheckFlatCenter( Capture( shader, tetrahedron, 8, false, true, "tetrahedron-flat-face", true ), 4.8f );
        Capture( shader, tetrahedron, 8, true, true, "tetrahedron-perspective", true );
        Capture( shader, tetrahedron, 8, false, true, "tetrahedron-reverse", true,
          cameraRotation: Quaternion.Euler( 0, 180, 0 ) );
        Capture( shader, tetrahedron, 8, true, false, "tetrahedron-near-plane", false, true );
        var unequalFace = (TerrainParticleSnapshotProvider.Particle[])triangle.Clone();
        unequalFace[1].Radius = 0.35f;
        Capture( shader, unequalFace, 8, true, true, "unequal-face", true );
        unequalFace[2].RenderingEligible = SoilParticleBridges.CenterAnchor;
        Capture( shader, unequalFace, 8, true, true, "face-center-anchor", true );
        var textureBaseline = Capture( shader, equal, 1.5f, false, true, "texture-baseline", true, readTexture: true );
        CompareTexture( Capture( shader, equal, 1.5f, false, false, "texture-unfiltered", true, readTexture: true ),
          textureBaseline, "maximum smoothing preserves texture detail", 0.00001f );
        var moved = (TerrainParticleSnapshotProvider.Particle[])equal.Clone();
        var translation = new Vector3( 0.37f, 0.19f, 0.43f );
        for ( int i = 0; i < moved.Length; ++i ) moved[i].Position += translation;
        CompareTexture( textureBaseline, Capture( shader, moved, 1.5f, false, true, "texture-translated", true,
          readTexture: true, cameraOffset: translation ), "translation" );
        var rotation = Quaternion.Euler( 0, 67, 0 );
        var rotated = (TerrainParticleSnapshotProvider.Particle[])equal.Clone();
        for ( int i = 0; i < rotated.Length; ++i ) { rotated[i].Position = rotation*rotated[i].Position; rotated[i].Rotation = rotation; }
        CompareTexture( textureBaseline, Capture( shader, rotated, 1.5f, false, true, "texture-rotated", true,
          readTexture: true, cameraRotation: rotation ), "rotation" );
        var faceTexture = Capture( shader, tetrahedron, 8, false, true, "face-texture-baseline", true, readTexture: true );
        CompareTexture( Capture( shader, tetrahedron, 8, false, false, "face-texture-unfiltered", true, readTexture: true ),
          faceTexture, "flat face smoothing preserves texture detail", 0.00001f );
        var movedFaces = (TerrainParticleSnapshotProvider.Particle[])tetrahedron.Clone();
        for ( int i = 0; i < movedFaces.Length; ++i ) movedFaces[i].Position += translation;
        CompareTexture( faceTexture, Capture( shader, movedFaces, 8, false, true, "face-texture-translated", true,
          readTexture: true, cameraOffset: translation ), "face translation" );
        for ( int i = 0; i < movedFaces.Length; ++i ) {
          movedFaces[i].Position = rotation*tetrahedron[i].Position; movedFaces[i].Rotation = rotation;
        }
        CompareTexture( faceTexture, Capture( shader, movedFaces, 8, false, true, "face-texture-rotated", true,
          readTexture: true, cameraRotation: rotation ), "face rotation" );
        CheckBoundaries( shader );
        BenchmarkFaces();
        var compiledMaterial = new Material( shader );
        try {
          for ( int pass = 0; pass < compiledMaterial.passCount; ++pass ) ShaderUtil.CompilePass( compiledMaterial, pass, true );
        }
        finally { Object.DestroyImmediate( compiledMaterial ); }
        foreach ( var message in ShaderUtil.GetShaderMessages( shader ) ) {
          s_results.Add( $"Shader {message.severity}: {message.file}:{message.line}: {message.message}" );
          if ( message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error ) throw new Exception( message.message );
        }
      }
      catch ( Exception exception ) { exit = 1; s_results.Add( "FAIL " + exception ); Debug.LogException( exception ); }
      string directory = Environment.GetEnvironmentVariable( "AGX_SOIL_VALIDATION_OUTPUT" );
      if ( string.IsNullOrEmpty( directory ) ) directory = Path.Combine( Path.GetTempPath(), "AGXSoilBridgeValidation" );
      Directory.CreateDirectory( directory );
      File.WriteAllLines( Path.Combine( directory, "bridges.txt" ), s_results );
      foreach ( var result in s_results ) Debug.Log( result );
      EditorApplication.Exit( exit );
    }

    private static void CheckBoundaries( Shader shader )
    {
      var floorObject = new GameObject( "Boundary regression floor" );
      var wallObject = new GameObject( "Boundary regression wall" );
      var bucketObject = new GameObject( "Boundary regression mesh" );
      var mesh = new Mesh();
      try {
        floorObject.transform.position = new Vector3( 0, -0.5f, 0 );
        var floor = floorObject.AddComponent<AGXUnity.Collide.Plane>();
        var soil = new[] { Particle( new Vector3( 0, 0.6f, 0 ), 1 ) }; soil[0].Id = 100;
        int added = 0;
        Capture( shader, soil, 1.5f, false, true, "boundary-plane-adhesion", cameraRotation: Quaternion.Euler( 25, 0, 0 ),
          boundaryShapes: new AGXUnity.Collide.Shape[] { floor }, checkBoundary: world => {
            if ( world.y < -0.501f ) throw new Exception( "Soil penetrates plane" );
            if ( Vector3.Distance( world, soil[0].Position ) > 1.01f && world.y < -0.39f ) ++added;
          } );
        if ( added < 10 ) throw new Exception( "Boundary adhesion did not fill the sphere/ground gap" );
        s_results.Add( $"PASS boundary adhesion fills {added} pixels outside the physical sphere without entering the ground" );
        soil[0].Position = new Vector3( 0, 0.2f, 0 );
        wallObject.transform.SetPositionAndRotation( new Vector3( 0.8f, 0, 0 ), Quaternion.Euler( 0, 0, 90 ) );
        var wall = wallObject.AddComponent<AGXUnity.Collide.Plane>();
        Action<Vector3> corner = world => { if ( world.y < -0.501f || world.x > 0.801f ) throw new Exception( "Soil penetrates ground/wall corner at " + world ); };
        Capture( shader, soil, 1.5f, true, true, "boundary-plane-corner", cameraRotation: Quaternion.Euler( 25, 0, 0 ),
          boundaryShapes: new AGXUnity.Collide.Shape[] { floor, wall }, checkBoundary: corner );
        floor.enabled = false; wall.enabled = false;
        var vertices = new List<Vector3>(); var indices = new List<int>();
        void Quad( Vector3 a, Vector3 b, Vector3 c, Vector3 d ) {
          int start = vertices.Count; vertices.Add( a ); vertices.Add( b ); vertices.Add( c ); vertices.Add( d );
          indices.AddRange( new[] { start, start+1, start+2, start+2, start+1, start+3 } );
        }
        Quad( new Vector3( -3, -0.5f, -3 ), new Vector3( -3, -0.5f, 0.8f ), new Vector3( 0.8f, -0.5f, -3 ), new Vector3( 0.8f, -0.5f, 0.8f ) );
        Quad( new Vector3( 0.8f, -0.5f, -3 ), new Vector3( 0.8f, -0.5f, 0.8f ), new Vector3( 0.8f, 3, -3 ), new Vector3( 0.8f, 3, 0.8f ) );
        Quad( new Vector3( -3, -0.5f, 0.8f ), new Vector3( -3, 3, 0.8f ), new Vector3( 0.8f, -0.5f, 0.8f ), new Vector3( 0.8f, 3, 0.8f ) );
        mesh.SetVertices( vertices ); mesh.SetTriangles( indices, 0 ); mesh.RecalculateBounds();
        var bucket = bucketObject.AddComponent<AGXUnity.Collide.Mesh>(); bucket.AddSourceObject( mesh );
        Action<Vector3> bucketConstraint = world => {
          corner( world ); if ( world.z > 0.801f ) throw new Exception( "Soil penetrates bucket back wall" );
        };
        Capture( shader, soil, 1.5f, false, true, "boundary-mesh-bucket", cameraRotation: Quaternion.Euler( 25, 0, 0 ),
          boundaryShapes: new AGXUnity.Collide.Shape[] { bucket }, checkBoundary: bucketConstraint );
        bucketObject.transform.SetPositionAndRotation( new Vector3( 0.4f, 0.1f, 0.2f ), Quaternion.Euler( 0, 35, 0 ) );
        var transform = bucketObject.transform;
        soil[0].Position = transform.TransformPoint( soil[0].Position ); soil[0].Rotation = transform.rotation;
        Capture( shader, soil, 1.5f, true, true, "boundary-moving-bucket", cameraRotation: Quaternion.Euler( 25, 0, 0 ),
          boundaryShapes: new AGXUnity.Collide.Shape[] { bucket }, checkBoundary: world => bucketConstraint( transform.InverseTransformPoint( world ) ) );
        floor.enabled = true;
        using var boundaries = new SoilSurfaceBoundaries();
        boundaries.Refresh( new AGXUnity.Collide.Shape[] { floor }, new Bounds( Vector3.zero, Vector3.one*100 ), true );
        var batch = new TerrainParticleSnapshotProvider.Particle[1024];
        for ( int i = 0; i < batch.Length; ++i ) { batch[i] = Particle( new Vector3( (i%32)*0.1f, -0.445f, (i/32)*0.1f ), 0.05f ); batch[i].Id = (uint)(i+1); }
        for ( int i = 0; i < 5; ++i ) boundaries.Build( batch, batch.Length, batch.Length, true, 0.25f, 1, 1.5f, false );
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for ( int i = 0; i < 30; ++i ) boundaries.Build( batch, batch.Length, batch.Length, true, 0.25f, 1, 1.5f, false );
        double stationary = timer.Elapsed.TotalMilliseconds/30;
        timer.Restart();
        for ( int step = 0; step < 30; ++step ) {
          for ( int i = 0; i < batch.Length; ++i ) batch[i].Position.x += 0.005f;
          boundaries.Build( batch, batch.Length, batch.Length, true, 0.25f, 1, 1.5f, false );
        }
        double moving = timer.Elapsed.TotalMilliseconds/30;
        boundaries.Build( batch, batch.Length, batch.Length, true, 0.25f, 1, 1.5f );
        timer.Restart();
        for ( int i = 0; i < 30; ++i ) boundaries.Build( batch, batch.Length, batch.Length, true, 0.25f, 1, 1.5f );
        double upload = timer.Elapsed.TotalMilliseconds/30;
        s_results.Add( $"Boundary CPU, 1024 attached particles: cached={stationary:F4} ms; moving/rebuild={moving:F4} ms; cached+GPU upload={upload:F4} ms; patches={boundaries.PatchCount}; queries={boundaries.QueryCount}; GPU rendering not measured" );
      }
      finally { Object.DestroyImmediate( floorObject ); Object.DestroyImmediate( wallObject ); Object.DestroyImmediate( bucketObject ); Object.DestroyImmediate( mesh ); }
    }

    private static void BenchmarkFaces()
    {
      var particles = new TerrainParticleSnapshotProvider.Particle[8192];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( (i%32)*0.095f, ((i/32)%8)*0.095f, (i/256)*0.095f );
        particles[i].Id = (uint)(i+1);
        particles[i].Radius = 0.04f+0.04f*(i%7)/6;
        particles[i].RenderingEligible = SoilParticleSelection.IsIndividualParticle( (uint)i, 0.05 ) ? SoilParticleBridges.CenterAnchor : 1;
      }
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, particles.Length, 1.5f );
      using var faces = new SoilParticleFaces();
      for ( int i = 0; i < 5; ++i ) faces.Build( particles.Length, bridges.Pairs, bridges.Count );
      var timer = System.Diagnostics.Stopwatch.StartNew();
      for ( int i = 0; i < 30; ++i ) faces.Build( particles.Length, bridges.Pairs, bridges.Count );
      timer.Stop();
      using var managed = new SoilParticleFaces( false );
      managed.Build( particles.Length, bridges.Pairs, bridges.Count );
      if ( managed.Count != faces.Count ) throw new Exception( "Managed/Burst face count mismatch" );
      for ( int i = 0; i < faces.Count; ++i ) {
        var a = managed.Faces[i]; var b = faces.Faces[i];
        if ( a.A != b.A || a.B != b.B || a.C != b.C ) throw new Exception( "Managed/Burst face mismatch at " + i );
      }
      s_results.Add( $"Face build ({faces.Backend}): 8192 particles, {bridges.Count} pairs, {faces.Count} triangles, {timer.Elapsed.TotalMilliseconds/30:F4} ms CPU; exact managed match; GPU cost not measured" );
      using var stable = new SoilParticleBridges();
      stable.Build( particles, particles.Length, 1.5f, 0.05f );
      using var managedStable = new SoilParticleBridges( false );
      managedStable.Build( particles, particles.Length, 1.5f, 0.05f );
      // Expand across several join thresholds while keeping previously joined
      // edges within the release radius. Both backends must retain the same set.
      for ( int i = 0; i < particles.Length; ++i ) particles[i].Position *= 1.01f;
      for ( int i = 0; i < 5; ++i ) { bridges.Build( particles, particles.Length, 1.5f ); stable.Build( particles, particles.Length, 1.5f, 0.05f ); }
      timer.Restart();
      for ( int i = 0; i < 50; ++i ) bridges.Build( particles, particles.Length, 1.5f );
      double withoutHistory = timer.Elapsed.TotalMilliseconds/50;
      timer.Restart();
      for ( int i = 0; i < 50; ++i ) stable.Build( particles, particles.Length, 1.5f, 0.05f );
      double withHistory = timer.Elapsed.TotalMilliseconds/50;
      managedStable.Build( particles, particles.Length, 1.5f, 0.05f );
      var expected = new HashSet<ulong>();
      for ( int i = 0; i < managedStable.Count; ++i ) expected.Add( ((ulong)managedStable.Pairs[i].A<<32)|managedStable.Pairs[i].B );
      if ( expected.Count != stable.Count || stable.Count <= bridges.Count ) throw new Exception( "Hysteresis did not retain the expected dense fringe" );
      for ( int i = 0; i < stable.Count; ++i )
        if ( !expected.Remove( ((ulong)stable.Pairs[i].A<<32)|stable.Pairs[i].B ) ) throw new Exception( "Managed/Burst hysteresis mismatch or duplicate" );
      s_results.Add( $"Hysteresis ({stable.Backend}): no history={withoutHistory:F4} ms; 5% history={withHistory:F4} ms; additional={withHistory-withoutHistory:F4} ms; fresh={bridges.Count}; retained={stable.Count}; exact managed match" );
    }

    private static TerrainParticleSnapshotProvider.Particle Particle( Vector3 position, float radius ) =>
      new TerrainParticleSnapshotProvider.Particle { Position = position, Radius = radius, Rotation = Quaternion.identity, Color = Color.white };

    private static Color[] Capture( Shader shader, TerrainParticleSnapshotProvider.Particle[] particles, float support,
                                    bool perspective, bool smooth, string name, bool expectBridge = false, bool near = false,
                                    bool readTexture = false, Vector3 cameraOffset = default, Quaternion? cameraRotation = null,
                                    AGXUnity.Collide.Shape[] boundaryShapes = null, Action<Vector3> checkBoundary = null )
    {
      var go = new GameObject( "Soil bridge validation camera" );
      var camera = go.AddComponent<Camera>();
      camera.enabled = false; camera.transform.position = new Vector3( 0, 0, near ? -0.5f : -5 );
      camera.transform.SetPositionAndRotation( (cameraRotation ?? Quaternion.identity)*camera.transform.position+cameraOffset, cameraRotation ?? Quaternion.identity );
      camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
      camera.orthographic = !perspective; camera.orthographicSize = 3; camera.aspect = 1; camera.fieldOfView = 60;
      var projection = GL.GetGPUProjectionMatrix( camera.projectionMatrix, true );
      var view = camera.worldToCameraMatrix;
      var material = new Material( shader ) { enableInstancing = true };
      using var builder = new SoilParticleBridges(); builder.Build( particles, particles.Length, support );
      var particleBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, particles.Length, 64 );
      particleBuffer.SetData( particles );
      var bridgeBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, Mathf.Max( 1, builder.Count ), 8 );
      if ( builder.Count > 0 ) bridgeBuffer.SetData( builder.Pairs, 0, 0, builder.Count );
      var textures = new List<RenderTexture>();
      var raw = Targets( textures );
      using var faces = new SoilParticleFaces();
      faces.Build( particles.Length, builder.Pairs, builder.Count );
      using var faceBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, Mathf.Max( 1, faces.Count ), 12 );
      if ( faces.Count > 0 ) faceBuffer.SetData( faces.Faces, 0, 0, faces.Count );
      var depth = new RenderTexture( Size, Size, 24, RenderTextureFormat.Depth ); depth.Create(); textures.Add( depth );
      var properties = new MaterialPropertyBlock();
      using var boundaries = new SoilSurfaceBoundaries();
      if ( boundaryShapes != null ) boundaries.Refresh( boundaryShapes, new Bounds( Vector3.zero, Vector3.one*100 ), true );
      boundaries.Build( particles, particles.Length, particles.Length, boundaryShapes != null, 0.25f, 1, support );
      boundaries.Bind( properties );
      properties.SetMatrix( "_SoilProjection", projection ); properties.SetMatrix( "_SoilInverseProjection", projection.inverse );
      properties.SetMatrix( "_SoilView", view ); properties.SetMatrix( "_SoilInverseView", view.inverse );
      properties.SetVector( "_SoilSize", new Vector4( Size, Size, 1f/Size, 1f/Size ) );
      properties.SetTexture( "_SoilSceneDepth", SystemInfo.usesReversedZBuffer ? Texture2D.blackTexture : Texture2D.whiteTexture );
      properties.SetBuffer( "_SoilParticles", particleBuffer ); properties.SetBuffer( "_SoilBridges", bridgeBuffer );
      properties.SetFloat( "_SoilSmoothing", 2 ); properties.SetFloat( "_SoilThreshold", 1 );
      var texture = new Texture2D( 16, 16, TextureFormat.RGBA32, false, true ) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
      var texels = new Color[256];
      for ( int y = 0; y < 16; ++y ) for ( int x = 0; x < 16; ++x ) texels[y*16+x] = new Color( x/15f, y/15f, ((x+y)%7)/6f, 1 );
      texture.SetPixels( texels ); texture.Apply();
      properties.SetTexture( "_SoilTexture", texture ); properties.SetFloat( "_SoilRepeatDistance", 0.7f );
      properties.SetFloat( "_SoilParticleTexture", 1 );
      var command = new CommandBuffer { name = "Soil physical envelope regression" };
      try {
        command.SetRenderTarget( Identifiers( raw ), depth );
        command.ClearRenderTarget( true, true, Color.clear );
        int surfaceCount = 0;
        while ( surfaceCount < particles.Length && particles[surfaceCount].RenderingEligible != SoilParticleBridges.CenterAnchor ) ++surfaceCount;
        command.DrawProcedural( Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6, surfaceCount, properties );
        if ( builder.Count > 0 ) command.DrawProcedural( Matrix4x4.identity, material, 5, MeshTopology.Triangles, 6, builder.Count, properties );
        if ( faces.Count > 0 ) {
          properties.SetBuffer( "_SoilFaces", faceBuffer );
          command.DrawProcedural( Matrix4x4.identity, material, 6, MeshTopology.Triangles, 6, faces.Count, properties );
        }
        if ( boundaries.PatchCount > 0 ) command.DrawProcedural( Matrix4x4.identity, material, 7, MeshTopology.Triangles, 21, boundaries.PatchCount, properties );
        Graphics.ExecuteCommandBuffer( command ); command.Clear();
        var output = raw;
        if ( smooth ) {
          for ( int pass = 0; pass < 2; ++pass ) {
            var next = Targets( textures );
            properties.SetTexture( "_SoilDepth", output[0] ); properties.SetTexture( "_SoilColor", output[1] ); properties.SetTexture( "_SoilNormal", output[2] );
            properties.SetTexture( "_SoilAlbedo", output[3] );
            properties.SetVector( "_SoilFilterDirection", pass == 0 ? Vector2.right : Vector2.up );
            command.SetRenderTarget( Identifiers( next ), depth );
            command.ClearRenderTarget( false, true, Color.clear );
            command.DrawProcedural( Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3, 1, properties );
            Graphics.ExecuteCommandBuffer( command ); command.Clear(); output = next;
          }
        }
        var values = Read( output[0] );
        int visible = 0;
        var inverse = projection.inverse;
        var inverseView = view.inverse;
        for ( int y = 0; y < Size; ++y ) for ( int x = 0; x < Size; ++x ) {
          var d = values[y*Size+x];
          if ( d.a < 0.5f ) continue;
          ++visible;
          float u = (x+0.5f)/Size, v = (y+0.5f)/Size;
          var a = Unproject( inverse, u, v, SystemInfo.usesReversedZBuffer ? 1 : 0 );
          var b = Unproject( inverse, u, v, 0.5f );
          var eye = a+(b-a)*((-d.r-a.z)/(b.z-a.z));
          var world = inverseView.MultiplyPoint( eye );
          checkBoundary?.Invoke( world );
          // A convex hull's support in ANY direction is max(dot(center,n)+radius).
          // Exercise 96 directions, including the collision planes +/-X/Y/Z.
          for ( int k = 0; k < 102; ++k ) {
            Vector3 n;
            if ( k < 6 ) { n = k < 2 ? Vector3.right : k < 4 ? Vector3.up : Vector3.forward; if ( k%2 == 1 ) n = -n; }
            else {
              float z = 1-2*(k-6+0.5f)/96;
              float angle = (k-6)*2.39996323f, radius = Mathf.Sqrt( 1-z*z );
              n = new Vector3( radius*Mathf.Cos( angle ), radius*Mathf.Sin( angle ), z );
            }
            float bound = float.NegativeInfinity;
            foreach ( var p in particles ) bound = Mathf.Max( bound, Vector3.Dot( p.Position, n )+(p.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0 : p.Radius) );
            for ( int patch = 0; patch < boundaries.PatchCount; ++patch ) {
              var t = boundaries.Patches[patch]; bound = Mathf.Max( bound, Mathf.Max( Vector3.Dot( t.A, n ), Mathf.Max( Vector3.Dot( t.B, n ), Vector3.Dot( t.C, n ) ) ) );
            }
            if ( Vector3.Dot( world, n ) > bound+0.002f )
              throw new Exception( $"{name}: pixel ({x},{y}) at {world} exceeds physical hull in direction {n}" );
          }
        }
        if ( visible == 0 ) throw new Exception( name + ": no particles rendered" );
        if ( expectBridge && (builder.Count == 0 || values[(Size/2)*Size+Size/2].a < 0.5f) ) throw new Exception( name + ": central gap was not bridged" );
        s_results.Add( $"PASS {name}: {visible} covered pixels within physical hull; {builder.Count} bridges; {faces.Count} faces" );
        return readTexture ? Read( output[3] ) : values;
      }
      finally {
        command.Release(); particleBuffer.Dispose(); bridgeBuffer.Dispose();
        foreach ( var target in textures ) { target.Release(); Object.DestroyImmediate( target ); }
        Object.DestroyImmediate( material ); Object.DestroyImmediate( go );
        Object.DestroyImmediate( texture );
      }
    }

    private static Vector3 Unproject( Matrix4x4 inverse, float u, float v, float depth )
    {
      var p = inverse * new Vector4( 2*u-1, SystemInfo.graphicsUVStartsAtTop ? 1-2*v : 2*v-1, depth, 1 );
      return new Vector3( p.x, p.y, p.z )/p.w;
    }
    private static RenderTexture[] Targets( List<RenderTexture> allocated )
    {
      var result = new RenderTexture[4];
      for ( int i = 0; i < result.Length; ++i ) {
        result[i] = new RenderTexture( Size, Size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear ) { filterMode = FilterMode.Bilinear };
        result[i].Create(); allocated.Add( result[i] );
      }
      return result;
    }
    private static RenderTargetIdentifier[] Identifiers( RenderTexture[] textures ) =>
      new[] { new RenderTargetIdentifier( textures[0] ), new RenderTargetIdentifier( textures[1] ), new RenderTargetIdentifier( textures[2] ), new RenderTargetIdentifier( textures[3] ) };
    private static void CheckFlatCenter( Color[] pixels, float depth )
    {
      // This patch lies far from every sphere and pair hull: tubes alone leave it empty.
      for ( int y = Size/2-3; y <= Size/2+3; ++y )
        for ( int x = Size/2-3; x <= Size/2+3; ++x ) {
          var pixel = pixels[y*Size+x];
          if ( pixel.a < 0.5f || Mathf.Abs( pixel.r-depth ) > 0.0001f )
            throw new Exception( "Expected a filled planar face at the center, depth " + depth );
        }
      s_results.Add( "PASS 7x7 interior patch is filled and planar, including after smoothing" );
    }
    private static void CompareTexture( Color[] baseline, Color[] moved, string motion, float tolerance = 0.015f )
    {
      int covered = 0; float minimum = 1, maximum = 0;
      for ( int i = 0; i < baseline.Length; ++i ) {
        if ( Mathf.Abs( baseline[i].a-moved[i].a ) > 0.01f ) throw new Exception( motion + ": texture coverage changed" );
        if ( baseline[i].a < 0.5f ) continue;
        ++covered; minimum = Mathf.Min( minimum, baseline[i].r ); maximum = Mathf.Max( maximum, baseline[i].r );
        var delta = baseline[i]-moved[i];
        if ( Mathf.Abs( delta.r )+Mathf.Abs( delta.g )+Mathf.Abs( delta.b ) > tolerance ) throw new Exception( motion + ": texture changed at pixel " + i );
      }
      if ( covered == 0 || maximum-minimum < 0.1f ) throw new Exception( "Texture test is empty or uniform" );
      s_results.Add( $"PASS texture comparison ({motion}): {covered} nonuniform pixels, including bridges" );
    }
    private static Color[] Read( RenderTexture target )
    {
      var previous = RenderTexture.active; RenderTexture.active = target;
      var image = new Texture2D( Size, Size, TextureFormat.RGBAFloat, false, true );
      try { image.ReadPixels( new Rect( 0, 0, Size, Size ), 0, 0 ); image.Apply(); return image.GetPixels(); }
      finally { RenderTexture.active = previous; Object.DestroyImmediate( image ); }
    }
  }
}
