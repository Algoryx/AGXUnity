using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Class that uses some algorithm to take the average of a set of
  /// quaternions:
  /// http://wiki.unity3d.com/index.php/Averaging_Quaternions_and_Vectors
  /// </summary>
  public class AverageQuaternion
  {
    private List<Quaternion> m_quaternions = new List<Quaternion>();

    public Quaternion Result
    {
      get
      {
        Quaternion result = Quaternion.identity;
        Vector4 cumulative = Vector4.zero;
        for ( int i = 0; i < m_quaternions.Count; ++i )
          result = Update( ref cumulative, m_quaternions[ i ], m_quaternions[ 0 ], m_quaternions.Count );

        return result;
      }
    }

    public static AverageQuaternion operator+ ( AverageQuaternion aq, Quaternion q )
    {
      aq.m_quaternions.Add( q );
      return aq;
    }

    //Get an average (mean) from more then two quaternions (with two, slerp would be used).
    //Note: this only works if all the quaternions are relatively close together.
    //Usage: 
    //-Cumulative is an external Vector4 which holds all the added x y z and w components.
    //-newRotation is the next rotation to be added to the average pool
    //-firstRotation is the first quaternion of the array to be averaged
    //-addAmount holds the total amount of quaternions which are currently added
    //This function returns the current average quaternion
    public static Quaternion Update( ref Vector4 cumulative, Quaternion newRotation, Quaternion firstRotation, int addAmount )
    {
      float w = 0.0f;
      float x = 0.0f;
      float y = 0.0f;
      float z = 0.0f;

      //Before we add the new rotation to the average (mean), we have to check whether the quaternion has to be inverted. Because
      //q and -q are the same rotation, but cannot be averaged, we have to make sure they are all the same.
      if ( !AreQuaternionsClose( newRotation, firstRotation ) ) {

        newRotation = InverseSignQuaternion( newRotation );
      }

      //Average the values
      float addDet = 1f / (float)addAmount;
      cumulative.w += newRotation.w;
      w = cumulative.w * addDet;
      cumulative.x += newRotation.x;
      x = cumulative.x * addDet;
      cumulative.y += newRotation.y;
      y = cumulative.y * addDet;
      cumulative.z += newRotation.z;
      z = cumulative.z * addDet;

      //note: if speed is an issue, you can skip the normalization step
      return NormalizeQuaternion( x, y, z, w );
    }

    public static Quaternion NormalizeQuaternion( float x, float y, float z, float w )
    {
      float lengthD = 1.0f / ( w * w + x * x + y * y + z * z );
      w *= lengthD;
      x *= lengthD;
      y *= lengthD;
      z *= lengthD;

      return new Quaternion( x, y, z, w );
    }

    //Changes the sign of the quaternion components. This is not the same as the inverse.
    public static Quaternion InverseSignQuaternion( Quaternion q )
    {
      return new Quaternion( -q.x, -q.y, -q.z, -q.w );
    }

    //Returns true if the two input quaternions are close to each other. This can
    //be used to check whether or not one of two quaternions which are supposed to
    //be very similar but has its component signs reversed (q has the same rotation as
    //-q)
    public static bool AreQuaternionsClose( Quaternion q1, Quaternion q2 )
    {
      float dot = Quaternion.Dot( q1, q2 );
      if ( dot < 0.0f ) {
        return false;
      }
      else {
        return true;
      }
    }
  }
}
