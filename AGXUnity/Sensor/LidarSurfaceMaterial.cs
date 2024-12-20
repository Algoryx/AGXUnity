using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;
using UnityEditor;
using AGXUnity.Sensor;
using agxWire;
using AGXUnity.Utils;
using System;

namespace AGXUnity.Sensor
{
  public class LidarSurfaceMaterial : ScriptComponent
  {
    public bool PropagateToChildrenRecusively = false;
    public bool PropagateToSiblings = false;

    public LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;

    private void CreateContainer(GameObject gameObject)
    {
      var component = gameObject.GetOrCreateComponent<LidarSurfaceMaterialContainer>();
      component.LidarSurfaceMaterialDefinition = this.LidarSurfaceMaterialDefinition;
    }

    public void Init()
    {
      if (PropagateToChildrenRecusively)
        gameObject.TraverseChildren(CreateContainer);

      if (PropagateToSiblings && transform.parent != null)
      {
        foreach (Transform sibling in transform.parent)
          CreateContainer(sibling.gameObject);
      }
      else
        CreateContainer(gameObject);

      if (LidarSurfaceMaterialDefinition != null)
        LidarSurfaceMaterialDefinition.Init();
    }

    protected override bool Initialize()
    {
      return true;
    }
  }

  internal class LidarSurfaceMaterialContainer : MonoBehaviour
  {
    public LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;
  }
}