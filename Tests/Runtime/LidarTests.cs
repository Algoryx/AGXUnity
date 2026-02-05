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

using GOList = System.Collections.Generic.List<UnityEngine.GameObject>;

namespace AGXUnityTesting.Runtime
{
  public class LidarTests : AGXUnityFixture
  {
    private GOList m_keep = new GOList();

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
      m_keep.Add( CreateShape<Box>( new Vector3( 3, 0, 3 ) ) );
      m_keep.Add( CreateShape<Sphere>( new Vector3( -3, 0, 3 ) ) );
      m_keep.Add( CreateShape<Cylinder>( new Vector3( -3, 0, -3 ) ) );
      m_keep.Add( CreateShape<Cone>( new Vector3( 3, 0, -3 ) ) );
      Simulation.Instance.PreIntegratePositions = true;
      m_keep.Add( Simulation.Instance.gameObject );
      m_keep.Add( SensorEnvironment.Instance.gameObject );

      // Lidar ray intervals are sensitive to time step so ensure that the timestep is exact here
      Simulation.Instance.Native.setTimeStep( 0.02 );
    }

    [UnityTearDown]
    public IEnumerator CleanLidarScene()
    {
#if UNITY_2022_2_OR_NEWER
      var objects = Object.FindObjectsByType<ScriptComponent>( FindObjectsSortMode.None );
#else
      var objects = Object.FindObjectsOfType<ScriptComponent>( );
#endif
      GOList toDestroy = new GOList();

      foreach ( var obj in objects ) {
        var root = obj.gameObject;
        while ( root.transform.parent != null )
          root = root.transform.parent.gameObject;
        if ( !m_keep.Contains( root ) )
          toDestroy.Add( root );
      }

      yield return TestUtils.DestroyAndWait( toDestroy.ToArray() );
    }

    [OneTimeTearDown]
    public void TearDownLidarScene()
    {
#if UNITY_2022_2_OR_NEWER
      var geoms = Object.FindObjectsByType<Shape>( FindObjectsSortMode.None );
#else
      var geoms = Object.FindObjectsOfType<Shape>( );
#endif      

      foreach ( var g in geoms )
        GameObject.Destroy( g.gameObject );

      GameObject.Destroy( SensorEnvironment.Instance.gameObject );
    }

    private (LidarSensor, GenericSweepData) CreateDefaultTestLidar( Vector3 position = default )
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

