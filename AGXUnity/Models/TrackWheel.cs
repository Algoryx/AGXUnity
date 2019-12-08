using UnityEngine;

namespace AGXUnity.Models
{
  public class TrackWheel : ScriptComponent
  {
    public agxVehicle.TrackWheel Native { get; private set; } = null;

    public RigidBody RigidBody
    {
      get { return GetComponent<RigidBody>(); }
    }

    protected override bool Initialize()
    {
      var rb = GetComponent<RigidBody>();
      if ( rb == null ) {
        Debug.LogError( "Component: TrackWheel requires RigidBody component.", this );
        return false;
      }

      if ( rb.GetInitialized<RigidBody>() == null )
        return false;

      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }

    private void Reset()
    {
      if ( GetComponent<RigidBody>() == null )
        Debug.LogError( "Component: TrackWheel requires RigidBody component.", this );
    }
  }
}
