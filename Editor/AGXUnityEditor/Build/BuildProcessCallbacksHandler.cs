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
    /// Creates instances of objects that may load dlls dynamically
    /// during runtime. E.g., Terrain: openvdb with its dependencies.
    /// </summary>
    public class DynamicallyLoadedDependencies : IDisposable
    {
      public DynamicallyLoadedDependencies()
      {
        m_disposables.Add( new agxTerrain.Terrain( 2, 2, 1, 10.0 ) );
      }

      public void Dispose()
      {
        for ( int i = 0; i < m_disposables.Count; ++i ) {
          m_disposables[ i ].Dispose();
          m_disposables[ i ] = null;
        }
        m_disposables.Clear();
      }

      private List<IDisposable> m_disposables = new List<IDisposable>();
    }

    /// <summary>
    /// Potential "out-of-install" dependencies. E.g., Python binary
    /// which either is loaded from AGX Dynamics directories or, e.g.,
    /// C:\Program Files\Python35\python35.dll when using external Python.
    /// </summary>
    private static List<string> m_ooiDependencies = new List<string>()
    {
      "python[3-9]{1}[0-9]{1}.dll",
      "tbb.dll" // Unity is using Thread Building Blocks so it won't appear as an AGX dll.
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
    /// Dynamically loaded dependencies are for some reason not possible
    /// to load from name_Data/Plugins folder. These has to be copied to
    /// where the executable is located.
    /// </summary>
#if UNITY_2019_2_OR_NEWER
    private static List<string> m_unsupportedPluginPathDependencies = new List<string>()
    {
      "tbb.dll",
      "Half.dll",
      "openvdb.dll",
      "vdbgrid.DLL"
    };
#endif

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

    /// <summary>
    /// Finds if module is supported to be located in name_Data/Plugins
    /// folder. This could be Unity version dependent or module (e.g.,
    /// dynamically loaded dlls) dependent.
    /// </summary>
    /// <param name="moduleName">Name of the module.</param>
    /// <returns>True when the module can be copied to name_Data/Plugins directory.
    ///          False when the module must be copied to the directory where the executable is located.</returns>
    public static bool IsPluginsPathSupported( string moduleName )
    {
#if UNITY_2019_2_OR_NEWER
      return string.IsNullOrEmpty( m_unsupportedPluginPathDependencies.Find( unsupportedName =>
                                                                               System.Text.RegularExpressions.Regex.IsMatch( moduleName,
                                                                                                                             unsupportedName ) ) );
#else
      // Unclear when or if the bug of having native dlls in the data plugins
      // folder has been solved:
      //     https://forum.unity.com/threads/dll-not-found-with-standalone-app-but-works-fine-in-editor.389392/page-2
      // Works with 2019.2.4, so for earlier version we copy the dlls to the
      // root folder of the executable.
      return false;
#endif
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
      using ( new DynamicallyLoadedDependencies() ) {
        var process = Process.GetCurrentProcess();
        foreach ( ProcessModule module in process.Modules ) {
          if ( module.FileName.IndexOf( "[In Memory]" ) >= 0 )
            continue;

          var isMatch = module.FileName.IndexOf( agxDynamicsPath ) == 0 ||
                        IsOutOfInstallDependency( module.FileName );
          if ( isMatch )
            loadedAgxModulesPaths.Add( module.FileName );
        }
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

      Debug.Log( "Copying Components to: " + Utils.GUI.AddColorTag( agxRuntimeDataPath + @"\Components", Color.green ) );
      CopyDirectory( new DirectoryInfo( agxPluginPath + Path.DirectorySeparatorChar + "Components" ),
                     new DirectoryInfo( agxRuntimeDataPath + Path.DirectorySeparatorChar + "Components" ) );

      foreach ( var modulePath in loadedAgxModulesPaths ) {
        var moduleFileInfo = new FileInfo( modulePath );
        try {
          bool isSupportedPlugin = IsPluginsPathSupported( moduleFileInfo.Name );
          if ( isSupportedPlugin )
            dllTargetPath = AGXUnity.IO.Environment.GetPlayerPluginPath( dataPath );
          else
            dllTargetPath = targetExecutableFileInfo.Directory.FullName;

          moduleFileInfo.CopyTo( dllTargetPath + Path.DirectorySeparatorChar + moduleFileInfo.Name, true );
          string additionalInfo = "";
          if ( IsOutOfInstallDependency( modulePath ) )
            additionalInfo = Utils.GUI.AddColorTag( $" ({modulePath})", Color.yellow );
          Debug.Log( "Successfully copied: " +
                     Utils.GUI.AddColorTag( dllTargetPath + Path.DirectorySeparatorChar, isSupportedPlugin ?
                                                                                           Color.green :
                                                                                           Color.Lerp( Color.blue, Color.white, 0.45f ) ) +
                     Utils.GUI.AddColorTag( moduleFileInfo.Name, Color.Lerp( Color.blue, Color.white, 0.75f ) ) +
                     additionalInfo );
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
