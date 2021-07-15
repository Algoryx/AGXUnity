using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

using AGXUnity.Utils;

using GUI   = AGXUnity.Utils.GUI;
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
        Debug.LogWarning( GUI.AddColorTag( "Copy AGX Dynamics binaries - unsupported build target: ",
                                           Color.red ) +
                          target.ToString() );
        return;
      }

      var nativeIsX64 = agx.agxSWIG.isBuiltWith( agx.BuildConfiguration.USE_64BIT_ARCHITECTURE );
      if ( (target == BuildTarget.StandaloneWindows64) != nativeIsX64 ) {
        Debug.LogWarning( GUI.AddColorTag( "Copy AGX Dynamics binaries - x86/x64 architecture mismatch: ",
                                           Color.red ) +
                          "Build target = " +
                          target.ToString() +
                          ", AGX Dynamics build: " +
                          ( nativeIsX64 ? "x64" : "x86" ) );
        return;
      }

      var agxDynamicsPath = AGXUnity.IO.Environment.AGXDynamicsPath;
      if ( string.IsNullOrEmpty( agxDynamicsPath ) ) {
        Debug.LogWarning( GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX Dynamics directory.",
                                           Color.red ) );
        return;
      }

      var agxPluginPath = AGXUnity.IO.Environment.Get( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH );
      if ( string.IsNullOrEmpty( agxPluginPath ) ) {
        Debug.LogWarning( GUI.AddColorTag( "Copy AGX Dynamics binaries - unable to find AGX_PLUGIN_PATH.", Color.red ) );
        return;
      }

      var targetExecutableFileInfo = new FileInfo( targetPathFilename );
      if ( !targetExecutableFileInfo.Exists ) {
        Debug.LogWarning( GUI.AddColorTag( "Target executable doesn't exist: ", Color.red ) + targetPathFilename );
        return;
      }

      // Application.dataPath is 'Assets' folder here in Editor but
      // Application.dataPath is '<name>_Data' in the Player. We're
      // explicitly constructing '<name>_Data' here.
      var targetDataPath = targetExecutableFileInfo.Directory.FullName +
                           Path.DirectorySeparatorChar +
                           Path.GetFileNameWithoutExtension( targetExecutableFileInfo.Name ) +
                           "_Data";


      if ( IO.Utils.AGXDynamicsInstalledInProject )
        PostBuildInternal( agxDynamicsPath,
                           agxPluginPath,
                           targetExecutableFileInfo,
                           targetDataPath );
      else
        PostBuildExternal( agxDynamicsPath,
                           agxPluginPath,
                           targetExecutableFileInfo,
                           targetDataPath );
    }

    /// <summary>
    /// Verifying all dll's in the project are located in the correct
    /// folder in the final build. If there's a mismatch, we check if
    /// all or some of the dll's are located in the parent directory and
    /// copy them to the correct.
    /// </summary>
    private static void VerifyBuild( string targetDataPath )
    {
      var nativePluginsDirectory = new DirectoryInfo( AGXUnity.IO.Environment.GetPlayerPluginPath( targetDataPath ) );
      if ( !nativePluginsDirectory.Exists ) {
        Debug.LogWarning( "Build Warning: ".Color( Color.yellow ) +
                          nativePluginsDirectory.FullName +
                          " doesn't exist. " +
                          "This directory is where the AGX Dynamics binaries are expected to be located." );
        return;
      }

      // It's not possible to use the PluginImporter during builds.
      Predicate<FileInfo> isNativePlugin = fi => { return !fi.Name.ToLower().EndsWith( "dotnet.dll" ); };
      var inProjectDllNames = new HashSet<string>( from dllFile
                                                   in new DirectoryInfo( IO.Utils.AGXUnityPluginDirectoryFull ).EnumerateFiles( "*.dll" )
                                                   where isNativePlugin( dllFile )
                                                   select dllFile.Name );
      Func<bool> matchingNumProjectDllsWithBuild = () =>
      {
        var numMatchingDllsInBuild = nativePluginsDirectory.EnumerateFiles( "*.dll" ).Count( dllFi => inProjectDllNames.Contains( dllFi.Name ) );
        // The dlls from the projects are all located in the
        // expected nativePluginsDirectory.
        return numMatchingDllsInBuild == inProjectDllNames.Count;
      };
      if ( matchingNumProjectDllsWithBuild() )
        return;

      // Check if the missing dlls are located in the parent
      // directory of nativePluginsDirectory.
      var nativePluginsParentDirectory = nativePluginsDirectory.Parent;
      int numMoved = 0;
      try {
        foreach ( var dllFi in nativePluginsParentDirectory.EnumerateFiles( "*.dll" ) ) {
          if ( !inProjectDllNames.Contains( dllFi.Name ) )
            continue;
          dllFi.MoveTo( nativePluginsDirectory.FullName +
                        Path.DirectorySeparatorChar +
                        dllFi.Name );
          ++numMoved;
        }
      }
      catch ( Exception e ) {
        Debug.LogException( e );
        return;
      }
      if ( matchingNumProjectDllsWithBuild() ) {
        Debug.Log( "Build info: ".Color( Color.green ) +
                   $"Successfully moved {numMoved} AGX Dynamics binaries to {nativePluginsDirectory.FullName.Replace( '\\', '/' )}" );
      }
      else {
        Debug.LogWarning( "Build warning: ".Color( Color.yellow ) +
                          "AGX Dynamics binaries mismatch in build vs project. The application may not properly load AGX Dynamics." );
      }
    }

    private static void PostBuildInternal( string agxDynamicsPath,
                                           string agxPluginPath,
                                           FileInfo targetExecutableFileInfo,
                                           string targetDataPath )
    {
      // Some versions of Unity 2019.3 (fixed in 2019.3.9) isn't consistent
      // where native modules are located. E.g., if Burst is installed, some
      // dll's will be created in <project>_Data/Plugins/x86_64 and if AGX
      // dll's are in <project>_Data/Plugins, the dll's wont load.
      //     - Unity 2019.3 and later:   <project>_Data/Plugins/x86_64
      //     - Unity 2019.2 and earlier: <project>_Data/Plugins
      if ( !Directory.Exists( AGXUnity.IO.Environment.GetPlayerPluginPath( targetDataPath ) ) )
        Directory.CreateDirectory( AGXUnity.IO.Environment.GetPlayerPluginPath( targetDataPath ) );

      // Assuming all dlls are present in the plugins directory and that
      // "Components" are in a folder named "agx" in the plugins directory.
      // Unity will copy the dlls.
      var sourceDirectory      = new DirectoryInfo( IO.Utils.AGXUnityPluginDirectoryFull + Path.DirectorySeparatorChar + "agx" );
      var destinationDirectory = new DirectoryInfo( AGXUnity.IO.Environment.GetPlayerAGXRuntimePath( targetDataPath ) );
      Debug.Log( GUI.AddColorTag( "Copying AGX runtime data from: " +
                                  IO.Utils.AGXUnityPluginDirectory +
                                  Path.DirectorySeparatorChar +
                                  "agx" +
                                  " to " +
                                  destinationDirectory.FullName, Color.green ) );
      CopyDirectory( sourceDirectory, destinationDirectory );

      // Deleting all .meta-files that are present in our "agx" folder.
      foreach ( var fi in destinationDirectory.EnumerateFiles( "*.meta", SearchOption.AllDirectories ) )
        fi.Delete();

      VerifyBuild( targetDataPath );

      CheckGenerateEncryptedRuntime( targetExecutableFileInfo );
    }

    private static void PostBuildExternal( string agxDynamicsPath,
                                           string agxPluginPath,
                                           FileInfo targetExecutableFileInfo,
                                           string targetDataPath )
    {
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
        Debug.LogWarning( GUI.AddColorTag( "Copy AGX Dynamics binaries - no binaries found in current process.", Color.red ) );
        return;
      }

      // dllTargetPath: ./<name>_Data/Plugins
      var dllTargetPath = AGXUnity.IO.Environment.GetPlayerPluginPath( targetDataPath );
      if ( !Directory.Exists( dllTargetPath ) )
        Directory.CreateDirectory( dllTargetPath );

      // agxRuntimeDataPath: ./<name>_Data/Plugins/agx
      var agxRuntimeDataPath = AGXUnity.IO.Environment.GetPlayerAGXRuntimePath( targetDataPath );
      if ( !Directory.Exists( agxRuntimeDataPath ) )
        Directory.CreateDirectory( agxRuntimeDataPath );

      Debug.Log( "Copying Components to: " + GUI.AddColorTag( agxRuntimeDataPath + Path.DirectorySeparatorChar + "Components", Color.green ) );
      CopyDirectory( new DirectoryInfo( agxPluginPath + Path.DirectorySeparatorChar + "Components" ),
                     new DirectoryInfo( agxRuntimeDataPath + Path.DirectorySeparatorChar + "Components" ) );

      var targetDataDir = agxRuntimeDataPath + Path.DirectorySeparatorChar + "data";
      Debug.Log( "Copying data to: " + GUI.AddColorTag( targetDataDir, Color.green ) );
      if ( !Directory.Exists( targetDataDir ) )
        Directory.CreateDirectory( targetDataDir );
      CopyDirectory( new DirectoryInfo( agxDynamicsPath + Path.DirectorySeparatorChar + "data" + Path.DirectorySeparatorChar + "TerrainMaterials" ),
                     new DirectoryInfo( targetDataDir + Path.DirectorySeparatorChar + "TerrainMaterials" ) );

      foreach ( var modulePath in loadedAgxModulesPaths ) {
        var moduleFileInfo = new FileInfo( modulePath );
        try {
          moduleFileInfo.CopyTo( dllTargetPath + Path.DirectorySeparatorChar + moduleFileInfo.Name, true );
          string additionalInfo = "";
          if ( IsOutOfInstallDependency( modulePath ) )
            additionalInfo = GUI.AddColorTag( $" ({modulePath})", Color.yellow );
          Debug.Log( "Successfully copied: " +
                     GUI.AddColorTag( dllTargetPath + Path.DirectorySeparatorChar, Color.green ) +
                     GUI.AddColorTag( moduleFileInfo.Name, Color.Lerp( Color.blue, Color.white, 0.75f ) ) +
                     additionalInfo );
        }
        catch ( Exception e ) {
          Debug.Log( "Failed copying: " +
                     GUI.AddColorTag( dllTargetPath + Path.DirectorySeparatorChar, Color.red ) +
                     GUI.AddColorTag( moduleFileInfo.Name, Color.red ) +
                     ": " + e.Message );
        }
      }

      CheckGenerateEncryptedRuntime( targetExecutableFileInfo );
    }

    private static void CheckGenerateEncryptedRuntime( FileInfo targetExecutableFileInfo )
    {
      if ( AGXUnity.LicenseManager.LicenseInfo.Type == AGXUnity.LicenseInfo.LicenseType.Service &&
           !Application.isBatchMode &&
           AskGenerateRuntimeLicense ) {
        var generateEncryptedRuntimeActivation = EditorUtility.DisplayDialogComplex( "Generate encrypted runtime license?",
                                                                                     "Open separate window to generate",
                                                                                     "Yes",
                                                                                     "No",
                                                                                     "Never" );
        if ( generateEncryptedRuntimeActivation == 0 )
          Windows.GenerateRuntimeLicenseActivationWindow.Open().Initialize( targetExecutableFileInfo.Directory.FullName,
                                                                            targetExecutableFileInfo.FullName );
        else if ( generateEncryptedRuntimeActivation == 2 )
          AskGenerateRuntimeLicense = false;
      }
    }

    private static bool AskGenerateRuntimeLicense
    {
      get
      {
        return GenerateRuntimeLicenseData.Bool;
      }
      set
      {
        GenerateRuntimeLicenseData.Bool = value;
      }
    }

    private static EditorDataEntry GenerateRuntimeLicenseData
    {
      get
      {
        return EditorData.Instance.GetStaticData( "BuildProcess_AskGenerateRuntimeLicense",
                                                  entry => entry.Bool = true );
      }
    }
  }
}
