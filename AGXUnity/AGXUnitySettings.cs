using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif

namespace AGXUnity
{
  public class RuntimeSettingsAttribute : System.Attribute { }

  interface IAGXUnitySetting
  {
    string SettingsPath { get; }
    string SettingsFile { get; }

    public void Save();
  }

  public class AGXUnitySettings<T> : ScriptableObject, IAGXUnitySetting
      where T : AGXUnitySettings<T>
  {
    public string SettingsPath => IO.Utils.SettingsDirectory + SettingsFile;
    public string SettingsFile => typeof( T ).Name + ".json";

    private static T s_instance = null;
    public static T Instance
    {
      get
      {
        if ( s_instance == null ) {
          s_instance = GetOrCreateInstance();
        }
        return s_instance;
      }
    }

    protected virtual bool RuntimePresistent => false;

    protected virtual void OnCreated() { }

    public void Save()
    {
      if ( !Application.isPlaying || RuntimePresistent ) {
        Directory.CreateDirectory( IO.Utils.SettingsDirectory );
        var json = JsonUtility.ToJson( this );
        File.WriteAllText( SettingsPath, json );
      }
    }

    public static T GetOrCreateInstance()
    {
      T instance = CreateInstance<T>();
      try {
        var json = File.ReadAllText( instance.SettingsPath );
        JsonUtility.FromJsonOverwrite( json, instance );
      }
      catch {
        instance.OnCreated();
        instance.Save();
      }
      return instance;
    }
  }

#if UNITY_EDITOR
  public static class SettingsCopier
  {
    [PostProcessBuild( 16 )]
    public static void OnPostProcessBuild( BuildTarget target, string targetPathFilename )
    {
      var runtimeSettings = TypeCache.GetTypesWithAttribute<RuntimeSettingsAttribute>();
      var targetExecutableFileInfo = new FileInfo( targetPathFilename );
      if ( !targetExecutableFileInfo.Exists ) {
        Debug.LogWarning( "Target executable doesn't exist: " + targetPathFilename );
        return;
      }

      var targetDataPath =  targetExecutableFileInfo.Directory.FullName +
                          Path.DirectorySeparatorChar +
                          Path.GetFileNameWithoutExtension( targetExecutableFileInfo.Name ) +
                          "_Data";

      var settingsTarDir = $"{targetDataPath}{Path.DirectorySeparatorChar}{IO.Utils.RuntimeSettingsDirectory}";
      if ( !File.Exists( settingsTarDir ) )
        Directory.CreateDirectory( settingsTarDir );

      foreach ( var settings in runtimeSettings ) {
        IAGXUnitySetting instance = (IAGXUnitySetting)settings.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
        instance.Save();
        File.Copy(
          IO.Utils.EditorSettingDirectory + instance.SettingsFile,
          $"{settingsTarDir}{instance.SettingsFile}",
          true );
      }
    }
  }
#endif
}
