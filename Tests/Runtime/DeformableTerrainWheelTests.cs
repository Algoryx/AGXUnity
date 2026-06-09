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
    private const float WheelRadius = 0.2f;
    private const float WheelHeight = 0.2f;
    private const float WheelClearance = 0.05f;

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

    [UnityTest]
    public IEnumerator WheelWithoutMaterialsDoesntWork()
    {
      var (terrain, wheel) = CreateTerrainAndWheel();

      TestUtils.InitializeAll();

      for ( int i = 0; i < 20; ++i )
        yield return TestUtils.Step();

      Assert.That( terrain.Native, Is.Not.Null );
      Assert.That( wheel.Native, Is.Not.Null );
      Assert.That( wheel.ActiveContactMaterialUsesTerrainWheelForceModel, Is.False );
    }

    [UnityTest]
    public IEnumerator WheelWithWrongFrictionModel()
    {
      var (terrain, wheel) = CreateTerrainWheelAndContactMaterial();

      TestUtils.InitializeAll();

      for ( int i = 0; i < 20; ++i )
        yield return TestUtils.Step();

      Assert.That( terrain.Native, Is.Not.Null );
      Assert.That( wheel.Native, Is.Not.Null );
      Assert.That( wheel.ActiveContactMaterialUsesTerrainWheelForceModel, Is.False );
    }

    private (DeformableTerrain terrain, DeformableTerrainWheel wheel) CreateTerrainAndWheel()
    {
      var terrainGO = new GameObject( "Deformable Terrain" );
      var unityTerrain = terrainGO.AddComponent<Terrain>();
      unityTerrain.terrainData = new TerrainData();
      unityTerrain.terrainData.size = new Vector3( 2.0f, 1.0f, 2.0f );
      unityTerrain.terrainData.heightmapResolution = 33;

      var terrain = terrainGO.AddComponent<DeformableTerrain>();
      terrain.MaximumDepth = 0.5f;

      var cylinderGO = Factory.Create<Cylinder>();
      var cylinder = cylinderGO.GetComponent<Cylinder>();
      cylinder.Radius = WheelRadius;
      cylinder.Height = WheelHeight;
      cylinder.Material = ShapeMaterial.CreateInstance<ShapeMaterial>();

      var wheelGO = Factory.Create<RigidBody>( cylinderGO );
      wheelGO.transform.position = new Vector3( 1.0f, WheelRadius + WheelClearance, 1.0f );
      wheelGO.transform.rotation = Quaternion.Euler( 90.0f, 0.0f, 0.0f );

      var wheel = wheelGO.AddComponent<DeformableTerrainWheel>();

      return (terrain, wheel);
    }

    private (DeformableTerrain terrain, DeformableTerrainWheel wheel) CreateTerrainWheelAndContactMaterial()
    {
      var (terrain, wheel) = CreateTerrainAndWheel();

      var wheelMaterial = ShapeMaterial.CreateInstance<ShapeMaterial>();
      var terrainMaterial = ShapeMaterial.CreateInstance<ShapeMaterial>();

      wheel.RigidBody.GetComponentInChildren<Cylinder>().Material = wheelMaterial;
      terrain.Material = terrainMaterial;

      var contactMaterial = ContactMaterial.CreateInstance<ContactMaterial>();
      contactMaterial.Material1 = wheelMaterial;
      contactMaterial.Material2 = terrainMaterial;

      ContactMaterialManager.Instance.Add( contactMaterial );

      return (terrain, wheel);
    }
  }
}
