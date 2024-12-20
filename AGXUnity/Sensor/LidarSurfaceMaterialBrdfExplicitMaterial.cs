using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSurfaceMaterialBrdfExplicit : LidarSurfaceMaterialDefinition
  {
    // TODO configuration of this is unimplemented, currently only the default form.
    // Hints for development: https://www.algoryx.se/documentation/complete/agx/tags/latest/doc/UserManual/source/agxsensor.html#explicit-brdf-surface
    protected RtBrdfExplicitMaterial m_material = null;
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
      m_material = RtBrdfExplicitMaterial.create();
    }

  }
}
