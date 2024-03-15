using AGXUnity;
using System.IO;
using UnityEngine;

namespace AGXUnityEditor
{
  public class PackageManifest
  {
    private static string s_manifestPath => IO.Utils.AGXUnityPackageDirectory + Path.DirectorySeparatorChar + "package.json";

    private static PackageManifest s_instance = null;

    public static PackageManifest Instance
    {
      get
      {
        if ( s_instance == null ) {
          var manifestRaw = File.ReadAllText(s_manifestPath);
          try {
            s_instance = JsonUtility.FromJson<PackageManifest>( manifestRaw );
          }
          catch {
            Debug.LogError( "Failed to load package manifest. AGXUnity installation might be corrupt." );
          }
        }
        return s_instance;
      }
    }

    public string name, version, agx, unity, description, platformName, platform;

    public VersionInfo GetAGXUnityVersionInfo() => VersionInfo.Parse( version );
  }
}
