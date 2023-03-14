using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AGXUnity.IO
{
  public static class Environment
  {
    /// <summary>
    /// Predefined variables with context.
    /// </summary>
    public enum Variable
    {
      AGX_DIR,
      AGX_PLUGIN_PATH,
      AGX_DEPENDENCIES_DIR
    }

    /// <summary>
    /// Finds result of predefined environment variable.
    /// </summary>
    /// <param name="variable">Predefined variable.</param>
    /// <returns>Value of predefined environment variable if set - otherwise string.isNullOrEmpty == true.</returns>
    public static string Get( Variable variable )
    {
      // Installed AGX Dynamics points to install directory and
      // in development mode to external dependencies directory.
      // Both version fulfill "dependencies" but, e.g., agxPhysics.dll
      // will be found in an installed version but not in development
      // mode. This should be used to find actual dependencies.
      if ( variable == Variable.AGX_DEPENDENCIES_DIR ) {
        var depPath = Get( variable.ToString() );
        if ( string.IsNullOrEmpty( depPath ) )
          return string.Empty;
        return depPath +
               Path.DirectorySeparatorChar +
               "bin" +
               Path.DirectorySeparatorChar +
               ( agx.agxSWIG.isBuiltWith( agx.BuildConfiguration.USE_64BIT_ARCHITECTURE ) ? "x64" : "x86" );
      }
      return Get( variable.ToString() );
    }

    /// <summary>
    /// Finds if predefined variable is set.
    /// </summary>
    /// <param name="variable">Predefined environment variable.</param>
    /// <returns>True if set - otherwise false.</returns>
    public static bool IsSet( Variable variable )
    {
      return !string.IsNullOrEmpty( Get( variable ) );
    }

    /// <summary>
    /// Set AGX environment variable for current process.
    /// Note that the this method doesn't check whether the variable
    /// is set (or appends to it), this method will set the environment
    /// variable regardless of previous value.
    /// </summary>
    /// <param name="variable">Variable to set.</param>
    /// <param name="value">Value for variable.</param>
    public static void Set( Variable variable, string value )
    {
      System.Environment.SetEnvironmentVariable( variable.ToString(),
                                                 value,
                                                 System.EnvironmentVariableTarget.Process );
    }

    /// <summary>
    /// Add <paramref name="dir"/> to PATH for current process.
    /// </summary>
    /// <param name="dir">Directory to add to PATH.</param>
    public static void AddToPath( string dir )
    {
      var path = System.Environment.GetEnvironmentVariable( "PATH" );
      if ( !path.Split( Path.PathSeparator ).Any( p => p == dir ) )
        System.Environment.SetEnvironmentVariable( "PATH",
                                                   path + Path.PathSeparator + dir,
                                                   System.EnvironmentVariableTarget.Process );
    }

    /// <summary>
    /// Checks if <paramref name="dir"/> is in PATH.
    /// </summary>
    /// <param name="dir">Directory to check.</param>
    /// <returns>True if <paramref name="dir"/> is in PATH, otherwise false.</returns>
    public static bool IsInPath( string dir )
    {
      return System.Environment.GetEnvironmentVariable( "PATH" ).Split( Path.PathSeparator ).Any( p => p == dir );
    }

    /// <summary>
    /// Remove given <paramref name="dir"/> from PATH.
    /// </summary>
    /// <param name="dir">Directory to remove from PATH.</param>
    /// <returns>True if <paramref name="dir"/> was successfully removed from PATH, otherwise false.</returns>
    public static bool RemoveFromPath( string dir )
    {
      var pathList = System.Environment.GetEnvironmentVariable( "PATH" ).Split( ';' ).ToList();
      if ( !pathList.Remove( dir ) )
        return false;
      System.Environment.SetEnvironmentVariable( "PATH",
                                                 string.Join( ";", pathList ) );
      return true;
    }

    /// <summary>
    /// Finds path to installed AGX Dynamics. Fails if Unity isn't
    /// started in an AGX Dynamics environment.
    /// </summary>
    public static string AGXDynamicsPath
    {
      get
      {
        var agxPath = Get( Variable.AGX_DIR );
        if ( !string.IsNullOrEmpty( agxPath ) ) {
          // Installed AGX Dynamics will add an extra \ to AGX_DIR.
          if ( agxPath.Last() == '\\' || agxPath.Last() == '/' )
            agxPath.Remove( agxPath.Length - 1 );
          return agxPath;
        }

        return null;
      }
    }

    /// <summary>
    /// Find environment variable with given <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Name of environment variable.</param>
    /// <param name="target">Environment variable target.</param>
    /// <returns>Value the environment variable carries - string.isNullOrEmpty == true if not found.</returns>
    public static string Get( string name, System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      return GetAll( name, target ).FirstOrDefault();
    }

    /// <summary>
    /// Find environment variable values with given <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Name of environment variable.</param>
    /// <param name="target">Environment variable target.</param>
    /// <returns>Array of results if found, an empty array if not found.</returns>
    public static string[] GetAll( string name, System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      var result = System.Environment.GetEnvironmentVariable( name, target );
      if ( result == null )
        return new string[] { };
      return result.Split( Path.PathSeparator );
    }

    /// <summary>
    /// Find file with given <paramref name="filename"/> in environment <paramref name="envVariable"/>
    /// and environment target <paramref name="target"/>.
    /// </summary>
    /// <param name="filename">Name of file.</param>
    /// <param name="envVariable">Environment variable - default: PATH.</param>
    /// <param name="target">Environment target to search in.</param>
    /// <returns>FileInfo of file with given <paramref name="filename"/> if found - otherwise null.</returns>
    public static FileInfo FindFile( string filename,
                                     string envVariable = "PATH",
                                     System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      var env = GetAll( envVariable, target );
      foreach ( var path in env ) {
        if ( File.Exists( path + Path.DirectorySeparatorChar + filename ) )
          return new FileInfo( path + Path.DirectorySeparatorChar + filename );
      }
      return null;
    }

    /// <summary>
    /// Plugins path where the AGX Dynamics native modules are located.
    /// </summary>
    /// <remarks>
    /// From Unity 2019.3 native modules should be located in data_folder/Plugins/x86_64
    /// and data_folder/Plugins for earlier versions.
    /// </remarks>
    /// <param name="dataPath">Path to player data folder - nameOfExecutable_Data.</param>
    /// <returns>Path to the plugins folder.</returns>
    public static string GetPlayerPluginPath( string dataPath )
    {
#if UNITY_2019_3_OR_NEWER
      return dataPath + "/Plugins/x86_64";
#else
      return dataPath + "/Plugins";
#endif
    }

    /// <summary>
    /// AGX Dynamics runtime path where Components and other runtime
    /// data is located.
    /// </summary>
    /// <param name="dataPath">Path to player data folder - nameOfExecutable_Data.</param>
    /// <returns>Path to the AGX Dynamics runtime folder.</returns>
    public static string GetPlayerAGXRuntimePath( string dataPath )
    {
      return GetPlayerPluginPath( dataPath ) + "/agx";
    }

    /// <summary>
    /// Finds new similar filename or returns <paramref name="filename"/> if it doesn't exist.
    /// E.g., if foo.bar already exist, foo (1).bar will be returned.
    /// </summary>
    /// <param name="filename">Desired filename including path.</param>
    /// <returns>Filename similar to <paramref name="filename"/> that doesn't exist.</returns>
    public static string FindUniqueFilename( string filename )
    {
      if ( !File.Exists( filename ) )
        return filename;

      var extension = Path.GetExtension( filename );
      var orgName   = Path.GetFileNameWithoutExtension( filename );
      var directory = new FileInfo( filename ).Directory;
      int counter   = 0;
      while ( directory.GetFiles( $"{orgName} ({++counter}){extension}", SearchOption.TopDirectoryOnly ).Length > 0 )
        ;
      return $"{directory.FullName.Replace( '\\', '/' )}/{orgName} ({counter}){extension}";
    }

    /// <summary>
    /// Opens given <paramref name="filename"/> and checks if it's possible
    /// to write to that file.
    /// </summary>
    /// <param name="filename">Filename to check.</param>
    /// <returns>True if the file exists and it's possible to write to it, otherwise false.</returns>
    public static bool CanWriteToExisting( string filename )
    {
      if ( !File.Exists( filename ) )
        return false;

      var canWrite = false;
      using ( var fs = File.Open( filename, FileMode.Open ) )
        canWrite = fs.CanWrite;

      return canWrite;
    }

    /// <summary>
    /// Command line starting the application.
    /// </summary>
    public static CommandLine CommandLine
    {
      get
      {
        if ( s_commandLine == null )
          s_commandLine = new CommandLine( System.Environment.GetCommandLineArgs() );
        return s_commandLine;
      }
    }

    /// <summary>
    /// Finds if an editor package is installed by parsing Packages/manifest.json.
    /// This method will always return false in a player, i.e., not in the editor.
    /// </summary>
    /// <param name="fullName">Full name of the package, e.g., "com.unity.inputsystem".</param>
    /// <returns>True if the package is installed, otherwise false.</returns>
    public static bool IsEditorPackageInstalled( string fullName )
    {
      if ( !ParseInstalledEditorPackages() )
        return false;

      return s_installedEditorPackages.ContainsKey( fullName );
    }

    /// <summary>
    /// Find version of an installed editor package, if possible to parse.
    /// </summary>
    /// <param name="fullName">Full name of the package, e.g., "com.unity.inputsystem".</param>
    /// <returns>Version info if the given package is installed and possible to parse, otherwise VersionInfo.Invalid.</returns>
    public static VersionInfo GetEditorPackageVersion( string fullName )
    {
      if ( ParseInstalledEditorPackages() && s_installedEditorPackages.TryGetValue( fullName, out var version ) )
        return version;
      return VersionInfo.Invalid;
    }

    public static string GetLogFileOverride()
    {
      return s_logFileOverride;
    }

    private static bool ParseInstalledEditorPackages()
    {
      if ( UnityEngine.Application.isEditor && s_installedEditorPackages == null ) {
        s_installedEditorPackages = new Dictionary<string, VersionInfo>();

        try {
          var lines = File.ReadAllText( "Packages/manifest.json" ).Split( new string[] { "\n", "\r\n", "\r" },
                                                                          StringSplitOptions.RemoveEmptyEntries );
          foreach ( var line in lines ) {
            var nameVersion = line.Split( new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries )
                                  .Select( str => str.Trim( ' ', ',', '\"' ) ).ToArray();
            if ( nameVersion.Length != 2 )
              continue;
            else if ( nameVersion[ 0 ] == "dependencies" )
              continue;

            // E.g., git checkout package with revision (with version) as given in:
            //     https://docs.unity3d.com/Manual/upm-git.html#revision
            var versionStr = nameVersion[ 1 ];
            var version = VersionInfo.Parse( versionStr );
            var revStartIndex = -1;
            var tryParseRevision = !version.IsValid &&
                                   ( revStartIndex = versionStr.LastIndexOf( '#' ) ) >= 0;
            if ( tryParseRevision ) {
              versionStr = versionStr.Substring( revStartIndex + 1,
                                                 versionStr.Length - ( revStartIndex + 1 ) );
              if ( versionStr.StartsWith( "v", true, System.Globalization.CultureInfo.InvariantCulture ) )
                versionStr = versionStr.Remove( 0, 1 );
              version = VersionInfo.Parse( versionStr );
            }

            s_installedEditorPackages.Add( nameVersion[ 0 ], version );
          }
        }
        catch ( Exception ) {
        }
      }

      return s_installedEditorPackages != null;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void OnEditorCLI()
    {
      OnCLI();
    }
#else
#if UNITY_2019_1_OR_NEWER
    [UnityEngine.RuntimeInitializeOnLoadMethod( UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen )]
#else
    [UnityEngine.RuntimeInitializeOnLoadMethod( UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad )]
#endif
    private static void OnPlayerCli()
    {
      OnCLI();
    }
#endif

    private static void OnCLI()
    {
      if ( CommandLine.HasArg( CommandLine.Arg.GenerateOfflineActivation ) ) {
        try {
          var offlineLicenseIdCode = CommandLine.GetValues( CommandLine.Arg.GenerateOfflineActivation );
          if ( offlineLicenseIdCode == null || offlineLicenseIdCode.Count < 2 )
            throw new AGXUnity.Exception( "wrong number of arguments. " +
                                          "Usage: -generate-offline-activation <license_id> <activation_code> <optional_output_file>" );

          var licenseId = int.Parse( offlineLicenseIdCode[ 0 ] );
          var activationCode = offlineLicenseIdCode[ 1 ];
          var file = offlineLicenseIdCode.Count == 3 ?
                       new FileInfo( offlineLicenseIdCode[ 2 ] ) :
                       new FileInfo( "license_activation_request.xml" );

          UnityEngine.Debug.Log( $"AGXUnity.CLI: Generating offline activation request for license ID {licenseId}, writing result to \"{file.FullName}\"." );

          var nativeHandler = NativeHandler.Instance;
          if ( !nativeHandler.Initialized )
            throw new AGXUnity.Exception( "AGX Dynamics initialization failed." );

          LicenseManager.GenerateOfflineActivation( licenseId, activationCode, file.FullName );

          UnityEngine.Debug.Log( $"AGXUnity.CLI: Successfully written offline activation file \"{file.FullName}\"." );
        }
        catch ( AGXUnity.Exception e ) {
          UnityEngine.Debug.LogError( $"AGXUnity.CLI: Generating offline activation failed - {e.Message}" );
        }
        catch ( System.Exception e ) {
          UnityEngine.Debug.LogException( e );
        }
      }

      if ( CommandLine.HasArg( CommandLine.Arg.CreateOfflineLicense ) ) {
        try {
          var offlineLicenseResponse = CommandLine.GetValues( CommandLine.Arg.CreateOfflineLicense );
          if ( offlineLicenseResponse == null || offlineLicenseResponse.Count == 0 )
            throw new AGXUnity.Exception( "wrong number of arguments. " +
                                          "Usage: --create-offline-license <web_response_file> <optional_output_license_file>" );

          var inputResponseFile = new FileInfo( offlineLicenseResponse[ 0 ] );
          if ( !inputResponseFile.Exists )
            throw new AGXUnity.Exception( $"input web response file doesn't exist: \"{inputResponseFile.FullName}\"" );

          var outputLicenseFile = offlineLicenseResponse.Count > 1 ?
                                    new FileInfo( offlineLicenseResponse[ 1 ] ) :
                                    new FileInfo( $"agx{LicenseManager.GetLicenseExtension( LicenseInfo.LicenseType.Service )}" );
          if ( outputLicenseFile.Extension != LicenseManager.GetLicenseExtension( LicenseInfo.LicenseType.Service ) )
            outputLicenseFile = new FileInfo( $"{outputLicenseFile.FullName}{LicenseManager.GetLicenseExtension( LicenseInfo.LicenseType.Service )}" );

          UnityEngine.Debug.Log( $"AGXUnity.CLI: Creating offline license given input \"{inputResponseFile.FullName}\" for " + 
                                 $"resulting license file \"{outputLicenseFile.FullName}\"." );

          var nativeHandler = NativeHandler.Instance;
          if ( !nativeHandler.Initialized )
            throw new AGXUnity.Exception( "AGX Dynamics initialization failed." );

          LicenseManager.CreateOfflineLicense( inputResponseFile.FullName, outputLicenseFile.FullName );

          UnityEngine.Debug.Log( $"AGXUnity.CLI: Successfully activated offline license file \"{outputLicenseFile.FullName}\"." );
        }
        catch ( AGXUnity.Exception e ) {
          UnityEngine.Debug.LogError( $"AGXUnity.CLI: Creating offline license failed - {e.Message}" );
        }
        catch ( System.Exception e ) {
          UnityEngine.Debug.LogException( e );
        }
      }
      if ( CommandLine.HasArg( CommandLine.Arg.AgxLogFile ) ) {
        try {
          var logPath = CommandLine.GetValues( CommandLine.Arg.AgxLogFile );
          if ( logPath == null || logPath.Count != 1 )
            throw new AGXUnity.Exception( "wrong number of arguments. " +
                                          "Usage: --agx-log-file <log_file>" );

          s_logFileOverride = Path.GetFullPath( logPath[ 0 ] );

        }
        catch ( AGXUnity.Exception e ) {
          UnityEngine.Debug.LogError( $"AGXUnity.CLI: Setting log file failed - {e.Message}" );
        }
        catch ( System.Exception e ) {
          UnityEngine.Debug.LogException( e );
        }
      }

      if ( !UnityEngine.Application.isEditor && CommandLine.HasArg( "quit" ) )
        UnityEngine.Application.Quit( 0 );
    }

    private static string s_logFileOverride = null;
    private static Dictionary<string, VersionInfo> s_installedEditorPackages = null;
    private static CommandLine s_commandLine = null;
  }
}
