using UnityEngine;

using B_Mechanics = Brick.Physics.Mechanics;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;

namespace AGXUnity.BrickUnity
{
  public static class BrickExtensions
  {
    /// <summary>
    /// Direct convert from Brick.Math.Vec3 to Vector3.
    /// </summary>
    /// <seealso cref="ToHandedVector3(Brick.Math.Vec3)"/>
    public static Vector3 ToVector3(this Brick.Math.Vec3 v)
    {
      return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }

    /// <summary>
    /// Convert from Brick.Math.Vec3 to Vector3 - flipping x axis, transforming
    /// from left/right handed to right/left handed coordinate system.
    /// </summary>
    /// <seealso cref="ToVector3(Brick.Math.Vec3)"/>
    public static Vector3 ToHandedVector3(this Brick.Math.Vec3 v)
    {
      return new Vector3(-(float)v.X, (float)v.Y, (float)v.Z);
    }

    /// <summary>
    /// Converts a left/right handed Brick.Math.Quat to a right/left handed Quaternion.
    /// </summary>
    public static Quaternion ToHandedQuaternion(this Brick.Math.Quat q)
    {
      return new Quaternion(-(float)q.X, (float)q.Y, (float)q.Z, -(float)q.W);
    }

    /// <summary>
    /// Directly converts a Brick.Math.Quat to a Quaternion, without taking handedness into account.
    /// </summary>
    public static Quaternion ToQuaternion(this Brick.Math.Quat q)
    {
      return new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }

    public static agx.RigidBody.MotionControl ToAgxMotionControl(this Brick.Physics.MotionControl b_mc)
    {
      switch (b_mc)
      {
        case Brick.Physics.MotionControl.Dynamics:
          return agx.RigidBody.MotionControl.DYNAMICS;
        case Brick.Physics.MotionControl.Static:
          return agx.RigidBody.MotionControl.STATIC;
        case Brick.Physics.MotionControl.Kinematics:
          return agx.RigidBody.MotionControl.KINEMATICS;
        default:
          throw new AGXUnity.Exception($"Could not Convert Brick motion control to AGX motion control. Unknown type: {b_mc}");
      }
    }

    public static Color ToUnityColor(this Brick.Math.Vec3 b_v)
    {
      var u_c = new Color
      {
        r = (float)b_v.X,
        g = (float)b_v.Y,
        b = (float)b_v.Z,
        a = 1
      };
      return u_c;
    }

    public static void SetLocalTransformFromBrick(this GameObject go, Brick.Scene.Node b_node)
    {
      go.transform.SetLocalFromBrick(b_node.LocalTransform);
    }

    public static void SetLocalFromBrick(this Transform u_transform, Brick.Scene.Transform b_transform)
    {
      u_transform.localPosition = b_transform.Position.ToHandedVector3();
      u_transform.localRotation = b_transform.Rotation.ToHandedQuaternion()*u_transform.localRotation;
    }

    public static BrickObject AddBrickObject(this GameObject go, Brick.Object b_object, GameObject go_parent = null)
    {
      var brickObject = go.AddComponent<BrickObject>();
      brickObject.SetObject(b_object, go_parent);
      return brickObject;
    }

    public static AGXUnity.ConstraintType GetAGXUnityConstraintType(this B_Connector b_connector)
    {
      switch (b_connector)
      {
        case B_Mechanics.HingeConnector _:
          return AGXUnity.ConstraintType.Hinge;
        case B_Mechanics.PrismaticConnector _:
          return AGXUnity.ConstraintType.Prismatic;
        case B_Mechanics.LockJointConnector _:
          return AGXUnity.ConstraintType.LockJoint;
        case B_Mechanics.BallJointConnector _:
          return AGXUnity.ConstraintType.BallJoint;
        case B_Mechanics.SpringJointConnector _:
          return AGXUnity.ConstraintType.DistanceJoint;
        case B_Mechanics.CylindricalConnector _:
          return AGXUnity.ConstraintType.CylindricalJoint;
        default:
          break;
      }
      return AGXUnity.ConstraintType.Unknown;
    }

    public static string GetValueNameOrModelPath(this Brick.Object b_object)
    {
      var b_value = b_object._ModelValue;
      if (b_value is null)
        return b_object._ModelPath.Str;
      else
        return b_value.Name.Str;
    }
  }
}