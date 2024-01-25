using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#using-different-terrain-materials" )]
  public class TerrainPatchRenderer : ScriptComponent
  {
    [SerializeField]
    private SerializableDictionary<DeformableTerrainMaterial,Texture2D> m_explicitMaterialRenderMap = new SerializableDictionary<DeformableTerrainMaterial, Texture2D>();

    [HideInInspector]
    public SerializableDictionary<DeformableTerrainMaterial, Texture2D> ExplicitMaterialRenderMap => m_explicitMaterialRenderMap;

    public Dictionary<DeformableTerrainMaterial, Texture2D> ImplicitMaterialRenderMap
    {
      get
      {
        Dictionary<DeformableTerrainMaterial, Texture2D> res = new Dictionary<DeformableTerrainMaterial, Texture2D>();
        foreach ( var patch in RenderedPatches )
          if ( patch.TerrainMaterial != null && patch.RenderTexture != null )
            res[ patch.TerrainMaterial ] = patch.RenderTexture;
        return res;
      }
    }

    public Dictionary<DeformableTerrainMaterial, Texture2D> MaterialRenderMap
    {
      get
      {
        var res = ImplicitMaterialRenderMap;
        foreach ( var (k, v) in ExplicitMaterialRenderMap )
          res[ k ] = v;
        return res;
      }
    }

    [SerializeField]
    private bool m_reduceTextureTiling = false;

    public bool ReduceTextureTiling {
      get => m_reduceTextureTiling;
      set
      { 
        m_reduceTextureTiling = value;
        if ( m_material != null )
          m_material.SetKeyword(new LocalKeyword(m_material.shader,"REDUCE_TILING"), value);
      }
    }

    public TerrainMaterialPatch[] RenderedPatches => gameObject.GetComponentsInChildren<TerrainMaterialPatch>();

    private Dictionary<agxTerrain.TerrainMaterial, int> m_materialMapping;

    private Terrain m_unityTerrain;
    private DeformableTerrainBase m_terrain;
    private Mesh m_mesh = null;
    private Material m_material;

    private bool m_changed = false;
    private byte[] m_materialAtlas;
    private Texture2D m_materialTexture;

    protected override bool Initialize()
    {
      m_terrain = gameObject.GetInitializedComponent<DeformableTerrainBase>();
      if ( m_terrain is not DeformableTerrain ) {
        Debug.LogError( "Terrain Patch Renderer currently only supports DeformableTerrain!", this );
        return false;
      }

      // The patches need to be initialized before the initial update pass, otherwise the materials might not yet have been added.
      foreach ( var patch in RenderedPatches )
        patch.GetInitialized();

      m_unityTerrain = GetComponent<UnityEngine.Terrain>();
      var td = m_unityTerrain.terrainData;

      m_mesh = new Mesh();
      m_mesh.vertices = new Vector3[ 3 ];
      m_mesh.triangles = new int[] { 0, 1, 2 };

      m_material = new Material( Shader.Find( "AGXUnity/BuiltIn/TerrainPatchDecal" ) );

      m_materialMapping = new Dictionary<agxTerrain.TerrainMaterial, int>();
      int idx = 1;
      foreach ( var (mat, tl) in MaterialRenderMap ) {
        if ( idx == 5 ) {
          Debug.LogWarning( "The TerrainDecalRenderer currently only supports rendering 4 patch materials. Further materials will not be rendered.", this );
          break;
        }
        var terrMat = mat.GetInitialized<DeformableTerrainMaterial>().Native;
        if ( terrMat != null ) {
          m_materialMapping.Add( mat.GetInitialized<DeformableTerrainMaterial>().Native, idx );
          if ( tl == null )
            Debug.LogWarning( $"Terrain Material '{mat.name}' is mapped to null texture.", this );
          m_material.SetTexture( $"_Decal{idx-1}", tl );
          idx++;
        }
      }

      var size = td.size;
      m_mesh.bounds = new Bounds( m_terrain.transform.position + size / 2.0f, size );
      m_mesh.UploadMeshData( false );

      m_materialAtlas = new byte[ td.heightmapResolution * td.heightmapResolution ];
      m_materialTexture = new Texture2D( td.heightmapResolution, td.heightmapResolution, TextureFormat.R8, false );
      m_materialTexture.filterMode = FilterMode.Point;
      m_materialTexture.anisoLevel = 0;

      m_material.SetTexture( "_Materials", m_materialTexture );
      m_material.SetTexture( "_Heightmap", td.heightmapTexture );
      m_material.SetVector( "_TerrainScale", td.size );
      m_material.SetVector( "_TerrainPosition", m_terrain.transform.position );
      m_material.SetFloat( "_TerrainResolution", td.heightmapResolution );

      m_terrain.OnModification += UpdateTextureAt;
      m_terrain.TriggerModifyAllCells();
      PostStep();

      Simulation.Instance.StepCallbacks.PostStepForward += PostStep;

      return true;
    }

    private void UpdateTextureAt( agxTerrain.Terrain aTerr, agx.Vec2i aIdx, UnityEngine.Terrain uTerr, Vector2Int uIdx )
    {
      var td = uTerr.terrainData;
      var heightsRes = td.heightmapResolution;

      var modPos = aTerr.getSurfacePositionWorld( aIdx );
      var mat = aTerr.getTerrainMaterial( modPos );

      var index = m_materialMapping.GetValueOrDefault(mat,0);

      m_materialAtlas[ uIdx.y * heightsRes + uIdx.x ] = (byte)index;
      m_changed = true;
    }

    void PostStep()
    {
      if ( m_changed ) {
        m_materialTexture.SetPixelData( m_materialAtlas, 0 );
        m_materialTexture.Apply( false );

        // Updating terrain heights seems to invalidiate the heightmap texture so we need to reset it here
        m_material.SetTexture( "_Heightmap", m_unityTerrain.terrainData.heightmapTexture );
      }
    }

    protected override void OnEnable()
    {
      // We hook into the rendering process to render even when the application is paused.
      // For the Built-in render pipeline this is done by adding a callback to the Camera.OnPreCull event which is called for each camera in the scene.
      // For SRPs such as URP and HDRP the beginCameraRendering event serves a similar purpose.
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      RenderPipelineManager.beginCameraRendering += SRPRender;
      Camera.onPreCull -= Render;
      Camera.onPreCull += Render;
    }

    protected override void OnDisable()
    {
      Camera.onPreCull -= Render;
      RenderPipelineManager.beginCameraRendering -= SRPRender;
    }

    private void SRPRender( ScriptableRenderContext context, Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      Render( cam );
    }

    private void Render( Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      Graphics.DrawMesh( m_mesh, Matrix4x4.identity, m_material, 0, cam, 0, null, false );
    }
  }
}