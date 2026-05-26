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

    [SerializeField]
    private float m_slipRatioVxThreshold = 0.01f * Mathf.Rad2Deg;

    /// <summary>
    /// Longitudinal velocity threshold for the slip-ratio dead-band (degrees/s angular equivalent).
    /// The slip ratio is clamped to zero when both the longitudinal and rotational speeds are
    /// below their respective thresholds.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
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
    private bool m_enableComputeRearAngleFromFrontAngle = false;

    /// <summary>
    /// When enabled, the rear contact angle theta_r is derived from the front contact angle
    /// theta_f using an empirical slip-dependent relation rather than from wheel-terrain geometry.
    /// </summary>
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
      Native.getTerrainWheelSettings().setSlipRatioVxAngularEquivalentThreshold( Mathf.Deg2Rad * m_slipRatioVxThreshold );
      Native.getTerrainWheelSettings().setSlipRatioOmegaYThreshold( Mathf.Deg2Rad * m_slipRatioOmegaYRThreshold );
      Native.getTerrainWheelSettings().setSlipRatioSmoothingAngularSpeed( Mathf.Deg2Rad * m_slipRatioSmoothingSpeed );
      Native.getTerrainWheelSettings().setEnableComputeRearAngleFromFrontAngle( m_enableComputeRearAngleFromFrontAngle );

      GetSimulation().add( Native );

      return true;
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
