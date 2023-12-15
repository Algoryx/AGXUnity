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
      if ( cam.cameraType == CameraType.Preview)
        return includeInPreview;

      // Render all except SceneView which require additional checks for prefab stage
      if ( cam.cameraType != CameraType.SceneView )
        return PrefabUtils.IsNonAssetInstance( allowedPrefabObject );

      // Only render in prefab stage if allowed object is present in it.
      if ( PrefabUtils.IsEditingPrefab )
        return PrefabUtils.IsPartOfEditingPrefab( allowedPrefabObject );

      // SceneView is not prefab stage, OK to render.
      return true;
    }
  }
}
