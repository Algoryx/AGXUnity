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
    private static List<string> m_additionalDependencies = new List<string>()
    {
      "python35.dll",
      "vcruntime140.dll",
      "msvcp140.dll"
    };

    public static bool IsAdditionalDependency( string modulePath )
    {
      foreach ( var additionalDependencyPath in m_additionalDependencies )
        if ( modulePath.EndsWith( additionalDependencyPath ) )
          return true;
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

      var agxDir = IO.Utils.AGXDynamicsDirectory;
      if ( string.IsNullOrEmpty( agxDir ) ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX Dynamics directory.", Color.red ) );
        return;
      }

      var agxPluginPath = AGXUnity.IO.Utils.GetEnvironmentVariable( "AGX_PLUGIN_PATH" );
      if ( string.IsNullOrEmpty( agxPluginPath ) ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX_PLUGIN_PATH.", Color.red ) );
        return;
      }

      var loadedAgxModulesPaths = new List<string>();
      var process = Process.GetCurrentProcess();
      foreach ( ProcessModule module in process.Modules ) {
        if ( module.FileName.IndexOf( "[In Memory]" ) >= 0 )
          continue;

        if ( module.FileName.IndexOf( agxDir ) == 0 )
          loadedAgxModulesPaths.Add( module.FileName );
        else if ( IsAdditionalDependency( module.FileName ) )
          loadedAgxModulesPaths.Add( module.FileName );
      }

      // TODO: Add wildcard to additional dependencies and search for them
      //       in AGX Dynamics installed directory if not loaded.

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

      // dllTargetPath:      ./<name>_Data/Plugins
      // agxRuntimeDataPath: ./<name>_Data/Plugins/agx
      var dllTargetPath      = AGXUnity.IO.Utils.GetPlayerPluginPath( dataPath );
      var agxRuntimeDataPath = AGXUnity.IO.Utils.GetPlayerAGXRuntimePath( dataPath );

      if ( !Directory.Exists( agxRuntimeDataPath ) )
        Directory.CreateDirectory( agxRuntimeDataPath );

      if ( !Directory.Exists( dllTargetPath ) )
        Directory.CreateDirectory( dllTargetPath );

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
