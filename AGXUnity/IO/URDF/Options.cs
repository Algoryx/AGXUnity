
namespace AGXUnity.IO.URDF
{
  public static class Options
  {
    /// <summary>
    /// True if loaded Collada (.dae) models has been transformed
    /// by Unity that has to be reverted.
    /// </summary>
    public static bool TransformCollada { get; set; } = true;
  }
}
