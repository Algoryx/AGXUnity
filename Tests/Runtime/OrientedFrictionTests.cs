using AGXUnity;
using AGXUnity.Collide;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class OrientedFrictionTests : AGXUnityFixture
  {
    private ShapeMaterial m_groundMat;
    private FrictionModel m_friction;

    [OneTimeSetUp]
    public void SetupGroundAssets()
    {
      m_groundMat = ShapeMaterial.CreateInstance<ShapeMaterial>();
      m_friction = FrictionModel.CreateInstance<FrictionModel>();
      m_friction.Type = FrictionModel.EType.BoxFriction;
      m_friction.SolveType = FrictionModel.ESolveType.Direct;
    }

    [UnitySetUp]
    public IEnumerator SetupFrictionScene()
    {
      var go = Factory.Create<AGXUnity.Collide.Box>();
      go.name = "Ground";
      go.transform.rotation = Quaternion.AngleAxis( 30, Vector3.forward );

      var boxComp = go.GetComponent<AGXUnity.Collide.Box>();
      boxComp.HalfExtents = new Vector3( 10, 0.2f, 10 );
      boxComp.Material = m_groundMat;

      AGXUnity.Rendering.ShapeVisual.Create( boxComp );

      yield return TestUtils.WaitUntilLoaded();
    }

    private (ShapeMaterial, RigidBody, Shape, ObserverFrame) CreateSlidingBox()
    {
      var rbGO = new GameObject("Sliding box");
      var rb = rbGO.AddComponent<RigidBody>();

      var boxMat = ShapeMaterial.CreateInstance<ShapeMaterial>();

      var boxGO = new GameObject("Box Geometry");
      boxGO.transform.parent = rbGO.transform;

      var boxGeom = boxGO.AddComponent<Box>();
      boxGeom.Material = boxMat;

      AGXUnity.Rendering.ShapeVisual.Create( boxGeom );

      var oFrameGO = new GameObject("Observer");
      oFrameGO.transform.parent = rbGO.transform;

      var oFrame = oFrameGO.AddComponent<ObserverFrame>();

      rbGO.transform.position = new Vector3( 5, 4, 0 );
      rbGO.transform.rotation = Quaternion.AngleAxis( 30, Vector3.forward );

      return (boxMat, rb, boxGeom, oFrame);
    }

    private void TestSlides( RigidBody box, bool shouldSlide )
    {
      Assert.That( box.transform.position.y, ( shouldSlide ? Is.Negative : Is.Positive ) );
      Assert.That( box.LinearVelocity.magnitude, ( shouldSlide ? Is.GreaterThan( 1.0f ) : Is.Zero.Within( 1e-5f ) ) );
    }

    private void AddOriented( GameObject refObject, ShapeMaterial otherMaterial, FrictionModel.PrimaryDirection direction )
    {
      var cm = ContactMaterial.CreateInstance<ContactMaterial>();
      cm.Material1 = m_groundMat;
      cm.Material2 = otherMaterial;

      cm.FrictionModel = m_friction;

      cm.FrictionCoefficients = new Vector2( 0, 1 );
      ContactMaterialManager.Instance.GetInitialized();
      ContactMaterialManager.Instance.Add( new ContactMaterialEntry()
      {
        ContactMaterial = cm,
        IsOriented = true,
        PrimaryDirection = direction,
        ReferenceObject = refObject
      } );
    }

    [UnityTest]
    public IEnumerator TestMainAxisRB()
    {
      var (mat, rb, _, _) = CreateSlidingBox();

      AddOriented( rb.gameObject, mat, FrictionModel.PrimaryDirection.X );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, true );
    }

    [UnityTest]
    public IEnumerator TestOffAxisRB()
    {
      var (mat, rb, _, _) = CreateSlidingBox();

      AddOriented( rb.gameObject, mat, FrictionModel.PrimaryDirection.Z );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, false );
    }

    [UnityTest]
    public IEnumerator TestMainAxisShape()
    {
      var (mat, rb, shape, _) = CreateSlidingBox();

      AddOriented( shape.gameObject, mat, FrictionModel.PrimaryDirection.X );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, true );
    }

    [UnityTest]
    public IEnumerator TestOffAxisShape()
    {
      var (mat, rb, shape, _) = CreateSlidingBox();

      AddOriented( shape.gameObject, mat, FrictionModel.PrimaryDirection.Z );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, false );
    }

    [UnityTest]
    public IEnumerator TestMainAxisObserverFrame()
    {
      var (mat, rb, _, oFrame) = CreateSlidingBox();

      AddOriented( oFrame.gameObject, mat, FrictionModel.PrimaryDirection.X );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, true );
    }

    [UnityTest]
    public IEnumerator TestOffAxisObserverFrame()
    {
      var (mat, rb, _, oFrame) = CreateSlidingBox();

      AddOriented( oFrame.gameObject, mat, FrictionModel.PrimaryDirection.Z );

      yield return TestUtils.SimulateSeconds( 3 );

      TestSlides( rb, false );
    }
  }
}
