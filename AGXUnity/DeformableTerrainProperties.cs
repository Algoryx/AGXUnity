using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  public class DeformableTerrainProperties : ScriptAsset
  {
    [SerializeField]
    private float m_soilMergeSpeedThreshold = 0.05f;

    /// <summary>
    /// The maximum speed (m/s) threshold where soil particles
    /// are allowed to merge with the terrain.
    /// Default: 0.05
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float SoilMergeSpeedThreshold
    {
      get { return m_soilMergeSpeedThreshold; }
      set
      {
        m_soilMergeSpeedThreshold = value;
        Propagate( properties => properties.setSoilMergeSpeedThreshold( m_soilMergeSpeedThreshold ) );
      }
    }

    [SerializeField]
    private float m_soilParticleLifeTime = float.PositiveInfinity;

    /// <summary>
    /// Set the lifetime (seconds) of created soil particles in the
    /// terrain. The particle will be deleted after existing for the
    /// specified lifetime.
    /// Default: Infinity
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float SoilParticleLifeTime
    {
      get { return m_soilParticleLifeTime; }
      set
      {
        m_soilParticleLifeTime = value;
        Propagate( properties => properties.setSoilParticleLifeTime( m_soilParticleLifeTime ) );
      }
    }

    [SerializeField]
    private float m_avalancheDecayFraction = 0.1f;

    /// <summary>
    /// Set the fraction of the height difference that violates the
    /// angle of repose condition that will be transferred in each
    /// time step during avalanching.
    /// Default: 0.1
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AvalancheDecayFraction
    {
      get { return m_avalancheDecayFraction; }
      set
      {
        m_avalancheDecayFraction = value;
        Propagate( properties => properties.setAvalancheDecayFraction( m_avalancheDecayFraction ) );
      }
    }

    [SerializeField]
    private float m_avalancheMaxHeightGrowth = float.PositiveInfinity;

    /// <summary>
    /// Set the maximum allowed height (meters) transfer per time
    /// step due to avalanching.
    /// Default: Infinity
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AvalancheMaxHeightGrowth
    {
      get { return m_avalancheMaxHeightGrowth; }
      set
      {
        m_avalancheMaxHeightGrowth = value;
        Propagate( properties => properties.setAvalancheMaxHeightGrowth( m_avalancheMaxHeightGrowth ) );
      }
    }

    [SerializeField]
    private bool m_createParticlesEnabled = true;

    /// <summary>
    /// Set whether the terrain should create particles or not during
    /// shovel interactions.
    /// Default: enabled
    /// </summary>
    public bool CreateParticlesEnabled
    {
      get { return m_createParticlesEnabled; }
      set
      {
        m_createParticlesEnabled = value;
        Propagate( properties => properties.setCreateParticles( m_createParticlesEnabled ) );
      }
    }

    [SerializeField]
    private bool m_soilCompactionEnabled = true;

    /// <summary>
    /// Set whether or not to use the soil compaction calculations in
    /// the terrain.
    /// Default: enabled
    /// </summary>
    public bool SoilCompactionEnabled
    {
      get { return m_soilCompactionEnabled; }
      set
      {
        m_soilCompactionEnabled = value;
        Propagate( properties => properties.setEnableSoilCompaction( m_soilCompactionEnabled ) );
      }
    }

    [SerializeField]
    private bool m_deleteSoilParticlesOutsideBoundsEnabled = false;

    /// <summary>
    /// Set if terrain should delete soil particles outside of terrain bounds.
    /// Default: disabled
    /// </summary>
    public bool DeleteSoilParticlesOutsideBoundsEnabled
    {
      get { return m_deleteSoilParticlesOutsideBoundsEnabled; }
      set
      {
        m_deleteSoilParticlesOutsideBoundsEnabled = value;
        Propagate( properties => properties.setDeleteSoilParticlesOutsideBounds( m_deleteSoilParticlesOutsideBoundsEnabled ) );
      }
    }

    [SerializeField]
    private bool m_lockedBorderEnabled = false;

    /// <summary>
    /// Set whether to fixate the height of the borders in the terrain, i.e,
    /// the borders of the terrain are not allowed to change from excavation
    /// and avalanching.
    /// Default: disabled
    /// </summary>
    public bool LockedBorderEnabled
    {
      get { return m_lockedBorderEnabled; }
      set
      {
        m_lockedBorderEnabled = value;
        Propagate( properties => properties.setEnableLockedBorders( m_lockedBorderEnabled ) );
      }
    }

    [SerializeField]
    private bool m_avalanchingEnabled = true;

    /// <summary>
    /// Set whether to enable avalanching in the terrain.
    /// Default: enabled
    /// </summary>
    public bool AvalanchingEnabled
    {
      get { return m_avalanchingEnabled; }
      set
      {
        m_avalanchingEnabled = value;
        Propagate( properties => properties.setEnableAvalanching( m_avalanchingEnabled ) );
      }
    }

    [SerializeField]
    private bool m_createDynamicMassEnabled = true;

    /// <summary>
    /// Sets if dynamic mass should be created during excavation. Setting this
    /// to false will prevent the creation of fluid and soil particle mass
    /// during shovel excavation; thus only solid removal will be active.
    /// Default: enabled
    /// </summary>
    public bool CreateDynamicMassEnabled
    {
      get { return m_createDynamicMassEnabled; }
      set
      {
        m_createDynamicMassEnabled = value;
        Propagate( properties => properties.setEnableCreateDynamicMass( m_createDynamicMassEnabled ) );
      }
    }

    [SerializeField]
    private Vector3 m_soilAggregateLockComplianceTranslational = 1.0E-9f * Vector3.one;

    /// <summary>
    /// The translational compliance of the soil aggregate lock joint
    /// the local constraint dimensions to relax or increase the force
    /// feedback interaction from shovel - terrain interaction.
    /// Default: [1.0E-9 1.0E-9 1.0E-9]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector3 SoilAggregateLockComplianceTranslational
    {
      get { return m_soilAggregateLockComplianceTranslational; }
      set
      {
        m_soilAggregateLockComplianceTranslational = value;
        Propagate( properties =>
        {
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceTranslational.x, 0 );
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceTranslational.y, 1 );
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceTranslational.z, 2 );
        } );
      }
    }

    [SerializeField]
    private Vector3 m_soilAggregateLockComplianceRotational = 1.0E-6f * Vector3.one;

    /// <summary>
    /// The rotational compliance of the soil aggregate lock joint
    /// the local constraint dimensions to relax or increase the force
    /// feedback interaction from shovel - terrain interaction.
    /// Default: [1.0E-6 1.0E-6 1.0E-6]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector3 SoilAggregateLockComplianceRotational
    {
      get { return m_soilAggregateLockComplianceRotational; }
      set
      {
        m_soilAggregateLockComplianceRotational = value;
        Propagate( properties =>
        {
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceRotational.x, 3 );
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceRotational.y, 4 );
          properties.setSoilAggregateLockCompliance( m_soilAggregateLockComplianceRotational.z, 5 );
        } );
      }
    }

    [SerializeField]
    private float m_penetrationForceVelocityScaling = 0.0f;

    /// <summary>
    /// Set the penetration force velocity scaling constant. This will
    /// scale the penetration force with the shovel velocity squared
    /// in the cutting direction according to: ( 1.0 + C * v^2 )
    /// Default: 0.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float PenetrationForceVelocityScaling
    {
      get { return m_penetrationForceVelocityScaling; }
      set
      {
        m_penetrationForceVelocityScaling = value;
        Propagate( properties => properties.setPenetrationForceVelocityScaling( m_penetrationForceVelocityScaling ) );
      }
    }

    [SerializeField]
    private float m_maximumParticleActivationVolume = float.PositiveInfinity;

    /// <summary>
    /// Sets the maximum volume of active zone wedges that should wake particles.
    /// Default: Infinity
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float MaximumParticleActivationVolume
    {
      get { return m_maximumParticleActivationVolume; }
      set
      {
        m_maximumParticleActivationVolume = value;
        Propagate( properties => properties.setMaximumParticleActivationVolume( m_maximumParticleActivationVolume ) );
      }
    }

    public static DeformableTerrainProperties operator+( DeformableTerrainProperties self,
                                                         DeformableTerrain terrain )
    {
      if ( !self.m_terrains.Contains( terrain ) )
        self.m_terrains.Add( terrain );
      return self;
    }

    public static DeformableTerrainProperties operator-( DeformableTerrainProperties self,
                                                         DeformableTerrain terrain )
    {
      self.m_terrains.Remove( terrain );
      return self;
    }

    public override void Destroy()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    private void Propagate( Action<agxTerrain.TerrainProperties> action )
    {
      if ( action == null )
        return;

      foreach ( var terrain in m_terrains )
        if ( terrain.Native != null )
          action( terrain.Native.getProperties() );
    }

    private List<DeformableTerrain> m_terrains = new List<DeformableTerrain>();
  }
}
