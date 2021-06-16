using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AGXUnityUpdate.Detail
{
  /// <summary>
  /// Patching AGX Dynamics for Unity import in Unity 2019.4 and later.
  /// 
  /// During update, we're deleting all our scripts, dll's and assets, then
  /// we call AssetDatabase.ImportPackage with the new version. This is done
  /// from AGXUnityEditor.dll which has to be unloaded with the updated version.
  /// This works in 2019.3 and earlier but may/will put the editor in an
  /// undefined state in later versions. E.g., AGXUnityEditor.dll isn't, or
  /// only partly, updated - resulting in various issues such as compile errors
  /// and/or crash when AGX Dynamics is called.
  /// 
  /// This script is listening to AssetDatabase.importPackageCompleted and
  /// EditorApplication.update from our InitializeOnLoad constructor. If
  /// nothing is happening we remove ourselves from EditorApplication.update
  /// but keep waiting for packages to be imported.
  /// 
  /// 2019.4: It's enough to re-import from the AGXUnity root directory. Similar
  ///         to right-click on the AGXUnity directory and click "Reimport".
  /// 2020.1: A "Reimport" isn't enough. Re-import and adding a define symbol
  ///         will resolve the issues. When AGXUnity and AGXUnityEditor is
  ///         properly loaded the toolbar assert occurs so we're adding an
  ///         additional define symbol to trigger compilation again.
  /// </summary>
  [InitializeOnLoad]
  internal static class AGXUnityImportCleanup
  {
    private static readonly string OnImportDefineSymbol = "AGXUNITY_ON_IMPORT";

    private static bool HasOngoingDefineSymbols
    {
      get { return DefineSymbols.Contains( OnImportDefineSymbol ); }
    }

    static AGXUnityImportCleanup()
    {
      if ( !EditorApplication.isPlayingOrWillChangePlaymode ) {
        AssetDatabase.importPackageCompleted += OnImportCompleted;
        EditorApplication.update += OnEditorUpdate;
      }
    }

    [MenuItem( "AGXUnity/Utils/Update Cleanup" )]
    private static void UpdateCleanup()
    {
      AssetDatabase.ImportAsset( AGXUnityDirectory,
                                 ImportAssetOptions.ImportRecursive |
                                 ImportAssetOptions.DontDownloadFromCacheServer |
                                 ImportAssetOptions.ForceSynchronousImport );
#if UNITY_2020_1_OR_NEWER
      EditorApplication.update += OnEditorUpdate;
      DefineSymbols.Add( OnImportDefineSymbol );
#endif
    }

    private static void OnImportCompleted( string package )
    {
      var filename = Path.GetFileName( package );
      if ( filename.StartsWith( "AGXDynamicsForUnity" ) ) {
        Debug.Log( $"Finishing up import of: {filename}" );
        UpdateCleanup();
      }
    }

    private static string AGXUnityDirectory
    {
      get
      {
        if ( Directory.Exists( "Assets/AGXUnity" ) )
          return "Assets/AGXUnity";

        var dir = Directory.GetFiles( "Assets", "RigidBody.cs", SearchOption.AllDirectories )
                           .Where( filename => new FileInfo( filename ).Directory.Parent.Name == "AGXUnity" )
                           .Select( filename => new FileInfo( filename ).Directory.Parent ).FirstOrDefault();
        if ( dir == null )
          return string.Empty;

        var path = dir.FullName.Replace( '\\', '/' );
        var indexOfAssetsDirectory = path.LastIndexOf( "Assets/" );
        if ( indexOfAssetsDirectory < 0 )
          return string.Empty;

        return path.Remove( 0, indexOfAssetsDirectory );
      }
    }

    private static void ShowNotification( string message, double fadeoutWait )
    {
#if UNITY_2019_1_OR_NEWER
      foreach ( SceneView sceneView in SceneView.sceneViews )
        sceneView.ShowNotification( new GUIContent( message ), fadeoutWait );
#endif
    }

    private static void OnEditorUpdate()
    {
      if ( EditorApplication.isCompiling ) {
        if ( HasOngoingDefineSymbols )
          ShowNotification( "AGX Dynamics for Unity is being installed...", 1.0 );
        return;
      }

      if ( DefineSymbols.Contains( OnImportDefineSymbol ) )
        DefineSymbols.Remove( OnImportDefineSymbol );
      else 
        EditorApplication.update -= OnEditorUpdate;
    }

    private static class DefineSymbols
    {
      internal static void Add( string define )
      {
        if ( Contains( define ) ) {
          Debug.LogWarning( $"DefineSymbols.Add: Define symbol already exist: {define}" );
          return;
        }

        var defines = Get();
        defines.Add( define );
        Set( defines );
      }

      internal static void Remove( string define )
      {
        if ( !Contains( define ) ) {
          Debug.LogWarning( $"DefineSymbols.Remove: Define symbol doesn't exist: {define}" );
          return;
        }

        var defines = Get();
        defines.Remove( define );
        Set( defines );
      }

      internal static bool Contains( string define )
      {
        return Get().Any( defineSymbol => defineSymbol == define );
      }

      private static void Set( List<string> symbols )
      {
        var symbolsToSet = symbols.Count == 0 ?
                             "" :
                           symbols.Count == 1 ?
                             symbols[ 0 ] :
                             string.Join( ";", symbols );
        PlayerSettings.SetScriptingDefineSymbolsForGroup( BuildTargetGroup.Standalone, symbolsToSet );
        AssetDatabase.SaveAssets();
      }

      private static List<string> Get()
      {
        return PlayerSettings.GetScriptingDefineSymbolsForGroup( BuildTargetGroup.Standalone ).Split( ';' ).ToList();
      }
    }
  }
}
