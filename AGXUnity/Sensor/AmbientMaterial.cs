using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class AmbientMaterial : ScriptAsset
  {
    public enum ConfigurationType
    {
      Air,
      Fog,
      Rainfall,
      Snowfall
    }

    public RtAmbientMaterial Native { get; private set; } = null;

    [SerializeField]
    private ConfigurationType m_ambientType = ConfigurationType.Air;

    [HideInInspector]
    public ConfigurationType AmbientType
    {
      get => m_ambientType;
      set
      {
        if ( m_ambientType != value ) {
          m_ambientType = value;
          OnBasicChange();
        }
      }
    }

    [SerializeField]
    private float m_visibility = 7.44703f;

    [HideInInspector]
    public float Visibility
    {
      get => m_visibility;
      set
      {
        if ( m_visibility != value ) {
          m_visibility = value;
          OnBasicChange();
        }
      }
    }

    [SerializeField]
    private float m_rate = 1.0f;

    [HideInInspector]
    public float Rate
    {
      get => m_rate;
      set
      {
        if ( m_rate != value ) {
          m_rate = value;
          OnBasicChange();
        }
      }
    }

    [SerializeField]
    private float m_wavelength = 905.0f;

    [HideInInspector]
    public float Wavelength
    {
      get => m_wavelength;
      set
      {
        if ( m_wavelength != value ) {
          m_wavelength = value;
          OnBasicChange();
        }
      }
    }

    [SerializeField]
    private float m_Maritimeness = 0.0f;

    [HideInInspector]
    public float Maritimeness
    {
      get => m_Maritimeness;
      set
      {
        if ( m_Maritimeness != value ) {
          m_Maritimeness = value;
          OnBasicChange();
        }
      }
    }

    [SerializeField]
    private float m_tropicalness = 0.0f;

    [HideInInspector]
    public float Tropicalness
    {
      get => m_tropicalness;
      set
      {
        if ( m_tropicalness != value ) {
          m_tropicalness = value;
          OnBasicChange();
        }
      }
    }

    [InspectorGroupBegin( Name = "Advanced" )]
    [field: SerializeField]
    [Tooltip( "When enabled, the advanced properties will be kept when changing the basic parameters. " +
              "This essentially makes the configuration 'manual'." )]
    public bool IgnoreBasicChanges { get; set; } = false;

    [SerializeField]
    private float m_attenuationCoefficient = 0.000402272f;

    [Tooltip( "Signal attenuation coefficient." )]
    public float AttenuationCoefficient
    {
      get => m_attenuationCoefficient;
      set
      {
        m_attenuationCoefficient = value;
        if ( Native != null )
          Native.setAttenuationCoefficient( m_attenuationCoefficient );
      }
    }

    [SerializeField]
    private float m_refractiveIndex = 1.000273f;

    [Tooltip( "Ambient material refractive index." )]
    public float RefractiveIndex
    {
      get => m_refractiveIndex;
      set
      {
        m_refractiveIndex = value;
        if ( Native != null )
          Native.setRefractiveIndex( m_refractiveIndex );
      }
    }

    [SerializeField]
    private float m_returnGammaDistributionScaleParameter = 0.52f;

    [Tooltip( "Atmospheric return gamma-distribution scale parameter." )]
    public float ReturnGammaDistributionScaleParameter
    {
      get => m_returnGammaDistributionScaleParameter;
      set
      {
        m_returnGammaDistributionScaleParameter = value;
        if ( Native != null )
          Native.setReturnGammaDistributionScaleParameter( m_returnGammaDistributionScaleParameter );
      }
    }

    [SerializeField]
    private float m_returnGammaDistributionShapeParameter = 9.5f;

    [Tooltip( "Atmospheric return gamma-distribution shape parameter." )]
    public float ReturnGammaDistributionShapeParameter
    {
      get => m_returnGammaDistributionShapeParameter;
      set
      {
        m_returnGammaDistributionShapeParameter = value;
        if ( Native != null )
          Native.setReturnGammaDistributionShapeParameter( m_returnGammaDistributionShapeParameter );
      }
    }

    [SerializeField]
    private float m_returnProbabilityScaling = 1.58899e-5f;

    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "Atmospheric return gamma-distribution scaling." )]
    public float ReturnProbabilityScaling
    {
      get => m_returnProbabilityScaling;
      set
      {
        m_returnProbabilityScaling = value;
        if ( Native != null )
          Native.setReturnProbabilityScaling( m_returnProbabilityScaling );
      }
    }

    private void OnBasicChange()
    {
      if ( IgnoreBasicChanges )
        return;
      var mat = Native ?? RtAmbientMaterial.create();
      switch ( AmbientType ) {
        case ConfigurationType.Air: mat.configureAsAir( Visibility ); break;
        case ConfigurationType.Fog: mat.configureAsFog( Visibility, Wavelength, Maritimeness ); break;
        case ConfigurationType.Rainfall: mat.configureAsRainfall( Rate, Tropicalness ); break;
        case ConfigurationType.Snowfall: mat.configureAsSnowfall( Rate, Wavelength ); break;
      }

      AttenuationCoefficient = mat.getAttenuationCoefficient();
      RefractiveIndex = mat.getRefractiveIndex();
      ReturnGammaDistributionScaleParameter = mat.getReturnGammaDistributionScaleParameter();
      ReturnGammaDistributionShapeParameter = mat.getReturnGammaDistributionShapeParameter();
      ReturnProbabilityScaling = mat.getReturnProbabilityScaling();
      if ( Native == null )
        mat.Dispose();
    }

    public override void Destroy()
    {
      Native?.Dispose();
      Native = null;
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      Native = RtAmbientMaterial.create();
      return true;
    }
  }
}
