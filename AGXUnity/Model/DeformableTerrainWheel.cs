using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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

    [SerializeField]
    private DeformableTerrainWheelSettings m_settings = null;

    [AllowRecursiveEditing]
    public DeformableTerrainWheelSettings Settings
    {
      get { return m_settings; }
      set
      {
        if ( Native != null && m_settings != null && m_settings != value )
          m_settings.Unregister( this );

        m_settings = value;

        if ( Native != null && m_settings != null )
          m_settings.Register( this );
      }
    }

    /// <summary>
    /// Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model.
    /// NB: if using multiple shape materials on the terrain this is not reliable!
    /// </summary>
    [SerializeField]
    [FormerlySerializedAs( "WarnIfNotUsingCorrectForceModel" )]
    private bool m_warnIfNotUsingCorrectForceModel = false;

    [Tooltip( "Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model." )]
    public bool WarnIfNotUsingCorrectForceModel
    {
      get { return m_warnIfNotUsingCorrectForceModel; }
      set { m_warnIfNotUsingCorrectForceModel = value; }
    }

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

      // TODO - this is needed because of a bug in agx. Remove this is that is fixed.
      var shapeMaterial = cylinder.getGeometry().getMaterial();
      if ( shapeMaterial == null ) {
        Debug.LogWarning( "Unable to initialize Cylinder shape for DeformableTerrainWheel - ShapeMaterial needs to be set!", this );
        return false;
      }

      Native = new agxTerrain.TerrainWheel( cylinder );

      if ( Settings == null )
        Settings = CreateSettingsFromLegacyValues();

      GetSimulation().add( Native );

      return true;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
    }

    protected override void OnDisable()
    {
      base.OnDisable();
    }

    private void LateUpdate()
    {
      if ( !WarnIfNotUsingCorrectForceModel || Native?.getActiveTerrain() == null )
        return;

      if ( !ActiveContactMaterialUsesTerrainWheelForceModel )
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
      if ( Settings != null )
        Settings.Unregister( this );

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

    private DeformableTerrainWheelSettings CreateSettingsFromLegacyValues()
    {
      var settings = ScriptAsset.Create<DeformableTerrainWheelSettings>();
      settings.name = "[Temporary]Wheel Settings";
      settings.EnableTerrainDeformation = m_enableTerrainDeformation;
      settings.EnableTerrainDisplacement = m_enableTerrainDisplacement;
      settings.SlipDependenceBulldozing = m_slipDependenceBulldozing;
      settings.SlipDependenceSlipDisplacement = m_slipDependenceSlipDisplacement;
      settings.ForwardDisplacementWeight = m_forwardDisplacementWeight;
      settings.BulldozeDisplacementAmountFactor = m_bulldozeDisplacementAmountFactor;
      settings.LateralDisplacementDistScaling = m_lateralDisplacementDistScaling;
      settings.ForwardDisplacementDistScaling = m_forwardDisplacementDistScaling;
      settings.BackwardDisplacementDistScaling = m_backwardDisplacementDistScaling;
      settings.AngularIntegrationStep = m_angularIntegrationStep;
      settings.PressureSinkageModel = m_pressureSinkageModel;
      settings.EnableComputeRearAngleFromFrontAngle = m_enableComputeRearAngleFromFrontAngle;
      settings.EnableComputeMaximumNormalStressAngleFromFrontAngle = m_enableComputeMaximumNormalStressAngleFromFrontAngle;
      settings.RearAndFrontAngleMaxMagnitude = m_rearAndFrontAngleMaxMagnitude;
      settings.SlipRatioVxThreshold = m_slipRatioVxThreshold;
      settings.SlipRatioOmegaYRThreshold = m_slipRatioOmegaYRThreshold;
      settings.SlipRatioSmoothingSpeed = m_slipRatioSmoothingSpeed;
      settings.SlipRatioMaxMagnitude = m_slipRatioMaxMagnitude;
      settings.SlipRatioFallbackValue = m_slipRatioFallbackValue;
      settings.SlipAngleVxAngularEquivalentThreshold = m_slipAngleVxAngularEquivalentThreshold;
      settings.SlipAngleVyAngularEquivalentThreshold = m_slipAngleVyAngularEquivalentThreshold;
      settings.SlipAngleMaxMagnitude = m_slipAngleMaxMagnitude;
      settings.SlipAngleFallbackValue = m_slipAngleFallbackValue;
      settings.RollingModeVxAngularEquivalentThreshold = m_rollingModeVxAngularEquivalentThreshold;
      settings.RollingModeOmegaYThreshold = m_rollingModeOmegaYThreshold;
      return settings;
    }

    private RigidBody m_rb = null;

    #region Legacy Serialized Settings
    [SerializeField, HideInInspector]
    private bool m_enableTerrainDeformation = true;

    [SerializeField, HideInInspector]
    private bool m_enableTerrainDisplacement = true;

    [SerializeField, HideInInspector]
    private bool m_slipDependenceBulldozing = false;

    [SerializeField, HideInInspector]
    private bool m_slipDependenceSlipDisplacement = true;

    [SerializeField, HideInInspector]
    private float m_forwardDisplacementWeight = 0.5f;

    [SerializeField, HideInInspector]
    private float m_bulldozeDisplacementAmountFactor = 0.5f;

    [SerializeField, HideInInspector]
    private float m_lateralDisplacementDistScaling = 0.5f;

    [SerializeField, HideInInspector]
    private float m_forwardDisplacementDistScaling = 0.5f;

    [SerializeField, HideInInspector]
    private float m_backwardDisplacementDistScaling = 0.5f;

    [SerializeField, HideInInspector]
    private float m_angularIntegrationStep = 0.001f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private agxTerrain.TerrainWheelSettings.PressureSinkageModel m_pressureSinkageModel = agxTerrain.TerrainWheelSettings.PressureSinkageModel.BEKKER;

    [SerializeField, HideInInspector]
    private bool m_enableComputeRearAngleFromFrontAngle = false;

    [SerializeField, HideInInspector]
    private bool m_enableComputeMaximumNormalStressAngleFromFrontAngle = true;

    [SerializeField, HideInInspector]
    private float m_rearAndFrontAngleMaxMagnitude = 90.0f;

    [SerializeField, HideInInspector]
    private float m_slipRatioVxThreshold = 0.01f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_slipRatioOmegaYRThreshold = 0.01f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_slipRatioSmoothingSpeed = 0.0001f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_slipRatioMaxMagnitude = 1.0f;

    [SerializeField, HideInInspector]
    private float m_slipRatioFallbackValue = 0.1f;

    [SerializeField, HideInInspector]
    private float m_slipAngleVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_slipAngleVyAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_slipAngleMaxMagnitude = 45.0f;

    [SerializeField, HideInInspector]
    private float m_slipAngleFallbackValue = 4.5f;

    [SerializeField, HideInInspector]
    private float m_rollingModeVxAngularEquivalentThreshold = 0.017453293f * Mathf.Rad2Deg;

    [SerializeField, HideInInspector]
    private float m_rollingModeOmegaYThreshold = 0.017453293f * Mathf.Rad2Deg;
    #endregion
  }
}
