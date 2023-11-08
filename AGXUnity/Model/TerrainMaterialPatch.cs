using UnityEngine;

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
      // TODO: Handle setting material during runtime properly.
      set { m_terrainMaterial = value; }
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
      // TODO: Handle setting material during runtime properly.
      set { m_materialHandle = value; }
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
      foreach(var shape in Shapes) {
        shape.enabled &= !DisableShapes;
        shape.Visual.GetComponent<MeshRenderer>().enabled &= !DisableVisuals;
      }

      // Compensate for the parent terrain being shifted down by the MaximumDepth.
      if(ParentTerrain != null ) 
        transform.position += Vector3.up * ParentTerrain.MaximumDepth;

      return true;

    }
  }
}