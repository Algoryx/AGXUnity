using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

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
  public class TerrainPatchRenderer : ScriptComponent
  {
    private DeformableTerrainBase terrain;
    private float[,,] alphamap;
    private Dictionary<agxTerrain.TerrainMaterial, int> m_materialMapping;
    private InitialTerrainData m_initialData;

    [SerializeField]
    private TerrainLayer m_defaultLayer;

    /// <summary>
    /// The deafult TerrainLayer to use to render the terrain in cells where no material patch is present
    /// or for patches which does not have an explicitly mapped layer.
    /// </summary>
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
    private SerializableDictionary<DeformableTerrainMaterial,TerrainLayer> m_materialRenderMap = new SerializableDictionary<DeformableTerrainMaterial, TerrainLayer>();

    /// <summary>
    /// Defines a map from DeformableTerrainMaterials to the TerrainLayers used to render patches of the specified terrain material.
    /// </summary>
    [HideInInspector]
    public SerializableDictionary<DeformableTerrainMaterial,TerrainLayer> MaterialRenderMap
    {
      get => m_materialRenderMap;
      set
      {
        if ( m_initialData != null )
          Debug.LogError( "Setting material TerrainLayers during runtime is not supported!" );
        else
          m_materialRenderMap = value;
      }
    }

    protected override bool Initialize()
    {
      terrain = gameObject.GetInitializedComponent<DeformableTerrainBase>();
      var uTerr = GetComponent<Terrain>();
      var td = uTerr.terrainData;

      m_initialData = new InitialTerrainData( td );

      if ( DefaultLayer == null ) {
        Debug.LogError( "No DefaultLayer provided!", this );
        return false;
      }

      m_materialMapping = new Dictionary<agxTerrain.TerrainMaterial, int>();
      var layers = new List<TerrainLayer> { DefaultLayer };
      int idx = 1;
      foreach ( var (mat, tl) in MaterialRenderMap ) {
        var terrMat = mat.GetInitialized<DeformableTerrainMaterial>().Native;
        if ( terrMat != null ) {
          m_materialMapping.Add( mat.GetInitialized<DeformableTerrainMaterial>().Native, idx++ );
          layers.Add( tl );
        }
      }
      td.terrainLayers = layers.ToArray();

      alphamap = td.GetAlphamaps( 0, 0, td.alphamapWidth, td.alphamapHeight );

      Simulation.Instance.StepCallbacks.SimulationPost += PostStep;
      terrain.OnModification += UpdateTextureAt;

      terrain.TriggerModifyAllCells();

      td.SetAlphamaps( 0, 0, alphamap );

      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();

      m_initialData?.Reset( GetComponent<Terrain>().terrainData );
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

      var index = m_materialMapping.GetValueOrDefault(mat,0);

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
          for ( int i = 0; i < MaterialRenderMap.Count + 1; i++ )
            alphamap[ y, x, i ] = i == index ? 1.0f : 0.0f;
        }
      }
    }
  }
}