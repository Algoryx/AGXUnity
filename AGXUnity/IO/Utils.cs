using UnityEngine;

namespace AGXUnity.IO
{

  public static class Utils
  {
    public static string PackageName => "com.algoryx.agxunity";

    public static string EditorSettingDirectory => $"ProjectSettings/Packages/{PackageName}/";
    public static string RuntimeSettingsDirectory => $"AGXUnitySettings/";

#if UNITY_EDITOR
    public static string SettingsDirectory => EditorSettingDirectory;
#else
    public static string SettingsDirectory => RuntimeSettingsDirectory;
#endif

    public static string SettingsDirectoryFull => Application.dataPath + RuntimeSettingsDirectory;
  }
}
