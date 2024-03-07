using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[AttributeUsage( AttributeTargets.Class, AllowMultiple = false )]
public class PreviousSettingsFile : Attribute
{
  public string FileName;
}

public class AGXUnitySettings<T> : ScriptableObject
  where T : AGXUnitySettings<T>
{
  public static string s_packageSettingsDirectory => "ProjectSettings/Packages/com.algoryx.agxunity/";
  public static string s_settingsPath => s_packageSettingsDirectory + typeof( T ).Name + ".json";

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

  protected virtual void OnCreated() { }

  public void Save()
  {
    if ( m_serializing )
      return;
    m_serializing = true;
    var json = JsonUtility.ToJson( this );
    File.WriteAllText( s_settingsPath, json );
    m_serializing = false;
  }

  private static bool m_serializing = false;

  public static T GetOrCreateInstance()
  {
    T instance = CreateInstance<T>();
    try {
      var json = File.ReadAllText( s_settingsPath );
      JsonUtility.FromJsonOverwrite( json, instance );
    }
    catch {
      var prevFile = typeof(T).GetCustomAttribute<PreviousSettingsFile>();
      var created = true;
      if ( prevFile != null && !AGXUnityEditor.IO.Utils.IsPackageContext ) {
        var assetPath = AGXUnityEditor.IO.Utils.AGXUnityEditorDirectory + "/Data/" + prevFile.FileName;
        var old = AssetDatabase.LoadAssetAtPath<T>( assetPath );
        if ( old != null ) {
          Debug.Log( $"Converting old asset-settings file '{assetPath}'" );
          var oldSO = new SerializedObject(old);
          var newSO = new SerializedObject(instance);
          var prop = oldSO.GetIterator();
          prop.Next( true );
          do
            newSO.CopyFromSerializedProperty( prop );
          while ( prop.Next( false ) );
          newSO.ApplyModifiedProperties();
          created = false;
          AssetDatabase.DeleteAsset( assetPath );
        }
      }

      if ( created == true )
        instance.OnCreated();
      instance.Save();
    }
    return instance;
  }
}
