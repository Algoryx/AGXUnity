using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using AGXUnity;

namespace AGXUnityEditor
{
  public static class AssetsMenu
  {
    [MenuItem( "Assets/AGXUnity/Shape Material" )]
    public static UnityEngine.Object CreateMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ShapeMaterial>( "material" );
    }

    [MenuItem( "Assets/AGXUnity/Contact Material" )]
    public static UnityEngine.Object CreateContactMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ContactMaterial>( "contact material" );
    }

    [MenuItem( "Assets/AGXUnity/Friction Model" )]
    public static UnityEngine.Object CreateFrictionModel()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<FrictionModel>( "friction model" );
    }

    [MenuItem( "Assets/AGXUnity/Cable Properties" )]
    public static UnityEngine.Object CreateCableProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<CableProperties>( "cable properties" );
    }

    [MenuItem( "Assets/AGXUnity/Geometry Contact Merge Split Thresholds" )]
    public static UnityEngine.Object CreateGeometryContactMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<GeometryContactMergeSplitThresholds>( "contact merge split thresholds" );
    }

    [MenuItem( "Assets/AGXUnity/Constraint Merge Split Thresholds" )]
    public static UnityEngine.Object CreateConstraintMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ConstraintMergeSplitThresholds>( "constraint merge split thresholds" );
    }

    [MenuItem("Assets/AGXUnity/Two Body Tire Properties")]
    public static UnityEngine.Object CreateTwoBodyTireProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<TwoBodyTireProperties>("two body tire properties");
    }


    [MenuItem( "Assets/Import AGX file as prefab", validate = true )]
    public static bool IsAGXFileSelected()
    {
      bool agxFileFound = false;
      foreach ( var obj in Selection.objects ) {
        var assetPath = AssetDatabase.GetAssetPath( obj );
        if ( assetPath == "" )
          continue;

        agxFileFound = agxFileFound ||
                       IO.AGXFileInfo.IsExistingAGXFile( new System.IO.FileInfo( assetPath ) );
      }
      return agxFileFound;
    }

    [MenuItem( "Assets/Import AGX file as prefab" )]
    public static void GenerateAGXFileAsPrefab()
    {
      foreach ( var obj in Selection.objects ) {
        var info = new IO.AGXFileInfo( AssetDatabase.GetAssetPath( obj ) );
        if ( info.Type != IO.AGXFileInfo.FileType.AGXBinary && info.Type != IO.AGXFileInfo.FileType.AGXAscii )
          continue;

        AssetPostprocessorHandler.ReadAGXFile( info );
      }
    }

    [MenuItem( "Assets/AGXUnity/Utils/Patch AGXUnity asset(s)" )]
    public static void ConvertDeprecatedToAGXUnity()
    {
      var hasFolder = false;
      var hasFile = false;

      foreach ( var guid in Selection.assetGUIDs ) {
        var isFolder = AssetDatabase.IsValidFolder( AssetDatabase.GUIDToAssetPath( guid ) );
        hasFolder = hasFolder || isFolder;
        hasFile = hasFile || !isFolder;
      }

      var searchSubFolders = hasFolder && EditorUtility.DisplayDialog( "Patch AGXUnity assets(s)", "Search sub-folders for files to patch?", "Yes", "No" );
      if ( hasFolder )
        System.Threading.Thread.Sleep( 250 );
      var saveBackup = ( hasFolder || hasFile ) && EditorUtility.DisplayDialog( "Patch AGXUnity assets(s)", "Save backup of affected files?", "Yes", "No" );

      var dllToScriptResolver = new IO.DllToScriptResolver();
      if ( !dllToScriptResolver.IsValid )
        return;

      var numChanged = 0;
      foreach ( var guid in Selection.assetGUIDs ) {
        var localAssetPath = AssetDatabase.GUIDToAssetPath( guid ).Remove( 0, "Assets".Length );
        var path = UnityEngine.Application.dataPath + localAssetPath;
        if ( AssetDatabase.IsValidFolder( AssetDatabase.GUIDToAssetPath( guid ) ) ) {
          try {
            numChanged += dllToScriptResolver.PatchFilesInDirectory( path,
                                                                     searchSubFolders ?
                                                                       System.IO.SearchOption.AllDirectories :
                                                                       System.IO.SearchOption.TopDirectoryOnly,
                                                                     saveBackup );
          }
          catch ( Exception e ) {
            UnityEngine.Debug.LogException( e );
          }
        }
        else {
          try {
            numChanged += System.Convert.ToInt32( dllToScriptResolver.PatchFile( path, saveBackup ) );
          }
          catch ( Exception e ) {
            UnityEngine.Debug.LogException( e );
          }
        }

        UnityEngine.Debug.Log( "Number of files changed: " + numChanged );
      }
    }
  }
}
