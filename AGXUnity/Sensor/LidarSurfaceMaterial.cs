using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;
using UnityEditor;
using AGXUnity.Sensor;

public class LidarSurfaceMaterial : ScriptComponent
{
  // TODO - as modeled by Unreal, for convenience this property could be elaborated to apply this material to only this, children or siblings. Would need some kind of placeholder invisible component that is probed by SensorEnv instead of this one though.
  //public Selection Selection;

  public  LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;

  protected override bool Initialize()
  {
    if (LidarSurfaceMaterialDefinition != null)
      LidarSurfaceMaterialDefinition.Init();

    return true;
  }
}
