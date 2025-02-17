using agxSensor;
using AGXUnity.Collide;
using AGXUnity.Model;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensor" )]
  [DisallowMultipleComponent]
  public class LidarSurfaceMaterial : ScriptComponent
  {
    public bool PropagateToChildrenRecusively = false;

    public LidarSurfaceMaterialDefinition LidarSurfaceMaterialDefinition = null;

    protected override bool Initialize()
    {
      if ( LidarSurfaceMaterialDefinition != null )
        LidarSurfaceMaterialDefinition.GetInitialized<LidarSurfaceMaterialDefinition>();

      SetNativeMaterial( gameObject );
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

    private void SetNativeMaterial( GameObject obj )
    {
      // Set material in the case where this material was added after initializing the SensorEnvironment
      // Any additional material handling added here should be added in SensorEnvironment as well to
      // properly handle both cases.
      if ( SensorEnvironment.HasInstance && SensorEnvironment.Instance.Native != null ) {
        if ( obj.TryGetComponent<LidarSurfaceMaterial>( out _ ) && obj != gameObject )
          return;

        foreach ( var mesh in obj.GetComponents<MeshFilter>() )
          SensorEnvironment.Instance.SetMaterialForMeshFilter( mesh, this );
        foreach ( var terrain in obj.GetComponents<DeformableTerrain>() )
          RtSurfaceMaterial.set( terrain.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );
        foreach ( var terrain in obj.GetComponents<DeformableTerrainPager>() )
          RtSurfaceMaterial.set( terrain.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );
        foreach ( var hf in obj.GetComponents<HeightField>() )
          RtSurfaceMaterial.set( hf.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );
        foreach ( var cable in obj.GetComponents<Cable>() )
          RtSurfaceMaterial.set( cable.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );
        foreach ( var wire in obj.GetComponents<Wire>() )
          RtSurfaceMaterial.set( wire.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );
        foreach ( var track in obj.GetComponents<Track>() )
          RtSurfaceMaterial.set( track.Native, LidarSurfaceMaterialDefinition.GetRtMaterial() );

        if ( PropagateToChildrenRecusively )
          foreach ( Transform child in obj.transform )
            SetNativeMaterial( child.gameObject );
      }
    }
  }
}
