using AGXUnity.Model;
using Brick;
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
    public BrickAgx.AgxCache AgxCache { get; } = new BrickAgx.AgxCache();

    public Dictionary<Brick.Physics.Charges.ContactGeometry, Collide.Shape> GeometryCache { get; } = new Dictionary<Brick.Physics.Charges.ContactGeometry, Collide.Shape>();
    public Dictionary<Brick.Core.Object, RigidBody> BodyCache { get; } = new Dictionary<Brick.Core.Object, RigidBody>();
    public Dictionary<Brick.Physics.System, GameObject> SystemCache { get; } = new Dictionary<Brick.Physics.System, GameObject>();
    public Dictionary<Brick.Core.Object, GameObject> FrameCache { get; } = new Dictionary<Brick.Core.Object, GameObject>();
    public Dictionary<Brick.Physics.Charges.Material, ShapeMaterial> MaterialCache { get; } = new Dictionary<Brick.Physics.Charges.Material, ShapeMaterial>();
    public Dictionary<Brick.Physics3D.Charges.MateConnector, GameObject> MateConnectorCache { get; } = new Dictionary<Brick.Physics3D.Charges.MateConnector, GameObject>();

    public Dictionary<uint, Material> MappedRenderMaterialCache { get; } = new Dictionary<uint, Material>();

    public List<ContactMaterial> ContactMaterials { get; } = new List<ContactMaterial>();
    public List<Mesh> CacheMappedMeshes { get; } = new List<Mesh>();
    public List<Material> CacheMappedMaterials { get; } = new List<Material>();
    public List<TrackProperties> CacheMappedTrackProperties { get; } = new List<TrackProperties>();
    public List<TrackInternalMergeProperties> CacheMappedTrackInternalMergeProperties { get; } = new List<TrackInternalMergeProperties>();
  }
}