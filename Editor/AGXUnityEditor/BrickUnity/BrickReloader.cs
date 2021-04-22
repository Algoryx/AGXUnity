using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.BrickUnity;

namespace AGXUnityEditor.BrickUnity {

  /// <summary>
  /// Class for reloading BrickObjects.
  ///
  /// Usage:
  ///   var reloader = new BrickReloader(brickRuntimeComponent);
  ///   reloader.Reload();
  /// </summary>
  public class BrickReloader
  {
    private readonly BrickRuntimeComponent runtimeComponent;
    private readonly List<AttachmentPairUpdater> newConstraints = new List<AttachmentPairUpdater>();

    public BrickReloader(BrickRuntimeComponent runtimeComponent)
    {
      this.runtimeComponent = runtimeComponent;
    }

    [System.Flags]
    private enum UpdateParameters
    {
      None = 0,
      ReferenceBody = 1,
      ConnectedBody = 2,
      ReferenceFrame = 4,
      ConnectedFrame = 8
    }


    private class AttachmentPairUpdater
    {
      public AttachmentPairUpdater(UpdateParameters updateParameters,
                                        AGXUnity.AttachmentPair oldAttachmentPair,
                                        AGXUnity.AttachmentPair newAttachmentPair,
                                        GameObject rootGameObject
                                        )
      {
        Updates = updateParameters;
        OldAttachmentPair = oldAttachmentPair;
        NewAttachmentPair = newAttachmentPair;
        RootGameObject = rootGameObject;
      }

      public UpdateParameters Updates { get; set; }
      public AGXUnity.AttachmentPair OldAttachmentPair { get; private set; }
      public AGXUnity.AttachmentPair NewAttachmentPair { get; private set; }
      private readonly GameObject RootGameObject;

      public void Update()
      {
        var newAp = NewAttachmentPair;
        var oldAp = OldAttachmentPair;

        // ConnectedBody
        if ((Updates & UpdateParameters.ConnectedBody) == UpdateParameters.ConnectedBody)
        {
          var newConnected = newAp.ConnectedObject;
          var oldConnected = oldAp.ConnectedObject;
          if (newConnected != null)
          {
            var path = newConnected.GetComponent<BrickObject>().path;
            oldAp.ConnectedObject = GetGameObjectFromBrickPath(path, RootGameObject);
          }
          else if (oldConnected != null)
          {
            oldAp.ConnectedObject = null;
          }
        }

        // ReferenceBody
        if ((Updates & UpdateParameters.ReferenceBody) == UpdateParameters.ReferenceBody)
        {
          var newReference = newAp.ReferenceObject;
          var path = newReference.GetComponent<BrickObject>().path;
          oldAp.ReferenceObject = GetGameObjectFromBrickPath(path, RootGameObject);
        }

        // ReferenceFrame
        if ((Updates & UpdateParameters.ConnectedFrame) == UpdateParameters.ConnectedFrame)
          UpdateFrame(newAp.ConnectedFrame, oldAp.ConnectedFrame);

        // ConnectedFrame
        if ((Updates & UpdateParameters.ReferenceFrame) == UpdateParameters.ReferenceFrame)
          UpdateFrame(newAp.ReferenceFrame, oldAp.ReferenceFrame);
      }

      private void UpdateFrame(AGXUnity.ConstraintFrame newFrame, AGXUnity.ConstraintFrame oldFrame)
      {
        if (newFrame.Parent != null)
        {
          var path = newFrame.Parent.GetComponent<BrickObject>().path;
          oldFrame.SetParent(GetGameObjectFromBrickPath(path, RootGameObject), false);
        }
        else if (oldFrame.Parent != null)
        {
          oldFrame.SetParent(null, false);
        }
        oldFrame.LocalPosition = newFrame.LocalPosition;
        oldFrame.LocalRotation = newFrame.LocalRotation;
      }
    }


    /// <summary>
    /// Reload the Brick file associated with this Reloader.
    /// </summary>
    public void Reload()
    {
      // Get the file path of the Brick object
      var filepath = runtimeComponent.filePath;
      var modelName = runtimeComponent.modelName;

      // Read the file into a new GameObject hierarchy
      var importer = new BrickPrefabImporter();
      var go_new = importer.ImportFile(filepath, modelName);
      var go_old = runtimeComponent.gameObject;

      // Make sure the new root GameObject has the same position as this one
      go_new.transform.localPosition = go_old.transform.localPosition;
      go_new.transform.localRotation = go_old.transform.localRotation;

      // Compare the new and old hierarchies
      try
      {
        RemoveOld(go_new, go_old);
        Compare(go_new, go_old);
        UpdateAttachmentPairs();
      }
      catch
      {
        Debug.LogError("Failed to reload Brick object!");
        throw;
      }
      finally
      {
        Object.DestroyImmediate(go_new);
      }
    }


