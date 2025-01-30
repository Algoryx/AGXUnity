using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSurfaceMaterialLambertianOpaque : LidarSurfaceMaterialDefinition
  {
    [SerializeField]
    private float m_reflectivity = 0.8f;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "The surface reflectivity of this material from 0 to 1. Cannot be changed during runtime!" )]
    public float Reflectivity
    {
      get => m_reflectivity;
      set
      {
        m_reflectivity = value;
        if ( Native != null )
          Native.setReflectivity( value );
      }
    }

    protected RtLambertianOpaqueMaterial Native = null;
    public override RtMaterialInstance GetRtMaterialInstance() => Native?.ToMaterialInstance();
    public override RtSurfaceMaterial GetRtMaterial() => Native;

    public override void Destroy()
    {
      Native.Dispose();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      Native = RtLambertianOpaqueMaterial.create();
      Native.setReflectivity( Reflectivity );
      return true;
    }
  }
}
