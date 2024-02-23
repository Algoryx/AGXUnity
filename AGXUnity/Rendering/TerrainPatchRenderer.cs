using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Wrapper class for storing/resetting initial state of TerrainData.
  /// This is by no means a complete store/restore, only the parts used by <see cref="TerrainPatchRenderer"/>.
  /// </summary>
  class InitialTerrainData
  {
    private float[,,] m_alphamaps;
    private TerrainLayer[] m_layers;

    public InitialTerrainData( TerrainData td )
    {
      m_alphamaps = td.GetAlphamaps( 0, 0, td.alphamapWidth, td.alphamapHeight );
      m_layers = td.terrainLayers;
    }

    public void Reset( TerrainData td )
    {
      if ( td != null ) {
        td.terrainLayers = m_layers;
        td.SetAlphamaps( 0, 0, m_alphamaps );
      }
    }
  }

  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#using-different-terrain-materials" )]
  public class TerrainPatchRenderer : ScriptComponent
  {
    private DeformableTerrainBase terrain;
    private float[,,] alphamap;
    private Dictionary<agxTerrain.TerrainMaterial, int> m_materialMapping;
    private InitialTerrainData m_initialData;
    private int m_layerCount;
    private int m_defaultLayerIndex;

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
        if ( m_initialData != null )
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
        if ( m_initialData != null )
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

    protected override bool Initialize()
    {
      terrain = gameObject.GetInitializedComponent<DeformableTerrainBase>();
      if ( terrain is not DeformableTerrain ) {
        Debug.LogError( "Terrain Patch Renderer currently only supports DeformableTerrain!", this );
        return false;
      }

      // The patches need to be initialized before the initial update pass, otherwise the materials might not yet have been added.
      foreach ( var patch in RenderedPatches )
        patch.GetInitialized();

      var uTerr = GetComponent<Terrain>();
      var td = uTerr.terrainData;

      m_initialData = new InitialTerrainData( td );

      var layers = td.terrainLayers.ToList();
      if ( DefaultLayer == null ) {
        Debug.LogWarning( "No DefaultLayer provided. Using first layer present in terrain.", this );
        m_defaultLayer = td.terrainLayers[ 0 ];
        m_defaultLayerIndex = 0;
      }
      else {
        if ( !layers.Contains( DefaultLayer ) )
          layers.Add( DefaultLayer );
        m_defaultLayerIndex = layers.IndexOf( DefaultLayer );
      }

      // Initialize terrain layers: 0 is default, 1+ are mapped.
      m_materialMapping = new Dictionary<agxTerrain.TerrainMaterial, int>();
      foreach ( var (mat, tl) in MaterialRenderMap ) {
        var terrMat = mat.GetInitialized<DeformableTerrainMaterial>().Native;
        if ( terrMat != null ) {
          if ( tl == null ) {
            Debug.LogWarning( $"Terrain Material '{mat.name}' is mapped to null texture.", this );
            continue;
          }

          if ( !layers.Contains( tl ) )
            layers.Add( tl );
          m_materialMapping.Add( mat.GetInitialized<DeformableTerrainMaterial>().Native, layers.IndexOf( tl ) );
        }
      }
      td.terrainLayers = layers.ToArray();
      m_layerCount = layers.Count;

      alphamap = td.GetAlphamaps( 0, 0, td.alphamapWidth, td.alphamapHeight );

      Simulation.Instance.StepCallbacks.SimulationPost += PostStep;
      terrain.OnModification += UpdateTextureAt;

      terrain.TriggerModifyAllCells();

      td.SetAlphamaps( 0, 0, alphamap );

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

    private void PostStep()
    {
      var td = GetComponent<Terrain>().terrainData;

      td.SetAlphamaps( 0, 0, alphamap );
    }

    private void UpdateTextureAt( agxTerrain.Terrain aTerr, agx.Vec2i aIdx, Terrain uTerr, Vector2Int uIdx )
    {
      var td = uTerr.terrainData;
      var alphamapRes = td.alphamapResolution;
      var heightsRes = td.heightmapResolution - 1;

      var modPos = aTerr.getSurfacePositionWorld( aIdx );
      var mat = aTerr.getTerrainMaterial( modPos );

      var index = m_materialMapping.GetValueOrDefault(mat,m_defaultLayerIndex);

      if ( index == m_defaultLayerIndex && State != States.INITIALIZED )
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
          for ( int i = 0; i < m_layerCount; i++ )
            alphamap[ y, x, i ] = i == index ? 1.0f : 0.0f;
        }
      }
    }
  }
}