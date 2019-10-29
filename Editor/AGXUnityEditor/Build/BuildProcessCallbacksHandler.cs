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
      "python35.dll"
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
      if ( agxDir == string.Empty ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX Dynamics directory.", Color.red ) );
        return;
      }

      var agxPluginsDir = AGXUnity.IO.Utils.GetEnvironmentVariable( "AGX_PLUGIN_PATH" );
      if ( agxPluginsDir == null || agxPluginsDir == string.Empty ) {
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

      if ( loadedAgxModulesPaths.Count == 0 ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Copy AGX Dynamics binaries - no binaries found in current process.", Color.red ) );
        return;
      }

      var targetExecutableFileInfo = new FileInfo( targetPathFilename );
      if ( !targetExecutableFileInfo.Exists ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( "Target executable doesn't exist: ", Color.red ) + targetPathFilename );
        return;
      }

      var dataDir = targetExecutableFileInfo.Directory.FullName + Path.DirectorySeparatorChar + PlayerSettings.productName + "_Data";
      var dataPluginsDir = dataDir + Path.DirectorySeparatorChar + "Plugins";
      Debug.Log( Utils.GUI.AddColorTag( "Everything seems alright.", Color.green ) );
      Debug.Log( Utils.GUI.AddColorTag( "Directory:         ", Color.green ) + targetExecutableFileInfo.Directory.FullName );
      Debug.Log( Utils.GUI.AddColorTag( "Data Directory:    ", Color.green ) + dataDir );
      Debug.Log( Utils.GUI.AddColorTag( "Plugins Directory: ", Color.green ) + dataPluginsDir );
      Debug.Log( "Dependent plugins to copy:" );
      foreach ( var modulePath in loadedAgxModulesPaths )
        Debug.Log( "    " + modulePath );

      if ( !Directory.Exists( dataPluginsDir ) )
        Directory.CreateDirectory( dataPluginsDir );

      foreach ( var modulePath in loadedAgxModulesPaths ) {
        var moduleFileInfo = new FileInfo( modulePath );
        moduleFileInfo.CopyTo( dataPluginsDir + Path.DirectorySeparatorChar + moduleFileInfo.Name, true );
      }

      CopyDirectory( new DirectoryInfo( agxPluginsDir + Path.DirectorySeparatorChar + "Components" ),
                     new DirectoryInfo( dataPluginsDir + Path.DirectorySeparatorChar + "Components" ) );
    }
  }
}
