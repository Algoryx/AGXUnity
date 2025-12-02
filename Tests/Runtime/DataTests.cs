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
  }
}
