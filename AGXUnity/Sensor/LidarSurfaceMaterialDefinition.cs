using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [Serializable]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public abstract class LidarSurfaceMaterialDefinition : ScriptAsset
  {
    [NonSerialized]
    protected RtSurfaceMaterial m_material = null;

    public RtMaterialInstance RtMaterialInstance => m_material?.ToMaterialInstance();
  }
}
