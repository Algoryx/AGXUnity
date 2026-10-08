using NUnit.Framework;

using Math = openplx.Math;

namespace AGXUnityTesting.Runtime
{
  public class OperatorTests
  {
    [Test]
    public void Vec3PlusTest()
    {
      var v1 = Math.Vec3.from_xyz(1, 2, 3);
      var v2 = Math.Vec3.from_xyz(2, 3, 4);

      var v3 = v1 + v2;

      Assert.AreEqual( 3, v3.x() );
      Assert.AreEqual( 5, v3.y() );
      Assert.AreEqual( 7, v3.z() );
    }

    [Test]
    public void Vec3DotTest()
    {
      var v1 = Math.Vec3.from_xyz(1, 2, 3);
      var v2 = Math.Vec3.from_xyz(2, 3, 4);

      var dot = v1 * v2;

      Assert.AreEqual( 20, dot );
    }

    [Test]
    public void Vec3MulTest()
    {
      var v1 = Math.Vec3.from_xyz(1, 2, 3);

      var v2 = v1 * 3;

      Assert.AreEqual( 3, v2.x() );
      Assert.AreEqual( 6, v2.y() );
      Assert.AreEqual( 9, v2.z() );

      var v3 = 3 * v1;

      Assert.AreEqual( 3, v3.x() );
      Assert.AreEqual( 6, v3.y() );
      Assert.AreEqual( 9, v3.z() );
    }

    [Test]
    public void Vec3MinusTest()
    {
      var v1 = Math.Vec3.from_xyz(1, 2, 3);
      var v2 = Math.Vec3.from_xyz(2, 3, 4);

      var v3 = v1 - v2;

      Assert.AreEqual( -1, v3.x() );
      Assert.AreEqual( -1, v3.y() );
      Assert.AreEqual( -1, v3.z() );
    }

    [Test]
    public void Vec3DivTest()
    {
      var v1 = Math.Vec3.from_xyz(3, 6, 9);

      var v2 = v1 / 3;

      Assert.AreEqual( 1, v2.x() );
      Assert.AreEqual( 2, v2.y() );
      Assert.AreEqual( 3, v2.z() );
    }

    public void QuatTest()
    {
      var q1 = Math.Quat.from_to(Math.Vec3.X_AXIS(), Math.Vec3.Z_AXIS());
      var q2 = Math.Quat.from_to(Math.Vec3.Z_AXIS(), Math.Vec3.Y_AXIS());

      var q3 = q1 * q2;

      var rotated = q3.rotate(Math.Vec3.X_AXIS());
      var tar = Math.Vec3.Y_AXIS();

      Assert.AreEqual( tar.x(), rotated.x() );
      Assert.AreEqual( tar.y(), rotated.y() );
      Assert.AreEqual( tar.z(), rotated.z() );
    }
  }
}
