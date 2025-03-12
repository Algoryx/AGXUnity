using AGXUnity.Model;
using openplx;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public class MapperData
  {
    public GameObject RootNode { get; set; } = null;
    public Material VisualMaterial { get; set; } = null;
    public ErrorReporter ErrorReporter { get; set; } = null;
    public ShapeMaterial DefaultMaterial { get; set; } = null;
    public FrictionModel DefaultFriction { get; set; } = null;

    public SavedPrefabLocalData PrefabLocalData { get; set; } = null;
    public agxopenplx.AgxCache AgxCache { get; } = new agxopenplx.AgxCache();

    public Dictionary<openplx.Physics.Charges.ContactGeometry, Collide.Shape> GeometryCache { get; } = new Dictionary<openplx.Physics.Charges.ContactGeometry, Collide.Shape>();
    public Dictionary<openplx.Core.Object, RigidBody> BodyCache { get; } = new Dictionary<openplx.Core.Object, RigidBody>();
    public Dictionary<openplx.Physics.System, GameObject> SystemCache { get; } = new Dictionary<openplx.Physics.System, GameObject>();
    public Dictionary<openplx.Core.Object, GameObject> FrameCache { get; } = new Dictionary<openplx.Core.Object, GameObject>();
    public Dictionary<openplx.Physics.Charges.Material, ShapeMaterial> MaterialCache { get; } = new Dictionary<openplx.Physics.Charges.Material, ShapeMaterial>();
    public Dictionary<openplx.Physics3D.Charges.MateConnector, GameObject> MateConnectorCache { get; } = new Dictionary<openplx.Physics3D.Charges.MateConnector, GameObject>();
    public Dictionary<openplx.Physics.Interactions.Dissipation.DefaultFriction, FrictionModel> FrictionModelCache { get; } = new Dictionary<openplx.Physics.Interactions.Dissipation.DefaultFriction, FrictionModel>();

    public Dictionary<uint, Material> NativeMappedRenderMaterialCache { get; } = new Dictionary<uint, Material>();
    public Dictionary<openplx.Visuals.Materials.Material, Material> RenderMaterialCache { get; } = new Dictionary<openplx.Visuals.Materials.Material, Material> { };

    public List<ContactMaterial> MappedContactMaterials { get; } = new List<ContactMaterial>();
    public List<FrictionModel> MappedFrictionModels { get; } = new List<FrictionModel>();
    public List<Mesh> MappedMeshes { get; } = new List<Mesh>();
    public List<Material> MappedMaterials { get; } = new List<Material>();
    public List<TrackProperties> MappedTrackProperties { get; } = new List<TrackProperties>();
    public List<TrackInternalMergeProperties> MappedTrackInternalMergeProperties { get; } = new List<TrackInternalMergeProperties>();
  }
}
