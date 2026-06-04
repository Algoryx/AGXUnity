using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Wheel" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( RigidBody ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain-wheel" )]
  public class DeformableTerrainWheel : ScriptComponent
  {
    /// <summary>
    /// Native instance of this terrain wheel.
    /// </summary>
    [HideInInspector]
    public agxTerrain.TerrainWheel Native { get; private set; } = null;

    /// <summary>
    /// Rigid body component of this terrain wheel.
    /// </summary>
    public RigidBody RigidBody { get { return m_rb ?? ( m_rb = GetComponent<RigidBody>() ); } }

    #region Wheel Deformation Properties
    [SerializeField]
    private bool m_enableTerrainDeformation = true;

    /// <summary>
    /// Determines whether this terrain wheel deforms the terrain it is in contact with.
    /// </summary>
    [InspectorGroupBegin( Name = "Wheel Deformation Properties" )]
    [Tooltip( "Determines whether this terrain wheel deforms the terrain it is in contact with." )]
    public bool EnableTerrainDeformation
    {
      get
      {
        return Native != null ?
               Native.getWheelDeformationProperties().getEnableDeformation() :
               m_enableTerrainDeformation;
      }
      set
      {
        m_enableTerrainDeformation = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setEnableDeformation( m_enableTerrainDeformation );
      }
    }

    [SerializeField]
    private bool m_enableTerrainDisplacement = true;

    /// <summary>
    /// Determines whether this terrain wheel displaces terrain soil to create ridges.
    /// </summary>
    [Tooltip( "Determines whether this terrain wheel displaces terrain soil to create ridges." )]
    public bool EnableTerrainDisplacement
    {
      get
      {
        return Native != null ?
               Native.getWheelDeformationProperties().getEnableDisplacement() :
               m_enableTerrainDisplacement;
      }
      set
      {
        m_enableTerrainDisplacement = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setEnableDisplacement( m_enableTerrainDisplacement );
      }
    }

    [SerializeField]
    private bool m_slipDependenceBulldozing = false;

    /// <summary>
    /// Determines whether bulldozing displacement in front and lateral directions depends on wheel slip ratio.
    /// </summary>
    [Tooltip( "Determines whether bulldozing displacement in front and lateral directions depends on wheel slip ratio." )]
    public bool SlipDependenceBulldozing
    {
      get
      {
        return Native != null ?
               Native.getWheelDeformationProperties().getSlipDependenceBulldozing() :
               m_slipDependenceBulldozing;
      }
      set
      {
        m_slipDependenceBulldozing = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setSlipDependenceBulldozing( m_slipDependenceBulldozing );
      }
    }

    [SerializeField]
    private bool m_slipDependenceSlipDisplacement = true;

    /// <summary>
    /// Determines whether slip-based displacement to the rear depends on wheel slip ratio.
    /// </summary>
    [Tooltip( "Determines whether slip-based displacement to the rear depends on wheel slip ratio." )]
    public bool SlipDependenceSlipDisplacement
    {
      get
      {
        return Native != null ?
               Native.getWheelDeformationProperties().getSlipDependenceSlipDisplacement() :
               m_slipDependenceSlipDisplacement;
      }
      set
      {
        m_slipDependenceSlipDisplacement = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setSlipDependenceSlipDisplacement( m_slipDependenceSlipDisplacement );
      }
    }

    [SerializeField]
    private float m_forwardDisplacementWeight = 0.5f;

    /// <summary>
    /// Weight [0, 1] determining how much of the bulldozed mass is distributed forward vs. laterally.
    /// </summary>
    [Range( 0.0f, 1.0f )]
    [Tooltip( "Weight [0, 1] determining how much of the bulldozed mass is distributed forward vs. laterally." )]
    public float ForwardDisplacementWeight
    {
      get
      {
        return Native != null ?
               (float)Native.getWheelDeformationProperties().getForwardDisplacementWeight() :
               m_forwardDisplacementWeight;
      }
      set
      {
        m_forwardDisplacementWeight = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setForwardDisplacementWeight( m_forwardDisplacementWeight );
      }
    }

    [SerializeField]
    private float m_bulldozeDisplacementAmountFactor = 0.5f;

    /// <summary>
    /// Fraction [0, 1] of removed mass allocated to bulldozing. The remainder is used for slip displacement.
    /// </summary>
    [Range( 0.0f, 1.0f )]
    [Tooltip( "Fraction [0, 1] of removed mass allocated to bulldozing. The remainder is used for slip displacement." )]
    public float BulldozeDisplacementAmountFactor
    {
      get
      {
        return Native != null ?
               (float)Native.getWheelDeformationProperties().getBulldozeDisplacementAmountFactor() :
               m_bulldozeDisplacementAmountFactor;
      }
      set
      {
        m_bulldozeDisplacementAmountFactor = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setBulldozeDisplacementAmountFactor( m_bulldozeDisplacementAmountFactor );
      }
    }

    [SerializeField]
    private float m_lateralDisplacementDistScaling = 0.5f;

    /// <summary>
    /// Scaling factor for lateral displacement distance, multiplied by wheel width.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for lateral displacement distance, multiplied by wheel width." )]
    public float LateralDisplacementDistScaling
    {
      get
      {
        return Native != null ?
               (float)Native.getWheelDeformationProperties().getLateralDisplacementDistScaling() :
               m_lateralDisplacementDistScaling;
      }
      set
      {
        m_lateralDisplacementDistScaling = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setLateralDisplacementDistScaling( m_lateralDisplacementDistScaling );
      }
    }

    [SerializeField]
    private float m_forwardDisplacementDistScaling = 0.5f;

    /// <summary>
    /// Scaling factor for forward bulldozing displacement distance, multiplied by wheel radius.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for forward bulldozing displacement distance, multiplied by wheel radius." )]
    public float ForwardDisplacementDistScaling
    {
      get
      {
        return Native != null ?
               (float)Native.getWheelDeformationProperties().getForwardDisplacementDistScaling() :
               m_forwardDisplacementDistScaling;
      }
      set
      {
        m_forwardDisplacementDistScaling = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setForwardDisplacementDistScaling( m_forwardDisplacementDistScaling );
      }
    }

    [SerializeField]
    private float m_backwardDisplacementDistScaling = 0.5f;

    /// <summary>
    /// Scaling factor for slip-based rearward displacement distance, multiplied by wheel radius.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for slip-based rearward displacement distance, multiplied by wheel radius." )]
    public float BackwardDisplacementDistScaling
    {
      get
      {
        return Native != null ?
               (float)Native.getWheelDeformationProperties().getBackwardDisplacementDistScaling() :
               m_backwardDisplacementDistScaling;
      }
      set
      {
        m_backwardDisplacementDistScaling = value;
        if ( Native != null )
          Native.getWheelDeformationProperties().setBackwardDisplacementDistScaling( m_backwardDisplacementDistScaling );
      }
    }
    #endregion

    #region Terrain Wheel Settings
    [SerializeField]
    private float m_angularIntegrationStep = 0.001f * Mathf.Rad2Deg;

    /// <summary>
    /// Angular integration step size in degrees.
    /// </summary>
    [InspectorGroupBegin( Name = "Terrain Wheel Settings" )]
    [ClampAboveZeroInInspector]
    [Tooltip( "Angular integration step size in degrees." )]
    public float AngularIntegrationStep
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getAngularIntegrationStep() :
               m_angularIntegrationStep;
      }
      set
      {
        m_angularIntegrationStep = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setAngularIntegrationStep( Mathf.Deg2Rad * m_angularIntegrationStep );
      }
    }

    [SerializeField]
    private agxTerrain.TerrainWheelSettings.PressureSinkageModel m_pressureSinkageModel = agxTerrain.TerrainWheelSettings.PressureSinkageModel.BEKKER;

    /// <summary>
    /// Pressure-sinkage model used by the terrain wheel.
    /// </summary>
    [Tooltip( "Pressure-sinkage model used by the terrain wheel." )]
    public agxTerrain.TerrainWheelSettings.PressureSinkageModel PressureSinkageModel
    {
      get
      {
        return Native != null ?
               Native.getTerrainWheelSettings().getPressureSinkageModel() :
               m_pressureSinkageModel;
      }
      set
      {
        m_pressureSinkageModel = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setPressureSinkageModel( m_pressureSinkageModel );
      }
    }

    [SerializeField]
    private bool m_enableComputeRearAngleFromFrontAngle = false;

    /// <summary>
    /// When enabled, the rear contact angle theta_r is derived from the front contact angle theta_f.
    /// </summary>
    [Tooltip( "When enabled, the rear contact angle theta_r is derived from the front contact angle theta_f." )]
    public bool EnableComputeRearAngleFromFrontAngle
    {
      get
      {
        return Native != null ?
               Native.getTerrainWheelSettings().getEnableComputeRearAngleFromFrontAngle() :
               m_enableComputeRearAngleFromFrontAngle;
      }
      set
      {
        m_enableComputeRearAngleFromFrontAngle = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setEnableComputeRearAngleFromFrontAngle( m_enableComputeRearAngleFromFrontAngle );
      }
    }

    [SerializeField]
    private bool m_enableComputeMaximumNormalStressAngleFromFrontAngle = true;

    /// <summary>
    /// When enabled, the maximum normal stress angle is derived from the front contact angle.
    /// </summary>
    [Tooltip( "When enabled, the maximum normal stress angle is derived from the front contact angle." )]
    public bool EnableComputeMaximumNormalStressAngleFromFrontAngle
    {
      get
      {
        return Native != null ?
               Native.getTerrainWheelSettings().getEnableComputeMaximumNormalStressAngleFromFrontAngle() :
               m_enableComputeMaximumNormalStressAngleFromFrontAngle;
      }
      set
      {
        m_enableComputeMaximumNormalStressAngleFromFrontAngle = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setEnableComputeMaximumNormalStressAngleFromFrontAngle( m_enableComputeMaximumNormalStressAngleFromFrontAngle );
      }
    }

    [SerializeField]
    private float m_rearAndFrontAngleMaxMagnitude = 90.0f;

    /// <summary>
    /// Maximum allowed magnitude for the rear and front contact angles in degrees.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed magnitude for the rear and front contact angles in degrees." )]
    public float RearAndFrontAngleMaxMagnitude
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getRearAndFrontAngleMaxMagnitude() :
               m_rearAndFrontAngleMaxMagnitude;
      }
      set
      {
        m_rearAndFrontAngleMaxMagnitude = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setRearAndFrontAngleMaxMagnitude( Mathf.Deg2Rad * m_rearAndFrontAngleMaxMagnitude );
      }
    }

    [SerializeField]
    private float m_slipRatioVxThreshold = 0.01f * Mathf.Rad2Deg;

    /// <summary>
    /// Longitudinal velocity threshold for the slip-ratio dead-band (degrees/s angular equivalent).
    /// The slip ratio is clamped to zero when both the longitudinal and rotational speeds are
    /// below their respective thresholds.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity threshold for the slip-ratio dead-band in degrees/s angular equivalent." )]
    public float SlipRatioVxThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipRatioVxAngularEquivalentThreshold() :
               m_slipRatioVxThreshold;
      }
      set
      {
        m_slipRatioVxThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipRatioVxThreshold );
      }
    }

    [SerializeField]
    private float m_slipRatioOmegaYRThreshold = 0.01f * Mathf.Rad2Deg;

    /// <summary>
    /// Rotational speed threshold for the slip-ratio dead-band (degrees/s).
    /// Corresponds to |omegaY| in the slip-ratio logic.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Rotational speed threshold for the slip-ratio dead-band in degrees/s." )]
    public float SlipRatioOmegaYRThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipRatioOmegaYThreshold() :
               m_slipRatioOmegaYRThreshold;
      }
      set
      {
        m_slipRatioOmegaYRThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioOmegaYThreshold( Mathf.Deg2Rad * m_slipRatioOmegaYRThreshold );
      }
    }

    [SerializeField]
    private float m_slipRatioSmoothingSpeed = 0.0001f * Mathf.Rad2Deg;

    /// <summary>
    /// Minimum angular speed used to smooth the slip-ratio computation near standstill (degrees/s).
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Minimum angular speed used to smooth the slip-ratio computation near standstill in degrees/s." )]
    public float SlipRatioSmoothingSpeed
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipRatioSmoothingAngularSpeed() :
               m_slipRatioSmoothingSpeed;
      }
      set
      {
        m_slipRatioSmoothingSpeed = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioSmoothingAngularSpeed( Mathf.Deg2Rad * m_slipRatioSmoothingSpeed );
      }
    }

    [SerializeField]
    private float m_slipRatioMaxMagnitude = 1.0f;

    /// <summary>
    /// Maximum allowed magnitude of the computed slip ratio.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed magnitude of the computed slip ratio." )]
    public float SlipRatioMaxMagnitude
    {
      get
      {
        return Native != null ?
               (float)Native.getTerrainWheelSettings().getSlipRatioMaxMagnitude() :
               m_slipRatioMaxMagnitude;
      }
      set
      {
        m_slipRatioMaxMagnitude = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioMaxMagnitude( m_slipRatioMaxMagnitude );
      }
    }

    [SerializeField]
    private float m_slipRatioFallbackValue = 0.1f;

    /// <summary>
    /// Slip ratio fallback value when the angular equivalent of vX and omegaY are below their thresholds.
    /// </summary>
    [Tooltip( "Slip ratio fallback value when the angular equivalent of vX and omegaY are below their thresholds." )]
    public float SlipRatioFallbackValue
    {
      get
      {
        return Native != null ?
               (float)Native.getTerrainWheelSettings().getSlipRatioFallbackValue() :
               m_slipRatioFallbackValue;
      }
      set
      {
        m_slipRatioFallbackValue = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioFallbackValue( m_slipRatioFallbackValue );
      }
    }

    [SerializeField]
    private float m_slipAngleVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    /// <summary>
    /// Longitudinal velocity angular equivalent threshold used by slip-angle computation in degrees/s.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity angular equivalent threshold used by slip-angle computation in degrees/s." )]
    public float SlipAngleVxAngularEquivalentThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipAngleVxAngularEquivalentThreshold() :
               m_slipAngleVxAngularEquivalentThreshold;
      }
      set
      {
        m_slipAngleVxAngularEquivalentThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipAngleVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVxAngularEquivalentThreshold );
      }
    }

    [SerializeField]
    private float m_slipAngleVyAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    /// <summary>
    /// Lateral velocity angular equivalent threshold used by slip-angle computation in degrees/s.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Lateral velocity angular equivalent threshold used by slip-angle computation in degrees/s." )]
    public float SlipAngleVyAngularEquivalentThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipAngleVyAngularEquivalentThreshold() :
               m_slipAngleVyAngularEquivalentThreshold;
      }
      set
      {
        m_slipAngleVyAngularEquivalentThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipAngleVyAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVyAngularEquivalentThreshold );
      }
    }

    [SerializeField]
    private float m_slipAngleMaxMagnitude = 45.0f;

    /// <summary>
    /// Maximum allowed slip-angle magnitude in degrees.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed slip-angle magnitude in degrees." )]
    public float SlipAngleMaxMagnitude
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipAngleMaxMagnitude() :
               m_slipAngleMaxMagnitude;
      }
      set
      {
        m_slipAngleMaxMagnitude = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipAngleMaxMagnitude( Mathf.Deg2Rad * m_slipAngleMaxMagnitude );
      }
    }

    [SerializeField]
    private float m_slipAngleFallbackValue = 4.5f;

    /// <summary>
    /// Slip-angle fallback value in degrees when vX and vY are below their thresholds.
    /// </summary>
    [Tooltip( "Slip-angle fallback value in degrees when vX and vY are below their thresholds." )]
    public float SlipAngleFallbackValue
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getSlipAngleFallbackValue() :
               m_slipAngleFallbackValue;
      }
      set
      {
        m_slipAngleFallbackValue = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipAngleFallbackValue( Mathf.Deg2Rad * m_slipAngleFallbackValue );
      }
    }

    [SerializeField]
    private float m_rollingModeVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    /// <summary>
    /// Longitudinal velocity angular equivalent threshold used for rolling-mode classification in degrees/s.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity angular equivalent threshold used for rolling-mode classification in degrees/s." )]
    public float RollingModeVxAngularEquivalentThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getRollingModeVxAngularEquivalentThreshold() :
               m_rollingModeVxAngularEquivalentThreshold;
      }
      set
      {
        m_rollingModeVxAngularEquivalentThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setRollingModeVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_rollingModeVxAngularEquivalentThreshold );
      }
    }

    [SerializeField]
    private float m_rollingModeOmegaYThreshold = 0.017453293f * Mathf.Rad2Deg;

    /// <summary>
    /// Wheel axle angular velocity magnitude threshold used for rolling-mode classification in degrees/s.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Wheel axle angular velocity magnitude threshold used for rolling-mode classification in degrees/s." )]
    public float RollingModeOmegaYThreshold
    {
      get
      {
        return Native != null ?
               Mathf.Rad2Deg * (float)Native.getTerrainWheelSettings().getRollingModeOmegaYThreshold() :
               m_rollingModeOmegaYThreshold;
      }
      set
      {
        m_rollingModeOmegaYThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setRollingModeOmegaYThreshold( Mathf.Deg2Rad * m_rollingModeOmegaYThreshold );
      }
    }
    #endregion

    /// <summary>
    /// Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model.
    /// NB: if using multiple shape materials on the terrain this is not reliable!
    /// </summary>
    [Tooltip( "Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model." )]
    public bool WarnIfNotUsingCorrectForceModel = false;

    public ContactMaterial contactMaterial;

    protected override bool Initialize()
    {
      var rb = RigidBody?.GetInitialized<RigidBody>()?.Native;
      if ( rb == null ) {
        Debug.LogWarning( "Unable to find RigidBody component for DeformableTerrainWheel - wheel instance ignored.", this );
        return false;
      }

      var cylinders = RigidBody.GetComponentsInChildren<Collide.Cylinder>()
                               .Where( c => c.GetComponentInParent<RigidBody>() == RigidBody )
                               .ToArray();

      if ( cylinders.Length != 1 ) {
        Debug.LogWarning( $"DeformableTerrainWheel requires exactly 1 Cylinder shape in the RigidBody, found {cylinders.Length}.", this );
        return false;
      }

      cylinders[ 0 ].GetInitialized<Collide.Cylinder>();
      var cylinder = cylinders[ 0 ].Native;
      if ( cylinder == null ) {
        Debug.LogWarning( "Unable to initialize Cylinder shape for DeformableTerrainWheel.", this );
        return false;
      }

      Native = new agxTerrain.TerrainWheel( cylinder );

      var wheelDeformationProperties = Native.getWheelDeformationProperties();
      wheelDeformationProperties.setEnableDeformation( m_enableTerrainDeformation );
      wheelDeformationProperties.setEnableDisplacement( m_enableTerrainDisplacement );
      wheelDeformationProperties.setSlipDependenceBulldozing( m_slipDependenceBulldozing );
      wheelDeformationProperties.setSlipDependenceSlipDisplacement( m_slipDependenceSlipDisplacement );
      wheelDeformationProperties.setForwardDisplacementWeight( m_forwardDisplacementWeight );
      wheelDeformationProperties.setBulldozeDisplacementAmountFactor( m_bulldozeDisplacementAmountFactor );
      wheelDeformationProperties.setLateralDisplacementDistScaling( m_lateralDisplacementDistScaling );
      wheelDeformationProperties.setForwardDisplacementDistScaling( m_forwardDisplacementDistScaling );
      wheelDeformationProperties.setBackwardDisplacementDistScaling( m_backwardDisplacementDistScaling );
      Native.setWheelDeformationProperties( wheelDeformationProperties );
      var terrainWheelSettings = Native.getTerrainWheelSettings();
      terrainWheelSettings.setAngularIntegrationStep( Mathf.Deg2Rad * m_angularIntegrationStep );
      terrainWheelSettings.setPressureSinkageModel( m_pressureSinkageModel );
      terrainWheelSettings.setEnableComputeRearAngleFromFrontAngle( m_enableComputeRearAngleFromFrontAngle );
      terrainWheelSettings.setEnableComputeMaximumNormalStressAngleFromFrontAngle( m_enableComputeMaximumNormalStressAngleFromFrontAngle );
      terrainWheelSettings.setRearAndFrontAngleMaxMagnitude( Mathf.Deg2Rad * m_rearAndFrontAngleMaxMagnitude );
      terrainWheelSettings.setSlipRatioVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipRatioVxThreshold );
      terrainWheelSettings.setSlipRatioOmegaYThreshold( Mathf.Deg2Rad * m_slipRatioOmegaYRThreshold );
      terrainWheelSettings.setSlipRatioSmoothingAngularSpeed( Mathf.Deg2Rad * m_slipRatioSmoothingSpeed );
      terrainWheelSettings.setSlipRatioMaxMagnitude( m_slipRatioMaxMagnitude );
      terrainWheelSettings.setSlipRatioFallbackValue( m_slipRatioFallbackValue );
      terrainWheelSettings.setSlipAngleVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVxAngularEquivalentThreshold );
      terrainWheelSettings.setSlipAngleVyAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVyAngularEquivalentThreshold );
      terrainWheelSettings.setSlipAngleMaxMagnitude( Mathf.Deg2Rad * m_slipAngleMaxMagnitude );
      terrainWheelSettings.setSlipAngleFallbackValue( Mathf.Deg2Rad * m_slipAngleFallbackValue );
      terrainWheelSettings.setRollingModeVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_rollingModeVxAngularEquivalentThreshold );
      terrainWheelSettings.setRollingModeOmegaYThreshold( Mathf.Deg2Rad * m_rollingModeOmegaYThreshold );
      Native.setTerrainWheelSettings( terrainWheelSettings );


      GetSimulation().add( Native );

      return true;
    }

    private void LateUpdate()
    {
      if ( Native?.getActiveTerrain() == null )
        return;
      
      if (!ActiveContactMaterialUsesTerrainWheelForceModel)
        Debug.LogWarning( "Active Contact Material is NOT using terrainWheelForceModel!" );
    }

    private bool ActiveContactMaterialUsesTerrainWheelForceModel => GetActiveContactMaterial()?.getFrictionModel()?.asTerrainWheelForceModel() != null;

    private agx.ContactMaterial GetActiveContactMaterial()
    {
      if ( Native == null || GetSimulation() == null )
        return null;

      var wheelShapeMaterial = Native.getWheelGeometry()?.getMaterial();
      var terrainShapeMaterial = Native.getActiveTerrain()?.getMaterial();

      if ( wheelShapeMaterial == null || terrainShapeMaterial == null )
        return null;

      var cm = GetSimulation()?.getMaterialManager()?.getContactMaterial( wheelShapeMaterial, terrainShapeMaterial );
      return cm;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().remove( Native );

      Native = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      if ( GetComponent<RigidBody>() == null )
        Debug.LogError( "Component: DeformableTerrainWheel requires a RigidBody component.", this );
    }

    private RigidBody m_rb = null;
  }
}
