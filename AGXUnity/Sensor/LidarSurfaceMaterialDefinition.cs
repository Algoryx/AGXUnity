using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [HelpURL("https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors")]
  public abstract class LidarSurfaceMaterialDefinition : ScriptAsset
  {
    abstract public RtMaterialInstance GetRtMaterialInstance();
    abstract public RtSurfaceMaterial GetRtMaterial();
    abstract public void Init();
  }
}
