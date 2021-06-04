using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.IO.URDF
{
  public static class Extensions
  {
    public static agx.Matrix3x3 RadEulerToRotationMatrix( this Vector3 v )
    {
      var euler = new agx.EulerAngles( v.ToVec3() );
      return new agx.Matrix3x3( new agx.Quat( euler ) );
    }

    public static Quaternion RadEulerToLeftHanded( this Vector3 v )
    {
      return new agx.Quat( new agx.EulerAngles( v.ToVec3() ) ).ToHandedQuaternion();
    }
  }
}
