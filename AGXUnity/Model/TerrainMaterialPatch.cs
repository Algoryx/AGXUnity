using AGXUnity.Collide;
using System.Linq;
using UnityEngine;
using UnityEngine.TerrainUtils;

namespace AGXUnity.Model
{
  public class TerrainMaterialPatch : ScriptComponent
  {
    /// <summary>
    /// The terrain which this patch belongs to
    /// </summary>
    [HideInInspector]
    public DeformableTerrainBase ParentTerrain => GetComponentInParent<DeformableTerrainBase>();

    [SerializeField]
    private DeformableTerrainMaterial m_terrainMaterial = null;

    /// <summary>
    /// Terrain material associated to this terrain patch.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainMaterial TerrainMaterial
    {
      get { return m_terrainMaterial; }
      set
      {
        if ( m_terrainMaterial == value )
          return;

        var matCount = ParentTerrain.MaterialPatches.Count(p => p.m_terrainMaterial == value );
        if ( matCount > 0 ) {
          Debug.LogError( "Another patch with the same material already exist in the terrain!" );
          return;
        }

        if ( (bool)ParentTerrain?.ReplaceTerrainMaterial( m_terrainMaterial, value ) )
          m_terrainMaterial = value;
      }
    }

    public void AddShape( Shape shape )
    {
      shape.enabled &= !DisableShapes;
      shape.Visual.GetComponent<MeshRenderer>().enabled &= !DisableVisuals;
      ParentTerrain?.AddTerrainMaterial( m_terrainMaterial, shape.GetInitialized<Shape>() );
    }


    [SerializeField]
    private ShapeMaterial m_materialHandle = null;

    /// <summary>
    /// Surface shape material associated to this terrain patch.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial MaterialHandle
    {
      get { return m_materialHandle; }
      set
      {
        m_materialHandle = value;
        ParentTerrain?.SetAssociatedMaterial( m_terrainMaterial, value );
      }
    }

    /// <summary>
    /// Whether to disable collision shapes used to define this patch during initialization.
    /// </summary>
    [field: SerializeField]
    public bool DisableShapes { get; set; } = true;

    /// <summary>
    /// Whether to disable visuals for shapes used to define this patch during initialization.
    /// </summary>
    [field: SerializeField]
    public bool DisableVisuals { get; set; } = true;

    // The shapes used to define this patch.
    public Collide.Shape[] Shapes { get => GetComponentsInChildren<Collide.Shape>(); }

    protected override bool Initialize()
    {
      ParentTerrain?.GetInitialized<DeformableTerrainBase>();
      // Compensate for the parent terrain being shifted down by the MaximumDepth.
      if ( ParentTerrain != null )
        transform.position += Vector3.up * ParentTerrain.MaximumDepth;

      if ( m_terrainMaterial == null ) {
        Debug.LogWarning( $"Terrain material of patch '{name}' is not set. Ignoring...", this );
        return false;
      }

      ParentTerrain?.AddTerrainMaterial( m_terrainMaterial.GetInitialized<DeformableTerrainMaterial>() );
      if(m_materialHandle != null)
        ParentTerrain?.SetAssociatedMaterial( m_terrainMaterial, m_materialHandle.GetInitialized<ShapeMaterial>());

      foreach ( var shape in Shapes )
        AddShape( shape );

      return true;
    }
  }
}