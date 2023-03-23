using UnityEngine.Rendering;

namespace AGXUnityEditor.Utils
{
  public class RenderPipelineDetector
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
  }
}