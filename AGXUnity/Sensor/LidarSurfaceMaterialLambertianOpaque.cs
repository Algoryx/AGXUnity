using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSurfaceMaterialLambertianOpaque : LidarSurfaceMaterialDefinition
  {
    [Range(0,1)]
    [Tooltip("The surface reflectivity of this material from 0 to 1. Cannot be changed during runtime!")]
    public float Reflectivity = 0.8f;
    protected RtLambertianOpaqueMaterial m_material = null;
    public override RtMaterialInstance GetRtMaterialInstance() => m_material?.ToMaterialInstance();
    public override RtSurfaceMaterial GetRtMaterial() => m_material;

    public override void Destroy()
    {
      m_material.Dispose();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    public override void Init()
    {
      m_material = RtLambertianOpaqueMaterial.create();
      m_material.setReflectivity(Reflectivity);
    }

  }
}
