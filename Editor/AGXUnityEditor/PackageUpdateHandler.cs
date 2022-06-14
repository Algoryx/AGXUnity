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
      return AGXUnity.VersionInfo.FromFile( IO.Utils.AGXUnityPackageDirectory +
                                            Path.DirectorySeparatorChar +
                                            "package.json" );
    }

    public static bool Install( FileInfo packageFileInfo )
    {
      if ( packageFileInfo == null ) {
        Debug.LogError( "Error: Unable to install AGX Dynamics for Unity - file info is null." );
        return false;
      }

      if ( !packageFileInfo.Exists ) {
        Debug.LogError( $"Error: Unable to install package from {packageFileInfo.FullName} - file doesn't exit." );
        return false;
      }

      if ( !EditorUtility.DisplayDialog( "AGX Dynamics for Unity update",
                                         $"AGX Dynamics for Unity is about to be updated, make sure all " +
                                         $"File Explorer and/or terminals in {IO.Utils.AGXUnityPackageDirectory} " +
                                         $"are closed during this process.\n\nAny new files or file modifications " +
                                         $"made prior to this update in the {IO.Utils.AGXUnityPackageDirectory} " +
                                         $"will be deleted in this process.\n\nAre all files/directories in {IO.Utils.AGXUnityPackageDirectory} " +
                                         $"backed up and all explorers and terminals closed and ready for install?",
                                         "Yes",
                                         "No" ) )
        return false;

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
          return false;

        if ( decision == 0 )
          EditorSceneManager.SaveScenes( unsavedScenes );
      }

      EditorSceneManager.NewScene( UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                                   UnityEditor.SceneManagement.NewSceneMode.Single );

      Debug.Log( "Preparing native plugins before restart..." );
#if UNITY_2019_1_OR_NEWER
      foreach ( var nativePlugin in NativePlugins ) {
        nativePlugin.isPreloaded = false;
        nativePlugin.SaveAndReimport();
      }
#else
      Debug.Log( $"Removing {IO.Utils.AGXUnityPluginDirectoryFull} from PATH ..." );
      var notInPathOrRemovedFromPath = !AGXUnity.IO.Environment.IsInPath( IO.Utils.AGXUnityPluginDirectoryFull ) ||
                                       AGXUnity.IO.Environment.RemoveFromPath( IO.Utils.AGXUnityPluginDirectoryFull );
      if ( notInPathOrRemovedFromPath )
        Debug.Log( "    ... success." );
      else {
        Debug.LogWarning( "    ...failed. This could cause plugins to be loaded post reload - leading to errors." );
        return false;
      }

      foreach ( var nativePlugin in NativePlugins ) {
        nativePlugin.SetCompatibleWithEditor( false );
        nativePlugin.SetCompatibleWithAnyPlatform( false );
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
      return true;
    }

    private static void PostReload()
    {
      EditorApplication.LockReloadAssemblies();

      try {
        Debug.Log( "Verifying native libraries aren't loaded..." );
        {
          var processModules  = Process.GetCurrentProcess().Modules;
          var nativePluginsId = new DirectoryInfo( IO.Utils.AGXUnityPluginDirectory );
          foreach ( ProcessModule processModule in processModules ) {
            if ( processModule.FileName.Contains( nativePluginsId.FullName ) )
              throw new System.Exception( $"AGX Dynamics module {processModule.ModuleName} is loaded. Unable to install new version of package." );
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
          catch ( System.Exception ) {
            Debug.LogError( "Fatal update error: Close Unity and remove AGX Dynamics for Unity directory and install the latest version." );
            throw;
          }
        }

        var newPackageFileInfo = new FileInfo( packageName );
        if ( newPackageFileInfo.Name.EndsWith( "_Plugins_x86_64.unitypackage" ) ) {
          // Manager is loaded when the binaries hasn't been added and ExternalAGXInitializer
          // pops up and asks if the user would like to located AGX Dynamics. We're
          // installing the binaries so it's a "user said no!" to begin with.
          ExternalAGXInitializer.UserSaidNo = true;

          var dataDirectory = IO.Utils.AGXUnityPluginDirectory +
                              Path.DirectorySeparatorChar +
                              "agx";
          if ( Directory.Exists( dataDirectory ) ) {
            var directoryHandler = new IO.DirectoryContentHandler( dataDirectory );
            Debug.Log( $"Removing AGX Dynamics data directory {directoryHandler.RootDirectory}..." );
            directoryHandler.DeleteAllCollected();
          }
        }
        else {
          Debug.Log( "Removing all non-user specific content..." );
          IO.DirectoryContentHandler.DeleteContent();
        }

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

    internal static IEnumerable<PluginImporter> NativePlugins
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
  }
}
