using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

using Debug = UnityEngine.Debug;

namespace AGXUnityEditor.Build
{
  public static class BuildProcessCallbacksHandler
  {
    /// <summary>
    /// Potential "out-of-install" dependencies. E.g., Python binary
    /// which either is loaded from AGX Dynamics directories or, e.g.,
    /// C:\Program Files\Python35\python35.dll when using external Python.
    /// </summary>
    private static List<string> m_ooiDependencies = new List<string>()
    {
      "python[3-9]{1}[0-9]{1}.dll"
    };

    /// <summary>
    /// Additional mandatory dependencies that may not appear with
    /// path in loaded modules. These dependencies are, e.g., needed
    /// when the player is used on non-developer/clean environments.
    /// </summary>
    private static List<string> m_additionalDependencies = new List<string>()
    {
      "vcruntime[0-9]{3}.dll",
      "msvcp[0-9]{3}.dll"
    };

    /// <summary>
    /// Finds if <paramref name="modulePath"/> matches out-of-install
    /// filename and/or regex wildcard.
    /// </summary>
    /// <param name="modulePath">Module path including filename.</param>
    /// <returns>True if match - otherwise false.</returns>
    public static bool IsOutOfInstallDependency( string modulePath )
    {
      foreach ( var dependency in m_ooiDependencies ) {
        var moduleFileInfo = new FileInfo( modulePath );
        if ( System.Text.RegularExpressions.Regex.IsMatch( moduleFileInfo.Name, dependency ) ) {
          return true;
        }
      }
      return false;
    }

    public static void CopyDirectory( DirectoryInfo source, DirectoryInfo destination )
    {
      if ( !destination.Exists )
        destination.Create();

      foreach ( var fileInfo in source.GetFiles() )
        fileInfo.CopyTo( Path.Combine( destination.FullName, fileInfo.Name ), true );

      foreach ( var directoryInfo in source.GetDirectories() )
        CopyDirectory( directoryInfo, new DirectoryInfo( Path.Combine( destination.FullName, directoryInfo.Name ) ) );
    }

    [PostProcessBuild( 16 )]
    private static void OnPostProcessBuild( BuildTarget target, string targetPathFilename )
    {
      if ( !EditorSettings.Instance.BuildPlayer_CopyBinaries )
        return;

      if ( target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unsupported build target: ",
                                                 Color.red ) +
                          target.ToString() );
        return;
      }

