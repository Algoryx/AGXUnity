using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.IO.URDF
{
  public static class Extensions
  {
    public static Vector3 ToLeftHanded( this Vector3 v )
    {
      return new Vector3( -v.x, v.y, v.z );
    }

    public static agx.Matrix3x3 RadEulerToRotationMatrix( this Vector3 v )
    {
      var euler = new agx.EulerAngles( v.ToVec3() );
      return new agx.Matrix3x3( new agx.Quat( euler ) );
    }

    public static Quaternion RadEulerToLeftHanded( this Vector3 v )
    {
      return Quaternion.Euler( Mathf.Rad2Deg * v ).ToLeftHanded();
    }

    public static Quaternion ToLeftHanded( this Quaternion q )
    {
      return new Quaternion( -q.x, q.y, q.z, -q.w );
    }
  }
}
