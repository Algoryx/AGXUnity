using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Sensor;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace AGXUnityTesting.Runtime
{
  public class LidarTests
  {
    private GameObject CreateShape<T>( Vector3 transform = new Vector3() )
      where T : Shape
    {
      var go = Factory.Create<T>();
      if ( go == null )
        return null;

      go.transform.localPosition = transform;

      AGXUnity.Rendering.ShapeVisual.Create( go.GetComponent<T>() );

      return go;
    }

    private LidarSurfaceMaterialLambertianOpaque AddLambertianMaterial( GameObject obj, float reflectivity )
    {
      var material = ScriptAsset.Create<LidarSurfaceMaterialLambertianOpaque>();
      material.Reflectivity = reflectivity;

      var matComp = obj.AddComponent<LidarSurfaceMaterial>();
      matComp.LidarSurfaceMaterialDefinition = material;
      matComp.PropagateToChildrenRecusively = true;

      return material;
    }


    [OneTimeSetUp]
    public void SetupLidarScene()
    {
      CreateShape<Box>( new Vector3( 3, 0, 3 ) );
      CreateShape<Sphere>( new Vector3( -3, 0, 3 ) );
      CreateShape<Cylinder>( new Vector3( -3, 0, -3 ) );
      CreateShape<Cone>( new Vector3( 3, 0, -3 ) );
    }

    [TearDown]
    public void TearDownLidarScene()
    {
#if UNITY_2022_2_OR_NEWER
      var lidars = Object.FindObjectsByType<LidarSensor>( FindObjectsSortMode.None );
#else
      var lidars = Object.FindObjectsOfType<LidarSensor>( );
#endif
      foreach ( var lidar in lidars )
        Object.Destroy( lidar.gameObject );
    }

    private LidarSensor CreateDefaultTestLidar( Vector3 position = default )
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position = position;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      if ( !Application.isBatchMode ) {
        var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();
      }

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGenericHorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      return lidarComp;
    }

    [UnityTest]
    public IEnumerator TestCreateLidar()
    {
      CreateDefaultTestLidar();

      yield return TestUtils.Step();
    }

    private struct PosIntensity
    {
      public agx.Vec3f position;
      public float intensity;
    }

    [UnityTest]
    public IEnumerator TestLidarOutputPreAdd()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      yield return TestUtils.Step();

      var data = output.View<PosIntensity>(out uint _);
      foreach ( var d in data ) {
        Assert.LessOrEqual( d.position.length(), 5 );
        Assert.NotZero( d.intensity );
      }
    }

    [UnityTest]
    public IEnumerator TestLidarOutputPostAdd()
    {
      var lidarComp = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      yield return TestUtils.Step();

      var data = output.View<PosIntensity>(out uint _);
      foreach ( var d in data ) {
        Assert.LessOrEqual( d.position.length(), 5 );
        Assert.NotZero( d.intensity );
      }
    }

    struct DataInvalid
    {
      public agx.Vec3f position;
      public float padding;
      public float intensity;
    }

    [UnityTest]
    public IEnumerator TestLidarOutputInvalidViewFails()
    {
      var lidarComp = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      yield return TestUtils.Step();

      Assert.Throws<System.ArgumentException>( () => output.View<DataInvalid>( out uint _ ) );
    }

    [Test]
    public void TestLidarOutputAddField()
    {
      var lidarComp = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };

      Assert.True( output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 ) );
      Assert.True( output.Contains( agxSensor.RtOutput.Field.INTENSITY_F32 ) );
      Assert.True( output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 ) );

      lidarComp.Add( output );

      LogAssert.Expect( LogType.Error, new Regex( ".*" ) );
      Assert.False( output.Add( agxSensor.RtOutput.Field.DISTANCE_F32 ) );
      Assert.False( output.Contains( agxSensor.RtOutput.Field.DISTANCE_F32 ) );
    }

    [Test]
    public void TestLidarOutputRemoveField()
    {
      var lidarComp = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };

      Assert.True( output.Remove( agxSensor.RtOutput.Field.INTENSITY_F32 ) );
      Assert.False( output.Contains( agxSensor.RtOutput.Field.INTENSITY_F32 ) );
      Assert.False( output.Remove( agxSensor.RtOutput.Field.INTENSITY_F32 ) );

      lidarComp.Add( output );

      LogAssert.Expect( LogType.Error, new Regex( ".*" ) );
      Assert.False( output.Remove( agxSensor.RtOutput.Field.XYZ_VEC3_F32 ) );
      Assert.True( output.Contains( agxSensor.RtOutput.Field.XYZ_VEC3_F32 ) );
    }

    [Test]
    public void TestAddLidarOutputToMultipleLidarsShouldFail()
    {
      var lidarComp1 = CreateDefaultTestLidar();
      var lidarComp2 = CreateDefaultTestLidar();

      lidarComp1.GetInitialized();
      lidarComp2.GetInitialized();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };

      Assert.True( lidarComp1.Add( output ) );
      Assert.False( lidarComp1.Add( output ) );
      LogAssert.Expect( LogType.Error, new Regex( ".*" ) );
      Assert.False( lidarComp2.Add( output ) );
    }

    [UnityTest]
    public IEnumerator TestSensorEnvironmentAmbientMaterial()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var ambMat = AmbientMaterial.CreateInstance<AmbientMaterial>();
      ambMat.AmbientType = AmbientMaterial.ConfigurationType.Fog;
      ambMat.Visibility = 2;
      SensorEnvironment.Instance.AmbientMaterial = ambMat;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      SensorEnvironment.Instance.AmbientMaterial = null;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestRemoveMisses()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      lidarComp.RemoveRayMisses = false;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      lidarComp.RemoveRayMisses = true;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestSurfaceMaterialReflectivity()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );
      var mat = AddLambertianMaterial( box, 0.2f );

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      yield return TestUtils.Step();

      var data = output.View<float>( out uint _ );
      var preAverage = data.Average();

      mat.Reflectivity = 0.8f;

      yield return TestUtils.Step();

      data = output.View<float>( out uint _ );
      var postAverage = data.Average();

      Assert.Greater( postAverage, preAverage );

      mat.Reflectivity = 0.0f;

      yield return TestUtils.Step();

      data = output.View<float>( out uint _ );
      var noReflect = data.Average();

      Assert.Zero( noReflect, "Intensity should be zero from material with 0 reflectivity" );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestLateAddSurfaceMaterial()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      yield return TestUtils.Step();

      var mat = AddLambertianMaterial( box, 0.2f );
      mat.Reflectivity = 0.0f;

      yield return TestUtils.Step();

      var data = output.View<float>( out uint _ );
      var noReflect = data.Average();

      Assert.Zero( noReflect, "Intensity should be zero from material with 0 reflectivity" );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestAddMeshAfterInitialization()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestAddMeshNonAGXAfterInitialization()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
      box.transform.localPosition = new Vector3( 0, 5, -3 );
      SensorEnvironment.Instance.RegisterCreatedObject( box );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestRemoveMesh()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      GameObject.Destroy( box );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }



    [UnityTest]
    public IEnumerator TestDisableEnableMesh()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      box.SetActive( false );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );

      box.SetActive( true );

      yield return TestUtils.Step();
      output.View<agx.Vec3f>( out count );
      Assert.NotZero( count );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestAddCableAfterInitialization()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput() { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -3 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      GameObject.Destroy( cable );
    }

    [UnityTest]
    public IEnumerator TestRemoveCable()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -3 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      GameObject.Destroy( cable );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestDisableEnableCable()
    {
      var lidarComp = CreateDefaultTestLidar(Vector3.up * 5);

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -3 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      cable.SetActive( false );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );

      cable.SetActive( true );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.NotZero( count );

      GameObject.Destroy( cable );
    }

    float CalculateAverageDifference( agx.Vec3f[] points, System.Func<agx.Vec3f, agx.Vec3f, float> err )
    {
      float tot = 0;
      int count = 0;
      for ( int i = 1; i < points.Length; i++ ) {
        if ( points[ i-1 ].normal().dot( points[ i ].normal() ) < 0.95f )
          continue;
        tot += err( points[ i-1 ], points[ i ] );
        count++;
      }
      return tot/count;
    }

    [UnityTest]
    public IEnumerator TestDistanceNoise()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      yield return TestUtils.Step();

      System.Func<agx.Vec3f, agx.Vec3f, float> err = (agx.Vec3f p1, agx.Vec3f p2) => Mathf.Pow( p2.length() - p1.length(), 2 );

      var points = output.View<agx.Vec3f>( out uint _ );
      var preAvgDiff = CalculateAverageDifference( points, err);

      lidarComp.DistanceGaussianNoise.Enable = true;
      lidarComp.DistanceGaussianNoise.StandardDeviationBase = 0.1f;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out uint _, points );
      var postAvgDiff = CalculateAverageDifference( points, err );

      Assert.Greater( postAvgDiff, preAvgDiff, "Expected average distance difference to be greater with noise." );

      lidarComp.DistanceGaussianNoise.Enable = false;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out uint _, points );
      var finalAvgDiff = CalculateAverageDifference( points, err );

      Assert.AreEqual( finalAvgDiff, preAvgDiff, 0.00001f, "Expected average distance to be same as before enabling noise." );
    }

    [UnityTest]
    public IEnumerator TestRayAngleNoise()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      yield return TestUtils.Step();

      System.Func<agx.Vec3f, agx.Vec3f, float> err = (agx.Vec3f p1, agx.Vec3f p2) => Mathf.Acos(p2.normal().dot(p1.normal()));

      var points = output.View<agx.Vec3f>( out uint _ );
      var preAvgDiff = CalculateAverageDifference( points, err );

      lidarComp.RayAngleGaussianNoise.Enable = true;
      lidarComp.RayAngleGaussianNoise.StandardDeviation = 0.2f;
      lidarComp.RayAngleGaussianNoise.DistortionAxis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Y;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out uint _, points );
      var postAvgDiff = CalculateAverageDifference( points, err );

      Assert.Greater( postAvgDiff, preAvgDiff, "Expected average angle difference difference to be greater with noise." );

      lidarComp.RayAngleGaussianNoise.Enable = false;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out uint _, points );
      var finalAvgDiff = CalculateAverageDifference( points, err );

      Assert.AreEqual( finalAvgDiff, preAvgDiff, 0.00001f, "Expected average angle difference to be same as before enabling noise." );
    }

    [UnityTest]
    public IEnumerator TestBeamDivergenceChange()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      lidarComp.BeamDivergence = 0;
      lidarComp.BeamExitRadius = 0;

      yield return TestUtils.Step();

      var points = output.View<float>( out uint _ );
      var preAvgInt = points.Average();

      lidarComp.BeamDivergence = 0.003f;

      yield return TestUtils.Step();

      points = output.View<float>( out uint _, points );
      var postAvgInt = points.Average();

      Assert.Less( postAvgInt, preAvgInt, "Expected average intensity to be lower with greater beam divergence." );

      lidarComp.BeamDivergence = 0;

      yield return TestUtils.Step();

      points = output.View<float>( out uint _, points );
      var finalAvgInt = points.Average();

      Assert.AreEqual( finalAvgInt, preAvgInt, 0.00001f, "Expected average intensity to be same as before changing beam divergence." );
    }

    [UnityTest]
    public IEnumerator TestBeamExitRadiusChange()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      lidarComp.BeamDivergence = 0;
      lidarComp.BeamExitRadius = 0;

      yield return TestUtils.Step();

      var points = output.View<float>( out uint _ );
      var preAvgInt = points.Average();

      lidarComp.BeamExitRadius = 0.003f;

      yield return TestUtils.Step();

      points = output.View<float>( out uint _, points );
      var postAvgInt = points.Average();

      Assert.AreNotEqual( postAvgInt, preAvgInt, "Expected average intensity to be change with greater beam exit radius." );

      lidarComp.BeamExitRadius = 0;

      yield return TestUtils.Step();

      points = output.View<float>( out uint _, points );
      var finalAvgInt = points.Average();

      Assert.AreEqual( finalAvgInt, preAvgInt, 0.00001f, "Expected average intensity to be same as before changing beam exit radius." );
    }

    [UnityTest]
    public IEnumerator TestBeamDivergence()
    {
      var lidarComp1 = CreateDefaultTestLidar();
      var lidarComp2 = CreateDefaultTestLidar();

      var output1 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp1.Add( output1 );

      var output2 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp2.Add( output2 );

      lidarComp1.BeamDivergence = 0.01f;
      lidarComp1.BeamExitRadius = 0.01f;

      lidarComp2.BeamDivergence = 0.03f;
      lidarComp2.BeamExitRadius = 0.01f;

      lidarComp1.GetInitialized();
      lidarComp2.GetInitialized();

      yield return TestUtils.Step();

      var points1 = output1.View<float>( out uint _ );
      var points2 = output2.View<float>( out uint _ );

      var cleanInt = points1.Average();
      var divergedInt = points2.Average();

      Assert.Less( divergedInt, cleanInt, "Expected average intensity to be lower with greater beam divergence." );
    }

    [UnityTest]
    public IEnumerator TestBeamExitRadius()
    {
      var lidarComp1 = CreateDefaultTestLidar();
      var lidarComp2 = CreateDefaultTestLidar();

      var output1 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp1.Add( output1 );

      var output2 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp2.Add( output2 );

      lidarComp1.BeamDivergence = 0.01f;
      lidarComp1.BeamExitRadius = 0.01f;

      lidarComp2.BeamDivergence = 0.01f;
      lidarComp2.BeamExitRadius = 0.03f;

      lidarComp1.GetInitialized();
      lidarComp2.GetInitialized();

      yield return TestUtils.Step();

      var points1 = output1.View<float>( out uint _ );
      var points2 = output2.View<float>( out uint _ );

      var cleanInt = points1.Average();
      var divergedInt = points2.Average();

      Assert.AreNotEqual( divergedInt, cleanInt, "Expected average intensity to be different with greater beam exit radius." );
    }

    [UnityTest]
    public IEnumerator TestRange()
    {
      var lidarComp = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      lidarComp.LidarRange = new RangeReal( 0.1f, 50 );
      lidarComp.RemoveRayMisses = true;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint firstCount );
      Assert.NotZero( firstCount, "Exepected lidar to have more than zero hits." );
      uint oldCount = firstCount;

      for ( float max = 50; max > 1; max-- ) {
        lidarComp.LidarRange = new RangeReal( 0.1f, max );
        yield return TestUtils.Step();
        output.View<agx.Vec3f>( out uint newCount );
        Assert.LessOrEqual( newCount, oldCount, "Expected number of points to decrease with lower range." );
        oldCount = newCount;
      }

      Assert.Zero( oldCount, "Exepected short range lidar to have zero hits." );

      for ( float max = 1; max < 50; max++ ) {
        lidarComp.LidarRange = new RangeReal( 0.1f, max );
        yield return TestUtils.Step();
        output.View<agx.Vec3f>( out uint newCount );
        Assert.GreaterOrEqual( newCount, oldCount, "Expected number of points to increase with higher range." );
        oldCount = newCount;
      }
    }
  }
}
