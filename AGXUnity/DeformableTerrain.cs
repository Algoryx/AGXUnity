using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain" )]
  [RequireComponent(typeof( Terrain ))]
  [DisallowMultipleComponent]
  public class DeformableTerrain : ScriptComponent
  {
    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public Terrain Terrain { get { return m_terrain ?? ( m_terrain = GetComponent<Terrain>() ); } }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

    protected override bool Initialize()
    {
      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }

    private Terrain m_terrain = null;
  }
}
