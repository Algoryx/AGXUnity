using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;
using System.Linq;
using AGXUnity.Utils;


namespace AGXUnity {

  /// <summary>
  /// This class manages the relative transformation between RigidBodies in a hiearchial tree of bodies.
  /// Each RigidBody part of the tree must have RelativeTransform == true. Which means that the transformation update
  /// from Native -> Unity is handled by this class.
  /// </summary>
  [AddComponentMenu("AGXUnity/Articulated RigidBody Base")]
  [DisallowMultipleComponent]
  public class ArticulatedRigidBodyBase : ScriptComponent
  {

    [InspectorGroupBegin(Name = "Articulated Bodies")]
    [AllowRecursiveEditing]

    public RigidBody[] ArticulatedBodies
    {
      get
      {
        return m_articulatedBodies;
      }
    }
 
    private RigidBody[] m_articulatedBodies;
    
    protected override bool Initialize()
    {
      // Collect all bodies as part of this hierarchy 
      m_articulatedBodies = GetComponentsInChildren<RigidBody>();
      foreach (var b in m_articulatedBodies)
        b.RelativeTransform = true;

      HandleUpdateCallbacks(true);

      return true;
    }

    private void HandleUpdateCallbacks(bool enable)
    {
      if (enable)
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransformsCallback;
      else
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransformsCallback;
    }

    protected override void OnEnable()
    {
      HandleUpdateCallbacks(true);
    }

    protected override void OnDisable()
    {
      HandleUpdateCallbacks(false);
    }

    /// <summary>
    /// Because all bodies being part of this Hiearchy should have RigidBody.RelativeTransform == true
    /// The update of their transform is handled by this callback.
    /// </summary>
    void OnPostSynchronizeTransformsCallback()
    {
      foreach(var b in m_articulatedBodies)
      {
        var pos = b.Native.getPosition().ToHandedVector3();
        var rot = b.Native.getRotation().ToHandedQuaternion();

        var parent = b.gameObject.transform.parent;

        // Update the Unity transformation based on the relative transformation of the parent for each RigidBody
        if (b.RelativeTransform && parent)
        {
          var t = parent.worldToLocalMatrix;
          rot = t.rotation * rot;
          pos = parent.transform.InverseTransformPoint(pos);
          b.SyncUnityTransform(pos, rot);
        }
        else
          b.transform.SetPositionAndRotation(pos, rot);
      }
    }
  }
}