    // Update AttachmentPairs that were saved during the Compare phase. This has to be done as a separate step
    // since the AttachmentPairs might refer to bodies not yet created.
    private void UpdateAttachmentPairs()
    {
      foreach (var apHolder in newConstraints)
      {
        apHolder.Update();
      }
    }


    // Get a GameObject from a Brick path (e.g. path.to.brick.object)
    private static GameObject GetGameObjectFromBrickPath(string path, GameObject rootGameObject)
    {
      var unityPath = path.Split(new[] { '.' }, 2)[1].Replace('.', '/');
      var go = rootGameObject.transform.Find(unityPath);
      return go.gameObject;
    }


    // Check if a ConstraintFrame should update. I.e. if their parents point to the same body
    private static bool ShouldUpdateFrame(AGXUnity.ConstraintFrame oldFrame, AGXUnity.ConstraintFrame newFrame)
    {
      var oldParent = oldFrame.Parent;
      var newParent = newFrame.Parent;
      if (oldParent != null && oldParent != null)
      {
        if (oldParent.GetComponent<BrickObject>().path != newParent.GetComponent<BrickObject>().path)
          return true;
        return false;
      }
      else if (oldParent == null && newParent != null)
      {
        return true;
      }
      else if (oldParent != null && newParent == null)
      {
        return true;
      }

      return false;  // Both are null
    }


    // Compare 2 Brick GameObjects
    // This is done by recursively going through all the old GameObject's children and comparing them to the
    // corresponding new GameObject
    // TODO: Handle Visuals
    private void Compare(GameObject go_new, GameObject go_old)
    {
      var shouldUpdate = true;
      foreach (Transform newChild in go_new.transform)
      {
        var newBrickObject = newChild.GetComponent<BrickObject>();
        if (newBrickObject == null)
          continue;

        var oldChild = go_old.transform.Find(newChild.name);

        // Create a new child if it doesn't exist yet
        if (oldChild != null && oldChild.GetComponent<BrickObject>() == null)
          throw new AGXUnity.Exception($"The new Brick object with path {newBrickObject.path} has an old GameObject which is not a Brick object. Remove or rename the old GameObject and try to reload again.");
        else if (oldChild == null)
        {
          oldChild = Object.Instantiate(newChild, go_old.transform).transform;
          var oldConstraint = oldChild.GetComponent<AGXUnity.Constraint>();
          if (oldConstraint != null) {
            var newConstraint = newChild.GetComponent<AGXUnity.Constraint>();
            this.newConstraints.Add( new AttachmentPairUpdater(
              UpdateParameters.ConnectedBody | UpdateParameters.ReferenceBody,
              oldConstraint.AttachmentPair,
              newConstraint.AttachmentPair,
              this.runtimeComponent.gameObject
            ) );
          }
          shouldUpdate = false;
        }

        Compare(newChild.gameObject, oldChild.gameObject);
      }

      go_old.transform.localPosition = go_new.transform.localPosition;
      go_old.transform.localRotation = go_new.transform.localRotation;

      if (!shouldUpdate)
        return;

      var brickType = go_old.GetComponent<BrickObject>().type;
      var brickPath = go_old.GetComponent<BrickObject>().path;

      if (brickType.StartsWith("Brick.Physics.Geometry"))
      {
        //Handle geometries
        var oldShape = go_old.GetComponent<AGXUnity.Collide.Shape>();
        var newShape = go_new.GetComponent<AGXUnity.Collide.Shape>();
        switch (oldShape)
        {
          case AGXUnity.Collide.Box oldBox:
            var newBox = newShape as AGXUnity.Collide.Box;
            oldBox.HalfExtents = newBox.HalfExtents;
            break;
          case AGXUnity.Collide.Capsule oldCapsule:
            var newCapsule = newShape as AGXUnity.Collide.Capsule;
            oldCapsule.Radius = newCapsule.Radius;
            oldCapsule.Height = newCapsule.Height;
            break;
          case AGXUnity.Collide.Cone oldCone:
            var newCone = newShape as AGXUnity.Collide.Cone;
            oldCone.Height = newCone.Height;
            oldCone.BottomRadius = newCone.BottomRadius;
            oldCone.TopRadius = newCone.TopRadius;
            break;
          case AGXUnity.Collide.Cylinder oldCylinder:
            var newCylinder = newShape as AGXUnity.Collide.Cylinder;
            oldCylinder.Radius = newCylinder.Radius;
            oldCylinder.Height = newCylinder.Height;
            break;
          case AGXUnity.Collide.HollowCone oldHollowCone:
            var newHollowCone = newShape as AGXUnity.Collide.HollowCone;
            oldHollowCone.Height = newHollowCone.Height;
            oldHollowCone.BottomRadius = newHollowCone.BottomRadius;
            oldHollowCone.TopRadius = newHollowCone.TopRadius;
            break;
          case AGXUnity.Collide.HollowCylinder oldHollowCylinder:
            var newHollowCylinder = newShape as AGXUnity.Collide.HollowCylinder;
            oldHollowCylinder.Radius = newHollowCylinder.Radius;
            oldHollowCylinder.Height = newHollowCylinder.Height;
            oldHollowCylinder.Thickness = newHollowCylinder.Thickness;
            break;
          case AGXUnity.Collide.Sphere oldSphere:
            var newSphere = newShape as AGXUnity.Collide.Sphere;
            oldSphere.Radius = newSphere.Radius;
            break;
          default:
            break;
        }
      }
      else if (brickType == "Brick.Physics.Mechanics.RigidBody")
      {
        var oldBody = go_old.GetComponent<AGXUnity.RigidBody>();
        var newBody = go_new.GetComponent<AGXUnity.RigidBody>();
        oldBody.MotionControl = newBody.MotionControl;

        var oldMP = oldBody.MassProperties;
        var newMP = newBody.MassProperties;
        oldMP.CenterOfMassOffset = newMP.CenterOfMassOffset;
        oldMP.InertiaDiagonal = newMP.InertiaDiagonal;
        oldMP.Mass = newMP.Mass;
      }
      else if (brickType.StartsWith("Brick.Visual"))
      {
        //Handle visuals
        Debug.LogWarning("Visuals can currently not be updated");
      }
      else if (brickType.StartsWith("Brick.Physics.Mechanics") && brickType.EndsWith("Connector"))
      {
        // Handle connectors
        var oldConstraint = go_old.GetComponent<AGXUnity.Constraint>();
        var newConstraint = go_new.GetComponent<AGXUnity.Constraint>();
        if (oldConstraint.Type != newConstraint.Type)
        {
          Debug.LogError($"Old constraint type ({oldConstraint.Type}) does not match new constraint type ({newConstraint.Type}) for Brick connector {brickPath}. Constraint will not be updated. Manually change the type of the old constraint to the same type as the old and try again.");
          return;
        }

        var oldAP = oldConstraint.AttachmentPair;
        var oldConnectedBody = oldAP.ConnectedObject;

        var newAP = newConstraint.AttachmentPair;
        var newConnectedBody = oldAP.ConnectedObject;

        // Check connectedBody
        var updateHolder = new AttachmentPairUpdater(UpdateParameters.None, oldAP, newAP, this.runtimeComponent.gameObject);
        if (newConnectedBody == null && oldConnectedBody != null)
        {
          oldAP.ConnectedObject = null;
        } else if (newConnectedBody != null && oldConnectedBody == null)
        {
          updateHolder.Updates |= UpdateParameters.ConnectedBody;
        }

        // Check referenceBody
        var newReferenceBrickObject = newAP.ReferenceObject.GetComponent<BrickObject>();
        var oldReferenceBrickObject = oldAP.ReferenceObject.GetComponent<BrickObject>();
        if (oldReferenceBrickObject.path != newReferenceBrickObject.path)
        {
          updateHolder.Updates |= UpdateParameters.ReferenceBody;
        }

        // Update frames
        if (ShouldUpdateFrame(oldAP.ReferenceFrame, newAP.ReferenceFrame))
          updateHolder.Updates |= UpdateParameters.ReferenceFrame;
        if (ShouldUpdateFrame(oldAP.ConnectedFrame, newAP.ConnectedFrame))
          updateHolder.Updates |= UpdateParameters.ConnectedFrame;

        if (updateHolder.Updates != UpdateParameters.None)
        {
          newConstraints.Add(updateHolder);
        }


        // Update elementary constraints
        var oldECs = oldConstraint.ElementaryConstraints;
        var newECs = newConstraint.ElementaryConstraints;
        for (int i = 0; i < oldECs.Length; i++)
        {
          var oldEC = oldECs[i];
          var newEC = newECs[i];
          oldEC.CopyFrom(newEC);
        }
      }
    }


