using AGXUnity;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  [AttributeUsage( AttributeTargets.Class, AllowMultiple = false )]
  public class PreviousSettingsFile : Attribute
  {
    public string FileName;
  }

  public class AGXUnityEditorSettings<T> : AGXUnitySettings<T>
    where T : AGXUnityEditorSettings<T>
  {
    public static new T GetOrCreateInstance()
    {
      T instance = CreateInstance<T>();
      try {
        var json = File.ReadAllText( Instance.SettingsPath );
        JsonUtility.FromJsonOverwrite( json, instance );
      }
      catch {
        var prevFile = typeof(T).GetCustomAttribute<PreviousSettingsFile>();
        var created = true;
        if ( prevFile != null && !IO.Utils.IsPackageContext ) {
          var assetPath = IO.Utils.AGXUnityEditorDirectory + "/Data/" + prevFile.FileName;
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
}
