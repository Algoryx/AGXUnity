using AGXUnity;
using AGXUnity.Model;
using NUnit.Framework;

namespace AGXUnityTesting.Runtime
{
  public class DataTests
  {
    [OneTimeSetUp]
    public void InitializeAGXEnvironment()
    {
      var _ = NativeHandler.Instance;
    }

    [Test]
    public void TerrainMaterialPresetsAreFound()
    {
      var presets = DeformableTerrainMaterial.GetAvailablePresets();
      Assert.NotNull( presets );
      Assert.NotZero( presets.Length );
    }

    [Test]
    public void LidarRayPatternsAreFound()
    {
      Assert.True( LidarDataUtil.LidarRayPatternsAreFound() );
    }

    [Test]
    public void CombustionEngineParametersAreFound()
    {
      agxDriveTrain.CombustionEngineParameters props = new();
      var defMaxTorque = props.maxTorque;
      Assert.True( agxDriveTrain.CombustionEngine.readLibraryProperties( "Cat-C27", props ), "Properties are expected to be found" );
      Assert.That( props.maxTorque, Is.Not.EqualTo( defMaxTorque ), "Properties are expected to be updated" );
    }
  }
}
