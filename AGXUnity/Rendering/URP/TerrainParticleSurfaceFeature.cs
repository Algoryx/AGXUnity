using System.Collections.Generic;
using AGXUnity.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using RenderingUtils = AGXUnity.Utils.RenderingUtils;

namespace AGXUnity.Rendering.URP
{
  /// <summary>URP 17.4 Forward+ Render Graph integration. Add to the camera's renderer data.</summary>
  public sealed class TerrainParticleSurfaceFeature : ScriptableRendererFeature
  {
    private Material m_material;
    private SurfacePass m_reconstruct;
    private SurfacePass m_composite;
    private static readonly int s_depth = Shader.PropertyToID( "_SoilDepth" );
    private static readonly int s_color = Shader.PropertyToID( "_SoilColor" );
    private static readonly int s_normal = Shader.PropertyToID( "_SoilNormal" );
    private static readonly int s_albedo = Shader.PropertyToID( "_SoilAlbedo" );
    public static readonly ProfilingSampler[] ProfileSamplers = {
      new ProfilingSampler( "AGX Soil.Input" ), new ProfilingSampler( "AGX Soil.FilterHorizontal" ),
      new ProfilingSampler( "AGX Soil.FilterVertical" ), new ProfilingSampler( "AGX Soil.DepthNormals" ),
      new ProfilingSampler( "AGX Soil.Composite" )
    };

    public override void Create()
    {
      CoreUtils.Destroy( m_material );
      var shader = Resources.Load<Shader>( "Shaders/URP/TerrainParticleSurface" );
      if ( shader != null ) { m_material = CoreUtils.CreateEngineMaterial( shader ); m_material.enableInstancing = true; }
      m_reconstruct = new SurfacePass( m_material, false ) { renderPassEvent = RenderPassEvent.AfterRenderingPrePasses };
      m_reconstruct.ConfigureInput( ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal );
      m_composite = new SurfacePass( m_material, true ) { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
      TerrainParticleSurfaceRenderer.CameraSupportsSurface = SupportsCamera;
    }

    public static bool SupportsCamera( Camera camera )
    {
      if ( camera == null || camera.stereoEnabled || camera.cameraType == CameraType.Reflection ||
           !SystemInfo.supportsInstancing || !SystemInfo.supportsComputeShaders || SystemInfo.supportedRenderTargetCount < 4 ||
           !FormatSupported( GraphicsFormat.R32G32B32A32_SFloat ) ||
           !SystemInfo.IsFormatSupported( GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render ) ) return false;
      if ( !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset) || asset.msaaSampleCount != 1 ) return false;
      var additional = camera.GetComponent<UniversalAdditionalCameraData>();
      if ( additional != null && (additional.renderType == CameraRenderType.Overlay || additional.cameraStack.Count > 0) ) return false;
      var renderer = additional != null ? additional.scriptableRenderer : asset.scriptableRenderer;
      var data = asset.rendererDataList;
      for ( int i = 0; i < data.Length; ++i ) {
        if ( asset.GetRenderer( i ) != renderer || !(data[i] is UniversalRendererData universal) ) continue;
        // Forward+ gives the fullscreen material the pipeline's clustered lights.
        // Depth priming would require matching the prepass depth in the opaque pass.
        if ( universal.renderingMode != RenderingMode.ForwardPlus || universal.depthPrimingMode != DepthPrimingMode.Disabled ) return false;
        foreach ( var feature in universal.rendererFeatures )
          if ( feature is TerrainParticleSurfaceFeature soil && soil.isActive && soil.m_material != null && soil.m_material.shader.isSupported ) return true;
      }
      return false;
    }

    private static bool FormatSupported( GraphicsFormat format ) =>
      SystemInfo.IsFormatSupported( format, GraphicsFormatUsage.Render ) && SystemInfo.IsFormatSupported( format, GraphicsFormatUsage.Sample );

    public override void AddRenderPasses( ScriptableRenderer renderer, ref RenderingData renderingData )
    {
      if ( TerrainParticleSurfaceRenderer.Active.Count == 0 || !SupportsCamera( renderingData.cameraData.camera ) ) return;
      renderer.EnqueuePass( m_reconstruct );
      renderer.EnqueuePass( m_composite );
    }

    protected override void Dispose( bool disposing ) { CoreUtils.Destroy( m_material ); m_material = null; }

