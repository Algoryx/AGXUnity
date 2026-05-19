using AGXUnity;


public class LidarDataUtil
{
  static public bool LidarRayPatternsAreFound()
  {
    var _ = NativeHandler.Instance;
    var fileTest = agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).find( "MaterialLibrary/LidarRayPatterns/avia.bin" );
    return !string.IsNullOrEmpty( fileTest );
  }
}
