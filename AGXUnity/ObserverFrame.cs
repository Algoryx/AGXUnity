using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Observer frame object mainly created in other applications
  /// to communicate reference transforms. This object is often
  /// only created when reading .agx files.
  /// </summary>
  [AddComponentMenu("AGXUnity/Observer Frame")]
  public class ObserverFrame : ScriptComponent
  {
    /// <summary>
    /// Native instance of the observer frame - created in Start/Initialize if valid.
    /// </summary>
    public agx.ObserverFrame Native { get; private set; } = null;

    public void RestoreLocalDataFrom(agx.ObserverFrame native, GameObject parent)
    {
      transform.SetParent(parent != null ? parent.transform : null);
      transform.position = native.getPosition().ToHandedVector3();
      transform.rotation = native.getRotation().ToHandedQuaternion();
    }

    protected override bool Initialize()
    {
      Native = new agx.ObserverFrame();

      Native.setName(name);

      var rb = gameObject.GetInitializedComponentInParent<RigidBody>();
      Native.attachWithWorldTransform(rb != null ? rb.Native : null,
                                       new agx.AffineMatrix4x4(transform.rotation.ToHandedQuat(),
                                                                transform.position.ToHandedVec3()));

      GetSimulation().add(Native);

      return true;
    }

    protected override void OnDestroy()
    {
      if (Simulation.HasInstance)
        GetSimulation().remove(Native);
      
      Native = null;
      base.OnDestroy();
    }
  }
}
