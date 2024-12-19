using System;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;
using agxSensor;
using agx;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSurfaceMaterialGgxAndOrenNayar : LidarSurfaceMaterialDefinition
  {
    // TODO ranges of these

    [Range(0,5)]
    [Tooltip("The real part of the material top-layer refractive index")]
    public float RefractiveIndexReal = 1.4517f;
    [Range(0,1)]
    [Tooltip("The imaginary part of the material top-layer refractive index")]
    public float RefractiveIndexImaginary = 0;
    [Range(0,1)]
    [Tooltip("The Beckman roughness of the material top-layer")]
    public float BeckmanRoughness = 0.3f;
    [Range(0,1)]
    [Tooltip("The Oren-Nayar roughness of the diffuse second layer of this material")]
    public float OrenNayarRoughness = 0.3f;
    [Range(0,1)]
    [Tooltip("The reflectivity of the diffuse second layer of this material")]
    public float DiffuseReflectivity = 0.8f;

    protected RtGgxAndOrenNayarMaterial m_material = null;
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
      m_material = RtGgxAndOrenNayarMaterial.create();
      m_material.setRefractiveIndexReal(RefractiveIndexReal);
      m_material.setRefractiveIndexImaginary(RefractiveIndexImaginary);
      m_material.setBeckmanRoughness(BeckmanRoughness);
      m_material.setOrenNayarRoughness(OrenNayarRoughness);
      m_material.setDiffuseReflectivity(DiffuseReflectivity);
    }

  }
}
