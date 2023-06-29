using System;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain-material" )]
  public class DeformableTerrainMaterial : ScriptAsset
  {
    /// <summary>
    /// Default path to official terrain materials library released
    /// with AGX Dynamics.
    /// </summary>
    [HideInInspector]
    public static string DefaultTerrainMaterialsPath
    {
      get
      {
        if ( s_defaultTerrainMaterialsPath == null ) {
          var terrainMaterialLibraryOptions = new string[]
          {
            "data/TerrainMaterials",
            "data/MaterialLibrary/TerrainMaterials"
          };

          foreach ( var materialLibraryOption in terrainMaterialLibraryOptions ) {
            var fileTest = agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).find( $"{materialLibraryOption}/dirt_1.json" );
            if ( !string.IsNullOrEmpty( fileTest ) ) {
              s_defaultTerrainMaterialsPath = materialLibraryOption;
              break;
            }
          }

          if ( s_defaultTerrainMaterialsPath == null )
            s_defaultTerrainMaterialsPath = terrainMaterialLibraryOptions[ 0 ];
        }

        return s_defaultTerrainMaterialsPath;
      }
    }

    /// <summary>
    /// Finds available material presets in the current material directory.
    /// </summary>
    /// <returns>Array of material preset names.</returns>
    public static string[] GetAvailablePresets()
    {
      return agxTerrain.TerrainMaterialLibrary.getAvailableLibraryMaterials( DefaultTerrainMaterialsPath ).ToArray();
    }

    /// <summary>
    /// Create native instance given preset name.
    /// </summary>
    /// <param name="presetName">Name of the material located in the TerrainMaterials directory.</param>
    /// <returns>Terrain material instance, valid if string.IsNullOrEmpty( instance.getLastError() ).</returns>
    public static agxTerrain.TerrainMaterial CreateNative( string presetName )
    {
      var terrainMaterial = new agxTerrain.TerrainMaterial();
      if ( !agxTerrain.TerrainMaterialLibrary.loadMaterialProfile( presetName,
                                                                   terrainMaterial,
                                                                   DefaultTerrainMaterialsPath ) ) {
        var errorMessage = string.Empty;
        if ( Array.IndexOf( GetAvailablePresets(), presetName ) < 0 )
          errorMessage = $"AGXUnity.Model.DeformableTerrainMaterial: Unable to find material name {presetName} in the library.";
        else
          errorMessage = terrainMaterial.getLastError();
        Debug.LogWarning( $"AGXUnity.Model.DeformableTerrainMaterial: Unable to load preset {presetName}: {errorMessage}" );
      }

      return terrainMaterial;
    }

    /// <summary>
    /// Native instance of the terrain material.
    /// </summary>
    [HideInInspector]
    public agxTerrain.TerrainMaterial Native { get; private set; }

    /// <summary>
    /// Name of material preset in the TerrainMaterials directory.
    /// </summary>
    [SerializeField]
    private string m_presetName = "dirt_1";

    /// <summary>
    /// Preset name to be found in the terrain materials directory.
    /// </summary>
    [HideInInspector]
    public string PresetName
    {
      get
      {
        return m_presetName;
      }
    }

    #region Bulk Properties
    [SerializeField]
    private float m_density = 1.4E3f;

    /// <summary>
    /// Bulk density (default unit: kg/m^3) of the bulk material. This translates
    /// to the specific density of the solid soil material and the bulk density
    /// of the dynamic soil, i.e, Soil Particles.
    /// Default: 1.4E3 kg/m^3.
    /// </summary>
    [InspectorGroupBegin( Name = "Bulk Properties" )]
    [ClampAboveZeroInInspector]
    public float Density
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getDensity() ) :
                 m_density;
      }
      set
      {
        m_density = value;
        if ( Native != null )
          Native.getBulkProperties().setDensity( m_density );
      }
    }

    [SerializeField]
    private float m_maximumDensity = float.PositiveInfinity;

    /// <summary>
    /// Maximum density (default unit: kg/m^3) of the bulk material.
    /// This is the upper limit for the soil compaction.
    /// Default: Infinity.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float MaximumDensity
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getMaximumDensity() ) :
                 m_maximumDensity;
      }
      set
      {
        m_maximumDensity = value;
        if ( Native != null )
          Native.getBulkProperties().setMaximumDensity( m_maximumDensity );
      }
    }

    [SerializeField]
    private float m_youngsModulus = 1.0E7f;

    /// <summary>
    /// Young's modulus (default unit: Pa) of the bulk material.
    /// This affects the Young's Modulus parameter of the contact materials.
    /// Default: 1.0E7
    /// </summary>
    [ClampAboveZeroInInspector]
    public float YoungsModulus
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getYoungsModulus() ) :
                 m_youngsModulus;
      }
      set
      {
        m_youngsModulus = value;
        if ( Native != null )
          Native.getBulkProperties().setYoungsModulus( m_youngsModulus );
      }
    }

    [SerializeField]
    private float m_poissonsRatio = 0.1f;

    /// <summary>
    /// Poisson's ratio of the bulk material - affects the soil penetration force calculations.
    /// Default: 0.1
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float PoissonsRatio
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getPoissonsRatio() ) :
                 m_poissonsRatio;
      }
      set
      {
        m_poissonsRatio = value;
        if ( Native != null )
          Native.getBulkProperties().setPoissonsRatio( m_poissonsRatio );
      }
    }

    [SerializeField]
    private float m_frictionAngle = 45.0f;

    /// <summary>
    /// Internal friction angle (degrees) of the bulk material. This affects
    /// the shape of the active zones during excavations and also the
    /// angle of repose in a 1 to 1 ration for the avalanching algorithm.
    /// Default: 45 degrees
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float FrictionAngle
    {
      get
      {
        return m_temporaryNative != null ?
                 Mathf.Rad2Deg * Convert.ToSingle( m_temporaryNative.getBulkProperties().getFrictionAngle() ) :
                 m_frictionAngle;
      }
      set
      {
        m_frictionAngle = value;
        if ( Native != null )
          Native.getBulkProperties().setFrictionAngle( Mathf.Deg2Rad * m_frictionAngle );
      }
    }

    [SerializeField]
    private float m_cohesion = 0.0f;

    /// <summary>
    /// Cohesion (default unit: Pa) of the bulk material.
    /// This translates to the adhesion parameter of the internal contact material.
    /// Default: 0.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float Cohesion
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getCohesion() ) :
                 m_cohesion;
      }
      set
      {
        m_cohesion = value;
        if ( Native != null )
          Native.getBulkProperties().setCohesion( m_cohesion );
      }
    }

    [SerializeField]
    private float m_dilatancyAngle = 15.0f;

    /// <summary>
    /// Dilatancy angle (degrees) of the bulk material - affects the soil penetration force calculations.
    /// Default: 15.0 degrees
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float DilatancyAngle
    {
      get
      {
        return m_temporaryNative != null ?
                 Mathf.Rad2Deg * Convert.ToSingle( m_temporaryNative.getBulkProperties().getDilatancyAngle() ) :
                 m_dilatancyAngle;
      }
      set
      {
        m_dilatancyAngle = value;
        if ( Native != null )
          Native.getBulkProperties().setDilatancyAngle( Mathf.Deg2Rad * m_dilatancyAngle );
      }
    }

    [SerializeField]
    private float m_swellFactor = 1.1f;

    /// <summary>
    /// Swell factor of the material, i.e how much the material will expand
    /// during excavation. The volume of the dynamic material created will
    /// increase according to the swell factor: volume dynamic = volume excavated * swellFactor
    /// Default: 1.1
    /// </summary>
    [ClampAboveZeroInInspector]
    public float SwellFactor
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getBulkProperties().getSwellFactor() ) :
                 m_swellFactor;
      }
      set
      {
        m_swellFactor = value;
        if ( Native != null )
          Native.getBulkProperties().setSwellFactor( m_swellFactor );
      }
    }
    #endregion

    #region Compaction Properties
    [SerializeField]
    private float m_hardeningConstantKE = 1.0f;

    /// <summary>
    /// Set the hardening constant k_e of the bulk material, i.e, how Young's
    /// modulus should of the terrain contacts should scale with increasing/decreasing
    /// compaction. The formula for this is:
    ///     E_eff = E_0 * ( 1.0 + sign( compaction - 1.0 ) * k_e * ( abs( compaction - 1.0 ) ^ n_e ) )
    /// where 'E_eff' is the effective Young's Modulus, 'E_0' is the original Young's
    /// modulus, 'compaction' is the local compaction of the material, 'k_e' is and 'n_e'
    /// are hardening parameters that for a constant packing ratio has values 1.0 and 0.5 respectively.
    /// Default: 1.0
    /// </summary>
    [InspectorGroupBegin( Name = "Compaction Properties" )]
    [ClampAboveZeroInInspector( true )]
    public float HardeningConstantKE
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getHardeningConstantKE() ) :
                 m_hardeningConstantKE;
      }
      set
      {
        m_hardeningConstantKE = value;
        if ( Native != null )
          Native.getCompactionProperties().setHardeningConstantKE( m_hardeningConstantKE );
      }
    }

    [SerializeField]
    private float m_hardeningConstantNE = 0.5f;

    /// <summary>
    /// Set the hardening constant n_e of the bulk material, i.e, how Young's
    /// modulus should of the terrain contacts should scale with increasing/decreasing
    /// compaction. The formula for this is:
    ///     E_eff = E_0 * ( 1.0 + sign( compaction - 1.0 ) * k_e * ( abs( compaction - 1.0 ) ^ n_e ) )
    /// where 'E_eff' is the effective Young's Modulus, 'E_0' is the original Young's
    /// modulus, 'compaction' is the local compaction of the material, 'k_e' is and 'n_e'
    /// are hardening parameters that for a constant packing ratio has values 1.0 and 0.5 respectively.
    /// Default: 0.5
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float HardeningConstantNE
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getHardeningConstantNE() ) :
                 m_hardeningConstantNE;
      }
      set
      {
        m_hardeningConstantNE = value;
        if ( Native != null )
          Native.getCompactionProperties().setHardeningConstantNE( m_hardeningConstantNE );
      }
    }

    [SerializeField]
    private float m_preconsolidationStress = 9.8E4f;

    /// <summary>
    /// Set the stress at which the soil in the default state was compressed in,
    /// i.e, when it has nominal compaction 1.0. In order to compress the soil
    /// further, the applied stress on the soil has to exceed this.
    /// Default: 9.8E4
    /// </summary>
    [ClampAboveZeroInInspector]
    public float PreconsolidationStress
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getPreconsolidationStress() ) :
                 m_preconsolidationStress;
      }
      set
      {
        m_preconsolidationStress = value;
        if ( Native != null )
          Native.getCompactionProperties().setPreconsolidationStress( m_preconsolidationStress );
      }
    }

    [SerializeField]
    private float m_bankStatePhi = 0.6666667f;

    /// <summary>
    /// Set the phi0 value of the bank state soil. This is used in the
    /// compaction calculation where soil stress generates compacted soil.
    /// Note: See 'CompressionIndex' for an explanation of how phi0 is
    ///       used in compaction calculations.
    /// Default: 0.6666667
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float BankStatePhi
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getBankStatePhi() ) :
                 m_bankStatePhi;
      }
      set
      {
        m_bankStatePhi = value;
        if ( Native != null )
          Native.getCompactionProperties().setBankStatePhi( m_bankStatePhi );
      }
    }

    [SerializeField]
    private float m_compressionIndex = 0.1f;

    /// <summary>
    /// Sets the compression index for the soil, which is the constant that determines how fast
    /// the soil should compress given increased surface stress. The formula for computing the compaction
    /// curve is given by the following expression:
    ///     rho_new = rho_old * ( 1.0 / ( 1.0 - phi0 * C * ln( stress / preconsolidationStress ) ) )
    /// where 'C' is the compression index, 'rho_new' is the new density of the material, 'rho_old' is the old density
    /// before compaction, 'preconsolidationStress' is stress used to initialize the soil in the bank sate, 'stress' is the
    /// applied compaction stress and phi zero is derived from the initial void ratio e0 ( Default phi0: 0.5 ) in the soil:
    ///     phi0 = e0 / ( 1.0 + e0 )
    /// Default: 0.1
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float CompressionIndex
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getCompressionIndex() ) :
                 m_compressionIndex;
      }
      set
      {
        m_compressionIndex = value;
        if ( Native != null )
          Native.getCompactionProperties().setCompressionIndex( m_compressionIndex );
      }
    }

    [SerializeField]
    private float m_compactionTimeRelaxationConstant = 0.05f;

    /// <summary>
    /// Set time relaxation for compaction. The factor is used to cap
    /// the change in density during the contact according to:
    ///     change in density during time step = change in density from compaction * timeFactor
    /// where:
    ///     timeFactor = 1.0 - exp(contactTime/tau)
    /// Default: 0.05
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float CompactionTimeRelaxationConstant
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getCompactionTimeRelaxationConstant() ) :
                 m_compactionTimeRelaxationConstant;
      }
      set
      {
        m_compactionTimeRelaxationConstant = value;
        if ( Native != null )
          Native.getCompactionProperties().setCompactionTimeRelaxationConstant( m_compactionTimeRelaxationConstant );
      }
    }

    [SerializeField]
    private float m_stressCutOffFraction = 0.01f;

    /// <summary>
    /// Set the fraction of the surface stress that should serve as
    /// a cutoff value from when the stress propagation from the surface
    /// downward into the soil should stop.
    /// Default: 0.01
    /// </summary>
    [ ClampAboveZeroInInspector( true )]
    public float StressCutOffFraction
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getStressCutOffFraction() ) :
                 m_stressCutOffFraction;
      }
      set
      {
        m_stressCutOffFraction = value;
        if ( Native != null )
          Native.getCompactionProperties().setStressCutOffFraction( m_stressCutOffFraction );
      }
    }

    [SerializeField]
    private float m_angleOfReposeCompactionRate = 1.0f;

    /// <summary>
    /// Set how the compaction should increase the angle of repose. The tan
    /// of the angle of repose is increased by the following factor: The applied
    /// angle of repose of the material is active when the soil is in loose
    /// compaction, i.e, has compaction 1.0 / swellFactor:
    ///     m  = 2.0 ^ ( angleOfReposeCompactionRate * ( compaction - (1.0/swellFactor) ) )
    /// Default: 1.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AngleOfReposeCompactionRate
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getCompactionProperties().getAngleOfReposeCompactionRate() ) :
                 m_angleOfReposeCompactionRate;
      }
      set
      {
        m_angleOfReposeCompactionRate = value;
        if ( Native != null )
          Native.getCompactionProperties().setAngleOfReposeCompactionRate( m_angleOfReposeCompactionRate );
      }
    }
    #endregion

    #region Particle <-> Particle Properties
    [SerializeField]
    private float m_particleYoungsModulus = 1.0E9f;

    /// <summary>
    /// Particle vs. particle Young's modulus (default unit: Pa).
    /// Default: 1.0E9
    /// </summary>
    [InspectorGroupBegin( Name = "Particle <-> Particle Properties" )]
    [ClampAboveZeroInInspector]
    public float ParticleYoungsModulus
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleYoungsModulus() ) :
                 m_particleYoungsModulus;
      }
      set
      {
        m_particleYoungsModulus = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleYoungsModulus( m_particleYoungsModulus );
      }
    }

    [SerializeField]
    private float m_particleRestitution = 0.5f;

    /// <summary>
    /// Particle vs. particle restitution coefficient.
    /// Default: 0.5
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleRestitution
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleRestitution() ) :
                 m_particleRestitution;
      }
      set
      {
        m_particleRestitution = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleRestitution( m_particleRestitution );
      }
    }

    [SerializeField]
    private float m_particleSurfaceFriction = 0.4f;

    /// <summary>
    /// Particle vs. particle surface friction coefficient.
    /// Default: 0.4
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleSurfaceFriction
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleSurfaceFriction() ) :
                 m_particleSurfaceFriction;
      }
      set
      {
        m_particleSurfaceFriction = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleSurfaceFriction( m_particleSurfaceFriction );
      }
    }

    [SerializeField]
    private float m_particleRollingResistance = 0.3f;

    /// <summary>
    /// Particle vs. particle rolling resistance coefficient.
    /// Default: 0.3
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleRollingResistance
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleRollingResistance() ) :
                 m_particleRollingResistance;
      }
      set
      {
        m_particleRollingResistance = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleRollingResistance( m_particleRollingResistance );
      }
    }

    [SerializeField]
    private float m_particleCohesion = 0.0f;

    /// <summary>
    /// Particle vs. particle cohesion (default unit: Pa).
    /// Default: 0.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleCohesion
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleCohesion() ) :
                 m_particleCohesion;
      }
      set
      {
        m_particleCohesion = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleCohesion( m_particleCohesion );
      }
    }
    #endregion

    #region Particle <-> Terrain Properties
    [SerializeField]
    private float m_particleTerrainYoungsModulus = 1.0E8f;

    /// <summary>
    /// Particle vs. terrain Young's modulus (default unit: Pa).
    /// Default: 1.0E8
    /// </summary>
    [InspectorGroupBegin( Name = "Particle <-> Terrain Properties" )]
    [ClampAboveZeroInInspector]
    public float ParticleTerrainYoungsModulus
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleTerrainYoungsModulus() ) :
                 m_particleTerrainYoungsModulus;
      }
      set
      {
        m_particleTerrainYoungsModulus = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleTerrainYoungsModulus( m_particleTerrainYoungsModulus );
      }
    }

    [SerializeField]
    private float m_particleTerrainSurfaceFriction = 0.8f;

    /// <summary>
    /// Particle vs. terrain surface friction coefficient.
    /// Default: 0.8
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleTerrainSurfaceFriction
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleTerrainSurfaceFriction() ) :
                 m_particleTerrainSurfaceFriction;
      }
      set
      {
        m_particleTerrainSurfaceFriction = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleTerrainSurfaceFriction( m_particleTerrainSurfaceFriction );
      }
    }

    [SerializeField]
    private float m_particleTerrainRestitution = 0.0f;

    /// <summary>
    /// Particle vs. terrain restitution coefficient.
    /// Default: 0.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleTerrainRestitution
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleTerrainRestitution() ) :
                 m_particleTerrainRestitution;
      }
      set
      {
        m_particleTerrainRestitution = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleTerrainRestitution( m_particleTerrainRestitution );
      }
    }

    [SerializeField]
    private float m_particleTerrainRollingResistance = 0.3f;

    /// <summary>
    /// Particle vs. terrain rolling resistance coefficient.
    /// Default: 0.3
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleTerrainRollingResistance
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleTerrainRollingResistance() ) :
                 m_particleTerrainRollingResistance;
      }
      set
      {
        m_particleTerrainRollingResistance = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleTerrainRollingResistance( m_particleTerrainRollingResistance );
      }
    }

    [SerializeField]
    private float m_particleTerrainCohesion = 0.0f;

    /// <summary>
    /// Particle vs. terrain cohesion (default unit: Pa).
    /// Default: 0.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ParticleTerrainCohesion
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getParticleProperties().getParticleTerrainCohesion() ) :
                 m_particleTerrainCohesion;
      }
      set
      {
        m_particleTerrainCohesion = value;
        if ( Native != null )
          Native.getParticleProperties().setParticleTerrainCohesion( m_particleTerrainCohesion );
      }
    }
    #endregion

    #region Excavation Contact Properties
    [SerializeField]
    private float m_maximumContactDepth = 1.0f;

    /// <summary>
    /// Set the maximum depth (m) of a soil aggregate <-> terrain contact.
    /// This increases when the separation direction of the excavation
    /// intersects the contact plane, causing virtual soil compression
    /// between soil aggregates and the terrain.
    /// Default: 1.0
    /// </summary>
    [InspectorGroupBegin( Name = "Excavation Contact Properties" )]
    [ClampAboveZeroInInspector( true )]
    public float MaximumContactDepth
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getMaximumContactDepth() ) :
                 m_maximumContactDepth;
      }
      set
      {
        m_maximumContactDepth = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setMaximumContactDepth( m_maximumContactDepth );
      }
    }

    [SerializeField]
    private float m_depthDecayFactor = 2.0f;

    /// <summary>
    /// Set the depth decay factor of a soil aggregate <-> terrain contact.
    /// This determines how rapidly the stored depth in a terrain <-> aggregate
    /// contact will decay during separation when the active zone moves away
    /// from the soil aggregate <-> terrain contact plane.
    /// Default: 2.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float DepthDecayFactor
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getDepthDecayFactor() ) :
                 m_depthDecayFactor;
      }
      set
      {
        m_depthDecayFactor = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setDepthDecayFactor( m_depthDecayFactor );
      }
    }

    [SerializeField]
    private float m_depthIncreaseFactor = 1.0f;

    /// <summary>
    /// Set the depth increase factor of a soil aggregate <-> terrain contact.
    /// This governs how fast the depth should increase when the separation
    /// direction of the excavation intersects the contact plane, causing
    /// virtual soil compression between soil aggregates and the terrain.
    /// Default: 1.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float DepthIncreaseFactor
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getDepthIncreaseFactor() ) :
                 m_depthIncreaseFactor;
      }
      set
      {
        m_depthIncreaseFactor = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setDepthIncreaseFactor( m_depthIncreaseFactor );
      }
    }

    [SerializeField]
    private float m_maximumAggregateNormalForce = float.PositiveInfinity;

    /// <summary>
    /// Set the maximum force that the soil aggregate<-> terrain contacts
    /// are allowed to have. Default maximum values are determined by the
    /// soil mechanics properties of the terrain.
    /// Default: Infinity
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float MaximumAggregateNormalForce
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getMaximumAggregateNormalForce() ) :
                 m_maximumAggregateNormalForce;
      }
      set
      {
        m_maximumAggregateNormalForce = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setMaximumAggregateNormalForce( m_maximumAggregateNormalForce );
      }
    }

    [SerializeField]
    private float m_aggregateStiffnessMultiplier = 1.0f;

    /// <summary>
    /// The contact stiffness multiplier for the generated contacts between
    /// the soil aggregates and terrains for excavation and deformation. The
    /// final Young's modulus value that will be used in the contact material
    /// thus becomes:
    ///   bulkYoungsModulus * stiffnessMultiplier
    /// </summary>
    [ClampAboveZeroInInspector]
    public float AggregateStiffnessMultiplier
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getAggregateStiffnessMultiplier() ) :
                 m_aggregateStiffnessMultiplier;
      }
      set
      {
        m_aggregateStiffnessMultiplier = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setAggregateStiffnessMultiplier( m_aggregateStiffnessMultiplier );
      }
    }

    [SerializeField]
    private float m_excavationStiffnessMultiplier = 1.0f;

    /// <summary>
    /// The contact stiffness multiplier for the generated contacts between
    /// the soil aggregates and shovels in primary excavation. The final
    /// Young's modulus value that will be used in the contact material
    /// thus becomes:
    ///   bulkYoungsModulus * stiffnessMultiplier
    /// </summary>
    [ClampAboveZeroInInspector]
    public float ExcavationStiffnessMultiplier
    {
      get
      {
        return m_temporaryNative != null ?
                 Convert.ToSingle( m_temporaryNative.getExcavationContactProperties().getExcavationStiffnessMultiplier() ) :
                 m_excavationStiffnessMultiplier;
      }
      set
      {
        m_excavationStiffnessMultiplier = value;
        if ( Native != null )
          Native.getExcavationContactProperties().setExcavationStiffnessMultiplier( m_excavationStiffnessMultiplier );
      }
    }
    #endregion

    /// <summary>
    /// Assign new preset name without updating any values.
    /// </summary>
    /// <param name="presetName">New material preset name.</param>
    public void SetPresetName( string presetName )
    {
      m_presetName = presetName;
    }

    /// <summary>
    /// Set preset name and update all values given <paramref name="presetName"/>.
    /// </summary>
    /// <param name="presetName">New preset name.</param>
    public void SetPresetNameAndUpdateValues( string presetName )
    {
      SetPresetName( presetName );
      ResetToPresetDefault();
    }
    
    /// <summary>
    /// Reset values to default given current preset.
    /// </summary>
    public void ResetToPresetDefault()
    {
      m_temporaryNative = CreateNative( m_presetName );
      Utils.PropertySynchronizer.SynchronizeGetToSet( this );
      m_temporaryNative.Dispose();
      m_temporaryNative = null;
    }

    public override void Destroy()
    {
      Native = null;
    }

    protected override void Construct()
    {
      // Setting values given default preset.
      ResetToPresetDefault();
    }

    protected override bool Initialize()
    {
      Native = new agxTerrain.TerrainMaterial( name );
      return true;
    }

    private agxTerrain.TerrainMaterial m_temporaryNative = null;

    [NonSerialized]
    private static string s_defaultTerrainMaterialsPath = null;
  }
}
