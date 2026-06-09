using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class DeformableTerrainWheelTests : AGXUnityFixture
  {
    [UnityTest]
    public IEnumerator CreateAndDestroyWithoutCylinder()
    {
      var go = Factory.Create<RigidBody>();
      var wheel = go.AddComponent<DeformableTerrainWheel>();

      LogAssert.Expect( LogType.Warning, "DeformableTerrainWheel requires exactly 1 Cylinder shape in the RigidBody, found 0." );

      TestUtils.InitializeAll();

      Assert.That( wheel.Native, Is.Null );

      yield return TestUtils.DestroyAndWait( go );
    }

    [UnityTest]
    public IEnumerator CreateAndDestroyWithCylinder()
    {
      var cylinderGO = Factory.Create<Cylinder>();
      var cylinder = cylinderGO.GetComponent<Cylinder>();
      cylinder.Material = ShapeMaterial.CreateInstance<ShapeMaterial>();

      var go = Factory.Create<RigidBody>( cylinderGO );
      var wheel = go.AddComponent<DeformableTerrainWheel>();

      TestUtils.InitializeAll();

      Assert.That( wheel.Native, Is.Not.Null );

      yield return TestUtils.DestroyAndWait( go );

      Assert.That( wheel.Native, Is.Null );
    }
  }
}
