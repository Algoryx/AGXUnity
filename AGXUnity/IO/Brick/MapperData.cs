using AGXUnity.Model;
using openplx;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public class MapperData
  {
    public GameObject RootNode { get; set; } = null;
    public Material VisualMaterial { get; set; } = null;
    public ErrorReporter ErrorReporter { get; set; } = null;
    public ShapeMaterial DefaultMaterial { get; set; } = null;

    public SavedPrefabLocalData PrefabLocalData { get; set; } = null;
    public agxopenplx.AgxCache AgxCache { get; } = new agxopenplx.AgxCache();

    public Dictionary<openplx.Physics.Charges.ContactGeometry, Collide.Shape> GeometryCache { get; } = new Dictionary<openplx.Physics.Charges.ContactGeometry, Collide.Shape>();
    public Dictionary<openplx.Core.Object, RigidBody> BodyCache { get; } = new Dictionary<openplx.Core.Object, RigidBody>();
    public Dictionary<openplx.Physics.System, GameObject> SystemCache { get; } = new Dictionary<openplx.Physics.System, GameObject>();
    public Dictionary<openplx.Core.Object, GameObject> FrameCache { get; } = new Dictionary<openplx.Core.Object, GameObject>();
    public Dictionary<openplx.Physics.Charges.Material, ShapeMaterial> MaterialCache { get; } = new Dictionary<openplx.Physics.Charges.Material, ShapeMaterial>();
    public Dictionary<openplx.Physics3D.Charges.MateConnector, GameObject> MateConnectorCache { get; } = new Dictionary<openplx.Physics3D.Charges.MateConnector, GameObject>();

    public Dictionary<uint, Material> MappedRenderMaterialCache { get; } = new Dictionary<uint, Material>();

    public List<ContactMaterial> ContactMaterials { get; } = new List<ContactMaterial>();
    public List<Mesh> CacheMappedMeshes { get; } = new List<Mesh>();
    public List<Material> CacheMappedMaterials { get; } = new List<Material>();
    public List<TrackProperties> CacheMappedTrackProperties { get; } = new List<TrackProperties>();
    public List<TrackInternalMergeProperties> CacheMappedTrackInternalMergeProperties { get; } = new List<TrackInternalMergeProperties>();
  }
}
