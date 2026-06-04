using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain-wheel" )]
  public class DeformableTerrainWheelSettings : ScriptAsset
  {
    #region Wheel Deformation Properties
    [SerializeField]
    private bool m_enableTerrainDeformation = true;

    [InspectorGroupBegin( Name = "Wheel Deformation Properties" )]
    [Tooltip( "Determines whether this terrain wheel deforms the terrain it is in contact with." )]
    public bool EnableTerrainDeformation
    {
      get { return m_enableTerrainDeformation; }
      set
      {
        m_enableTerrainDeformation = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setEnableDeformation( m_enableTerrainDeformation ) );
      }
    }

    [SerializeField]
    private bool m_enableTerrainDisplacement = true;

    [Tooltip( "Determines whether this terrain wheel displaces terrain soil to create ridges." )]
    public bool EnableTerrainDisplacement
    {
      get { return m_enableTerrainDisplacement; }
      set
      {
        m_enableTerrainDisplacement = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setEnableDisplacement( m_enableTerrainDisplacement ) );
      }
    }

    [SerializeField]
    private bool m_slipDependenceBulldozing = false;

    [Tooltip( "Determines whether bulldozing displacement in front and lateral directions depends on wheel slip ratio." )]
    public bool SlipDependenceBulldozing
    {
      get { return m_slipDependenceBulldozing; }
      set
      {
        m_slipDependenceBulldozing = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setSlipDependenceBulldozing( m_slipDependenceBulldozing ) );
      }
    }

    [SerializeField]
    private bool m_slipDependenceSlipDisplacement = true;

    [Tooltip( "Determines whether slip-based displacement to the rear depends on wheel slip ratio." )]
    public bool SlipDependenceSlipDisplacement
    {
      get { return m_slipDependenceSlipDisplacement; }
      set
      {
        m_slipDependenceSlipDisplacement = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setSlipDependenceSlipDisplacement( m_slipDependenceSlipDisplacement ) );
      }
    }

    [SerializeField]
    private float m_forwardDisplacementWeight = 0.5f;

    [Range( 0.0f, 1.0f )]
    [Tooltip( "Weight [0, 1] determining how much of the bulldozed mass is distributed forward vs. laterally." )]
    public float ForwardDisplacementWeight
    {
      get { return m_forwardDisplacementWeight; }
      set
      {
        m_forwardDisplacementWeight = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setForwardDisplacementWeight( m_forwardDisplacementWeight ) );
      }
    }

    [SerializeField]
    private float m_bulldozeDisplacementAmountFactor = 0.5f;

    [Range( 0.0f, 1.0f )]
    [Tooltip( "Fraction [0, 1] of removed mass allocated to bulldozing. The remainder is used for slip displacement." )]
    public float BulldozeDisplacementAmountFactor
    {
      get { return m_bulldozeDisplacementAmountFactor; }
      set
      {
        m_bulldozeDisplacementAmountFactor = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setBulldozeDisplacementAmountFactor( m_bulldozeDisplacementAmountFactor ) );
      }
    }

    [SerializeField]
    private float m_lateralDisplacementDistScaling = 0.5f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for lateral displacement distance, multiplied by wheel width." )]
    public float LateralDisplacementDistScaling
    {
      get { return m_lateralDisplacementDistScaling; }
      set
      {
        m_lateralDisplacementDistScaling = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setLateralDisplacementDistScaling( m_lateralDisplacementDistScaling ) );
      }
    }

    [SerializeField]
    private float m_forwardDisplacementDistScaling = 0.5f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for forward bulldozing displacement distance, multiplied by wheel radius." )]
    public float ForwardDisplacementDistScaling
    {
      get { return m_forwardDisplacementDistScaling; }
      set
      {
        m_forwardDisplacementDistScaling = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setForwardDisplacementDistScaling( m_forwardDisplacementDistScaling ) );
      }
    }

    [SerializeField]
    private float m_backwardDisplacementDistScaling = 0.5f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Scaling factor for slip-based rearward displacement distance, multiplied by wheel radius." )]
    public float BackwardDisplacementDistScaling
    {
      get { return m_backwardDisplacementDistScaling; }
      set
      {
        m_backwardDisplacementDistScaling = value;
        Propagate( wheel => wheel.getWheelDeformationProperties().setBackwardDisplacementDistScaling( m_backwardDisplacementDistScaling ) );
      }
    }
    #endregion

    #region Terrain Wheel Settings
    [SerializeField]
    private float m_angularIntegrationStep = 0.001f * Mathf.Rad2Deg;

    [InspectorGroupBegin( Name = "Terrain Wheel Settings" )]
    [ClampAboveZeroInInspector]
    [Tooltip( "Angular integration step size in degrees." )]
    public float AngularIntegrationStep
    {
      get { return m_angularIntegrationStep; }
      set
      {
        m_angularIntegrationStep = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setAngularIntegrationStep( Mathf.Deg2Rad * m_angularIntegrationStep ) );
      }
    }

    [SerializeField]
    private agxTerrain.TerrainWheelSettings.PressureSinkageModel m_pressureSinkageModel = agxTerrain.TerrainWheelSettings.PressureSinkageModel.BEKKER;

    [Tooltip( "Pressure-sinkage model used by the terrain wheel." )]
    public agxTerrain.TerrainWheelSettings.PressureSinkageModel PressureSinkageModel
    {
      get { return m_pressureSinkageModel; }
      set
      {
        m_pressureSinkageModel = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setPressureSinkageModel( m_pressureSinkageModel ) );
      }
    }

    [SerializeField]
    private bool m_enableComputeRearAngleFromFrontAngle = false;

    [Tooltip( "When enabled, the rear contact angle theta_r is derived from the front contact angle theta_f." )]
    public bool EnableComputeRearAngleFromFrontAngle
    {
      get { return m_enableComputeRearAngleFromFrontAngle; }
      set
      {
        m_enableComputeRearAngleFromFrontAngle = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setEnableComputeRearAngleFromFrontAngle( m_enableComputeRearAngleFromFrontAngle ) );
      }
    }

    [SerializeField]
    private bool m_enableComputeMaximumNormalStressAngleFromFrontAngle = true;

    [Tooltip( "When enabled, the maximum normal stress angle is derived from the front contact angle." )]
    public bool EnableComputeMaximumNormalStressAngleFromFrontAngle
    {
      get { return m_enableComputeMaximumNormalStressAngleFromFrontAngle; }
      set
      {
        m_enableComputeMaximumNormalStressAngleFromFrontAngle = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setEnableComputeMaximumNormalStressAngleFromFrontAngle( m_enableComputeMaximumNormalStressAngleFromFrontAngle ) );
      }
    }

    [SerializeField]
    private float m_rearAndFrontAngleMaxMagnitude = 90.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed magnitude for the rear and front contact angles in degrees." )]
    public float RearAndFrontAngleMaxMagnitude
    {
      get { return m_rearAndFrontAngleMaxMagnitude; }
      set
      {
        m_rearAndFrontAngleMaxMagnitude = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setRearAndFrontAngleMaxMagnitude( Mathf.Deg2Rad * m_rearAndFrontAngleMaxMagnitude ) );
      }
    }

    [SerializeField]
    private float m_slipRatioVxThreshold = 0.01f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity threshold for the slip-ratio dead-band in degrees/s angular equivalent." )]
    public float SlipRatioVxThreshold
    {
      get { return m_slipRatioVxThreshold; }
      set
      {
        m_slipRatioVxThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipRatioVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipRatioVxThreshold ) );
      }
    }

    [SerializeField]
    private float m_slipRatioOmegaYRThreshold = 0.01f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Rotational speed threshold for the slip-ratio dead-band in degrees/s." )]
    public float SlipRatioOmegaYRThreshold
    {
      get { return m_slipRatioOmegaYRThreshold; }
      set
      {
        m_slipRatioOmegaYRThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipRatioOmegaYThreshold( Mathf.Deg2Rad * m_slipRatioOmegaYRThreshold ) );
      }
    }

    [SerializeField]
    private float m_slipRatioSmoothingSpeed = 0.0001f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Minimum angular speed used to smooth the slip-ratio computation near standstill in degrees/s." )]
    public float SlipRatioSmoothingSpeed
    {
      get { return m_slipRatioSmoothingSpeed; }
      set
      {
        m_slipRatioSmoothingSpeed = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipRatioSmoothingAngularSpeed( Mathf.Deg2Rad * m_slipRatioSmoothingSpeed ) );
      }
    }

    [SerializeField]
    private float m_slipRatioMaxMagnitude = 1.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed magnitude of the computed slip ratio." )]
    public float SlipRatioMaxMagnitude
    {
      get { return m_slipRatioMaxMagnitude; }
      set
      {
        m_slipRatioMaxMagnitude = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipRatioMaxMagnitude( m_slipRatioMaxMagnitude ) );
      }
    }

    [SerializeField]
    private float m_slipRatioFallbackValue = 0.1f;

    [Tooltip( "Slip ratio fallback value when the angular equivalent of vX and omegaY are below their thresholds." )]
    public float SlipRatioFallbackValue
    {
      get { return m_slipRatioFallbackValue; }
      set
      {
        m_slipRatioFallbackValue = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipRatioFallbackValue( m_slipRatioFallbackValue ) );
      }
    }

    [SerializeField]
    private float m_slipAngleVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity angular equivalent threshold used by slip-angle computation in degrees/s." )]
    public float SlipAngleVxAngularEquivalentThreshold
    {
      get { return m_slipAngleVxAngularEquivalentThreshold; }
      set
      {
        m_slipAngleVxAngularEquivalentThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipAngleVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVxAngularEquivalentThreshold ) );
      }
    }

    [SerializeField]
    private float m_slipAngleVyAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Lateral velocity angular equivalent threshold used by slip-angle computation in degrees/s." )]
    public float SlipAngleVyAngularEquivalentThreshold
    {
      get { return m_slipAngleVyAngularEquivalentThreshold; }
      set
      {
        m_slipAngleVyAngularEquivalentThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipAngleVyAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipAngleVyAngularEquivalentThreshold ) );
      }
    }

    [SerializeField]
    private float m_slipAngleMaxMagnitude = 45.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum allowed slip-angle magnitude in degrees." )]
    public float SlipAngleMaxMagnitude
    {
      get { return m_slipAngleMaxMagnitude; }
      set
      {
        m_slipAngleMaxMagnitude = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipAngleMaxMagnitude( Mathf.Deg2Rad * m_slipAngleMaxMagnitude ) );
      }
    }

    [SerializeField]
    private float m_slipAngleFallbackValue = 4.5f;

    [Tooltip( "Slip-angle fallback value in degrees when vX and vY are below their thresholds." )]
    public float SlipAngleFallbackValue
    {
      get { return m_slipAngleFallbackValue; }
      set
      {
        m_slipAngleFallbackValue = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setSlipAngleFallbackValue( Mathf.Deg2Rad * m_slipAngleFallbackValue ) );
      }
    }

    [SerializeField]
    private float m_rollingModeVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Longitudinal velocity angular equivalent threshold used for rolling-mode classification in degrees/s." )]
    public float RollingModeVxAngularEquivalentThreshold
    {
      get { return m_rollingModeVxAngularEquivalentThreshold; }
      set
      {
        m_rollingModeVxAngularEquivalentThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setRollingModeVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_rollingModeVxAngularEquivalentThreshold ) );
      }
    }

    [SerializeField]
    private float m_rollingModeOmegaYThreshold = 0.017453293f * Mathf.Rad2Deg;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Wheel axle angular velocity magnitude threshold used for rolling-mode classification in degrees/s." )]
    public float RollingModeOmegaYThreshold
    {
      get { return m_rollingModeOmegaYThreshold; }
      set
      {
        m_rollingModeOmegaYThreshold = value;
        Propagate( wheel => wheel.getTerrainWheelSettings().setRollingModeOmegaYThreshold( Mathf.Deg2Rad * m_rollingModeOmegaYThreshold ) );
      }
    }
    #endregion

    public void Synchronize( DeformableTerrainWheel wheel )
    {
      try {
        m_singleSynchronizeInstance = wheel;
        Utils.PropertySynchronizer.Synchronize( this );
      }
      finally {
        m_singleSynchronizeInstance = null;
      }
    }

    public void Register( DeformableTerrainWheel wheel )
    {
      if ( !m_wheels.Contains( wheel ) )
        m_wheels.Add( wheel );

      Synchronize( wheel );
    }

    public void Unregister( DeformableTerrainWheel wheel )
    {
      m_wheels.Remove( wheel );
    }

    public override void Destroy()
    {
      m_wheels.Clear();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    private void Propagate( Action<agxTerrain.TerrainWheel> action )
    {
      if ( action == null )
        return;

      if ( m_singleSynchronizeInstance != null ) {
        if ( m_singleSynchronizeInstance.Native != null )
          action( m_singleSynchronizeInstance.Native );
        return;
      }

      foreach ( var wheel in m_wheels )
        if ( wheel.Native != null )
          action( wheel.Native );
    }

    [NonSerialized]
    private List<DeformableTerrainWheel> m_wheels = new List<DeformableTerrainWheel>();

    [NonSerialized]
    private DeformableTerrainWheel m_singleSynchronizeInstance = null;
  }
}
