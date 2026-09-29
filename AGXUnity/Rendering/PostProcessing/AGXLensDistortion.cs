using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AGXUnity.Rendering.PostProcessing
{
  [VolumeComponentMenu( "AGXUnity/AGX Lens Distortion" )]
  [VolumeRequiresRendererFeatures( typeof( AGXCameraPostProcessing ) )]
  [SupportedOnRenderPipeline( typeof( UniversalRenderPipelineAsset ) )]
  [DoNotGenerateCustomEditor]
  public sealed class AGXLensDistortion : VolumeComponent, IPostProcessComponent
  {
    // Set the name of the volume component in the list in the Volume Profile.
    public AGXLensDistortion()
    {
      displayName = "AGX Lens Distortion";
    }

    public EnumParameter<AGXUnity.Sensor.CameraSensor.LensDistortionModel> type = new EnumParameter<AGXUnity.Sensor.CameraSensor.LensDistortionModel>(AGXUnity.Sensor.CameraSensor.LensDistortionModel.None);

    public Vector3Parameter radialCoefficients = new Vector3Parameter(Vector3.zero);
    public Vector2Parameter tangentialCoefficients = new Vector2Parameter(Vector2.zero);

    public bool IsActive()
    {
      if ( type == AGXUnity.Sensor.CameraSensor.LensDistortionModel.BrownConrady )
        return !( radialCoefficients.value.sqrMagnitude == 0.0f && tangentialCoefficients.value.sqrMagnitude == 0.0f );
      return false;
    }
  }
}
