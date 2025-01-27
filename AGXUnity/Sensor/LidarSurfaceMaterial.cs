using UnityEngine;

namespace AGXUnity.Sensor
{
  public class LidarSurfaceMaterial : ScriptComponent
  {
    public bool PropagateToChildrenRecusively = false;

    public LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;

    protected override bool Initialize()
    {
      if ( LidarSurfaceMaterialDefinition != null )
        LidarSurfaceMaterialDefinition.GetInitialized<LidarSurfaceMaterialDefinition>();
      return true;
    }

    public static LidarSurfaceMaterialDefinition FindClosestMaterial( GameObject target )
    {
      var current = target;

      while ( current != null ) {
        if ( current.TryGetComponent<LidarSurfaceMaterial>( out var mat ) ) {
          mat.GetInitialized<LidarSurfaceMaterial>();
          if ( current == target || mat.PropagateToChildrenRecusively )
            return mat.LidarSurfaceMaterialDefinition;
        }
        current = current.transform?.parent?.gameObject;
      }
      return null;
    }
  }
}
