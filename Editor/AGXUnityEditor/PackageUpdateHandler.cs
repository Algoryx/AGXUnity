using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using Debug = UnityEngine.Debug;
using SceneManager = UnityEngine.SceneManagement.SceneManager;
using Scene = UnityEngine.SceneManagement.Scene;
using EditorSceneManager = UnityEditor.SceneManagement.EditorSceneManager;

namespace AGXUnityEditor
{
  public static class PackageUpdateHandler
  {
    public static AGXUnity.VersionInfo FindCurrentVersion()
    {
      var packageJsonFile = IO.Utils.AGXUnityPackageDirectory +
                            Path.DirectorySeparatorChar +
                            "package.json";
      var data = new PackageData();
      if ( File.Exists( packageJsonFile ) )
        EditorJsonUtility.FromJsonOverwrite( File.ReadAllText( packageJsonFile ),
                                             data );
      return AGXUnity.VersionInfo.Parse( data.version );
    }

    public static void Install( FileInfo packageFileInfo )
    {
      if ( packageFileInfo == null ) {
        Debug.LogError( "Error: Unable to install AGX Dynamics for Unity - file info is null." );
        return;
      }

      if ( !packageFileInfo.Exists ) {
        Debug.LogError( $"Error: Unable to install package from {packageFileInfo.FullName} - file doesn't exit." );
        return;
      }

      if ( !EditorUtility.DisplayDialog( "AGX Dynamics for Unity update",
                                         $"AGX Dynamics for Unity is about to be updated, make sure all" +
                                         $"File Explorer and/or terminals in {IO.Utils.AGXUnityPackageDirectory}" +
                                         $"are closed during this process.\n\nAny new files or file modifications " +
                                         $"made prior to this update in the {IO.Utils.AGXUnityPackageDirectory} " +
                                         $"will be deleted in this process.\n\nAre all files/directories in {IO.Utils.AGXUnityPackageDirectory} " +
                                         $"backed up and all explorers and terminals closed and ready for install?",
                                         "Yes",
                                         "No" ) )
        return;

      var unsavedScenes = ( from scene in Scenes where scene.isDirty select scene ).ToArray();
      if ( unsavedScenes.Length > 0 ) {
        var saveSceneInstruction = "AGX Dynamics for Unity is about to be updated. " +
                                   "A new empty scene is required for AGXUnity to update. " +
                                   "The following scenes aren't saved:\n";
        foreach ( var scene in unsavedScenes )
          saveSceneInstruction += '\n' + scene.name;
        saveSceneInstruction += "\n\n" + "Select Cancel to abort the update.";

        var decision = EditorUtility.DisplayDialogComplex( "Save unsaved scenes?",
                                                            saveSceneInstruction,
                                                            "Save and continue",
                                                            "Cancel",
                                                            "Continue without saving" );
        if ( decision == 1 )
          return;

        if ( decision == 0 )
          EditorSceneManager.SaveScenes( unsavedScenes );
      }

      EditorSceneManager.NewScene( UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                                   UnityEditor.SceneManagement.NewSceneMode.Single );

#if UNITY_2019_1_OR_NEWER
      Debug.Log( "Preparing native plugins before restart..." );
      foreach ( var nativePlugin in NativePlugins ) {
        nativePlugin.isPreloaded = false;
        nativePlugin.SaveAndReimport();
      }
#endif

      Debug.Log( "Preparing AGX Dynamics for Unity before restart..." );
      Build.DefineSymbols.Add( Build.DefineSymbols.ON_AGXUNITY_UPDATE );

      Debug.Log( "Restarting Unity..." );
      EditorApplication.OpenProject( Path.Combine( Application.dataPath, ".." ),
                                     "-executeMethod",
                                     "AGXUnityEditor.PackageUpdateHandler.PostReload",
                                     s_packageIdentifier + packageFileInfo.FullName.Replace( '\\', '/' ) );
    }

    private static void PostReload()
    {
      EditorApplication.LockReloadAssemblies();

      try {
        Debug.Log( "Verifying native libraries aren't loaded..." );
        {
          var processModules  = Process.GetCurrentProcess().Modules;
          var nativePlugins   = NativePlugins.ToArray();
          var nativePluginsId = IO.Utils.AGXUnityPluginDirectory;
          foreach ( ProcessModule processModule in processModules ) {
            if ( processModule.FileName.Contains( nativePluginsId ) )
              throw new System.Exception( $"Module {processModule.FileName} is loaded. Unable to install new version of package." );
          }
        }

        var packageName = string.Empty;
        foreach ( var arg in System.Environment.GetCommandLineArgs() ) {
          if ( arg.StartsWith( s_packageIdentifier ) )
            packageName = arg.Substring( s_packageIdentifier.Length );
        }

        if ( string.IsNullOrEmpty( packageName ) )
          throw new System.Exception( $"Unable to find package name identifier {s_packageIdentifier} in arguments list: " +
                                      string.Join( " ", System.Environment.GetEnvironmentVariables() ) );

        Debug.Log( "Removing old native binaries..." );
        foreach ( var nativePlugin in NativePlugins ) {
          var fi = new FileInfo( nativePlugin.assetPath );
          Debug.Log( $"    - {fi.Name}" );
          var fiMeta = new FileInfo( nativePlugin.assetPath + ".meta" );
          try {
            fi.Delete();
            if ( fiMeta.Exists )
              fiMeta.Delete();
          }
          catch ( System.Exception e ) {
            Debug.LogException( e );
            Debug.LogError( "Fatal update error: Close Unity and remove AGX Dynamics for Unity directory and install the latest version." );
            return;
          }
        }

        Debug.Log( "Removing all non-user specific content..." );
        IO.DirectoryContentHandler.DeleteContent();

        // This will generate compile errors from scripts using AGXUnity and
        // we don't know how to hide these until we're done, mainly because
        // we have no idea when we're done with the update.
        // 
        // If this isn't performed, the compiler will throw exception due
        // to missing references and Unity will crash the first time we
        // use AGX Dynamics.
        AssetDatabase.Refresh();

        Debug.Log( $"Starting import of package: {packageName}" );
        AssetDatabase.ImportPackage( packageName, false );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }

      Build.DefineSymbols.Remove( Build.DefineSymbols.ON_AGXUNITY_UPDATE );
      EditorApplication.UnlockReloadAssemblies();
    }

    private static IEnumerable<PluginImporter> NativePlugins
    {
      get
      {
        var pluginId = IO.Utils.AGXUnityPluginDirectory;
        foreach ( var plugin in PluginImporter.GetAllImporters() )
          if ( plugin.isNativePlugin && plugin.assetPath.Contains( pluginId ) )
            yield return plugin;
      }
    }

    private static IEnumerable<Scene> Scenes
    {
      get
      {
        for ( int i = 0; i < SceneManager.sceneCount; ++i )
          yield return SceneManager.GetSceneAt( i );
      }
    }

    private static IEnumerable<Scene> LoadedScenes
    {
      get
      {
        foreach ( var scene in Scenes )
          if ( scene.isLoaded )
            yield return scene;
      }
    }

    private static readonly string s_packageIdentifier = "package_name:";

    private class PackageData
    {
      public string version = string.Empty;
    }
  }
}
