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

    public Dictionary<Brick.Core.Object, RigidBody> BodyCache { get; } = new Dictionary<Brick.Core.Object, RigidBody>();
    public Dictionary<Brick.Physics.System, GameObject> SystemCache { get; } = new Dictionary<Brick.Physics.System, GameObject>();
    public Dictionary<Brick.Core.Object, GameObject> FrameCache { get; } = new Dictionary<Brick.Core.Object, GameObject>();
  }
}