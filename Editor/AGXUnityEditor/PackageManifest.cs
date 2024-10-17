using AGXUnity;
using System.IO;
using UnityEngine;

namespace AGXUnityEditor
{
  public class PackageManifest
  {
    private static string s_manifestPath => IO.Utils.AGXUnityPackageDirectory + Path.DirectorySeparatorChar + "package.json";

    private static PackageManifest s_instance = null;

    public static string Raw => File.ReadAllText( s_manifestPath );

    public static PackageManifest Instance
    {
      get
      {
        if ( s_instance == null ) {
          try {
            s_instance = JsonUtility.FromJson<PackageManifest>( Raw );
          }
          catch {
            Debug.LogError( "Failed to load package manifest. AGXUnity installation might be corrupt." );
          }
        }
        return s_instance;
      }
    }

    public string name, version, agx, unity, description, platformName, platform;

    public VersionInfo GetAGXUnityVersionInfo()
    {
      var vi = VersionInfo.Parse( version );
      vi.Platform = platform;
      vi.PlatformName = platformName;

      return vi;
    }
  }
}
