using AGXUnity;
using AGXUnity.Model;
using NUnit.Framework;

namespace AGXUnityTesting.Runtime
{
  public class DataTests
  {
    [Test]
    public void TerrainMaterialPresetsAreFound()
    {
      var _ = NativeHandler.Instance;
      var presets = DeformableTerrainMaterial.GetAvailablePresets();
      Assert.NotNull( presets );
      Assert.NotZero( presets.Length );
    }

    [Test]
    public void LidarRayPatternsAreFound()
    {
      var _ = NativeHandler.Instance;
      Assert.True( AGXUnity.Sensor.TestHelper.LidarRayPatternsAreFound() );
    }
  }
}
