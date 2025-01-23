using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Sensor;
using NUnit.Framework;
using System.Collections;
using System.Linq;
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
      foreach ( var lidar in Object.FindObjectsOfType<LidarSensor>() )
        Object.Destroy( lidar.gameObject );
    }

    [UnityTest]
    public IEnumerator TestCreateLidar()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      yield return TestUtils.SimulateSeconds( 0.2f );
    }

    private struct DataValid
    {
      public agx.Vec3f position;
      public float intensity;
    }

    [UnityTest]
    public IEnumerator TestLidarOutputPreAdd()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      yield return TestUtils.SimulateSeconds( 0.2f );

      var data = output.Native.View<DataValid>(out uint _);
      foreach ( var d in data ) {
        Assert.LessOrEqual( d.position.length(), 5 );
        Assert.NotZero( d.intensity );
      }
    }

    [UnityTest]
    public IEnumerator TestLidarOutputPostAdd()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      yield return TestUtils.SimulateSeconds( 0.2f );

      var data = output.Native.View<DataValid>(out uint _);
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
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      yield return TestUtils.SimulateSeconds( 0.2f );

      Assert.Throws<System.ArgumentException>( () => output.Native.View<DataInvalid>( out uint _ ) );
    }

    [UnityTest]
    public IEnumerator TestSensorEnvironmentAmbientMaterial()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position = Vector3.up * 5;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      var ambMat = AmbientMaterial.CreateInstance<AmbientMaterial>();
      ambMat.AmbientType = AmbientMaterial.ConfigurationType.Fog;
      ambMat.Visibility = 2;
      SensorEnvironment.Instance.AmbientMaterial = ambMat;

      yield return TestUtils.SimulateSeconds( 0.2f );

      output.Native.View<DataValid>( out uint count );
      Assert.Greater( count, 0 );

      SensorEnvironment.Instance.AmbientMaterial = null;

      yield return TestUtils.SimulateSeconds( 0.2f );

      output.Native.View<DataValid>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestRemoveMisses()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position = Vector3.up * 5;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      lidarComp.SetEnableRemoveRayMisses = false;

      yield return TestUtils.SimulateSeconds( 0.2f );

      output.Native.View<DataValid>( out uint count );
      Assert.Greater( count, 0 );

      lidarComp.SetEnableRemoveRayMisses = true;

      yield return TestUtils.SimulateSeconds( 0.2f );

      output.Native.View<DataValid>( out count );
      Assert.Zero( count );
    }

    [UnityTest]
    public IEnumerator TestSurfaceMaterialReflectivity()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position += Vector3.up * 5;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = (lidarComp.ModelData as GenericSweepData);
      modelData.VerticalResolution = 2;
      modelData.HorizontalResolution = 2;
      modelData.HorizontalFoV = 40;
      modelData.VerticalFoV = 40;
      modelData.Frequency = 1/Simulation.Instance.TimeStep;

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );
      var mat = AddLambertianMaterial( box, 0.2f );

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      yield return TestUtils.SimulateSeconds( 0.2f );

      var data = output.Native.View<float>( out uint _ );
      var preAverage = data.Average();

      mat.Reflectivity = 0.8f;

      yield return TestUtils.SimulateSeconds( 0.2f );

      data = output.Native.View<float>( out uint _ );
      var postAverage = data.Average();

      Assert.Greater( postAverage, preAverage );

      mat.Reflectivity = 0.0f;

      yield return TestUtils.SimulateSeconds( 0.2f );

      data = output.Native.View<float>( out uint _ );
      var noReflect = data.Average();

      Assert.Zero( noReflect, "Intensity should be zero from material with 0 reflectivity" );

      GameObject.Destroy( box );
    }

    [UnityTest]
    public IEnumerator TestAddMeshAfterInitialization()
    {
      var lidarGO = new GameObject("Lidar");
      lidarGO.transform.localRotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      lidarGO.transform.position += Vector3.up * 5;
      var lidarComp = lidarGO.AddComponent<LidarSensor>();
      var lidarRender = lidarGO.AddComponent<LidarPointCloudRenderer>();

      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGeneric360HorizontalSweep;
      var modelData = ( lidarComp.ModelData as GenericSweepData );
      modelData.Frequency = 1.0f/Simulation.Instance.TimeStep;
      modelData.HorizontalResolution = 2;
      modelData.VerticalResolution = 2;

      var output = new LidarOutput();
      output.Add( agxSensor.RtOutput.Field.XYZ_VEC3_F32 );
      output.Add( agxSensor.RtOutput.Field.INTENSITY_F32 );
      lidarComp.Add( output );

      lidarComp.GetInitialized();
      lidarRender.GetInitialized();

      var box = CreateShape<Box>( new Vector3( 0, 5, -3 ) );

      yield return TestUtils.SimulateSeconds( 0.2f );

      output.Native.View<DataValid>( out uint count );
      Assert.Greater( count, 0 );

      GameObject.Destroy( box );
    }
  }
}
