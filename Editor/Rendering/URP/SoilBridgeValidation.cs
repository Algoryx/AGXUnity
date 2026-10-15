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
        var textureBaseline = Capture( shader, equal, 1.5f, false, true, "texture-baseline", true, readTexture: true );
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
        var compiledMaterial = new Material( shader );
        try {
          for ( int pass = 0; pass < compiledMaterial.passCount; ++pass ) ShaderUtil.CompilePass( compiledMaterial, pass, true );
        }
        finally { Object.DestroyImmediate( compiledMaterial ); }
        foreach ( var message in ShaderUtil.GetShaderMessages( shader ) ) {
          s_results.Add( "Shader: " + message.message );
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

    private static TerrainParticleSnapshotProvider.Particle Particle( Vector3 position, float radius ) =>
      new TerrainParticleSnapshotProvider.Particle { Position = position, Radius = radius, Rotation = Quaternion.identity, Color = Color.white };

    private static Color[] Capture( Shader shader, TerrainParticleSnapshotProvider.Particle[] particles, float support,
                                    bool perspective, bool smooth, string name, bool expectBridge = false, bool near = false,
                                    bool readTexture = false, Vector3 cameraOffset = default, Quaternion? cameraRotation = null )
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
      var depth = new RenderTexture( Size, Size, 24, RenderTextureFormat.Depth ); depth.Create(); textures.Add( depth );
      var properties = new MaterialPropertyBlock();
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
            if ( Vector3.Dot( world, n ) > bound+0.002f )
              throw new Exception( $"{name}: pixel ({x},{y}) at {world} exceeds physical hull in direction {n}" );
          }
        }
        if ( visible == 0 ) throw new Exception( name + ": no particles rendered" );
        if ( expectBridge && (builder.Count == 0 || values[(Size/2)*Size+Size/2].a < 0.5f) ) throw new Exception( name + ": central gap was not bridged" );
        s_results.Add( $"PASS {name}: {visible} covered pixels within physical hull; {builder.Count} bridges" );
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
    private static void CompareTexture( Color[] baseline, Color[] moved, string motion )
    {
      int covered = 0; float minimum = 1, maximum = 0;
      for ( int i = 0; i < baseline.Length; ++i ) {
        if ( Mathf.Abs( baseline[i].a-moved[i].a ) > 0.01f ) throw new Exception( motion + ": texture coverage changed" );
        if ( baseline[i].a < 0.5f ) continue;
        ++covered; minimum = Mathf.Min( minimum, baseline[i].r ); maximum = Mathf.Max( maximum, baseline[i].r );
        var delta = baseline[i]-moved[i];
        if ( Mathf.Abs( delta.r )+Mathf.Abs( delta.g )+Mathf.Abs( delta.b ) > 0.015f ) throw new Exception( motion + ": texture slid relative to particles at pixel " + i );
      }
      if ( covered == 0 || maximum-minimum < 0.1f ) throw new Exception( "Texture test is empty or uniform" );
      s_results.Add( $"PASS texture follows particle {motion}: {covered} nonuniform pixels, including smoothed bridges" );
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
