using agxSensor;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#lidar-ambient-materials" )]
  public class AmbientMaterial : ScriptAsset
  {
    /// <summary>
    /// Preconfigured atmosphere types that allow mor intuitive controll of the ambient parameters
    /// </summary>
    public enum ConfigurationType
    {
      Air,
      Fog,
      Rainfall,
      Snowfall
    }

    /// <summary>
    /// Native instance of the Ambient material
    /// </summary>
    public RtAmbientMaterial Native { get; private set; } = null;

    [SerializeField]
    private ConfigurationType m_ambientType = ConfigurationType.Air;

    /// <summary>
    /// The high-level atmospheric model to use to calculate the low-level constants passed to the sensor environment.
    /// The high-level paramaters used vary depending on the type selected.
    /// </summary>
    [Tooltip( "The high-level atmospheric model to use to calculate the low-level constants passed to the sensor environment." +
              "The high-level paramaters used vary depending on the type selected." )]
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

    // These are used to dynamically show the ambient parameters based on chosen configuration type.
#pragma warning disable IDE0051
    private bool HasVisibility => m_ambientType == ConfigurationType.Air || m_ambientType == ConfigurationType.Fog;
    private bool HasRate => m_ambientType == ConfigurationType.Snowfall || m_ambientType == ConfigurationType.Rainfall;
    private bool HasWavelength => m_ambientType == ConfigurationType.Snowfall || m_ambientType == ConfigurationType.Fog;
    private bool HasMaritimeness => m_ambientType == ConfigurationType.Fog;
    private bool HasTropicalness => m_ambientType == ConfigurationType.Rainfall;
#pragma warning restore IDE0051

    [SerializeField]
    private float m_visibility = 7.44703f;

    /// <summary>
    /// The visibility of objects as defined by the meterological optical range (MOR) of the medium.
    /// </summary>
    [DynamicallyShowInInspector( "HasVisibility", true )]
    [ClampAboveZeroInInspector()]
    [Tooltip( "The visibility of objects as defined by the meterological optical range (MOR) of the medium." )]
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

    /// <summary>
    /// The rate of percipitation in the atmosphere.
    /// </summary>
    [DynamicallyShowInInspector( "HasRate", true )]
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The rate of percipitation in the atmosphere." )]
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

    /// <summary>
    /// The signal wavelength used to configure the medium.
    /// </summary>
    [DynamicallyShowInInspector( "HasWavelength", true )]
    [ClampAboveZeroInInspector()]
    [Tooltip( "The signal wavelength used to configure the medium." )]
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

    /// <summary>
    /// A maritimeness parameter used to specify an interpolation value between fine droplet continental radiation fog (0.0) and large droplet maritime fog (1.0).
    /// </summary>
    [DynamicallyShowInInspector( "HasMaritimeness", true )]
    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "A maritimeness parameter used to specify an interpolation value between fine droplet continental radiation fog (0.0) and large droplet maritime fog (1.0)." )]
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

    /// <summary>
    /// A tropicalness parameter used to specify an interpolation value between common rain of smaller drop size (0.0) and tropical rain of larger drop size (1.0).
    /// </summary>
    [DynamicallyShowInInspector( "HasTropicalness", true )]
    [FloatSliderInInspector( 0, 1 )]
    [Tooltip( "A tropicalness parameter used to specify an interpolation value between common rain of smaller drop size (0.0) and tropical rain of larger drop size (1.0)." )]
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

    /// <summary>
    /// When enabled, the advanced properties will be kept when changing the basic parameters. This essentially makes the configuration 'manual'.
    /// </summary>
    [InspectorGroupBegin( Name = "Advanced" )]
    [field: SerializeField]
    [Tooltip( "When enabled, the advanced properties will be kept when changing the basic parameters. " +
              "This essentially makes the configuration 'manual'." )]
    public bool IgnoreBasicChanges { get; set; } = false;

    [SerializeField]
    private float m_attenuationCoefficient = 0.000402272f;

    /// <summary>
    /// Signal attenuation coefficient.
    /// </summary>
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

    /// <summary>
    /// Ambient material refractive index.
    /// </summary>
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

    /// <summary>
    /// Atmospheric return gamma-distribution scale parameter.
    /// </summary>
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

    /// <summary>
    /// Atmospheric return gamma-distribution shape parameter.
    /// </summary>
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

    /// <summary>
    /// Atmospheric return gamma-distribution scaling.
    /// </summary>
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