    private struct Targets
    {
      public TextureHandle Depth, Color, Normal, Albedo;
    }
    private struct Surface
    {
      public TerrainParticleSurfaceRenderer Renderer;
      public Targets Targets;
      public Matrix4x4 Projection, View;
      public Vector4 Size;
    }
    private sealed class FrameSurfaces : ContextItem
    {
      public readonly List<Surface> Items = new List<Surface>();
      public override void Reset() => Items.Clear();
    }
    private sealed class PassData
    {
      public TerrainParticleSurfaceRenderer Renderer;
      public Material Material;
      public Targets Input;
      public TextureHandle SceneDepth;
      public Matrix4x4 Projection, View;
      public Vector4 Size;
      public int Pass;
      public Vector2 Direction;
      public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
    }

    private sealed class SurfacePass : ScriptableRenderPass
    {
      private readonly Material m_material;
      private readonly bool m_composite;
      public SurfacePass( Material material, bool composite ) { m_material = material; m_composite = composite; requiresIntermediateTexture = true; }

      public override void RecordRenderGraph( RenderGraph graph, ContextContainer frame )
      {
        var resources = frame.Get<UniversalResourceData>();
        var camera = frame.Get<UniversalCameraData>();
        var surfaces = frame.GetOrCreate<FrameSurfaces>();
        if ( m_composite ) {
          foreach ( var surface in surfaces.Items ) {
            using ( var builder = graph.AddRasterRenderPass<PassData>( "AGX Soil.Composite", out var data, ProfileSamplers[4] ) ) {
              SetData( data, surface.Renderer, surface.Projection, surface.View, surface.Size, 3 );
              data.Input = surface.Targets;
              ReadTargets( builder, data.Input );
              if ( resources.mainShadowsTexture.IsValid() ) builder.UseTexture( resources.mainShadowsTexture );
              if ( resources.additionalShadowsTexture.IsValid() ) builder.UseTexture( resources.additionalShadowsTexture );
              if ( resources.ssaoTexture.IsValid() ) builder.UseTexture( resources.ssaoTexture );
              builder.UseAllGlobalTextures( true );
              builder.SetRenderAttachment( resources.activeColorTexture, 0, AccessFlags.ReadWrite );
              builder.SetRenderAttachmentDepth( resources.activeDepthTexture, AccessFlags.ReadWrite );
              builder.SetRenderFunc( static (PassData d, RasterGraphContext c) => Fullscreen( d, c ) );
            }
          }
          return;
        }

        foreach ( var renderer in TerrainParticleSurfaceRenderer.Active ) {
          if ( renderer.Display != TerrainParticleSurfaceRenderer.DisplayMode.Surface || renderer.SurfaceCount == 0 ||
               !renderer.IsSurfaceMaterialValid ||
               renderer.Snapshot == null || !renderer.Snapshot.isActiveAndEnabled || renderer.Snapshot.Terrain == null || !renderer.Snapshot.Terrain.isActiveAndEnabled ||
               renderer.SurfaceBuffer == null || renderer.ParticleMesh == null ||
               !RenderingUtils.CameraShouldRender( camera.camera, renderer.gameObject ) ||
               (camera.camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ) continue;
          var descriptor = camera.cameraTargetDescriptor;
          float dynamicX = descriptor.useDynamicScale ? ScalableBufferManager.widthScaleFactor : 1;
          float dynamicY = descriptor.useDynamicScale ? ScalableBufferManager.heightScaleFactor : 1;
          int width = Mathf.Max( 1, Mathf.CeilToInt( descriptor.width * renderer.ResolutionScale * dynamicX ) );
          int height = Mathf.Max( 1, Mathf.CeilToInt( descriptor.height * renderer.ResolutionScale * dynamicY ) );
          var size = new Vector4( width, height, 1.0f / width, 1.0f / height );
          var projection = GL.GetGPUProjectionMatrix( camera.GetProjectionMatrix(), true );
          var view = camera.GetViewMatrix();
          var raw = CreateTargets( graph, width, height );
          var depth = graph.CreateTexture( new TextureDesc( width, height ) {
            name = "Soil nearest depth", format = GraphicsFormat.D32_SFloat,
            clearBuffer = true, clearColor = Color.clear
          } );
          using ( var builder = graph.AddRasterRenderPass<PassData>( "AGX Soil.Input", out var data, ProfileSamplers[0] ) ) {
            SetData( data, renderer, projection, view, size, renderer.SurfaceInput == TerrainParticleSurfaceRenderer.SurfaceInputMode.Sphere ? 0 : 1 );
            data.SceneDepth = resources.cameraDepthTexture;
            builder.UseTexture( data.SceneDepth );
            builder.UseBuffer( graph.ImportBuffer( renderer.SurfaceBuffer ) );
            if ( renderer.BridgeCount > 0 ) builder.UseBuffer( graph.ImportBuffer( renderer.BridgeBuffer ) );
            if ( renderer.FaceCount > 0 ) builder.UseBuffer( graph.ImportBuffer( renderer.FaceBuffer ) );
            builder.UseBuffer( graph.ImportBuffer( renderer.Boundaries.ContactBuffer ) );
            builder.UseBuffer( graph.ImportBuffer( renderer.Boundaries.ShapeBuffer ) );
            builder.UseBuffer( graph.ImportBuffer( renderer.Boundaries.NodeBuffer ) );
            builder.UseBuffer( graph.ImportBuffer( renderer.Boundaries.TriangleBuffer ) );
            builder.UseBuffer( graph.ImportBuffer( renderer.Boundaries.PatchBuffer ) );
            WriteTargets( builder, raw );
            builder.SetRenderAttachmentDepth( depth, AccessFlags.Write );
            builder.SetRenderFunc( static (PassData d, RasterGraphContext c) => {
              Configure( d );
              d.Properties.SetTexture( "_SoilSceneDepth", d.SceneDepth );
              d.Properties.SetBuffer( "_SoilParticles", d.Renderer.SurfaceBuffer );
              d.Renderer.Boundaries.Bind( d.Properties );
              if ( d.Pass == 0 )
                c.cmd.DrawProcedural( Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 6, d.Renderer.SurfaceCount, d.Properties );
              else
                c.cmd.DrawMeshInstancedProcedural( d.Renderer.ParticleMesh, 0, d.Material, 1, d.Renderer.SurfaceCount, d.Properties );
              if ( d.Renderer.BridgeCount > 0 ) {
                d.Properties.SetBuffer( "_SoilBridges", d.Renderer.BridgeBuffer );
                c.cmd.DrawProcedural( Matrix4x4.identity, d.Material, 5, MeshTopology.Triangles, 6, d.Renderer.BridgeCount, d.Properties );
              }
              if ( d.Renderer.FaceCount > 0 ) {
                d.Properties.SetBuffer( "_SoilFaces", d.Renderer.FaceBuffer );
                c.cmd.DrawProcedural( Matrix4x4.identity, d.Material, 6, MeshTopology.Triangles, 6, d.Renderer.FaceCount, d.Properties );
              }
              if ( d.Renderer.Boundaries.PatchCount > 0 )
                c.cmd.DrawProcedural( Matrix4x4.identity, d.Material, 7, MeshTopology.Triangles, 21, d.Renderer.Boundaries.PatchCount, d.Properties );
            } );
          }
          var horizontal = Filter( graph, renderer, raw, resources.cameraDepthTexture, projection, view, size, Vector2.right );
          var filtered = Filter( graph, renderer, horizontal, resources.cameraDepthTexture, projection, view, size, Vector2.up );
          // Prepass depth is a distinct attachment from activeDepthTexture in this
          // supported configuration. Feed SSAO and every sampled depth consumer.
          using ( var builder = graph.AddRasterRenderPass<PassData>( "AGX Soil.DepthNormals", out var data, ProfileSamplers[3] ) ) {
            SetData( data, renderer, projection, view, size, 4 );
            data.Input = filtered;
            ReadTargets( builder, filtered );
            builder.SetRenderAttachment( resources.cameraNormalsTexture, 0, AccessFlags.ReadWrite );
            builder.SetRenderAttachmentDepth( resources.cameraDepthTexture, AccessFlags.ReadWrite );
            builder.SetRenderFunc( static (PassData d, RasterGraphContext c) => Fullscreen( d, c ) );
          }
          surfaces.Items.Add( new Surface { Renderer = renderer, Targets = filtered, Projection = projection, View = view, Size = size } );
        }
      }

      private void SetData( PassData data, TerrainParticleSurfaceRenderer renderer, Matrix4x4 projection, Matrix4x4 view, Vector4 size, int pass )
      {
        data.Renderer = renderer; data.Material = renderer.SoilMaterial != null ? renderer.SoilMaterial : m_material;
        data.Projection = projection; data.View = view; data.Size = size; data.Pass = pass;
      }

      private Targets Filter( RenderGraph graph, TerrainParticleSurfaceRenderer renderer, Targets input, TextureHandle sceneDepth,
                              Matrix4x4 projection, Matrix4x4 view, Vector4 size, Vector2 direction )
      {
        var output = CreateTargets( graph, (int)size.x, (int)size.y );
        using ( var builder = graph.AddRasterRenderPass<PassData>( direction.x > 0 ? "AGX Soil.FilterHorizontal" : "AGX Soil.FilterVertical", out var data, ProfileSamplers[direction.x > 0 ? 1 : 2] ) ) {
          SetData( data, renderer, projection, view, size, 2 );
          data.Input = input; data.Direction = direction; data.SceneDepth = sceneDepth;
          ReadTargets( builder, input );
          builder.UseTexture( sceneDepth );
          WriteTargets( builder, output );
          builder.SetRenderFunc( static (PassData d, RasterGraphContext c) => Fullscreen( d, c ) );
        }
        return output;
      }

      private static Targets CreateTargets( RenderGraph graph, int width, int height )
      {
        var descriptor = new TextureDesc( width, height ) {
          name = "Soil attributes", format = GraphicsFormat.R32G32B32A32_SFloat,
          filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
          clearBuffer = true, clearColor = Color.clear
        };
        var targets = new Targets { Depth = graph.CreateTexture( descriptor ) };
        // Front/back bounds in alpha must retain full depth precision.
        targets.Color = graph.CreateTexture( descriptor ); targets.Normal = graph.CreateTexture( descriptor );
        if ( FormatSupported( GraphicsFormat.R16G16B16A16_SFloat ) ) descriptor.format = GraphicsFormat.R16G16B16A16_SFloat;
        targets.Albedo = graph.CreateTexture( descriptor );
        return targets;
      }

      private static void ReadTargets( IRasterRenderGraphBuilder builder, Targets targets )
      {
        builder.UseTexture( targets.Depth ); builder.UseTexture( targets.Color ); builder.UseTexture( targets.Normal );
        builder.UseTexture( targets.Albedo );
      }
      private static void WriteTargets( IRasterRenderGraphBuilder builder, Targets targets )
      {
        builder.SetRenderAttachment( targets.Depth, 0 ); builder.SetRenderAttachment( targets.Color, 1 ); builder.SetRenderAttachment( targets.Normal, 2 );
        builder.SetRenderAttachment( targets.Albedo, 3 );
      }

      private static void Configure( PassData data )
      {
        var p = data.Properties;
        p.Clear();
        p.SetMatrix( "_SoilProjection", data.Projection );
        p.SetMatrix( "_SoilInverseProjection", data.Projection.inverse );
        p.SetMatrix( "_SoilView", data.View ); p.SetMatrix( "_SoilInverseView", data.View.inverse );
        p.SetVector( "_SoilSize", data.Size );
        p.SetFloat( "_SoilSmoothing", data.Renderer.Smoothing );
        p.SetFloat( "_SoilThreshold", data.Renderer.DepthThreshold );
        p.SetFloat( "_SoilNormalContribution", data.Renderer.InputNormalContribution );
        p.SetFloat( "_SoilRepeatDistance", data.Renderer.TextureRepeatDistance );
        p.SetFloat( "_SoilParticleTexture", data.Renderer.TextureMapping == TerrainParticleSurfaceRenderer.TextureMappingMode.Particle ? 1 : 0 );
        p.SetFloat( "_SoilSmoothness", data.Renderer.Smoothness );
        p.SetColor( "_SoilTint", data.Renderer.SoilTint.linear );
        p.SetFloat( "_SoilParticleColorContribution", data.Renderer.ParticleColorContribution );
        p.SetTexture( "_SoilTexture", data.Renderer.SoilTexture != null ? data.Renderer.SoilTexture : Texture2D.whiteTexture );
      }

      private static void Fullscreen( PassData data, RasterGraphContext context )
      {
        Configure( data );
        data.Properties.SetTexture( s_depth, data.Input.Depth );
        data.Properties.SetTexture( s_color, data.Input.Color );
        data.Properties.SetTexture( s_normal, data.Input.Normal );
        data.Properties.SetTexture( s_albedo, data.Input.Albedo );
        if ( data.Pass == 2 ) {
          data.Properties.SetTexture( "_SoilSceneDepth", data.SceneDepth );
          data.Properties.SetVector( "_SoilFilterDirection", data.Direction );
        }
        context.cmd.DrawProcedural( Matrix4x4.identity, data.Material, data.Pass, MeshTopology.Triangles, 3, 1, data.Properties );
      }
    }
  }
}
