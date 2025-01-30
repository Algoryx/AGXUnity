using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarSurfaceMaterialGgxAndOrenNayar : LidarSurfaceMaterialDefinition
  {
    // TODO ranges of these

    [SerializeField]
    private float m_refractiveIndexReal = 1.4517f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The real part of the material top-layer refractive index" )]
    public float RefractiveIndexReal
    {
      get => m_refractiveIndexReal;
      set
      {
        m_refractiveIndexReal = value;
        if ( Native != null )
          Native.setRefractiveIndexReal( value );
      }
    }

    [SerializeField]
    private float m_refractiveIndexImaginary = 0;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "The imaginary part of the material top-layer refractive index" )]
    public float RefractiveIndexImaginary
    {
      get => m_refractiveIndexImaginary;
      set
      {
        m_refractiveIndexImaginary = value;
        if ( Native != null )
          Native.setRefractiveIndexImaginary( value );
      }
    }

    [SerializeField]
    private float m_beckmanRoughness = 0.3f;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "The Beckman roughness of the material top-layer" )]
    public float BeckmanRoughness
    {
      get => m_beckmanRoughness;
      set
      {
        m_beckmanRoughness = value;
        if ( Native != null )
          Native.setBeckmanRoughness( value );
      }
    }

    [SerializeField]
    private float m_orenNayarRoughness = 0.3f;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "The Oren-Nayar roughness of the diffuse second layer of this material" )]
    public float OrenNayarRoughness
    {
      get => m_orenNayarRoughness;
      set
      {
        m_orenNayarRoughness = value;
        if ( Native != null )
          Native.setOrenNayarRoughness( value );
      }
    }

    [SerializeField]
    private float m_diffuseReflectivity = 0;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "The reflectivity of the diffuse second layer of this material" )]
    public float DiffuseReflectivity
    {
      get => m_diffuseReflectivity;
      set
      {
        m_diffuseReflectivity = value;
        if ( Native != null )
          Native.setDiffuseReflectivity( value );
      }
    }

    protected RtGgxAndOrenNayarMaterial Native = null;
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
      Native = RtGgxAndOrenNayarMaterial.create();
      Native.setRefractiveIndexReal( RefractiveIndexReal );
      Native.setRefractiveIndexImaginary( RefractiveIndexImaginary );
      Native.setBeckmanRoughness( BeckmanRoughness );
      Native.setOrenNayarRoughness( OrenNayarRoughness );
      Native.setDiffuseReflectivity( DiffuseReflectivity );
      return true;
    }
  }
}
