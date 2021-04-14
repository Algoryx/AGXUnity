using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.BrickUnity;

namespace AGXUnityEditor.BrickUnity {
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
        var new_ap = NewAttachmentPair;
        var old_ap = OldAttachmentPair;

        // ConnectedBody
        if ((Updates & UpdateParameters.ConnectedBody) == UpdateParameters.ConnectedBody)
        {
          var new_connected = new_ap.ConnectedObject;
          var old_connected = old_ap.ConnectedObject;
          if (new_connected != null)
          {
            var path = new_connected.GetComponent<BrickObject>().path;
            old_ap.ConnectedObject = GetGameObjectFromBrickPath(path, RootGameObject);
          }
          else if (old_connected != null)
          {
            old_ap.ConnectedObject = null;
          }
        }

        // ReferenceBody
        if ((Updates & UpdateParameters.ReferenceBody) == UpdateParameters.ReferenceBody)
        {
          var new_reference = new_ap.ReferenceObject;
          var path = new_reference.GetComponent<BrickObject>().path;
          old_ap.ReferenceObject = GetGameObjectFromBrickPath(path, RootGameObject);
        }

        // ReferenceFrame
        if ((Updates & UpdateParameters.ConnectedFrame) == UpdateParameters.ConnectedFrame)
          UpdateFrame(new_ap.ConnectedFrame, old_ap.ConnectedFrame);