      return (lidarComp, modelData);
    }

    private (LidarSensor, LivoxData) CreateLivoxTestLidar( Vector3 position = default, uint downsample = 1 )
    {
      var lidarGO = new GameObject("LivoxLidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position = position;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      if ( !Application.isBatchMode ) {
        var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();
      }

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelLivoxAvia;
      var modelData = ( lidarComp.ModelData as LivoxData );
      modelData.Downsample = downsample;

      return (lidarComp, modelData);
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
      var (lidarComp, _) = CreateDefaultTestLidar();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      TestUtils.InitializeAll();

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
      var (lidarComp, _) = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      TestUtils.InitializeAll();
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
      var (lidarComp, _) = CreateDefaultTestLidar();

      lidarComp.GetInitialized();

      var output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };
      lidarComp.Add( output );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      Assert.Throws<System.ArgumentException>( () => output.View<DataInvalid>( out uint _ ) );
    }

    [Test]
    public void TestLidarOutputAddField()
    {
      var (lidarComp, _) = CreateDefaultTestLidar();

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
      var (lidarComp, _) = CreateDefaultTestLidar();

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
      var (lidarComp1, _) = CreateDefaultTestLidar();
      var (lidarComp2, _) = CreateDefaultTestLidar();

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
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var ambMat = AmbientMaterial.CreateInstance<AmbientMaterial>();
      ambMat.AmbientType = AmbientMaterial.ConfigurationType.Fog;
      ambMat.Visibility = 2;
      SensorEnvironment.Instance.AmbientMaterial = ambMat;

      TestUtils.InitializeAll();
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
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      lidarComp.RemoveRayMisses = false;

      TestUtils.InitializeAll();
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
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );
      var mat = AddLambertianMaterial( box, 0.2f );

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      var data = output.View<float>( out uint _ );
      var preAverage = data.Average();

      mat.Reflectivity = 0.8f;

      yield return TestUtils.Step();

      data = output.View<float>( out uint _ );
      var postAverage = data.Average();

      Assert.Greater( postAverage, preAverage );

      mat.Reflectivity = 0.0001f; // Points with intensity 0 are filtered so give material a little bit of reflectivity

      yield return TestUtils.Step();

      data = output.View<float>( out uint _ );
      var noReflect = data.Average();

      Assert.That( noReflect, Is.Zero.Within( 1e-6f ), "Intensity should be zero from material with 0 reflectivity" );

      yield return TestUtils.DestroyAndWait( box );
    }

    [UnityTest]
    public IEnumerator TestLateAddSurfaceMaterial()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      var mat = AddLambertianMaterial( box, 0.2f );
      mat.Reflectivity = 0.0001f; // Points with intensity 0 are filtered so give material a little bit of reflectivity

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      var data = output.View<float>( out uint _ );
      var noReflect = data.Average();

      Assert.That( noReflect, Is.Zero.Within( 1e-6f ), "Intensity should be zero from material with 0 reflectivity" );

      yield return TestUtils.DestroyAndWait( box );
    }

    [UnityTest]
    public IEnumerator TestAddMeshAfterInitialization()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      yield return TestUtils.DestroyAndWait( box );
    }

    [UnityTest]
    public IEnumerator TestAddMeshNonAGXAfterInitialization()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
      box.transform.localPosition = new Vector3( 0, 5, -3 );
      SensorEnvironment.Instance.RegisterCreatedObject( box );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      yield return TestUtils.DestroyAndWait( box );
    }

    [UnityTest]
    public IEnumerator TestRemoveMesh()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      yield return TestUtils.DestroyAndWait( box );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }



    [UnityTest]
    public IEnumerator TestDisableEnableMesh()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      TestUtils.InitializeAll();
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

      yield return TestUtils.DestroyAndWait( box );
    }

    [UnityTest]
    public IEnumerator TestAddCableAfterInitialization()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput() { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -1 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      TestUtils.InitializeAll();

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      yield return TestUtils.DestroyAndWait( cable );
    }

    [UnityTest]
    public IEnumerator TestRemoveCable()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -1 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      TestUtils.InitializeAll();

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint count );
      Assert.NotZero( count );

      yield return TestUtils.DestroyAndWait( cable );

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestDisableEnableCable()
    {
      var (lidarComp, _) = CreateDefaultTestLidar( Vector3.up * 5 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      var cable = new GameObject();
      cable.transform.localPosition = new Vector3( 0, 5, -1 );
      var cableComp = cable.AddComponent<Cable>();
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.right, Quaternion.Euler( 0, -90, 0 ) );
      cableComp.Route.Add( Cable.NodeType.BodyFixedNode, cable, Vector3.left, Quaternion.Euler( 0, -90, 0 ) );
      cable.AddComponent<CableRenderer>();

      TestUtils.InitializeAll();
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

      yield return TestUtils.DestroyAndWait( cable );
    }

    float CalculateAverageDifference( agx.Vec3f[] points, uint numPoints, System.Func<agx.Vec3f, agx.Vec3f, float> err )
    {
      float tot = 0;
      int count = 0;
      for ( int i = 1; i < numPoints; i++ ) {
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
      var (lidarComp, _) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      System.Func<agx.Vec3f, agx.Vec3f, float> err = (agx.Vec3f p1, agx.Vec3f p2) => Mathf.Pow( p2.length() - p1.length(), 2 );

      var points = output.View<agx.Vec3f>( out uint count );
      var preAvgDiff = CalculateAverageDifference( points, count, err );

      lidarComp.DistanceGaussianNoise.Enable = true;
      lidarComp.DistanceGaussianNoise.StandardDeviationBase = 0.1f;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out count, points );
      var postAvgDiff = CalculateAverageDifference( points, count, err );

      Assert.Greater( postAvgDiff, preAvgDiff, "Expected average distance difference to be greater with noise." );

      lidarComp.DistanceGaussianNoise.Enable = false;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out count, points );
      var finalAvgDiff = CalculateAverageDifference( points, count, err );

      Assert.AreEqual( finalAvgDiff, preAvgDiff, 0.00001f, "Expected average distance to be same as before enabling noise." );
    }

    [UnityTest]
    public IEnumerator TestRayAngleNoise()
    {
      var (lidarComp, _) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      var noise = new LidarRayAngleGaussianNoise();

      lidarComp.RayAngleGaussianNoises.Add( noise );

      TestUtils.InitializeAll();
      yield return TestUtils.Step();

      System.Func<agx.Vec3f, agx.Vec3f, float> err = (agx.Vec3f p1, agx.Vec3f p2) => Mathf.Acos(p2.normal().dot(p1.normal()));

      var points = output.View<agx.Vec3f>( out uint count );
      var preAvgDiff = CalculateAverageDifference( points, count, err );

      noise.Enable = true;
      noise.StandardDeviation = 0.2f;
      noise.DistortionAxis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Y;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out count, points );
      var postAvgDiff = CalculateAverageDifference( points, count, err );

      Assert.Greater( postAvgDiff, preAvgDiff, "Expected average angle difference difference to be greater with noise." );

      noise.Enable = false;

      yield return TestUtils.Step();

      points = output.View<agx.Vec3f>( out count, points );
      var finalAvgDiff = CalculateAverageDifference( points, count, err );

      Assert.AreEqual( preAvgDiff, finalAvgDiff, 0.00001f, "Expected average angle difference to be same as before enabling noise." );
    }

    [UnityTest]
    public IEnumerator TestBeamDivergenceChange()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      sweepData.BeamDivergence = 0;
      sweepData.BeamExitRadius = 0.01f;

      yield return TestUtils.Step();

      var points = output.View<float>( out uint count );
      var preAvgInt = points.Average();

      sweepData.BeamDivergence = 0.003f;

      yield return TestUtils.Step();

      points = output.View<float>( out count, points );
      var postAvgInt = points.Take((int)count).Average();

      Assert.Less( postAvgInt, preAvgInt, "Expected average intensity to be lower with greater beam divergence." );

      sweepData.BeamDivergence = 0;

      yield return TestUtils.Step();

      points = output.View<float>( out count, points );
      var finalAvgInt = points.Take((int)count).Average();

      Assert.AreEqual( finalAvgInt, preAvgInt, 0.00001f, "Expected average intensity to be same as before changing beam divergence." );
    }

    [UnityTest]
    public IEnumerator TestBeamExitRadiusChange()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      sweepData.BeamDivergence = 0;
      sweepData.BeamExitRadius = 0;

      yield return TestUtils.Step();

      var points = output.View<float>( out uint count );
      var preAvgInt = points.Take((int)count).Average();

      sweepData.BeamExitRadius = 0.003f;

      yield return TestUtils.Step();

      points = output.View<float>( out count, points );
      var postAvgInt = points.Take((int)count).Average();

      Assert.AreNotEqual( postAvgInt, preAvgInt, "Expected average intensity to be change with greater beam exit radius." );

      sweepData.BeamExitRadius = 0;

      yield return TestUtils.Step();

      points = output.View<float>( out count, points );
      var finalAvgInt = points.Take((int)count).Average();

      Assert.AreEqual( finalAvgInt, preAvgInt, 0.00001f, "Expected average intensity to be same as before changing beam exit radius." );
    }

    [UnityTest]
    public IEnumerator TestBeamDivergence()
    {
      var (lidarComp1, sweepData1) = CreateDefaultTestLidar();
      var (lidarComp2, sweepData2) = CreateDefaultTestLidar();

      var output1 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp1.Add( output1 );

      var output2 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp2.Add( output2 );

      sweepData1.BeamDivergence = 0.01f;
      sweepData1.BeamExitRadius = 0.01f;

      sweepData2.BeamDivergence = 0.03f;
      sweepData2.BeamExitRadius = 0.01f;

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
      var (lidarComp1, sweepData1) = CreateDefaultTestLidar();
      var (lidarComp2, sweepData2) = CreateDefaultTestLidar();

      var output1 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp1.Add( output1 );

      var output2 = new LidarOutput { agxSensor.RtOutput.Field.INTENSITY_F32 };
      lidarComp2.Add( output2 );

      sweepData1.BeamDivergence = 0.01f;
      sweepData1.BeamExitRadius = 0.01f;

      sweepData2.BeamDivergence = 0.01f;
      sweepData2.BeamExitRadius = 0.03f;

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
      var (lidarComp, sweepData) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();

      sweepData.Range = new RangeReal( 0.1f, 50 );
      lidarComp.RemoveRayMisses = true;

      yield return TestUtils.Step();

      output.View<agx.Vec3f>( out uint firstCount );
      Assert.NotZero( firstCount, "Exepected lidar to have more than zero hits." );
      uint oldCount = firstCount;

      for ( float max = 50; max > 1; max-- ) {
        sweepData.Range = new RangeReal( 0.1f, max );
        yield return TestUtils.Step();
        output.View<agx.Vec3f>( out uint newCount );
        Assert.LessOrEqual( newCount, oldCount, "Expected number of points to decrease with lower range." );
        oldCount = newCount;
      }

      Assert.Zero( oldCount, "Exepected short range lidar to have zero hits." );

      for ( float max = 1; max < 50; max++ ) {
        sweepData.Range = new RangeReal( 0.1f, max );
        yield return TestUtils.Step();
        output.View<agx.Vec3f>( out uint newCount );
        Assert.GreaterOrEqual( newCount, oldCount, "Expected number of points to increase with higher range." );
        oldCount = newCount;
      }
    }

    [UnityTest]
    public IEnumerator TestLocalPosition()
    {
      var (lidarComp, _) = CreateDefaultTestLidar();

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = true;

      yield return TestUtils.Step();

      var elevations = output.View<agx.Vec3f>( out uint _ );
      var preMaxElevation = elevations.Select(p => p.z).Max();

      lidarComp.LocalPosition = new Vector3( 0, 0, 1 );
      yield return TestUtils.Step();

      elevations = output.View<agx.Vec3f>( out uint _ );
      var postMaxElevation = elevations.Select(p => p.z).Max();

      Assert.Less( postMaxElevation, preMaxElevation - 0.8f );
    }

    [UnityTest]
    public IEnumerator TestExcludeSpecificMesh()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();
      sweepData.Range = new RangeReal( 0, 100 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.DISTANCE_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = true;

      var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      mesh.transform.localScale = Vector3.one * 0.1f;
      mesh.transform.position = lidarComp.transform.position;

      SensorEnvironment.Instance.RegisterCreatedObject( mesh );

      yield return TestUtils.Step();
      var distances = output.View<float>( out uint count );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "By default, the mesh is added to the sensor environment" );

      var include = mesh.AddComponent<ExplicitSensorEnvironmentInclusion>();
      include.Include = false;
      yield return TestUtils.Step();
      yield return TestUtils.Step();
      distances = output.View<float>( out count, distances );
      Assert.That( distances.Take( (int)count ).All( d => d > 0.15f ), Is.True, "After adding an explicit exclusion, the mesh should no longer be visible" );

      include.Include = true;
      yield return TestUtils.Step();
      yield return TestUtils.Step();
      distances = output.View<float>( out count, distances );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "Disabling the exclude makes the mesh visible again" );
    }

    [UnityTest]
    public IEnumerator TestExcludeRecursive()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();
      sweepData.Range = new RangeReal( 0, 100 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.DISTANCE_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = true;

      var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      mesh.transform.localScale = Vector3.one * 0.1f;
      mesh.transform.position = lidarComp.transform.position;

      var parent = new GameObject("Parent");
      mesh.transform.parent = parent.transform;

      SensorEnvironment.Instance.RegisterCreatedObject( mesh );

      yield return TestUtils.Step();
      var distances = output.View<float>( out uint count );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "By default, the mesh is added to the sensor environment" );

      var include = parent.AddComponent<ExplicitSensorEnvironmentInclusion>();
      include.Include = false;
      include.PropagateToChildrenRecusively = false;

      yield return TestUtils.Step();
      yield return TestUtils.Step();
      distances = output.View<float>( out count, distances );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "After adding an explicit exclusion to parent, the mesh should still be visible" );
    }

    [UnityTest]
    public IEnumerator TestExcludeOverride()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();
      sweepData.Range = new RangeReal( 0, 100 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.DISTANCE_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = true;

      var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      mesh.transform.localScale = Vector3.one * 0.1f;
      mesh.transform.position = lidarComp.transform.position;

      var parent = new GameObject("Parent");
      mesh.transform.parent = parent.transform;

      SensorEnvironment.Instance.RegisterCreatedObject( mesh );

      yield return TestUtils.Step();
      var distances = output.View<float>( out uint count );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "By default, the mesh is added to the sensor environment" );

      var include = parent.AddComponent<ExplicitSensorEnvironmentInclusion>();
      include.Include = false;

      yield return TestUtils.Step();
      yield return TestUtils.Step();
      distances = output.View<float>( out count, distances );
      Assert.That( distances.Take( (int)count ).All( d => d > 0.15f ), Is.True, "After adding an explicit exclusion to parent, the mesh should no longer be visible" );

      var localInclude = mesh.AddComponent<ExplicitSensorEnvironmentInclusion>();
      localInclude.Include = true;
      yield return TestUtils.Step();
      yield return TestUtils.Step();
      distances = output.View<float>( out count, distances );
      Assert.That( distances.Take( (int)count ).All( d => d < 0.15f ), Is.True, "After adding an explicit include to local object, the mesh should be visible again" );
    }

    private agx.Vec2f HitPosToAzimuthElevation( agx.Vec3f point )
    {
      if ( point.length2() > 1e11 )
        point /= 1e30f;
      var dir = point.normal();
      float elevation = (float)agx.agxMath.Acos(dir.z) * Mathf.Rad2Deg;
      float azimuth = (float)agx.agxMath.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
      return new agx.Vec2f( azimuth, elevation );
    }

    [UnityTest]
    public IEnumerator TestLidarRayWindow()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();
      sweepData.FoVMode = GenericSweepData.FoVModes.Window;
      sweepData.HorizontalFoVWindow = new RangeReal( 0, 90 );
      sweepData.VerticalFoVWindow = new RangeReal( 0, 90 );
      sweepData.Range = new RangeReal( 0, 100 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = false;

      yield return TestUtils.Step();
      var distances = output.View<agx.Vec3f>( out uint count );

      Assert.That( count, Is.GreaterThan( 0 ), "Window should contain points" );

      bool withinWindow = distances
        .Take( (int)count )
        .Select(HitPosToAzimuthElevation)
        .All( p =>
          p.x <= sweepData.HorizontalFoVWindow.Max &&
          p.x >= sweepData.HorizontalFoVWindow.Min &&
          p.y <= sweepData.VerticalFoVWindow.Max &&
          p.y >= sweepData.VerticalFoVWindow.Min );

      Assert.That( withinWindow, Is.True, "All points should lie within the specified window" );
    }

    [UnityTest]
    public IEnumerator TestLidarRayCountResolution()
    {
      var (lidarComp, sweepData) = CreateDefaultTestLidar();
      sweepData.ResolutionMode = GenericSweepData.ResolutionModes.TotalPoints;
      sweepData.HorizontalResolutionTotal = 600;
      sweepData.VerticalResolutionTotal = 20;
      sweepData.Range = new RangeReal( 0, 100 );

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = false;

      yield return TestUtils.Step();
      var _ = output.View<agx.Vec3f>( out uint count );
      Assert.That( count, Is.EqualTo( 600 * 20 ), "Total amount of points should be horizontal * vertical" );
    }

    [UnityTest]
    public IEnumerator TestLivoxLidarDownSample()
    {
      var ( lidarComp1, modelData1 ) = CreateLivoxTestLidar( Vector3.zero, 1 );
      var output1 = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp1.Add( output1 );
      lidarComp1.GetInitialized();
      lidarComp1.RemoveRayMisses = false;

      var ( lidarComp2, modelData2 ) = CreateLivoxTestLidar( Vector3.zero, 2 );
      var output2 = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp2.Add( output2 );
      lidarComp2.GetInitialized();
      lidarComp2.RemoveRayMisses = false;

      yield return TestUtils.Step();
      var _1 = output1.View<agx.Vec3f>( out uint count1 );
      var _2 = output2.View<agx.Vec3f>( out uint count2 );

      Assert.That( count1, Is.EqualTo( count2 * 2 ), "Downsample is wrong yo. Downsample 2 should yield half the amount of points as Downsample 1" );
    }

    [UnityTest]
    public IEnumerator TestReadFromFileCsvLidar()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      var lidarComp = lidarGO.AddComponent<LidarSensor>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelReadFromFile;
      var modelData = ( lidarComp.ModelData as ReadFromFileData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.AnglesInDegrees = true;
      modelData.Delimiter = ',';
      modelData.FirstLineIsHeader = false;
      modelData.FrameSize = 2;
      modelData.TwoColumns = false;
      modelData.FilePath = "Assets/AGXUnity/Tests/Runtime/Test Resources/csv_lidar_pattern.csv";

      var output = new LidarOutput { agxSensor.RtOutput.Field.XYZ_VEC3_F32 };
      lidarComp.Add( output );
      lidarComp.GetInitialized();
      lidarComp.RemoveRayMisses = false;

      yield return TestUtils.Step();
      var _ = output.View<agx.Vec3f>( out uint count );

      Assert.That( count, Is.EqualTo( 2 ), "Couldn't create readfromfile lidar and get 2 points back" );
    }
  }
}
