using System;
using UnityEngine;

namespace AGXUnity
{
  public interface IExtraNodeData
  {
    public abstract bool Initialize( RouteNode parent );
  }

  [Serializable]
  public abstract class RouteNode : IFrame
  {
    [field: SerializeReference]
    public IExtraNodeData NodeData { get; protected set; } = null;
  }
}
