using AGXUnity;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.Utils
{
  public static class AssetFactory
  {
    public static T Create<T>( string name ) where T : ScriptAsset
    {
      string path = Selection.activeObject == null ? "Assets" : AssetDatabase.GetAssetPath( Selection.activeObject );
      if ( System.IO.Path.GetExtension( path ) != "" ) {
        Debug.Log( "Not valid to create asset in an asset that isn't a folder." );
        return null;
      }

      T asset = ScriptAsset.Create<T>();

      string pathAndName = AssetDatabase.GenerateUniqueAssetPath( path + "/" + name + ".asset" );

      AssetDatabase.CreateAsset( asset, pathAndName );

      AssetDatabase.SaveAssets();
      EditorUtility.FocusProjectWindow();
      Selection.activeObject = asset;

      return asset;
    }
  }
}
