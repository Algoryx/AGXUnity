using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Utils
{
  public static class RenderingUtils
  {
    public enum PipelineType
    {
      Unsupported,
      BuiltIn,
      Universal,
      HDRP
    }

    /// <summary>
    /// Returns the type of render pipeline that is currently running
    /// </summary>
    /// <returns>An enum representing the render pipeline currently in use</returns>
    public static PipelineType DetectPipeline()
    {
      if ( GraphicsSettings.defaultRenderPipeline != null ) {
        // SRP
        var srpType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
        if ( srpType.Contains( "HDRenderPipelineAsset" ) )
          return PipelineType.HDRP;
        else if ( srpType.Contains( "UniversalRenderPipelineAsset" ) || srpType.Contains( "LightweightRenderPipelineAsset" ) )
          return PipelineType.Universal;
        else return PipelineType.Unsupported;
      }
      // no SRP
      return PipelineType.BuiltIn;
    }

    private static string[] s_supportedHDRP = new string[] {
      "HDRenderPipeline",
      "HighDefinitionRenderPipeline"
    };

    private static string[] s_supportedURP = new string[] {
      "UniversalRenderPipeline",
      "UniversalPipeline"
    };

    /// <summary>
    /// Checks whether the material supports a given pipeline type. 
    /// Some assumptions are made here. 
    /// If we don't recognize the pipeline type we assume that the material does
    /// not support the pipeline. Similarly we assume that all materials support
    /// the built-in pipeline. This is done since it is not possible to easily tell
    /// if a given material is supported and the chance of having a HDRP or URP material
    /// being used is quite small since project conversions to the Built-in pipeline
    /// is not supported by Unity.
    /// 
    /// This works by checking whether any subshader defines the RenderPipeline
    /// tag and if so, compares the value to a set of supported identifiers:
    /// For HDRP:
    ///  - HDRenderPipeline
    ///  - HighDefinitionRenderPipeline
    /// For URP:
    ///  - UniversalRenderPipeline
    /// </summary>
    /// <param name="pipelineType">The pipeline to check for support against</param>
    /// <returns>true if the specified pipeline is supported, false otherwise</returns>
    public static bool SupportsPipeline( this Material mat, PipelineType pipelineType )
    {
      if ( mat.shader.name == "Hidden/InternalErrorShader" )
        return false;
      if ( pipelineType == PipelineType.Unsupported )
        return false;
      if ( pipelineType == PipelineType.BuiltIn )
        return true;

      var tag = new ShaderTagId( "RenderPipeline" );
      if ( pipelineType == PipelineType.HDRP ) {
        for ( int i = 0; i < mat.shader.subshaderCount; i++ ) {
          var tagName = mat.shader.FindSubshaderTagValue( i, tag ).name;
          if ( s_supportedHDRP.Contains( tagName ) )
            return true;
        }
      }
      else if ( pipelineType == PipelineType.Universal ) {
        for ( int i = 0; i < mat.shader.subshaderCount; i++ ) {
          var tagName = mat.shader.FindSubshaderTagValue( i, tag ).name;
          if ( s_supportedURP.Contains( tagName ) )
            return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Check if the given camera should render at this point in time.
    /// This should be used as an early-out in custom render callbacks to avoid rendering dynamic objects in
    /// views other than the game and scene view as well as to prevent the scene view camera to render
    /// objects when in the prefab stage.
    /// </summary>
    /// <param name="cam">The camera currently rendering</param>
    /// <param name="allowedPrefabObject">Allow rendering in prefab stage if this object is part of the edited prefab.</param>
    /// <param name="includeInPreview">Allow rendering in preview images.</param>
    /// <returns>True if the camera should render, false otherwise</returns>
    public static bool CameraShouldRender( Camera cam, GameObject allowedPrefabObject = null, bool includeInPreview = false )
    {
      // Only render preview if specified in flag
      if ( cam.cameraType == CameraType.Preview )
        return includeInPreview;

      // Render all except SceneView which require additional checks for prefab stage
      if ( cam.cameraType != CameraType.SceneView )
        return ( !PrefabUtils.IsPrefabInstance( allowedPrefabObject ) && !PrefabUtils.IsPartOfEditingPrefab( allowedPrefabObject ) ) ||
                PrefabUtils.IsNonAssetInstance( allowedPrefabObject );

      // Only render in prefab stage if allowed object is present in it.
      if ( PrefabUtils.IsEditingPrefab )
        return PrefabUtils.IsPartOfEditingPrefab( allowedPrefabObject );

      // SceneView is not prefab stage, OK to render.
      return true;
    }

    /// <summary>
    /// Attempts to create a default lit material with an appropriate shader for the current render pipeline
    /// </summary>
    /// <returns>A new material if the current render pipeline is recognized or null otherwise</returns>
    public static Material CreateDefaultMaterial()
    {
      return new Material( Shader.Find( "AGXUnity/Shader Graph/CrossRPDefault" ) );
    }

    /// <summary>
    /// Attempts to set the main texture property on the given material, respecting the current render pipeline.
    /// </summary>
    /// <param name="mat">The material on which to set the main texture</param>
    /// <param name="tex">The texture to set as the main texture</param>
    public static void SetMainTexture( Material mat, Texture tex )
    {
      switch ( DetectPipeline() ) {
        case PipelineType.BuiltIn:
          mat.SetTexture( "_MainTex", tex );
          break;
        case PipelineType.HDRP:
          mat.SetTexture( "_BaseColorMap", tex );
          break;
        case PipelineType.Universal:
          mat.SetTexture( "_BaseMap", tex );
          break;
        default:
          mat.mainTexture = tex;
          break;
      }
    }

    /// <summary>
    /// Attempts to set the main color property on the given material, respecting the current render pipeline
    /// </summary>
    /// <param name="mat">The material on which to set the main color</param>
    /// <param name="col">The color to set as the main color</param>
    public static void SetColor( Material mat, Color col )
    {
      mat.SetVector( "_BaseColor", col );
      mat.SetVector( "_Color", col );
    }

    /// <summary>
    /// Attempts to set the smoothness on the material, respecting the current render pipeline
    /// This maps to _Smoothness on SRPs and _Glossiness on Built-in
    /// </summary>
    /// <param name="mat">The material on which to set the smoothness</param>
    /// <param name="smoothness">The smoothness to set on the material</param>
    public static void SetSmoothness( Material mat, float smoothness )
    {
      var pipeline = DetectPipeline();
      mat.SetFloat( "_Smoothness", smoothness );
    }

    /// <summary>
    /// Attempts to enable transparency on the given material. This process varies quite a bit 
    /// depending on the active pipeline but generally sets a surface type, blend mode, alpha clipping mode,
    /// zwrite and render queue.
    /// In addition to this various shader keywords might be set/unset and/or passes enabled/disabled.
    /// </summary>
    /// <param name="mat">The material on which to set transparency</param>
    /// <param name="enable">Whether to enable or disable transparency</param>
    public static void SetTransparencyEnabled( Material mat, bool enable )
    {
      var pipeline = DetectPipeline();
      switch ( pipeline ) {
        case PipelineType.BuiltIn:
          mat.SetBlendMode( enable ? Rendering.BlendMode.Transparent : Rendering.BlendMode.Opaque );
          mat.EnableKeyword( $"_BUILTIN_SURFACE_TYPE_TRANSPARENT" );
          mat.SetFloat( $"_BUILTIN_Surface", enable ? 1 : 0 );
          mat.SetFloat( $"_BUILTIN_Blend", 0 );
          mat.SetFloat( $"_BUILTIN_AlphaClip", 0 );
          mat.SetFloat( $"_BUILTIN_SrcBlend", enable ? (int)BlendMode.SrcAlpha : (int)BlendMode.One );
          mat.SetFloat( $"_BUILTIN_DstBlend", enable ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero );
          mat.SetFloat( $"_BUILTIN_ZWrite", enable ? 0 : 1 );
          mat.SetFloat( $"_BUILTIN_ZWriteControl", enable ? 0 : 1 );
          break;
        case PipelineType.HDRP:
        case PipelineType.Universal:
          mat.SetFloat( "_SurfaceType", enable ? 1 : 0 );
          mat.SetFloat( "_BlendMode", 0 );
          mat.SetFloat( "_AlphaCutoffEnable", 0 );
          mat.SetFloat( "_EnableBlendModePreserveSpecularLighting", 1 );
          mat.SetFloat( "_Surface", enable ? 1 : 0 );
          mat.SetFloat( "_Blend", 0 );
          mat.SetFloat( "_Clip", 0 );
          mat.SetInt( "_SrcBlend", enable ? (int)BlendMode.SrcAlpha : (int)BlendMode.One );
          mat.SetInt( "_DstBlend", enable ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero );
          if ( enable ) {
            mat.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
            mat.EnableKeyword( "_SURFACE_TYPE_TRANSPARENT" );
          }
          else {
            mat.EnableKeyword( "_ALPHAPREMULTIPLY_ON" );
            mat.DisableKeyword( "_SURFACE_TYPE_TRANSPARENT" );
          }
          mat.SetInt( "_ZWrite", enable ? 0 : 1 );
          break;
        default:
          break;
      }
      mat.renderQueue = enable ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
      mat.SetOverrideTag( "RenderType", "Transparent" );
    }

    public static void SetShadowcastingEnabled( Material mat, bool enable )
    {
      mat.SetFloat( "_CastShadows", enable ? 1 : 0 );
      mat.SetShaderPassEnabled( "SHADOWCASTER", enable );
    }
  }
}