      var nativeIsX64 = agx.agxSWIG.isBuiltWith( agx.BuildConfiguration.USE_64BIT_ARCHITECTURE );
      if ( (target == BuildTarget.StandaloneWindows64) != nativeIsX64 ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - x86/x64 architecture mismatch: ", Color.red ) +
                          "Build target = " + target.ToString() + ", AGX Dynamics build: " + ( nativeIsX64 ? "x64" : "x86" ) );
        return;
      }

      var agxDynamicsPath = AGXUnity.IO.Environment.AGXDynamicsPath;
      if ( string.IsNullOrEmpty( agxDynamicsPath ) ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX Dynamics directory.", Color.red ) );
        return;
      }

      var agxPluginPath = AGXUnity.IO.Environment.Get( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH );
      if ( string.IsNullOrEmpty( agxPluginPath ) ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX_PLUGIN_PATH.", Color.red ) );
        return;
      }

      // Finding loaded modules/binaries in current process located
      // in current environment AGX Dynamics directory. Additional
      // modules/binaries that are optional, i.e., possibly located
      // in another directory, are also included here.
      var loadedAgxModulesPaths = new List<string>();
      var process = Process.GetCurrentProcess();
      foreach ( ProcessModule module in process.Modules ) {
        if ( module.FileName.IndexOf( "[In Memory]" ) >= 0 )
          continue;

        var isMatch = module.FileName.IndexOf( agxDynamicsPath ) == 0 ||
                      IsOutOfInstallDependency( module.FileName );
        if ( isMatch )
          loadedAgxModulesPaths.Add( module.FileName );
      }

      // Finding additional modules/binaries which an AGX Dynamics
      // runtime may depend on, e.g., vcruntimeIII.dll and msvcpIII.dll.
      var agxDepDir = AGXUnity.IO.Environment.Get( AGXUnity.IO.Environment.Variable.AGX_DEPENDENCIES_DIR );
      if ( !string.IsNullOrEmpty( agxDepDir ) ) {
        var agxDepDirInfo = new DirectoryInfo( agxDepDir );
        foreach ( var file in agxDepDirInfo.EnumerateFiles( "*.dll", SearchOption.AllDirectories ) ) {
          foreach ( var dependency in m_additionalDependencies )
            if ( System.Text.RegularExpressions.Regex.IsMatch( file.Name, dependency ) )
              loadedAgxModulesPaths.Add( file.FullName );
        }
      }

      if ( loadedAgxModulesPaths.Count == 0 ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - no binaries found in current process.", Color.red ) );
        return;
      }

      var targetExecutableFileInfo = new FileInfo( targetPathFilename );
      if ( !targetExecutableFileInfo.Exists ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Target executable doesn't exist: ", Color.red ) + targetPathFilename );
        return;
      }


      // Application.dataPath is 'Assets' folder here in Editor but
      // Application.dataPath is '<name>_Data' in the Player. We're
      // explicitly constructing '<name>_Data' here.
      var dataPath = targetExecutableFileInfo.Directory.FullName +
                     Path.DirectorySeparatorChar +
                     Path.GetFileNameWithoutExtension( targetExecutableFileInfo.Name ) +
                     "_Data";

      // dllTargetPath: ./<name>_Data/Plugins
      var dllTargetPath = AGXUnity.IO.Environment.GetPlayerPluginPath( dataPath );
      if ( !Directory.Exists( dllTargetPath ) )
        Directory.CreateDirectory( dllTargetPath );

      // agxRuntimeDataPath: ./<name>_Data/Plugins/agx
      var agxRuntimeDataPath = AGXUnity.IO.Environment.GetPlayerAGXRuntimePath( dataPath );
      if ( !Directory.Exists( agxRuntimeDataPath ) )
        Directory.CreateDirectory( agxRuntimeDataPath );

      // Unclear when or if the bug of having native dlls in the data plugins
      // folder has been solved:
      //     https://forum.unity.com/threads/dll-not-found-with-standalone-app-but-works-fine-in-editor.389392/page-2
      // Works with 2019.2.4, so for earlier version we copy the dlls to the
      // root folder of the executable.
#if !UNITY_2019_2_4_OR_NEWER
      dllTargetPath = targetExecutableFileInfo.Directory.FullName;
#endif

      Debug.Log( "Copying Components to: " + Utils.GUI.AddColorTag( agxRuntimeDataPath + @"\Components", Color.green ) );
      CopyDirectory( new DirectoryInfo( agxPluginPath + Path.DirectorySeparatorChar + "Components" ),
                     new DirectoryInfo( agxRuntimeDataPath + Path.DirectorySeparatorChar + "Components" ) );

      foreach ( var modulePath in loadedAgxModulesPaths ) {
        var moduleFileInfo = new FileInfo( modulePath );
        try {
          moduleFileInfo.CopyTo( dllTargetPath + Path.DirectorySeparatorChar + moduleFileInfo.Name, true );
          Debug.Log( "Successfully copied: " +
                     Utils.GUI.AddColorTag( dllTargetPath + Path.DirectorySeparatorChar, Color.green ) +
                     Utils.GUI.AddColorTag( moduleFileInfo.Name, Color.Lerp( Color.blue, Color.white, 0.75f ) ) );
        }
        catch ( Exception e ) {
          Debug.Log( "Failed copying: " +
                     Utils.GUI.AddColorTag( dllTargetPath + Path.DirectorySeparatorChar, Color.red ) +
                     Utils.GUI.AddColorTag( moduleFileInfo.Name, Color.red ) +
                     ": " + e.Message );
        }
      }
    }
  }
}
