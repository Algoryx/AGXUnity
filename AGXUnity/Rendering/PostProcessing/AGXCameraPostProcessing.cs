using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AGXUnity.Rendering.PostProcessing
{
  // Create a Scriptable Renderer Feature that implements a post-processing effect when the camera is inside a custom volume.
  // For more information about creating scriptable renderer features, refer to https://docs.unity3d.com/Manual/urp/customizing-urp.html
  [DisallowMultipleRendererFeature]
  public sealed class AGXCameraPostProcessing : ScriptableRendererFeature
  {
    #region FEATURE_FIELDS

    // Declare the material used to render the post-processing effect.
    // Add a [SerializeField] attribute so Unity serializes the property and includes it in builds.
    [SerializeField]
    [HideInInspector]
    private Material m_lensDistortionMaterial;
    private AGXLensDistortionPass m_lensDistortionPass;

    #endregion

    #region FEATURE_METHODS

    // Override the Create method.
    // Unity calls this method when the Scriptable Renderer Feature loads for the first time, and when you change a property.
    public override void Create()
    {
      if ( m_lensDistortionMaterial == null )
        m_lensDistortionMaterial = new Material( Shader.Find( "AGXUnity/Shader Graph/CameraLensDistortion" ) );

      if ( m_lensDistortionMaterial )
        m_lensDistortionPass = new AGXLensDistortionPass( name, m_lensDistortionMaterial );
    }

    // Override the AddRenderPasses method to inject passes into the renderer. Unity calls AddRenderPasses once per camera.
    public override void AddRenderPasses( ScriptableRenderer renderer, ref RenderingData renderingData )
    {
      // Skip rendering if the target is a Reflection Probe or a preview camera.
      if ( renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection )
        return;

      if ( !renderingData.postProcessingEnabled )
        return;

      AddLensDistortion( renderer );
    }

    private void AddLensDistortion( ScriptableRenderer renderer )
    {
      // Skip rendering if m_Material or the pass instance are null.
      if ( m_lensDistortionMaterial == null || m_lensDistortionPass == null )
        return;

      // Skip rendering if the camera is outside the custom volume.
      AGXLensDistortion myVolume = VolumeManager.instance.stack?.GetComponent<AGXLensDistortion>();
      if ( myVolume == null || !myVolume.IsActive() )
        return;

      // Specify when the effect will execute during the frame.
      // For a post-processing effect, the injection point is usually BeforeRenderingTransparents, BeforeRenderingPostProcessing, or AfterRenderingPostProcessing.
      // For more information, refer to https://docs.unity3d.com/Manual/urp/customize/custom-pass-injection-points.html 
      m_lensDistortionPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

      // Specify that the effect doesn't need scene depth, normals, motion vectors, or the color texture as input.
      m_lensDistortionPass.ConfigureInput( ScriptableRenderPassInput.None );

      // Add the render pass to the renderer.
      renderer.EnqueuePass( m_lensDistortionPass );
    }

    #endregion

    // Create the custom render pass.
    private class AGXLensDistortionPass : ScriptableRenderPass
    {
      #region PASS_FIELDS

      // Declare the material used to render the post-processing effect.
      private Material m_Material;

      // Declare a property block to set additional properties for the material.
      private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();

      // Declare a property that enables or disables the render pass that samples the color texture.
      private static readonly bool kSampleActiveColor = true;

      // Declare a property that adds or removes depth-stencil support.
      private static readonly bool kBindDepthStencilAttachment = false;

      // Create shader properties in advance, which is more efficient than referencing them by string.
      private static readonly int kSourceTexturePropertyId = Shader.PropertyToID("_SourceTexture");

      #endregion

      public AGXLensDistortionPass( string passName, Material material )
      {
        // Add a profiling sampler.
        profilingSampler = new ProfilingSampler( passName );

        // Assign the material to the render pass.
        m_Material = material;

        // To make sure the render pass can sample the active color buffer, set URP to render to intermediate textures instead of directly to the backbuffer.
        requiresIntermediateTexture = kSampleActiveColor;
      }

      private static bool s_warned = false;

      [Obsolete( "Deprecated in favour of render graph implementation" )]
      public override void Execute( ScriptableRenderContext context, ref RenderingData renderingData )
      {
        if ( !s_warned ) {
          Debug.LogWarning( "AGX camera sensor lens distortion will not work with URP compatiblity mode" );
          s_warned = true;
        }
      }

      #region PASS_RENDER_GRAPH_PATH

      // Declare the resources the main render pass uses.
      // This method is used only in the render graph system path.
      private class MainPassData
      {
        public Material material;
        public TextureHandle inputTexture;
      }

      private static void ExecuteMainPass( MainPassData data, RasterGraphContext context )
      {
        // Clear the material properties.
        s_SharedPropertyBlock.Clear();
        if ( data.inputTexture.IsValid() )
          s_SharedPropertyBlock.SetTexture( kSourceTexturePropertyId, data.inputTexture );

        // Set the material properties based on the blended values of the custom volume.
        // For more information, refer to https://docs.unity3d.com/Manual/urp/post-processing/custom-post-processing-with-volume.html
        AGXLensDistortion myVolume = VolumeManager.instance.stack?.GetComponent<AGXLensDistortion>();
        if ( myVolume != null ) {
          s_SharedPropertyBlock.SetFloat( "_k1", myVolume.radialCoefficients.value.x );
          s_SharedPropertyBlock.SetFloat( "_k2", myVolume.radialCoefficients.value.y );
          s_SharedPropertyBlock.SetFloat( "_k3", myVolume.radialCoefficients.value.z );
          s_SharedPropertyBlock.SetFloat( "_p1", myVolume.tangentialCoefficients.value.x );
          s_SharedPropertyBlock.SetFloat( "_p2", myVolume.tangentialCoefficients.value.y );
        }

        // Draw to the current render target.
        context.cmd.DrawProcedural( Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock );
      }

      // Override the RecordRenderGraph method to implement the rendering logic.
      // This method is used only in the render graph system path.
      public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData )
      {

        // Get the resources the pass uses.
        UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // Sample from the current color texture.
        using ( var builder = renderGraph.AddRasterRenderPass<MainPassData>( passName, out var passData, profilingSampler ) ) {
          passData.material = m_Material;

          TextureHandle destination;

          // Copy cameraColor to a temporary texture, if the kSampleActiveColor property is set to true. 
          if ( kSampleActiveColor ) {
            var cameraColorDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
            cameraColorDesc.name = "_CameraColorCustomPostProcessing";
            cameraColorDesc.clearBuffer = false;

            destination = renderGraph.CreateTexture( cameraColorDesc );
            passData.inputTexture = resourcesData.cameraColor;

            // If you use framebuffer fetch in your material, use builder.SetInputAttachment to reduce GPU bandwidth usage and power consumption. 
            builder.UseTexture( passData.inputTexture, AccessFlags.Read );
          }
          else {
            destination = resourcesData.cameraColor;
            passData.inputTexture = TextureHandle.nullHandle;
          }


          // Set the render graph to render to the temporary texture.
          builder.SetRenderAttachment( destination, 0, AccessFlags.Write );

          // Bind the depth-stencil buffer.
          // This is a demonstration. The code isn't used in the example.
          if ( kBindDepthStencilAttachment )
            builder.SetRenderAttachmentDepth( resourcesData.activeDepthTexture, AccessFlags.Write );

          // Set the render method.
          builder.SetRenderFunc( ( MainPassData data, RasterGraphContext context ) => ExecuteMainPass( data, context ) );

          // Set cameraColor to the new temporary texture so the next render pass can use it. You don't need to blit to and from cameraColor if you use the render graph system.
          if ( kSampleActiveColor ) {
            resourcesData.cameraColor = destination;
          }
        }
      }

      #endregion
    }
  }
}
