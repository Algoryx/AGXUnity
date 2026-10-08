using UnityEngine;

namespace AGXUnityEditor.Utils
{
  public static partial class PhysXConversion
  {
    public static bool HasShear( Matrix4x4 matrix )
    {
      Vector3 x = matrix.GetColumn( 0 );
      Vector3 y = matrix.GetColumn( 1 );
      Vector3 z = matrix.GetColumn( 2 );
      return Mathf.Abs( Vector3.Dot( x.normalized, y.normalized ) ) > 1.0E-4f ||
             Mathf.Abs( Vector3.Dot( x.normalized, z.normalized ) ) > 1.0E-4f ||
             Mathf.Abs( Vector3.Dot( y.normalized, z.normalized ) ) > 1.0E-4f;
    }

    public static void CapsuleDimensions( CapsuleCollider source, out float radius, out float height, out Quaternion rotation )
    {
      var scale = Abs( source.transform.lossyScale );
      int axis = source.direction;
      radius = source.radius * Mathf.Max( scale[ ( axis + 1 ) % 3 ], scale[ ( axis + 2 ) % 3 ] );
      height = Mathf.Max( 0, source.height * scale[ axis ] - 2 * radius );
      rotation = Quaternion.FromToRotation( Vector3.up, axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up );
    }

    public static void InertiaTensor( Vector3 principal, Quaternion rotation, out Vector3 diagonal, out Vector3 offDiagonal )
    {
      var r = Matrix4x4.Rotate( rotation );
      var tensor = r * Matrix4x4.Scale( principal ) * r.transpose;
      diagonal = new Vector3( tensor.m00, tensor.m11, tensor.m22 );
      // AGX uses right-handed coordinates, obtained by reflecting Unity's X axis.
      offDiagonal = new Vector3( -tensor.m01, -tensor.m02, tensor.m12 );
    }

    private static Vector3 Abs( Vector3 value ) => new Vector3( Mathf.Abs( value.x ), Mathf.Abs( value.y ), Mathf.Abs( value.z ) );

  }
}
