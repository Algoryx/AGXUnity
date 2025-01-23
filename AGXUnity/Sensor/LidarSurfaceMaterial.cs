using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Sensor
{
  public class LidarSurfaceMaterial : ScriptComponent
  {
    public bool PropagateToChildrenRecusively = false;

    public LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;

    private void CreateContainer( GameObject gameObject )
    {
      var component = gameObject.GetOrCreateComponent<LidarSurfaceMaterialContainer>();
      component.LidarSurfaceMaterialDefinition = this.LidarSurfaceMaterialDefinition;
    }

    public void Init()
    {
      if ( PropagateToChildrenRecusively )
        gameObject.TraverseChildren( CreateContainer );

      CreateContainer( gameObject );

      if ( LidarSurfaceMaterialDefinition != null )
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
