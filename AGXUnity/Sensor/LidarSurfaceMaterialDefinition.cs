using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public abstract class LidarSurfaceMaterialDefinition : ScriptAsset
  {
    abstract public RtSurfaceMaterial GetRtMaterial();
  }
}
