using AGXUnity.Model;
using NUnit.Framework;

namespace AGXUnityTesting.Editor
{
  public class DataTests
  {
    [Test]
    public void TerrainMaterialPresetsAreFound()
    {
      var presets = DeformableTerrainMaterial.GetAvailablePresets();
      Assert.NotNull( presets );
      Assert.NotZero( presets.Length );
    }
  }
}
