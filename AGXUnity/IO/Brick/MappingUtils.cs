using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static partial class Extensions
  {
    public static agx.Vec3 ToVec3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new agx.Vec3( vec3.x() * scale, vec3.y() * scale, vec3.z() * scale );
    }

    public static Vector3 ToVector3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Vector3 ToHandedVector3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( -vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Quaternion ToHandedQuaternion( this Brick.Math.Quat quat )
    {
      return new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() ).ToHandedQuaternion();
    }

  }
  public static class Utils
  {
    public static void mapLocalTransform( Transform transform, Brick.Physics3D.Transform local_transform )
    {
      transform.localPosition = local_transform.position().ToHandedVector3();
      transform.localRotation = local_transform.rotation().ToHandedQuaternion();
    }
  }
}