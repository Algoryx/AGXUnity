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
      if ( GraphicsSettings.renderPipelineAsset != null ) {
        // SRP
        var srpType = GraphicsSettings.renderPipelineAsset.GetType().ToString();
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
      switch ( DetectPipeline() ) {
        case PipelineType.BuiltIn:
          return new Material( Shader.Find( "Standard" ) );
        case PipelineType.HDRP:
          return new Material( Shader.Find( "HDRP/Lit" ) );
        case PipelineType.Universal:
          return new Material( Shader.Find( "Universal Render Pipeline/Lit" ) );
        default:
          return null;
      }
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
      var pipeline = DetectPipeline();
      if ( pipeline == PipelineType.Universal || pipeline == PipelineType.HDRP )
        mat.SetVector( "_BaseColor", col );
      else
        mat.SetVector( "_Color", col );
    }

    public static void SetSmoothness( Material mat, float smoothness )
    {
      var pipeline = DetectPipeline();
      if ( pipeline == PipelineType.Universal || pipeline == PipelineType.HDRP )
        mat.SetFloat( "_Smoothness", smoothness );
      else
        mat.SetFloat( "_Glossiness", smoothness );
    }
  }
}
