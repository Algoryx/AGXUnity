using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu("AGXUnity/Model/Terrain Pager")]
  [RequireComponent(typeof(Terrain))]
  [DisallowMultipleComponent]
  public class TerrainPager : ScriptComponent
  {
    /// <summary>
    /// Native TerrainPager instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.TerrainPager Native { get; private set; } = null;

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    /// <summary>
    /// Shovels associated to this terrain.
    /// </summary>
    [HideInInspector]
    public DeformableTerrainShovel[] Shovels { get { return m_shovels.ToArray(); } }

    [SerializeField]
    private List<RigidBody> m_rigidbodies = new List<RigidBody>();

    /// <summary>
    /// Rigidbodies associated to this terrain.
    /// </summary>
    [HideInInspector]
    public RigidBody[] RigidBodies { get { return m_rigidbodies.ToArray(); } }

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public Terrain Terrain
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<Terrain>() :
                 m_terrain;
      }
    }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    [HideInInspector]
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

    /// <summary>
    /// Unity Terrain heightmap resolution.
    /// </summary>
    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution(TerrainData); } }

    /// <summary>
    /// Size in units which each heightmap texel represent
    /// </summary>
    public float ElementSize
    {
      get
      {
        return TerrainData.size.x / (TerrainDataResolution - 1);
      }
    }

    /// <summary>
    /// The size of the underlying AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public int TileSize { get; set; } = 35;

    /// <summary>
    /// The overlap of adjacent AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public int TileOverlap { get; set; } = 5;

    /// <summary>
    /// Associates the given shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to add.</param>
    /// <param name="requiredRadius">The radius around the shovel instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the shovel instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add(DeformableTerrainShovel shovel, float requiredRadius = 10, float preloadRadius = 10)
    {
      if (shovel == null || m_shovels.Contains(shovel))
        return false;

      m_shovels.Add(shovel);

      // Initialize shovel if we're initialized.
      if (Native != null)
        Native.add(shovel.GetInitialized<DeformableTerrainShovel>().Native, requiredRadius, preloadRadius);

      return true;
    }

    /// <summary>
    /// Disassociate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove(DeformableTerrainShovel shovel)
    {
      if (shovel == null || !m_shovels.Contains(shovel))
        return false;

      if (Native != null)
        Native.remove(shovel.Native);

      return m_shovels.Remove(shovel);
    }

    /// <summary>
    /// Associates the given rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to add.</param>
    /// <param name="requiredRadius">The radius around the rigidbody instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the rigidbody instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add(RigidBody rigidbody, float requiredRadius = 10, float preloadRadius = 10)
    {
      if (rigidbody == null || m_rigidbodies.Contains(rigidbody))
        return false;

      m_rigidbodies.Add(rigidbody);

      // Initialize shovel if we're initialized.
      if (Native != null)
        Native.add(rigidbody.GetInitialized<RigidBody>().Native, requiredRadius, preloadRadius);

      return true;
    }

    /// <summary>
    /// Disassociate rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove(RigidBody rigidbody)
    {
      if (rigidbody == null || !m_rigidbodies.Contains(rigidbody))
        return false;

      if (Native != null)
        Native.remove(rigidbody.Native);

      return m_rigidbodies.Remove(rigidbody);
    }

    /// <summary>
    /// Verifies so that all added shovels still exists. Shovels that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidShovels()
    {
      m_shovels.RemoveAll(shovel => shovel == null);
    }

    /// <summary>
    /// Checks if the current TerrainPager parameters tile the underlying Unity Terrain
    /// The amount of tiles R can be calculated as (l - O - 1) / (S - O - 1) where l is heightmap size O is overlap and S is tile size
    /// Parameters are valid if O and S tile l, that is if R is an integer
    /// </summary>
    /// <returns>True if the parameters tile the Unity Terrain</returns>
    public bool ValidateParameters()
    {
      float r = (float)(TerrainDataResolution - TileOverlap - 1) / (TileSize - TileOverlap - 1);
      return Mathf.Approximately(r, Mathf.Round(r));
    }

    protected override bool Initialize()
    {
      m_initialHeights = TerrainData.GetHeights(0, 0, TerrainDataResolution, TerrainDataResolution);

      if (TerrainData.size.x != TerrainData.size.z)
        Debug.LogError("Unity Terrain is not square, this is not supported");

      if (!ValidateParameters())
        Debug.LogWarning("Tile settings used does not fill the Unity terrain");

      Native = new agxTerrain.TerrainPager(
        (uint)TileSize,
        (uint)TileOverlap,
        ElementSize,
        2,
        -transform.position.ToHandedVec3(),
        agx.Quat.rotate(agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS()),
        new agxTerrain.Terrain(10, 10, 1, 1));

      // Generate AGX heightmap from the Unity Terrain
      var heights = TerrainUtils.FindHeights(Terrain.terrainData);
      var hm = new agxCollide.Geometry(new agxCollide.HeightField((uint)heights.ResolutionX, (uint)heights.ResolutionY, Terrain.terrainData.size.x, Terrain.terrainData.size.z, heights.Heights));
      hm.setRotation(agx.Quat.rotate(agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS()));
      Vector3 offset = new(transform.position.x + TerrainData.size.x / 2, transform.position.y, transform.position.z + TerrainData.size.z / 2);
      hm.setPosition(offset.ToHandedVec3());

      // Create a data source using the generated heightmap and add it to the pager
      var tds = new agxTerrain.TerrainRasterizer();
      tds.addSourceGeometry(hm);
      Native.setTerrainDataSource(tds);

      // Add Rigidbodies and shovels to pager
      // TODO: Track radii and use the tracked values here
      foreach (DeformableTerrainShovel shovel in m_shovels)
        Native.add(shovel.GetInitialized<DeformableTerrainShovel>().Native, 5, 5);
      foreach (RigidBody rb in m_rigidbodies)
        Native.add(rb.GetInitialized<RigidBody>().Native, 5, 5);

      GetSimulation().add(Native);
      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      Native.setEnable(true);

      return base.Initialize();
    }

    protected override void OnDestroy()
    {
      if (m_initialHeights == null)
        return;

      TerrainData.SetHeights(0, 0, m_initialHeights);

#if UNITY_EDITOR
      // If the editor is closed during play the modified height
      // data isn't saved, this resolves corrupt heights in such case.
      UnityEditor.EditorUtility.SetDirty(TerrainData);
      UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    private void OnPostStepForward()
    {
      UpdateHeights();
    }

    private void UpdateHeights()
    {
      var terrains = Native.getActiveTerrains();
      foreach (var terr in terrains)
      {
        DebugDrawTile(terr);
        UpdateTerrain(terr);
      }
      TerrainData.SyncHeightmap();
    }

    private void UpdateTerrain(agxTerrain.TerrainRef terrain)
    {
      var modifications = terrain.getModifiedVertices();

      if (modifications.Count == 0)
        return;

      var scale = TerrainData.heightmapScale.y;
      var result = new float[,] { { 0.0f } };
      foreach (var index in modifications)
      {
        var ui = AGXIndexToUnity(terrain, index);
        var h = (float)terrain.getHeight(index);

        result[0, 0] = h / scale;

        TerrainData.SetHeightsDelayLOD(ui.x, ui.y, result);
      }
    }

    private Vector3 IndexToWorldpos(agxTerrain.TerrainRef terrain, agx.Vec2i index)
    {
      var pos = terrain.getPosition().ToHandedVector3();
      var elemSize = terrain.getElementSize();
      var indexWorldPos = new agx.Vec3(
        pos.x + (terrain.getResolutionX() / 2 - index.x) * elemSize,
        pos.y,
        pos.z + (terrain.getResolutionY() / 2 - index.y) * elemSize);
      return indexWorldPos.ToVector3();
    }

    private Vector2Int WorldPosToUnityIndex(Vector3 pos)
    {
      Vector3 relPos = pos - transform.position;
      Vector3 size = Terrain.terrainData.size;
      Vector3 normRelPos = new Vector3(relPos.x / size.x, relPos.y / size.y, relPos.z / size.z);
      var utidx = (normRelPos * (TerrainDataResolution - 1));
      return new(Mathf.RoundToInt(utidx.x), Mathf.RoundToInt(utidx.z));
    }

    private Vector2Int AGXIndexToUnity(agxTerrain.TerrainRef terrain, agx.Vec2i index)
    {
      Vector3 iwp = IndexToWorldpos(terrain, index);
      return WorldPosToUnityIndex(iwp);
    }


    // Remove this:
    private void DebugDrawTile(agxTerrain.TerrainRef terr)
    {
      Vector3 basePos = terr.getPosition().ToHandedVector3();
      var size = terr.getSize() / 2;

      Vector3 v0 = basePos + new Vector3((float)size.x, 0.1f, (float)size.y);
      Vector3 v1 = basePos + new Vector3((float)size.x, 0.1f, (float)-size.y);
      Vector3 v2 = basePos + new Vector3((float)-size.x, 0.1f, (float)-size.y);
      Vector3 v3 = basePos + new Vector3((float)-size.x, 0.1f, (float)size.y);

      Debug.DrawLine(v0, v1);
      Debug.DrawLine(v1, v2);
      Debug.DrawLine(v2, v3);
      Debug.DrawLine(v3, v0);
    }

    private Terrain m_terrain = null;
    private float[,] m_initialHeights = null;
  }
}
