using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarLambertianOpaqueMaterial : LidarSurfaceMaterialDefinition
  {
    [Range(0,1)]
    [Tooltip("The surface reflectivity of this material from 0 to 1. Cannot be changed during runtime!")]
    public float Reflectivity = 0.8f;

    public override void Destroy()
    {
      m_material.Dispose();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      m_material = RtLambertianOpaqueMaterial.create();

      return true;
    }
  }
}
