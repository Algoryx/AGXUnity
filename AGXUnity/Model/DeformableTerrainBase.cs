using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain" )]
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
    abstract public void RemoveInvalidShovels( bool removeDisabled = false, bool warn = false );

    /// <summary>
    /// Converts any part of the terrain that overlaps the provided shape into dynamic mass
    /// </summary>
    /// <param name="failureVolume">The shape in which to convert the terrain into dynamic mass</param>
    abstract public void ConvertToDynamicMassInShape( Collide.Shape failureVolume );

    /// <summary>
    /// Sets the heights in the terrain starting at the specified terrain indices using Unity's indexing convention.
    /// This function is meant to mimic <see cref="UnityEngine.TerrainData.SetHeights"/>.
    /// The width and height of the terrain patch which is set is inferred from the size of the provided heights array.
    /// </summary>
    /// <param name="xstart">The x-index at which to start setting the heights.</param>
    /// <param name="ystart">The y-index at which to start setting the heights.</param>
    /// <param name="heights">The heights to write into the terrain.</param>
    abstract public void SetHeights( int xstart, int ystart, float[,] heights );

    /// <summary>
    /// Sets the height in the terrain at the specified terrain index using Unity's indexing convention.
    /// This function is meant to mimic <see cref="UnityEngine.TerrainData.SetHeights"/>.
    /// </summary>
    /// <param name="x">The x-index at which to set the height.</param>
    /// <param name="y">The y-index at which to set the height.</param>
    /// <param name="height">The height to write into the terrain.</param>
    abstract public void SetHeight( int x, int y, float height );

    /// <summary>
    /// Gets the heights in the terrain patch starting at the specified terrain indices using Unity's indexing convention
    /// and covering the specified width and height.
    /// This function is meant to mimic <see cref="UnityEngine.TerrainData.GetHeights"/>.
    /// </summary>
    /// <param name="xstart">The x-index at which to start getting the heights.</param>
    /// <param name="ystart">The y-index at which to start getting the heights.</param>
    /// <param name="width">The width of the region for which to get the heights.</param>
    /// <param name="height">The height of the region for which to get the heights.</param>
    /// <returns>A 2D array with the specified width and height containing the heights of the terrain in the specified patch.</returns>
    abstract public float[,] GetHeights( int xstart, int ystart, int width, int height );

    /// <summary>
    /// Gets the heights in the terrain patch starting at the specified terrain index using Unity's indexing convention.
    /// This function is meant to mimic <see cref="UnityEngine.TerrainData.GetHeight"/>.
    /// </summary>
    /// <param name="x">The x-index at which to get the height.</param>
    /// <param name="y">The y-index at which to get the height.</param>
    /// <returns>The height of the terrain at the specified index.</returns>
    abstract public float GetHeight( int x, int y );

    abstract protected bool IsNativeNull();
    abstract protected void SetShapeMaterial( agx.Material material, agxTerrain.Terrain.MaterialType type );
    abstract protected void SetTerrainMaterial( agxTerrain.TerrainMaterial material );
    abstract protected void SetEnable( bool enable );
  }
}
