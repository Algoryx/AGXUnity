using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Rendering
{
  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Rendering/Debug Terrain Height Change Renderer" )]
  public class DebugTerrainHeightChangeRenderer : ScriptComponent
  {
    private class TerrainDebugData
    {
      public Terrain Terrain;
      public TerrainData TerrainData;
      public TerrainLayer[] InitialLayers;
      public float[,,] InitialAlphamaps;
      public float[,] InitialHeights;
      public float[,,] Alphamaps;
      public int InitialLayerCount;
      public int LoweredLayerIndex;
      public int RaisedLayerIndex;
      public bool IsDirty;

      public TerrainDebugData( Terrain terrain, TerrainLayer loweredLayer, TerrainLayer raisedLayer )
      {
        Terrain = terrain;
        TerrainData = terrain.terrainData;
        InitialLayers = TerrainData.terrainLayers;
        InitialLayerCount = InitialLayers.Length;
        InitialAlphamaps = TerrainData.GetAlphamaps( 0, 0, TerrainData.alphamapWidth, TerrainData.alphamapHeight );
        InitialHeights = TerrainData.GetHeights( 0, 0, TerrainData.heightmapResolution, TerrainData.heightmapResolution );

        var layers = InitialLayers.ToList();
        if ( !layers.Contains( loweredLayer ) )
          layers.Add( loweredLayer );
        LoweredLayerIndex = layers.IndexOf( loweredLayer );

        if ( !layers.Contains( raisedLayer ) )
          layers.Add( raisedLayer );
        RaisedLayerIndex = layers.IndexOf( raisedLayer );

        TerrainData.terrainLayers = layers.ToArray();
        Alphamaps = TerrainData.GetAlphamaps( 0, 0, TerrainData.alphamapWidth, TerrainData.alphamapHeight );
        RestoreAlphamapBuffer();
      }

      public void RestoreAlphamapBuffer()
      {
        for ( int y = 0; y < TerrainData.alphamapHeight; y++ ) {
          for ( int x = 0; x < TerrainData.alphamapWidth; x++ ) {
            for ( int layer = 0; layer < Alphamaps.GetLength( 2 ); layer++ )
              Alphamaps[ y, x, layer ] = layer < InitialLayerCount ? InitialAlphamaps[ y, x, layer ] : 0.0f;
          }
        }
      }

      public void ResetTerrainData()
      {
        if ( TerrainData == null )
          return;

        TerrainData.terrainLayers = InitialLayers;
        TerrainData.SetAlphamaps( 0, 0, InitialAlphamaps );
      }
    }

    [SerializeField]
    private TerrainLayer m_loweredLayer = null;

    [IgnoreSynchronization]
    public TerrainLayer LoweredLayer
    {
      get => m_loweredLayer;
      set => m_loweredLayer = value;
    }

    [SerializeField]
    private TerrainLayer m_raisedLayer = null;

    [IgnoreSynchronization]
    public TerrainLayer RaisedLayer
    {
      get => m_raisedLayer;
      set => m_raisedLayer = value;
    }

    [field: SerializeField]
    [Tooltip( "Fallback color used when Lowered Layer is not assigned." )]
    public Color LoweredColor { get; set; } = new Color( 0.85f, 0.18f, 0.08f, 1.0f );

    [field: SerializeField]
    [Tooltip( "Fallback color used when Raised Layer is not assigned." )]
    public Color RaisedColor { get; set; } = new Color( 0.12f, 0.45f, 1.0f, 1.0f );

    [field: SerializeField]
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Minimum absolute height change in meters required before a terrain texel is painted." )]
    public float HeightThreshold { get; set; } = 0.01f;

    [field: SerializeField]
    [Range( 0.0f, 1.0f )]
    [Tooltip( "Blend weight of the debug layer over the original terrain paint." )]
    public float PaintStrength { get; set; } = 0.7f;

    [field: SerializeField]
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Absolute height change in meters where the debug layer reaches Paint Strength." )]
    public float FullStrengthHeightChange { get; set; } = 0.1f;

    [field: SerializeField]
    [Min( 0 )]
    [Tooltip( "Paint radius in heightmap samples around each modified terrain vertex." )]
    public int BrushRadius { get; set; } = 0;

    public void Clear()
    {
      foreach ( var data in m_terrainData.Values ) {
        data.RestoreAlphamapBuffer();
        data.TerrainData.SetAlphamaps( 0, 0, data.Alphamaps );
        data.IsDirty = false;
      }
      m_dirtyTerrains.Clear();
    }

    protected override bool Initialize()
    {
      if ( GetComponent<TerrainPatchRenderer>() != null ) {
        Debug.LogError( "DebugTerrainHeightChangeRenderer cannot be used together with TerrainPatchRenderer on the same terrain.", this );
        return false;
      }

      m_terrain = gameObject.GetInitializedComponent<DeformableTerrainBase>();
      if ( m_terrain == null )
        return false;

      var rootTerrain = GetComponent<Terrain>();
      if ( rootTerrain == null ) {
        Debug.LogError( "DebugTerrainHeightChangeRenderer requires a Unity Terrain and does not support mesh based terrains.", this );
        return false;
      }

      if ( !CreateDefaultLayers() )
        return false;

      foreach ( var terrain in TerrainUtils.CollectTerrains( rootTerrain ) )
        EnsureTerrainData( terrain );

      Subscribe();

      return true;
    }

    protected override void OnApplicationQuit()
    {
      RestoreTerrains();
    }

    protected override void OnDestroy()
    {
      Unsubscribe();
      RestoreTerrains();
      DestroyRuntimeLayer( m_runtimeLoweredLayer );
      DestroyRuntimeLayer( m_runtimeRaisedLayer );

      base.OnDestroy();
    }

    protected override void OnDisable()
    {
      Unsubscribe();
      base.OnDisable();
    }

    protected override void OnEnable()
    {
      if ( State == States.INITIALIZED )
        Subscribe();

      base.OnEnable();
    }

    private bool CreateDefaultLayers()
    {
      if ( m_loweredLayer == null ) {
        m_runtimeLoweredLayer = CreateRuntimeLayer( "Debug Lowered Terrain Layer", LoweredColor );
        m_loweredLayer = m_runtimeLoweredLayer;
      }

      if ( m_raisedLayer == null ) {
        m_runtimeRaisedLayer = CreateRuntimeLayer( "Debug Raised Terrain Layer", RaisedColor );
        m_raisedLayer = m_runtimeRaisedLayer;
      }

      if ( m_loweredLayer == m_raisedLayer ) {
        Debug.LogError( "DebugTerrainHeightChangeRenderer requires separate terrain layers for lowered and raised terrain.", this );
        return false;
      }

      return true;
    }

    private static TerrainLayer CreateRuntimeLayer( string layerName, Color color )
    {
      var texture = new Texture2D( 1, 1, TextureFormat.RGBA32, false );
      texture.name = layerName + " Texture";
      texture.hideFlags = HideFlags.HideAndDontSave;
      texture.SetPixel( 0, 0, color );
      texture.Apply();

      var layer = new TerrainLayer();
      layer.name = layerName;
      layer.hideFlags = HideFlags.HideAndDontSave;
      layer.diffuseTexture = texture;
      layer.tileSize = Vector2.one;
      return layer;
    }

    private static void DestroyRuntimeLayer( TerrainLayer layer )
    {
      if ( layer == null )
        return;

      var texture = layer.diffuseTexture;
      if ( Application.isPlaying ) {
        Object.Destroy( layer );
        if ( texture != null )
          Object.Destroy( texture );
      }
      else {
        Object.DestroyImmediate( layer );
        if ( texture != null )
          Object.DestroyImmediate( texture );
      }
    }

    private void Subscribe()
    {
      if ( m_isSubscribed || m_terrain == null || !Simulation.HasInstance )
        return;

      Simulation.Instance.StepCallbacks.SimulationPost += PostStep;
      m_terrain.OnModification += UpdateTextureAt;
      m_isSubscribed = true;
    }

    private void Unsubscribe()
    {
      if ( !m_isSubscribed )
        return;

      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.SimulationPost -= PostStep;
        if ( m_terrain != null )
          m_terrain.OnModification -= UpdateTextureAt;
      }
      m_isSubscribed = false;
    }

    private TerrainDebugData EnsureTerrainData( Terrain terrain )
    {
      if ( terrain == null )
        return null;

      if ( m_terrainData.TryGetValue( terrain, out var data ) )
        return data;

      data = new TerrainDebugData( terrain, m_loweredLayer, m_raisedLayer );
      m_terrainData.Add( terrain, data );
      return data;
    }

    private void RestoreTerrains()
    {
      foreach ( var data in m_terrainData.Values )
        data.ResetTerrainData();

      m_dirtyTerrains.Clear();
    }

    private void PostStep()
    {
      foreach ( var data in m_dirtyTerrains ) {
        if ( data.TerrainData != null )
          data.TerrainData.SetAlphamaps( 0, 0, data.Alphamaps );
        data.IsDirty = false;
      }

      m_dirtyTerrains.Clear();
    }

    private void UpdateTextureAt( agxTerrain.Terrain aTerr, agx.Vec2i aIdx, Terrain uTerr, Vector2Int uIdx )
    {
      var data = EnsureTerrainData( uTerr );
      if ( data == null )
        return;

      var td = data.TerrainData;
      var baselineHeight = data.InitialHeights[ uIdx.y, uIdx.x ] * td.heightmapScale.y;
      var currentHeight = td.GetHeight( uIdx.x, uIdx.y );
      var delta = currentHeight - baselineHeight;

      var debugLayer = -1;
      var absDelta = Mathf.Abs( delta );
      if ( delta < -HeightThreshold )
        debugLayer = data.LoweredLayerIndex;
      else if ( delta > HeightThreshold )
        debugLayer = data.RaisedLayerIndex;

      var fullStrengthHeightChange = Mathf.Max( FullStrengthHeightChange, HeightThreshold + Mathf.Epsilon );
      var strength = debugLayer >= 0 ?
                       Mathf.Clamp01( PaintStrength ) *
                       Mathf.InverseLerp( HeightThreshold, fullStrengthHeightChange, absDelta ) :
                       0.0f;

      Paint( data, uIdx, debugLayer, strength );

      if ( !data.IsDirty ) {
        data.IsDirty = true;
        m_dirtyTerrains.Add( data );
      }
    }

    private void Paint( TerrainDebugData data, Vector2Int heightIndex, int debugLayer, float strength )
    {
      var td = data.TerrainData;
      var alphamapWidth = td.alphamapWidth;
      var alphamapHeight = td.alphamapHeight;
      var heightmapMaxX = td.heightmapResolution - 1;
      var heightmapMaxY = td.heightmapResolution - 1;

      var minAlphaX = Mathf.FloorToInt( ( heightIndex.x - 0.5f - BrushRadius ) / heightmapMaxX * alphamapWidth );
      var minAlphaY = Mathf.FloorToInt( ( heightIndex.y - 0.5f - BrushRadius ) / heightmapMaxY * alphamapHeight );
      var maxAlphaX = Mathf.CeilToInt( ( heightIndex.x + 0.5f + BrushRadius ) / heightmapMaxX * alphamapWidth );
      var maxAlphaY = Mathf.CeilToInt( ( heightIndex.y + 0.5f + BrushRadius ) / heightmapMaxY * alphamapHeight );

      minAlphaX = Mathf.Clamp( minAlphaX, 0, alphamapWidth );
      minAlphaY = Mathf.Clamp( minAlphaY, 0, alphamapHeight );
      maxAlphaX = Mathf.Clamp( Mathf.Max( maxAlphaX, minAlphaX + 1 ), 0, alphamapWidth );
      maxAlphaY = Mathf.Clamp( Mathf.Max( maxAlphaY, minAlphaY + 1 ), 0, alphamapHeight );

      for ( int y = minAlphaY; y < maxAlphaY; y++ ) {
        for ( int x = minAlphaX; x < maxAlphaX; x++ ) {
          for ( int layer = 0; layer < data.Alphamaps.GetLength( 2 ); layer++ ) {
            var original = layer < data.InitialLayerCount ? data.InitialAlphamaps[ y, x, layer ] : 0.0f;
            data.Alphamaps[ y, x, layer ] = original * ( 1.0f - strength );
          }

          if ( debugLayer >= 0 )
            data.Alphamaps[ y, x, debugLayer ] += strength;
        }
      }
    }

    private readonly Dictionary<Terrain, TerrainDebugData> m_terrainData = new Dictionary<Terrain, TerrainDebugData>();
    private readonly List<TerrainDebugData> m_dirtyTerrains = new List<TerrainDebugData>();
    private DeformableTerrainBase m_terrain = null;
    private TerrainLayer m_runtimeLoweredLayer = null;
    private TerrainLayer m_runtimeRaisedLayer = null;
    private bool m_isSubscribed = false;
  }
}
