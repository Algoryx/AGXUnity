using System;
using UnityEngine;

namespace AGXUnity.Utils
{
  public static class Math
  {
    public static bool Approximately( float a, float b, float epsilon = 0.000001f )
    {
      return System.Math.Abs( a - b ) <= epsilon;
    }

    public static bool Approximately( Vector3 v1, Vector3 v2, float epsilon = 0.000001f )
    {
      return System.Math.Abs( v1.x - v2.x ) <= epsilon &&
             System.Math.Abs( v1.y - v2.y ) <= epsilon &&
             System.Math.Abs( v1.z - v2.z ) <= epsilon;
    }

    public static Vector3 Clamp( Vector3 v, float minValue )
    {
      return new Vector3( Mathf.Max( v.x, minValue ), Mathf.Max( v.y, minValue ), Mathf.Max( v.z, minValue ) );
    }

    public static T Clamp<T>( T value, T min, T max ) where T : IComparable<T>
    {
      return value.CompareTo( min ) < 0 ? min : value.CompareTo( max ) > 0 ? max : value;
    }

    public static bool EqualsZero( float value, float epsilon = 0.000001f )
    {
      return System.Math.Abs( value ) < epsilon;
    }

    public static bool Equivalent( float a, float b, float epsilon = float.Epsilon )
    {
      return Mathf.Abs( a - b ) < epsilon;
    }

    public static float ClampAbove( float value, float minimum )
    {
      return Mathf.Max( value, minimum );
    }

    public static bool IsUniform( Vector3 v, float eps = 1.0E-6f )
    {
      return ( v.x - v.y ) <= eps &&
             ( v.x - v.z ) <= eps &&
             ( v.y - v.z ) <= eps;
    }

    public static float SignedAngle( Vector3 from, Vector3 to, Vector3 refAxis )
    {
      return Mathf.Rad2Deg * Mathf.Atan2( Vector3.Dot( to, refAxis ), Vector3.Dot( from, to ) );
    }

    public static void Swap<T>( ref T lhs, ref T rhs )
      where T : struct
    {
      T tmp = lhs;
      lhs = rhs;
      rhs = tmp;
    }
  }
}