    // Remove Brick GameObjects that exist in the old structure but not the new.
    // This is done by recursively iterating through the new GameObject's children and making sure that they exist in
    // the new one as well.
    private static void RemoveOld(GameObject go_new, GameObject go_old)
    {
      foreach (Transform old_child in go_old.transform)
      {
        var old_brickObject = old_child.GetComponent<BrickObject>();
        if (old_brickObject == null)
          continue;

        var new_child = go_new.transform.Find(old_child.name);
        if (new_child == null)
        {
          Debug.Log($"New child corresponding to {old_brickObject.path}");
          Object.DestroyImmediate(old_child);
          continue;
        }

        var new_child_bo = new_child.GetComponent<BrickObject>();
        if (new_child_bo == null)
        {
          Debug.Log($"New child {new_child.name} does not have BrickObject corresponding to {old_brickObject.path}. Destroying old object.");
          Object.DestroyImmediate(old_child);
          continue;
        }

        if (new_child_bo.path != old_brickObject.path)
        {
          Debug.LogError($"Brick path of new child ({new_child_bo.path} is not the same as the old child {old_brickObject.path}");
          Object.DestroyImmediate(old_child);
          continue;
        }

        RemoveOld(new_child.gameObject, old_child.gameObject);
      }
    }
  }
}