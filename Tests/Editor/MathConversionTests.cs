using AGXUnity.Utils;
using NUnit.Framework;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class MathConversionTests
  {
    [Test]
    public void TestConvertUnityMatrixToAffine4x4(
      [Values( -10, 0, 10 )] float tX,
      [Values( -10, 0, 10 )] float tY,
      [Values( -10, 0, 10 )] float tZ,
      [Values( -75, 0, 75 )] float rX,
      [Values( -75, 0, 75 )] float rY,
      [Values( -75, 0, 75 )] float rZ )
    {
      // Set up translation and rotation
      Vector3 uTranslate = new Vector3(tX,tY,tZ);
      Quaternion uRotation = Quaternion.Euler(rX,rY,rZ);

      // Create Unity transformation matrix
      Matrix4x4 uMatrix = Matrix4x4.TRS(uTranslate,uRotation,Vector3.one);

      // Run the utility method to convert the unity matrix to an agx.AffineMatrix4x4
      agx.AffineMatrix4x4 converted = uMatrix.ToAffine4x4();
      agx.AffineMatrix4x4 expected = new agx.AffineMatrix4x4( uRotation.ToHandedQuat(), uTranslate.ToHandedVec3() );

      // Verify that each element is equivalent between the two construction methods
      for ( int x = 0; x < 4; x++ )
        for ( int y = 0; y < 4; y++ )
          Assert.That( (float)converted[ y, x ], Is.EqualTo( (float)expected[ y, x ] ).Within( 1e-6f ) );
    }

    [Test]
    public void TestConvertUnityMatrixToAffine4x4f(
      [Values( -10, 0, 10 )] float tX,
      [Values( -10, 0, 10 )] float tY,
      [Values( -10, 0, 10 )] float tZ,
      [Values( -75, 0, 75 )] float rX,
      [Values( -75, 0, 75 )] float rY,
      [Values( -75, 0, 75 )] float rZ )
    {
      // Set up translation and rotation
      Vector3 uTranslate = new Vector3(tX,tY,tZ);
      Quaternion uRotation = Quaternion.Euler(rX,rY,rZ);

      // Create Unity transformation matrix
      Matrix4x4 uMatrix = Matrix4x4.TRS(uTranslate,uRotation,Vector3.one);

      // Run the utility method to convert the unity matrix to an agx.AffineMatrix4x4
      agx.AffineMatrix4x4f converted = uMatrix.ToAffine4x4f();
      agx.AffineMatrix4x4f expected = new agx.AffineMatrix4x4f( uRotation.ToHandedQuat(), uTranslate.ToHandedVec3f() );

      // Verify that each element is equivalent between the two construction methods
      for ( int x = 0; x < 4; x++ )
        for ( int y = 0; y < 4; y++ )
          Assert.That( converted[ y, x ], Is.EqualTo( expected[ y, x ] ).Within( 1e-6f ) );
    }

    [Test]
    public void TestConvertAffine4x4fToUnityMatrix(
      [Values( -10, 0, 10 )] float tX,
      [Values( -10, 0, 10 )] float tY,
      [Values( -10, 0, 10 )] float tZ,
      [Values( -75, 0, 75 )] float rX,
      [Values( -75, 0, 75 )] float rY,
      [Values( -75, 0, 75 )] float rZ )
    {

      // Set up translation and rotation
      agx.Vec3f aTranslate = new agx.Vec3f(tX,tY,tZ);
      agx.Quat aRotation = new agx.Quat(new agx.EulerAngles(rX,rY,rZ));

      // Create an AGX Affine transformation matrix
      agx.AffineMatrix4x4f aMatrix = new agx.AffineMatrix4x4f(aRotation,aTranslate);

      // Run the utility method to convert the agx.AffineMatrix4x4f to a unity matrix 
      Matrix4x4 converted = aMatrix.ToMatrix4x4();
      Matrix4x4 expected = Matrix4x4.TRS(aTranslate.ToHandedVector3(),aRotation.ToHandedQuaternion(), Vector3.one);

      for ( int x = 0; x < 4; x++ )
        for ( int y = 0; y < 4; y++ )
          Assert.That( converted[ y, x ], Is.EqualTo( expected[ y, x ] ).Within( 1e-6f ) );
    }

    [Test]
    public void TestConvertAffine4x4ToUnityMatrix(
      [Values( -10, 0, 10 )] float tX,
      [Values( -10, 0, 10 )] float tY,
      [Values( -10, 0, 10 )] float tZ,
      [Values( -75, 0, 75 )] float rX,
      [Values( -75, 0, 75 )] float rY,
      [Values( -75, 0, 75 )] float rZ )
    {

      // Set up translation and rotation
      agx.Vec3 aTranslate = new agx.Vec3(tX,tY,tZ);
      agx.Quat aRotation = new agx.Quat(new agx.EulerAngles(rX,rY,rZ));

      // Create an AGX Affine transformation matrix
      agx.AffineMatrix4x4 aMatrix = new agx.AffineMatrix4x4(aRotation,aTranslate);

      // Run the utility method to convert the agx.AffineMatrix4x4 to a unity matrix 
      Matrix4x4 converted = aMatrix.ToMatrix4x4();
      Matrix4x4 expected = Matrix4x4.TRS(aTranslate.ToHandedVector3(),aRotation.ToHandedQuaternion(), Vector3.one);

      for ( int x = 0; x < 4; x++ )
        for ( int y = 0; y < 4; y++ )
          Assert.That( converted[ y, x ], Is.EqualTo( expected[ y, x ] ).Within( 1e-6f ) );
    }
  }
}