        // ConnectedFrame
        if ((Updates & UpdateParameters.ReferenceFrame) == UpdateParameters.ReferenceFrame)
          UpdateFrame(new_ap.ReferenceFrame, old_ap.ReferenceFrame);
      }

      private void UpdateFrame(AGXUnity.ConstraintFrame new_frame, AGXUnity.ConstraintFrame old_frame)
      {
        if (new_frame.Parent != null)
        {
          var path = new_frame.Parent.GetComponent<BrickObject>().path;
          old_frame.SetParent(GetGameObjectFromBrickPath(path, RootGameObject), false);
        }
        else if (old_frame.Parent != null)
        {
          old_frame.SetParent(null, false);
        }
        old_frame.LocalPosition = new_frame.LocalPosition;
        old_frame.LocalRotation = new_frame.LocalRotation;
      }
    }


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
        UpdateConstraints();
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


    private void UpdateConstraints()
    {
      foreach (var apHolder in newConstraints)
      {
        apHolder.Update();
      }
    }


    private static GameObject GetGameObjectFromBrickPath(string path, GameObject rootGameObject)
    {
      var unityPath = path.Split(new[] { '.' }, 2)[1].Replace('.', '/');
      var go = rootGameObject.transform.Find(unityPath);

      //if (body is null)
      //{
      //  throw new AGXUnity.Exception($"Failed to find connected body with path {unityPath}");
      //}

      //if (body.GetComponent<AGXUnity.RigidBody>() is null)
      //{
      //  throw new AGXUnity.Exception($"ConnectedBody GameObject with path {unityPath} lacks a RigidBody component.");
      //}
      return go.gameObject;
    }

    private void Compare(GameObject go_new, GameObject go_old)
    {
      var new_children = go_new.GetComponents<BrickObject>();
      var shouldUpdate = true;
      foreach (var new_child in new_children)
      {
        var old_child = go_old.transform.Find(new_child.name);

        // Create a new child if it doesn't exist yet
        if (old_child != null && old_child.GetComponent<BrickObject>() == null)
          throw new AGXUnity.Exception($"The new Brick object with path {new_child.path} has an old GameObject which is not a Brick object. Remove or rename the old GameObject and try to reload again.");
        else if (old_child is null)
        {
          old_child = Object.Instantiate(new_child, go_old.transform).transform;
          var old_constraint = old_child.GetComponent<AGXUnity.Constraint>();
          if (old_constraint != null) {
            var new_constraint = new_child.GetComponent<AGXUnity.Constraint>();
            this.newConstraints.Add( new AttachmentPairUpdater(
              UpdateParameters.ConnectedBody | UpdateParameters.ReferenceBody,
              old_constraint.AttachmentPair,
              new_constraint.AttachmentPair,
              this.runtimeComponent.gameObject
            ) );
          }
          shouldUpdate = false;
        }

        Compare(new_child.gameObject, old_child.gameObject);
      }

      go_old.transform.localPosition = go_new.transform.localPosition;
      go_old.transform.localRotation = go_new.transform.localRotation;

      if (!shouldUpdate)
        return;

      var brickType = go_old.GetComponent<BrickObject>().type;
      var brickPath = go_old.GetComponent<BrickObject>().path;

      if (brickType.StartsWith("Brick.Physics.Mechanics.Geometry"))
      {
        //Handle geometries
        var old_shape = go_old.GetComponent<AGXUnity.Collide.Shape>();
        var new_shape = go_new.GetComponent<AGXUnity.Collide.Shape>();
        switch (old_shape)
        {
          case AGXUnity.Collide.Box old_box:
            var new_box = new_shape as AGXUnity.Collide.Box;
            old_box.HalfExtents = new_box.HalfExtents;
            break;
          case AGXUnity.Collide.Capsule old_capsule:
            var new_capsule = new_shape as AGXUnity.Collide.Capsule;
            old_capsule.Radius = new_capsule.Radius;
            old_capsule.Height = new_capsule.Height;
            break;
          case AGXUnity.Collide.Cone old_cone:
            var new_cone = new_shape as AGXUnity.Collide.Cone;
            old_cone.Height = new_cone.Height;
            old_cone.BottomRadius = new_cone.BottomRadius;
            old_cone.TopRadius = new_cone.TopRadius;
            break;
          case AGXUnity.Collide.Cylinder old_cylinder:
            var new_cylinder = new_shape as AGXUnity.Collide.Cylinder;
            old_cylinder.Radius = new_cylinder.Radius;
            old_cylinder.Height = new_cylinder.Height;
            break;
          case AGXUnity.Collide.HollowCone old_hollowCone:
            var new_hollowCone = new_shape as AGXUnity.Collide.HollowCone;
            old_hollowCone.Height = new_hollowCone.Height;
            old_hollowCone.BottomRadius = new_hollowCone.BottomRadius;
            old_hollowCone.TopRadius = new_hollowCone.TopRadius;
            break;
          case AGXUnity.Collide.HollowCylinder old_hollowCylinder:
            var new_hollowCylinder = new_shape as AGXUnity.Collide.HollowCylinder;
            old_hollowCylinder.Radius = new_hollowCylinder.Radius;
            old_hollowCylinder.Height = new_hollowCylinder.Height;
            old_hollowCylinder.Thickness = new_hollowCylinder.Thickness;
            break;
          case AGXUnity.Collide.Sphere old_sphere:
            var new_sphere = new_shape as AGXUnity.Collide.Sphere;
            old_sphere.Radius = new_sphere.Radius;
            break;
          default:
            break;
        }
      }
      else if (brickType == "Brick.Physics.Mechanics.RigidBody")
      {
        var old_body = go_old.GetComponent<AGXUnity.RigidBody>();
        var new_body = go_new.GetComponent<AGXUnity.RigidBody>();
        old_body.MotionControl = new_body.MotionControl;

        var old_mp = old_body.MassProperties;
        var new_mp = new_body.MassProperties;
        old_mp.CenterOfMassOffset = new_mp.CenterOfMassOffset;
        old_mp.InertiaDiagonal = new_mp.InertiaDiagonal;
        old_mp.Mass = new_mp.Mass;
      }
      else if (brickType.StartsWith("Brick.Visual"))
      {
        //Handle visuals
        Debug.LogWarning("Visuals can currently not be updated");
      }
      else if (brickType.StartsWith("Brick.Physics.Mechanics") && brickType.EndsWith("Connector"))
      {
        // Handle connectors
        var old_constraint = go_old.GetComponent<AGXUnity.Constraint>();
        var new_constraint = go_new.GetComponent<AGXUnity.Constraint>();
        var old_ecs = old_constraint.ElementaryConstraints;
        var new_ecs = new_constraint.ElementaryConstraints;
        if (old_constraint.Type != new_constraint.Type)
        {
          Debug.LogError($"Old constraint type ({old_constraint.Type}) does not match new constraint type ({new_constraint.Type}) for Brick connector {brickPath}. Constraint will not be updated. Manually change the type of the old constraint to the same type as the old and try again.");
          return;
        }

        var old_ap = old_constraint.AttachmentPair;
        var old_connectedBody = old_ap.ConnectedObject;

        var new_ap = new_constraint.AttachmentPair;
        var new_connectedBody = old_ap.ConnectedObject;

        // Check connectedBody
        var updateHolder = new AttachmentPairUpdater(UpdateParameters.None, old_ap, new_ap, this.runtimeComponent.gameObject);
        if (new_connectedBody is null && old_connectedBody is null)
        {
          // Do nothing
        } else if (new_connectedBody is null && old_connectedBody != null)
        {
          old_ap.ConnectedObject = null;
        } else if (new_connectedBody != null && old_connectedBody is null)
        {
          updateHolder.Updates |= UpdateParameters.ConnectedBody;
        }

        // Check referenceBody
        var new_connectedBrickObject = new_connectedBody.GetComponent<BrickObject>();
        var old_connectedBrickObject = old_connectedBody.GetComponent<BrickObject>();
        if (old_connectedBrickObject.path != new_connectedBrickObject.path)
        {
          updateHolder.Updates |= UpdateParameters.ReferenceBody;
        }

        // Update frames
        {
          var old_referenceFrame = old_ap.ReferenceFrame;
          var new_referenceFrame = new_ap.ReferenceFrame;
          var old_path = old_referenceFrame.Parent.GetComponent<BrickObject>().path;
          var new_path = new_referenceFrame.Parent.GetComponent<BrickObject>().path;
          if (new_path != old_path)
            updateHolder.Updates |= UpdateParameters.ReferenceFrame;
        }

        {
          var old_connectedFrame = old_ap.ConnectedFrame;
          var new_connectedFrame = new_ap.ConnectedFrame;
          var old_path = old_connectedFrame.Parent.GetComponent<BrickObject>().path;
          var new_path = new_connectedFrame.Parent.GetComponent<BrickObject>().path;
          if (new_path != old_path)
            updateHolder.Updates |= UpdateParameters.ConnectedFrame;
        }


        if (updateHolder.Updates != UpdateParameters.None)
        {
          newConstraints.Add(updateHolder);
        }


        // Update elementary constraints
        for (int i = 0; i < old_ecs.Length; i++)
        {
          var old_ec = old_ecs[i];
          var new_ec = new_ecs[i];
          old_ec.CopyFrom(new_ec);
        }
      }
    }


    private static void CopyLocalTransform()
    {

    }


    private static void RemoveOld(GameObject go_new, GameObject go_old)
    {
      var old_children = go_old.GetComponents<BrickObject>();
      foreach (var old_child in old_children)
      {
        var new_child = go_new.transform.Find(old_child.name);
        if (new_child == null)
        {
          Debug.Log($"New child corresponding to {old_child.path}");
          Object.DestroyImmediate(old_child);
          continue;
        }

        var new_child_bo = new_child.GetComponent<BrickObject>();
        if (new_child_bo == null)
        {
          Debug.Log($"New child {new_child.name} does not have BrickObject corresponding to {old_child.path}. Destroying old object.");
          Object.DestroyImmediate(old_child);
          continue;
        }

        if (new_child_bo.path != old_child.path)
        {
          Debug.LogError($"Brick path of new child ({new_child_bo.path} is not the same as the old child {old_child.path}");
          Object.DestroyImmediate(old_child);
          continue;
        }

        RemoveOld(new_child.gameObject, old_child.gameObject);
      }
    }
  }
}