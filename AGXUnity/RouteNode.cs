using System;
using UnityEngine;

namespace AGXUnity
{
  public interface IExtraNodeData
  {
    public abstract bool Initialize( RouteNode parent );
  }

  public class NoExtraData : IExtraNodeData
  {
    public bool Initialize( RouteNode parent ) { return true; }
  }

  [Serializable]
  public abstract class RouteNode : IFrame
  {
    [field: SerializeReference]
    public IExtraNodeData NodeData { get; protected set; } = null;
  }
}
