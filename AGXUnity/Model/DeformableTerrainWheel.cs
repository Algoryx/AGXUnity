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

    [SerializeField]
    private bool m_enableTerrainDeformation = true;

    /// <summary>
    /// Determines whether this terrain wheel deforms the terrain it is in contact with.
    /// </summary>
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
    private float m_slipRatioVxThreshold = 0.01f;

    /// <summary>
    /// Longitudinal velocity threshold for the slip-ratio dead-band (rad/s angular equivalent).
    /// The slip ratio is clamped to zero when both the longitudinal and rotational speeds are
    /// below their respective thresholds.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float SlipRatioVxThreshold
    {
      get
      {
        return Native != null ?
               (float)Native.getTerrainWheelSettings().getSlipRatioVxAngularEquivalentThreshold() :
               m_slipRatioVxThreshold;
      }
      set
      {
        m_slipRatioVxThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioVxAngularEquivalentThreshold( m_slipRatioVxThreshold );
      }
    }

    [SerializeField]
    private float m_slipRatioOmegaYRThreshold = 0.01f;

    /// <summary>
    /// Rotational speed threshold for the slip-ratio dead-band (rad/s).
    /// Corresponds to |omegaY| in the slip-ratio logic.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float SlipRatioOmegaYRThreshold
    {
      get
      {
        return Native != null ?
               (float)Native.getTerrainWheelSettings().getSlipRatioOmegaYThreshold() :
               m_slipRatioOmegaYRThreshold;
      }
      set
      {
        m_slipRatioOmegaYRThreshold = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioOmegaYThreshold( m_slipRatioOmegaYRThreshold );
      }
    }

    [SerializeField]
    private float m_slipRatioSmoothingSpeed = 0.0001f;

    /// <summary>
    /// Minimum angular speed used to smooth the slip-ratio computation near standstill (rad/s).
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float SlipRatioSmoothingSpeed
    {
      get
      {
        return Native != null ?
               (float)Native.getTerrainWheelSettings().getSlipRatioSmoothingAngularSpeed() :
               m_slipRatioSmoothingSpeed;
      }
      set
      {
        m_slipRatioSmoothingSpeed = value;
        if ( Native != null )
          Native.getTerrainWheelSettings().setSlipRatioSmoothingAngularSpeed( m_slipRatioSmoothingSpeed );
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

      Native.getWheelDeformationProperties().setEnableDeformation( m_enableTerrainDeformation );
      Native.getWheelDeformationProperties().setEnableDisplacement( m_enableTerrainDisplacement );
      Native.getTerrainWheelSettings().setSlipRatioVxAngularEquivalentThreshold( m_slipRatioVxThreshold );
      Native.getTerrainWheelSettings().setSlipRatioOmegaYThreshold( m_slipRatioOmegaYRThreshold );
      Native.getTerrainWheelSettings().setSlipRatioSmoothingAngularSpeed( m_slipRatioSmoothingSpeed );
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
