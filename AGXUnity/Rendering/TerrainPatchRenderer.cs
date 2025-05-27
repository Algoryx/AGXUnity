using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace AGXUnity.Rendering
{
  class TileData
  {
    public float[,,] alphamap;
    public Dictionary<agxTerrain.TerrainMaterial, int> m_materialMapping;
    public int m_layerCount;
    public int m_defaultLayerIndex;
  }

  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#using-different-terrain-materials" )]
  public class TerrainPatchRenderer : ScriptComponent
  {
    private DeformableTerrainBase terrain;

    private Dictionary<Terrain, TileData> m_perTileData;
    private List<Terrain> m_shouldUpdate;

    [SerializeField]
    private TerrainLayer m_defaultLayer;

    /// <summary>
    /// The deafult TerrainLayer to use to render the terrain in cells where no material patch is present
    /// or for patches which does not have an explicitly mapped layer.
    /// </summary>
    [IgnoreSynchronization]
    public TerrainLayer DefaultLayer
    {
      get => m_defaultLayer;
      set
      {
        if ( State == States.INITIALIZED )
          Debug.LogError( "Setting material TerrainLayers during runtime is not supported!" );
        else
          m_defaultLayer = value;
      }
    }

    [SerializeField]
    [FormerlySerializedAs("m_materialRenderMap")]
    private SerializableDictionary<DeformableTerrainMaterial,TerrainLayer> m_explicitMaterialRenderMap = new SerializableDictionary<DeformableTerrainMaterial, TerrainLayer>();

    /// <summary>
    /// Defines a map from DeformableTerrainMaterials to the TerrainLayers used to render patches of the specified terrain material.
    /// </summary>
    [HideInInspector]
    [IgnoreSynchronization]
    public SerializableDictionary<DeformableTerrainMaterial, TerrainLayer> ExplicitMaterialRenderMap
    {
      get => m_explicitMaterialRenderMap;
      set
      {
        if ( State == States.INITIALIZED )
          Debug.LogError( "Setting material TerrainLayers during runtime is not supported!" );
        else
          m_explicitMaterialRenderMap = value;
      }
    }

    public Dictionary<DeformableTerrainMaterial, TerrainLayer> ImplicitMaterialRenderMap
    {
      get
      {
        Dictionary<DeformableTerrainMaterial,TerrainLayer> res = new Dictionary<DeformableTerrainMaterial,TerrainLayer>();
        foreach ( var patch in RenderedPatches )
          if ( patch.TerrainMaterial != null && patch.RenderLayer != null )
            res[ patch.TerrainMaterial ] = patch.RenderLayer;
        return res;
      }
    }

    public Dictionary<DeformableTerrainMaterial, TerrainLayer> MaterialRenderMap
    {
      get
      {
        var res = ImplicitMaterialRenderMap;
        foreach ( var (mat, tl) in ExplicitMaterialRenderMap )
          res[ mat ] = tl;
        return res;
      }
    }

    public TerrainMaterialPatch[] RenderedPatches => gameObject.GetComponentsInChildren<TerrainMaterialPatch>();

    private void InitializeTile( Terrain tile )
    {
      TileData tileData = new TileData();
      var td = tile.terrainData;

      var layers = td.terrainLayers.ToList();
      if ( !layers.Contains( DefaultLayer ) )
        layers.Add( DefaultLayer );
      tileData.m_defaultLayerIndex = layers.IndexOf( DefaultLayer );

      // Initialize terrain layers: 0 is default, 1+ are mapped.
      tileData.m_materialMapping = new Dictionary<agxTerrain.TerrainMaterial, int>();
      foreach ( var (mat, tl) in MaterialRenderMap ) {
        var terrMat = mat.GetInitialized<DeformableTerrainMaterial>().Native;
        if ( terrMat != null ) {
          if ( tl == null ) {
            Debug.LogWarning( $"Terrain Material '{mat.name}' is mapped to null texture.", this );
            continue;
          }

          if ( !layers.Contains( tl ) )
            layers.Add( tl );
          tileData.m_materialMapping.Add( mat.GetInitialized<DeformableTerrainMaterial>().Native, layers.IndexOf( tl ) );
        }
      }
      td.terrainLayers = layers.ToArray();
      tileData.m_layerCount = layers.Count;

      tileData.alphamap = td.GetAlphamaps( 0, 0, td.alphamapWidth, td.alphamapHeight );

      m_perTileData.Add( tile, tileData );
    }

    protected override bool Initialize()
    {
      terrain = gameObject.GetInitializedComponent<DeformableTerrainBase>();
      if ( terrain is MovableTerrain ) {
        Debug.LogError( "Terrain Patch Renderer does not support MovableTerrain!", this );
        return false;
      }

      // The patches need to be initialized before the initial update pass, otherwise the materials might not yet have been added.
      foreach ( var patch in RenderedPatches )
        patch.GetInitialized();

      var uTerr = GetComponent<Terrain>();
      var td = uTerr.terrainData;

      m_perTileData = new Dictionary<Terrain, TileData>();
      m_shouldUpdate = new List<Terrain>();

      if ( DefaultLayer == null ) {
        Debug.LogWarning( "No DefaultLayer provided. Using first layer present in terrain.", this );
        m_defaultLayer = td.terrainLayers[ 0 ];
      }

      Simulation.Instance.StepCallbacks.SimulationPost += PostStep;
      terrain.OnModification += UpdateTextureAt;

      if ( terrain is DeformableTerrain ) {
        InitializeTile( uTerr );
        terrain.TriggerModifyAllCells();
        td.SetAlphamaps( 0, 0, m_perTileData[ uTerr ].alphamap );
      }

      return true;
    }

    protected override void OnApplicationQuit()
    {
      m_initialData?.Reset( GetComponent<Terrain>().terrainData );
    }

    protected override void OnDestroy()
    {
      m_initialData?.Reset( GetComponent<Terrain>().terrainData );

      base.OnDestroy();
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.SimulationPost-= PostStep;
        terrain.OnModification -= UpdateTextureAt;
      }

      base.OnDisable();
    }

    protected override void OnEnable()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.SimulationPost += PostStep;
        terrain.OnModification += UpdateTextureAt;
      }
      base.OnEnable();
    }

    private void PostStep()
    {
      foreach ( var terr in m_shouldUpdate )
        terr.terrainData.SetAlphamaps( 0, 0, m_perTileData[ terr ].alphamap );

      m_shouldUpdate.Clear();
    }

    private void UpdateTextureAt( agxTerrain.Terrain aTerr, agx.Vec2i aIdx, Terrain uTerr, Vector2Int uIdx )
    {
      if ( !m_perTileData.ContainsKey( uTerr ) )
        InitializeTile( uTerr );

      if ( !m_shouldUpdate.Contains( uTerr ) )
        m_shouldUpdate.Add( uTerr );

      var td = uTerr.terrainData;
      var alphamapRes = td.alphamapResolution;
      var heightsRes = td.heightmapResolution - 1;

      var modPos = aTerr.getSurfacePositionWorld( aIdx );
      var mat = aTerr.getTerrainMaterial( modPos );

      var data = m_perTileData[uTerr];

      var index = data.m_materialMapping.GetValueOrDefault(mat,data.m_defaultLayerIndex);

      if ( index == data.m_defaultLayerIndex && State != States.INITIALIZED )
        return;

      var modAlphaX = Mathf.RoundToInt((uIdx.x - 0.5f)/heightsRes * alphamapRes);
      var modAlphaY = Mathf.RoundToInt((uIdx.y - 0.5f)/heightsRes * alphamapRes);
      var modAlphaXend = Mathf.RoundToInt((uIdx.x + 0.5f)/heightsRes * alphamapRes);
      var modAlphaYend = Mathf.RoundToInt((uIdx.y + 0.5f)/heightsRes * alphamapRes);

      for ( int y = modAlphaY; y < modAlphaYend; y++ ) {
        if ( y < 0 || y >= alphamapRes )
          continue;
        for ( int x = modAlphaX; x < modAlphaXend; x++ ) {
          if ( x < 0 || x >= alphamapRes )
            continue;
          for ( int i = 0; i < data.m_layerCount; i++ )
            data.alphamap[ y, x, i ] = i == index ? 1.0f : 0.0f;
        }
      }
    }
  }
}
