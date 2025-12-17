using AGXUnity.Model;
using AGXUnity.Utils;
using openplx;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public class MapperData
  {
    public GameObject RootNode { get; set; } = null;

    public HashSet<GameObject> CreatedGameObjects { get; } = new HashSet<GameObject>();

    public HashSet<string> RegisteredDocuments { get; } = new HashSet<string>();

    private Material m_defaultVisualMaterial = null;
    public Material DefaultVisualMaterial
    {
      get
      {
        if ( m_defaultVisualMaterial == null ) {
          m_defaultVisualMaterial = RenderingUtils.CreateDefaultMaterial();
          m_defaultVisualMaterial.hideFlags = HideFlags.HideInHierarchy;
        }
        return m_defaultVisualMaterial;
      }
    }
    public bool HasDefaultVisualMaterial => m_defaultVisualMaterial != null;

    public ErrorReporter ErrorReporter { get; set; } = null;

    private ShapeMaterial m_defaultMaterial = null;
    public ShapeMaterial DefaultMaterial
    {
      get
      {
        if ( m_defaultMaterial == null ) {
          m_defaultMaterial = ShapeMaterial.CreateInstance<ShapeMaterial>();
          m_defaultMaterial.Density = 1000;
          m_defaultMaterial.name = "Default";
        }
        return m_defaultMaterial;
      }
    }
    public bool HasDefaultMaterial => m_defaultMaterial != null;

    public FrictionModel DefaultFriction { get; set; } = null;

    public SavedPrefabLocalData PrefabLocalData { get; set; } = null;
    public agxopenplx.AgxCache AgxCache { get; } = new agxopenplx.AgxCache();

    public Dictionary<openplx.Physics.Geometries.ContactGeometry, Collide.Shape> GeometryCache { get; } = new Dictionary<openplx.Physics.Geometries.ContactGeometry, Collide.Shape>();
    public Dictionary<openplx.Core.Object, RigidBody> BodyCache { get; } = new Dictionary<openplx.Core.Object, RigidBody>();
    public Dictionary<openplx.Physics.System, GameObject> SystemCache { get; } = new Dictionary<openplx.Physics.System, GameObject>();
    public Dictionary<openplx.Core.Object, GameObject> FrameCache { get; } = new Dictionary<openplx.Core.Object, GameObject>();
    public Dictionary<openplx.Physics3D.Interactions.Mate, Constraint> MateCache { get; } = new Dictionary<openplx.Physics3D.Interactions.Mate, Constraint>();
    public Dictionary<openplx.Physics.Geometries.Material, ShapeMaterial> MaterialCache { get; } = new Dictionary<openplx.Physics.Geometries.Material, ShapeMaterial>();
    public Dictionary<openplx.Physics3D.Interactions.MateConnector, GameObject> MateConnectorCache { get; } = new Dictionary<openplx.Physics3D.Interactions.MateConnector, GameObject>();
    public Dictionary<openplx.Physics.Interactions.Dissipation.DefaultFriction, FrictionModel> FrictionModelCache { get; } = new Dictionary<openplx.Physics.Interactions.Dissipation.DefaultFriction, FrictionModel>();
    public Dictionary<openplx.Terrain.TerrainMaterial, DeformableTerrainMaterial> TerrainMaterialCache { get; } = new Dictionary<openplx.Terrain.TerrainMaterial, DeformableTerrainMaterial>();
    public Dictionary<openplx.Vehicles.Wheels.ElasticWheel, TwoBodyTireProperties> TirePropertyCache { get; } = new Dictionary<openplx.Vehicles.Wheels.ElasticWheel, TwoBodyTireProperties>();
    public Dictionary<openplx.Terrain.Shovel, DeformableTerrainShovelSettings> ShovelSettingsCache { get; } = new Dictionary<openplx.Terrain.Shovel, DeformableTerrainShovelSettings>();

    public Dictionary<uint, Material> NativeMappedRenderMaterialCache { get; } = new Dictionary<uint, Material>();
    public Dictionary<openplx.Visuals.Materials.Material, Material> RenderMaterialCache { get; } = new Dictionary<openplx.Visuals.Materials.Material, Material> { };

    public List<ContactMaterial> MappedContactMaterials { get; } = new List<ContactMaterial>();
    public List<FrictionModel> MappedFrictionModels { get; } = new List<FrictionModel>();
    public List<Mesh> MappedMeshes { get; } = new List<Mesh>();
    public List<Material> MappedMaterials { get; } = new List<Material>();
    public List<TrackProperties> MappedTrackProperties { get; } = new List<TrackProperties>();
    public List<TrackInternalMergeProperties> MappedTrackInternalMergeProperties { get; } = new List<TrackInternalMergeProperties>();
    public List<DeformableTerrainMaterial> MappedTerrainMaterials { get; } = new List<DeformableTerrainMaterial> { };

    public bool TerrainParticleRendererAdded { get; set; } = false;

    public GameObject CreateGameObject( string name = null )
    {
      GameObject go = new GameObject( name );
      RegisterGameObject( go );
      return go;
    }

    public void RegisterGameObject( GameObject go ) => CreatedGameObjects.Add( go );

    public GameObject CreateOpenPLXObject( string name )
    {
      GameObject go = new GameObject( );
      RegisterOpenPLXObject( name, go );

      return go;
    }

    public void RegisterOpenPLXObject( string name, GameObject go )
    {
      var bo = go.GetOrCreateComponent<OpenPLXObject>();
      RegisterGameObject( go );
      if ( bo.SourceDeclarations.Count == 0 ) {
        var nameShort = name.Split('.').Last();
        go.name = nameShort;
      }
      if ( !bo.SourceDeclarations.Contains( name ) )
        bo.SourceDeclarations.Add( name );
    }
  }
}
