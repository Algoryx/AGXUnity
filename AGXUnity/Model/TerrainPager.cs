using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  public class TerrainPager : ScriptComponent
  {
    public agxTerrain.TerrainPager Native { get; private set; } = null;

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    [HideInInspector]
    public DeformableTerrainShovel[] Shovels { get { return m_shovels.ToArray(); } }

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

    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution(TerrainData); } }

    public float ElementSize
    {
      get
      {
        return TerrainData.size.x / (TerrainDataResolution - 1);
      }
    }

    [field: SerializeField]
    public uint TileSize { get; set; } = 30;

    [field: SerializeField]
    public uint TileOverlap { get; set; } = 5;

    protected override bool Initialize()
    {
      m_initialHeights = TerrainData.GetHeights(0, 0, TerrainDataResolution, TerrainDataResolution);

      if (TerrainData.size.x != TerrainData.size.z)
        Debug.LogError("Unity Terrain is not square, this is not supported");

      float num = TerrainData.heightmapResolution - TileOverlap - 1;
      float denom = TileSize - TileOverlap - 1;
      float frac = num / denom;
      if(Mathf.Abs(frac - Mathf.Round(frac)) > 0.01)
        Debug.LogWarning("Tile settings used does not fill the Unity terrain");

      Native = new agxTerrain.TerrainPager(
        TileSize,
        TileOverlap,
        ElementSize,
        2,
        -transform.position.ToHandedVec3(), //+ new agx.Vec3(- tileSize / 2 * ElementSize, 0, tileSize / 2 * ElementSize),
        //new agx.Quat(),
        agx.Quat.rotate(agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS()), 
        new agxTerrain.Terrain(10, 10, 1, 1));

      var tds = new agxTerrain.TerrainRasterizer();

      var heights = TerrainUtils.FindHeights(Terrain.terrainData);

      var hm = new agxCollide.Geometry(new agxCollide.HeightField((uint)heights.ResolutionX, (uint)heights.ResolutionY, Terrain.terrainData.size.x, Terrain.terrainData.size.z, heights.Heights));
      hm.setRotation(agx.Quat.rotate(agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS()));
      Vector3 offset = new Vector3(transform.position.x + Terrain.terrainData.size.x/2, transform.position.y, transform.position.z + Terrain.terrainData.size.z/2);
      Debug.Log(offset);
      hm.setPosition(offset.ToHandedVec3());

      tds.addSourceGeometry(hm);

      Native.setTerrainDataSource(tds);
      
      foreach (DeformableTerrainShovel shovel in m_shovels)
        Native.add(shovel.GetInitialized<DeformableTerrainShovel>().Native, 5, 5);

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

    public void OnPostStepForward()
    {
      UpdateHeights();
    }

    public void UpdateHeights()
    {
      var terrains = Native.getActiveTerrains();
      foreach( var terr in terrains)
      {
        DebugDrawTile(terr);
        UpdateTerrain(terr);
      }
      TerrainData.SyncHeightmap();
    }

    protected void UpdateTerrain(agxTerrain.TerrainRef terrain)
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

        result[0, 0] = h / scale ;

        TerrainData.SetHeightsDelayLOD(ui.x, ui.y, result);
      }
    }

    protected Vector3 IndexToWorldpos(agxTerrain.TerrainRef terrain, agx.Vec2i index)
    {
      var pos = terrain.getPosition().ToHandedVector3();
      var elemSize = terrain.getElementSize();
      var indexWorldPos = new agx.Vec3(
        pos.x + (terrain.getResolutionX() / 2 - index.x) * elemSize,
        pos.y,
        pos.z + (terrain.getResolutionY() / 2 - index.y) * elemSize);
      return indexWorldPos.ToVector3();
    }

    protected Vector2Int WorldPosToUnityIndex(Vector3 pos)
    {
      Vector3 relPos = pos - transform.position;
      Vector3 size = Terrain.terrainData.size;
      Vector3 normRelPos = new Vector3(relPos.x / size.x, relPos.y / size.y, relPos.z / size.z);
      var utidx = (normRelPos * (TerrainDataResolution - 1));
      return new(Mathf.RoundToInt(utidx.x), Mathf.RoundToInt(utidx.z));
    }

    protected Vector2Int AGXIndexToUnity(agxTerrain.TerrainRef terrain, agx.Vec2i index)
    {
      Vector3 iwp = IndexToWorldpos(terrain,index);
      return WorldPosToUnityIndex(iwp);
    }


    // Remove this:
    protected void DebugDrawTile(agxTerrain.TerrainRef terr)
    {
      Vector3 basePos = terr.getPosition().ToHandedVector3();
      var size = terr.getSize() / 2;

      Vector3 v0 = basePos + new Vector3((float) size.x, 0.1f, (float) size.y);
      Vector3 v1 = basePos + new Vector3((float) size.x, 0.1f, (float)-size.y);
      Vector3 v2 = basePos + new Vector3((float)-size.x, 0.1f, (float)-size.y);
      Vector3 v3 = basePos + new Vector3((float)-size.x, 0.1f, (float) size.y);

      Debug.DrawLine(v0, v1);
      Debug.DrawLine(v1, v2);
      Debug.DrawLine(v2, v3);
      Debug.DrawLine(v3, v0);
    }

    private Terrain m_terrain = null;
    private float[,] m_initialHeights = null;
  }
}
