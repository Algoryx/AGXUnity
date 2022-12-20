using UnityEngine;

namespace AGXUnity.Model
{
  public abstract class DeformableTerrainBase : ScriptComponent
  {

    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Surface shape material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    [HideInInspector]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( !IsNativeNull() ) {
          if ( m_material != null && m_material.Native == null )
            m_material.GetInitialized<ShapeMaterial>();
          if ( m_material != null )
            SetShapeMaterial( m_material.Native, agxTerrain.Terrain.MaterialType.TERRAIN );

          // TODO: When m_material is null here it means "use default" but
          //       it's currently not possible to understand which parameters
          //       that has been set in e.g., Terrain::loadLibraryMaterial.
        }
      }
    }

    [SerializeField]
    private ShapeMaterial m_particleMaterial = null;

    /// <summary>
    /// Particle shape material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    [HideInInspector]
    public ShapeMaterial ParticleMaterial
    {
      get { return m_particleMaterial; }
      set
      {
        m_particleMaterial = value;
        if ( !IsNativeNull() ) {
          if ( m_particleMaterial != null && m_particleMaterial.Native == null )
            m_particleMaterial.GetInitialized<ShapeMaterial>();
          if ( m_particleMaterial != null )
            SetShapeMaterial( m_particleMaterial.Native, agxTerrain.Terrain.MaterialType.PARTICLE );

          // TODO: When m_material is null here it means "use default" but
          //       it's currently not possible to understand which parameters
          //       that has been set in e.g., Terrain::loadLibraryMaterial.
        }
      }
    }

    [SerializeField]
    private DeformableTerrainMaterial m_terrainMaterial = null;

    /// <summary>
    /// Terrain material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainMaterial TerrainMaterial
    {
      get { return m_terrainMaterial; }
      set
      {
        m_terrainMaterial = value;

        if ( !IsNativeNull() ) {
          if ( m_terrainMaterial != null )
            SetTerrainMaterial( m_terrainMaterial.GetInitialized<DeformableTerrainMaterial>().Native );
          else
            SetTerrainMaterial( DeformableTerrainMaterial.CreateNative( "dirt_1" ) );

        }
      }
    }

    [SerializeField]
    private DeformableTerrainProperties m_properties = null;

    /// <summary>
    /// Terrain properties associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( !IsNativeNull() && m_properties != null )
          m_properties.Unregister( this );

        m_properties = value;

        if ( !IsNativeNull() && m_properties != null )
          m_properties.Register( this );
      }
    }

    [SerializeField]
    private float m_maximumDepth = 20.0f;

    /// <summary>
    /// Maximum depth, it's not possible to dig deeper than this value.
    /// This game object will be moved down MaximumDepth and MaximumDepth
    /// will be added to the heights.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector( true )]
    public float MaximumDepth
    {
      get { return m_maximumDepth; }
      set
      {
        if ( !IsNativeNull() ) {
          Debug.LogWarning( "DeformableTerrainBase MaximumDepth: Value is used during initialization" +
                            " and cannot be changed when the terrain has been initialized.", this );
          return;
        }
        m_maximumDepth = value;
      }
    }

    /// <summary>
    /// Size in units which each heightmap texel represent
    /// </summary>
    abstract public float ElementSize { get; }

    /// <summary>
    /// Shovels associated to this terrain.
    /// </summary>
    [HideInInspector]
    abstract public DeformableTerrainShovel[] Shovels { get; }

    protected override void OnEnable()
    {
      SetEnable( true );
    }

    protected override void OnDisable()
    {
      SetEnable( false );
    }

    /// <summary>
    /// Returns an array containing the soil particles used by this terrain
    /// </summary>
    abstract public agx.GranularBodyPtrArray GetParticles();

    /// <summary>
    /// Returns the TerrainProperties used by this terrain instance
    /// </summary>
    abstract public agxTerrain.TerrainProperties GetProperties();

    /// <summary>
    /// Returns the soil simulation interface used by this terrain instance
    /// </summary>
    abstract public agxTerrain.SoilSimulationInterface GetSoilSimulationInterface();

    /// <summary>
    /// Callback which should be called when the TerrainProperties of this terrain is updated
    /// </summary>
    virtual public void OnPropertiesUpdated() { }

    /// <summary>
    /// Associate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to add.</param>
    /// <returns>True if added, false if null or already added.</returns>
    abstract public bool Add( DeformableTerrainShovel shovel );

    /// <summary>
    /// Disassociate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    abstract public bool Remove( DeformableTerrainShovel shovel );

    /// <summary>
    /// Find if shovel has been associated to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to check.</param>
    /// <returns>True if associated, otherwise false.</returns>
    abstract public bool Contains( DeformableTerrainShovel shovel );

    /// <summary>
    /// Verifies so that all added shovels still exists. Shovels that
    /// has been deleted are removed.
    /// </summary>
    abstract public void RemoveInvalidShovels();

    abstract protected bool IsNativeNull();
    abstract protected void SetShapeMaterial( agx.Material material, agxTerrain.Terrain.MaterialType type );
    abstract protected void SetTerrainMaterial( agxTerrain.TerrainMaterial material );
    abstract protected void SetEnable( bool enable );
  }
}
