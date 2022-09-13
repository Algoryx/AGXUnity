using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  public static class LicenseManager
  {
    /// <summary>
    /// Current license information loaded in AGX Dynamics.
    /// </summary>
    public static LicenseInfo LicenseInfo
    {
      get
      {
        if ( !s_licenseInfo.IsParsed )
          LicenseInfo = LicenseInfo.Create();
        return s_licenseInfo;
      }
      private set
      {
        s_licenseInfo = value;

        // We could end up here during instantiation of the
        // NativeHandler. Only validate license in the native
        // handler if it has an instance.
        if ( NativeHandler.HasInstance )
          NativeHandler.Instance.ValidateLicense();
      }
    }

    /// <summary>
    /// Enumerate known license types.
    /// </summary>
    public static IEnumerable<LicenseInfo.LicenseType> LicenseTypes
    {
      get
      {
        foreach ( LicenseInfo.LicenseType licenseType in Enum.GetValues( typeof( LicenseInfo.LicenseType ) ) ) {
          if ( licenseType == LicenseInfo.LicenseType.Unknown )
            continue;
          else
            yield return licenseType;
        }
      }
    }

    /// <summary>
    /// True if a license is being refreshed.
    /// </summary>
    public static bool IsRefreshing => s_refreshTask != null && !s_refreshTask.IsCompleted;

    /// <summary>
    /// True if a license is being activated.
    /// </summary>
    public static bool IsActivating => s_activationTask != null && !s_activationTask.IsCompleted;

    /// <summary>
    /// True if the activation task is running, otherwise false.
    /// </summary>
    public static bool IsBusy => IsActivating || IsRefreshing;

    /// <summary>
    /// Load license file (service or legacy) located in any directory
    /// under application/project root. Service (*.lfx) is searched
    /// for before legacy (*.lic). The first valid license found is loaded.
    /// </summary>
    /// <returns>
    /// True if successful, false if license files weren't found
    /// or if the loaded license isn't valid.
    /// </returns>
    public static bool LoadFile()
    {
      // This is potentially a license unlock by a script. It's not
      // possible to know if it exists scripts that manually unlocks AGX.
      var potentialScriptLoaded = LicenseInfo.Create();

      Reset();

      var licenseFiles = FindLicenseFiles();
      foreach ( var licenseFile in licenseFiles ) {
        var file = licenseFile.PrettyPath();
        if ( LoadFile( file,
                       $"License file \"{file}\" found in search from application root." ) )
          return true;
      }

      // Recover the last license if parsed, i.e., there were a license
      // loaded before calling this method. Note that all license files
      // (if any) failed to load before this.
      if ( potentialScriptLoaded.IsParsed ) {
        LicenseInfo = potentialScriptLoaded;
        return LicenseInfo.IsValid;
      }

      return false;
    }

    /// <summary>
    /// Load license file (service or legacy) given filename including path.
    /// </summary>
    /// <param name="filename">License filename, including path, to load.</param>
    /// <returns> True if successful, false if the file doesn't exist or is an invalid license.</returns>
    public static bool LoadFile( string filename )
    {
      return LoadFile( filename,
                       $"Explicit license filename: {filename}." );
    }

    /// <summary>
    /// Load file content or obfuscated legacy license information.
    /// </summary>
    /// <param name="licenseContent">License file content or obfuscated string.</param>
    /// <returns>True if successfully loaded and is a valid license, otherwise false.</returns>
    public static bool Load( string licenseContent )
    {
      var context = "Explicit license content.";
      // Loading encrypted runtime from script. AGX is writing the generated
      // file as given in 'context' here, see ActivateEncryptedRuntime.
      if ( FindLicenseContentType( licenseContent ) == LicenseContentType.EncryptedRuntimeService ) {
        context = IO.Environment.GetPlayerPluginPath( Application.dataPath ) +
                  $"/agx{GetLicenseExtension( LicenseInfo.LicenseType.Service )}";
      }
      return Load( licenseContent,
                   context );
    }

    /// <summary>
    /// Generate an encrypted license file containing runtime license activation
    /// information for a given build/application. The main purpose of this is to
    /// protect from undesired activations of the license outside of the application.
    /// 
    /// The <paramref name="applicationRootDirectory"/> is expected to be an absolute
    /// path to the root directory of the build/application, i.e., the directory
    /// containing the main executable and the application data directory.
    /// 
    /// The <paramref name="referenceApplicationFile"/> is expected to be relative to
    /// <paramref name="applicationRootDirectory"/>. This file location (relative
    /// to application root) must be exist now, exist during the activation and contain
    /// the exact same information (e.g., a dll or something else static and unique
    /// to the application).
    /// </summary>
    /// <param name="runtimeLicenseId">Runtime license activation id.</param>
    /// <param name="runtimeLicensePassword">Runtime license activation password.</param>
    /// <param name="applicationRootDirectory">Absolute path to the application/build directory.</param>
    /// <param name="referenceApplicationFile">Relative (to <paramref name="applicationRootDirectory"/>) path to a static file.</param>
    /// <param name="onSuccess">Optional callback with the generated filename (including path) if successful - otherwise check return value and logs.</param>
    /// <returns>True if successfully generated, otherwise false (check console output).</returns>
    public static bool GenerateEncryptedRuntime( int runtimeLicenseId,
                                                 string runtimeLicensePassword,
                                                 string applicationRootDirectory,
                                                 string referenceApplicationFile,
                                                 Action<string> onSuccess = null )
    {
      if ( !Directory.Exists( applicationRootDirectory ) ) {
        Debug.LogError( "AGXUnity.LicenseManager: Unable to generate encrypted runtime license - " +
                        $"application root directory \"{applicationRootDirectory}\" doesn't exist." );
        return false;
      }

      if ( !Path.IsPathRooted( applicationRootDirectory ) ) {
        Debug.LogError( "AGXUnity.LicenseManager: Unable to generate encrypted runtime license - " +
                        $"application root directory \"{applicationRootDirectory}\" isn't rooted." );
        return false;
      }

      var absolutePathToReferenceFile = $"{applicationRootDirectory}/{referenceApplicationFile}".Replace( '\\', '/' );
      if ( !File.Exists( absolutePathToReferenceFile ) ) {
        Debug.LogError( "AGXUnity.LicenseManager: Unable to generate encrypted runtime license - " +
                        $"reference file \"{referenceApplicationFile}\" doesn't exist relative to \"{applicationRootDirectory}\"." );
        return false;
      }

      try {
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( applicationRootDirectory );
        var encrypted = agx.Runtime.instance().encryptRuntimeActivation( runtimeLicenseId,
                                                                         runtimeLicensePassword,
                                                                         referenceApplicationFile );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).removeFilePath( applicationRootDirectory );

        if ( !string.IsNullOrEmpty( encrypted ) ) {
          // Fall-back directory is application root but we'll try to find
          // the AGX Dynamics binaries and place it there if applicationRootDirectory
          // is part of the resources of AGX Dynamics.
          var licenseTargetDirectory = Directory.GetFiles( applicationRootDirectory,
                                                           "agxPhysics.dll",
                                                           SearchOption.AllDirectories ).Select( file => new FileInfo( file ).Directory.FullName ).FirstOrDefault() ??
                                       applicationRootDirectory;

          var encryptedFilename = $"{licenseTargetDirectory}/agx{s_runtimeActivationExtension}".Replace( '\\', '/' );

          File.WriteAllText( encryptedFilename, encrypted );

          onSuccess?.Invoke( encryptedFilename );

          return true;
        }

        Debug.LogError( "AGXUnity.LicenseManager: Unable to generate encrypted runtime license - " +
                        $"encryption failed with status: {agx.Runtime.instance().getStatus()}" );
      }
      catch ( System.Exception e ) {
        Debug.LogError( "AGXUnity.LicenseManager: Exception occurred during generate of encrypted runtime " +
                        $"for application root \"{applicationRootDirectory}\"." );
        Debug.LogException( e );
      }

      return false;
    }

    /// <summary>
    /// Searches for encrypted runtime license from application root, and if
    /// found, activates the license and writes the file in <paramref name="targetDirectory"/>.
    /// The encrypted license file found will be deleted if the activation is successful.
    /// </summary>
    /// <param name="targetDirectory">Target directory of the activated license file.</param>
    /// <returns>True if successfully activated, otherwise false.</returns>
    public static bool ActivateEncryptedRuntime( string targetDirectory )
    {
      var filename = FindRuntimeActivationFiles().FirstOrDefault();
      if ( string.IsNullOrEmpty( filename ) ) {
        IssueLoadWarning( "Unable to activate runtime license, license file not found.",
                          $"Searching all directories under {Application.dataPath} for {s_runtimeActivationExtension} files." );
        return false;
      }

      return ActivateEncryptedRuntime( filename, targetDirectory );
    }

    /// <summary>
    /// Activate encrypted license file <paramref name="filename"/> given target directory
    /// <paramref name="targetDirectory"/> where the activated license file should be written.
    /// 
    /// The given encrypted file will be deleted if the activation is successful.
    /// </summary>
    /// <param name="filename">Encrypted license file, including path.</param>
    /// <param name="targetDirectory">Target directory where the activated license file should be written.</param>
    /// <returns>True if successfully activated and written, otherwise false.</returns>
    public static bool ActivateEncryptedRuntime( string filename, string targetDirectory )
    {
      if ( string.IsNullOrEmpty( filename ) || !File.Exists( filename ) ) {
        IssueLoadWarning( "Unable to activate runtime license, filename not given or doesn't exist.",
                          $"Explicit runtime activation with filename: \"{filename}\"" );
        return false;
      }

      try {
        if ( !Directory.Exists( targetDirectory ) )
          Directory.CreateDirectory( targetDirectory );
      }
      catch ( System.Exception e ) {
        Debug.LogError( $"AGXUnity.LicenseManager: Unable to create given target directory - \"{targetDirectory}\"." );
        Debug.LogException( e );
        return false;
      }

      var success = LoadFile( filename, $"{targetDirectory}/agx{GetLicenseExtension( LicenseInfo.LicenseType.Service )}" );
      if ( success )
        File.Delete( filename );

      return success;
    }

    /// <summary>
    /// Generate offline license activation file for this machine given license id,
    /// activation code and target filename. The target filename can be anything and will
    /// be a text file which content can be cut and pasted into, or uploaded to:
    ///     https://secure.softwarekey.com/solo/customers/ManualRequest.aspx
    /// </summary>
    /// <param name="licenseId">License id for this machine.</param>
    /// <param name="licensePassword">License activation code for the given license id.</param>
    /// <param name="outputFilename">Output filename of the text file containing the necessary information for the manual request.</param>
    /// <param name="throwOnError">Throw AGXUnity.Exception on errors if true, otherwise rely on the return value.</param>
    /// <returns>True if offline activation is successful, otherwise false or throw (if error) AGXUnity.Exception if <paramref name="throwOnError"/> = true.</returns>
    public static bool GenerateOfflineActivation( int licenseId,
                                                  string licensePassword,
                                                  string outputFilename,
                                                  bool throwOnError = true )
    {
      var success = false;

      try {
        var activationText = agx.Runtime.instance().generateOfflineActivationRequest( licenseId, licensePassword );
        if ( !string.IsNullOrEmpty( agx.Runtime.instance().getStatus() ) )
          throw new AGXUnity.Exception( agx.Runtime.instance().getStatus() );

        File.WriteAllText( outputFilename, activationText );

        success = File.Exists( outputFilename );
      }
      catch ( System.Exception e ) {
        if ( throwOnError )
          throw;

        success = false;

        Debug.LogError( e.Message );
      }

      return success;
    }

    /// <summary>
    /// Creates a license file <paramref name="licenseFilename"/> given manual request, offline,
    /// license file or content of the procedure of offline license activation from:
    ///     https://secure.softwarekey.com/solo/customers/ManualRequest.aspx
    /// </summary>
    /// <seealso cref="GenerateOfflineActivation"/>
    /// <param name="webResponseFilenameOrContent">
    /// Filename (including path) to offline activation response or the content of the file.
    /// </param>
    /// <param name="licenseFilename">The valid, activated license filename (including absolute or relative path).</param>
    /// <param name="throwOnerror">Throw AGXUnity.Exception on errors if true, otherwise rely on the return value.</param>
    /// <returns>True if create of offline license is successful, otherwise false or throw (if error) AGXUnity.Exception if <paramref name="throwOnError"/> = true.</returns>
    public static bool CreateOfflineLicense( string webResponseFilenameOrContent,
                                             string licenseFilename,
                                             bool throwOnerror = true )
    {
      var success = false;

      try {
        if ( Path.GetExtension( licenseFilename ) != GetLicenseExtension( LicenseInfo.LicenseType.Service ) )
          licenseFilename += GetLicenseExtension( LicenseInfo.LicenseType.Service );

        if ( File.Exists( webResponseFilenameOrContent ) )
          webResponseFilenameOrContent = File.ReadAllText( webResponseFilenameOrContent );

        if ( !agx.Runtime.instance().processOfflineActivationRequest( webResponseFilenameOrContent ) )
          throw new AGXUnity.Exception( agx.Runtime.instance().getStatus() );

        File.WriteAllText( licenseFilename, agx.Runtime.instance().readEncryptedLicense() );

        success = File.Exists( licenseFilename );
      }
      catch ( System.Exception e ) {
        if ( throwOnerror )
          throw;

        success = false;

        Debug.LogError( e.Message );
      }

      return success;
    }

    /// <summary>
    /// Update license information of the license loaded
    /// into AGX Dynamics.
    /// </summary>
    public static LicenseInfo UpdateLicenseInformation()
    {
      return (LicenseInfo = LicenseInfo.Create());
    }

    /// <summary>
    /// Activate license with given id and password.
    /// </summary>
    /// <param name="licenseId">License id.</param>
    /// <param name="licensePassword">License password.</param>
    /// <param name="targetDirectory">Target directory of the activated license file.</param>
    /// <param name="onDone">Callback when the request has been done.</param>
    public static void ActivateAsync( int licenseId,
                                      string licensePassword,
                                      string targetDirectory,
                                      Action<bool> onDone )
    {
      if ( IsBusy ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to activate license with id {licenseId} - activation is still in progress." );
        onDone?.Invoke( false );
        return;
      }

      var licenseFilename = IO.Environment.FindUniqueFilename( $"{targetDirectory}/agx{GetLicenseExtension( LicenseInfo.LicenseType.Service )}" );
      s_activationTask = Task.Run( () =>
      {
        var success = false;
        try {
          success = agx.Runtime.instance().activateAgxLicense( licenseId,
                                                               licensePassword,
                                                               licenseFilename );
          UpdateLicenseInformation();
        }
        catch ( Exception e ) {
          Debug.LogException( e );
          success = false;
        }
        onDone?.Invoke( success );
      } );
    }

    /// <summary>
    /// Activate license with given id and password. The activation is blocking
    /// and may take several seconds to perform.
    /// </summary>
    /// <param name="licenseId">License id.</param>
    /// <param name="licensePassword">License password.</param>
    /// <param name="targetDirectory">Target directory of the activated license file.</param>
    /// <returns>True if successful, otherwise false.</returns>
    public static bool Activate( int licenseId,
                                 string licensePassword,
                                 string targetDirectory )
    {
      var success = false;
      ActivateAsync( licenseId, licensePassword, targetDirectory, isSuccess => success = isSuccess );
      s_activationTask?.Wait();
      return success;
    }

    /// <summary>
    /// Refresh given license with updated information from the license server.
    /// Note that the given license will be loaded after a successful refresh.
    /// </summary>
    /// <param name="filename">License file to refresh and load.</param>
    /// <param name="onDone">Callback with the result.</param>
    public static void RefreshAsync( string filename,
                                     Action<bool> onDone )
    {
      var isBusy     = IsBusy;
      var seemsValid = !string.IsNullOrEmpty( filename ) &&
                       !isBusy &&
                       File.Exists( filename ) &&
                       GetLicenseType( filename ) == LicenseInfo.LicenseType.Service;
      if ( !seemsValid ) {
        var warning = $"AGXUnity.LicenseManager: Unable to refresh license file \"{filename}\" - ";
        if ( string.IsNullOrEmpty( filename ) )
          warning += "the license file is null or empty.";
        else if ( isBusy )
          warning += "the license manager is busy activating or refreshing another license.";
        else if ( !File.Exists( filename ) )
          warning += "the license file doesn't exist.";
        else
          warning += "the license file doesn't support refresh.";

        Debug.LogWarning( warning );
        onDone?.Invoke( false );

        return;
      }

      s_refreshTask = Task.Run( () =>
      {
        var success = false;
        try {
          success = agx.Runtime.instance().loadLicenseFile( filename, true );
          UpdateLicenseInformation();
        }
        catch ( Exception e ) {
          Debug.LogException( e );
          success = false;
        }
        onDone?.Invoke( success );
      } );
    }

    /// <summary>
    /// Refresh given license with updated information from the license server.
    /// Note that the given license will be loaded after a successful refresh.
    /// </summary>
    /// <param name="filename">License file to refresh and load.</param>
    public static bool Refresh( string filename )
    {
      var success = false;
      RefreshAsync( filename, isSuccess => success = isSuccess );
      s_refreshTask?.Wait();
      return success;
    }

    /// <summary>
    /// Deactivates the license and deletes the license file. Legacy licenses
    /// are only deleted.
    /// </summary>
    /// <param name="filename">License file to deactivate.</param>
    /// <returns>True if the license were successfully deactivated and deleted.</returns>
    public static bool DeactivateAndDelete( string filename )
    {
      if ( !File.Exists( filename ) ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - file doesn't exist." );
        return false;
      }

      var licenseType = GetLicenseType( filename );
      if ( licenseType == LicenseInfo.LicenseType.Unknown ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - unknown file extension." );
        return false;
      }

      if ( licenseType == LicenseInfo.LicenseType.Legacy ) {
        Reset();
        return DeleteFile( filename );
      }

      // If we're not able to load the license we cannot deactivate it because
      // we don't know if we're deactivating the given file or some other
      // license loaded.
      if ( LoadFile( filename, $"Deactivating license: \"{filename}\"." ) )
        DeactivateLoaded();
      else {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate the license {filename} - the license has to be valid.\n" +
                          LicenseInfo.Status );
      }

      return DeleteFile( filename );
    }

    /// <summary>
    /// Deactivate currently loaded license.
    /// </summary>
    /// <returns>True if successfully deactivated, otherwise false.</returns>
    public static bool DeactivateLoaded()
    {
      var success = false;
      try {
        success = agx.Runtime.instance().deactivateAgxLicense();

        if ( success )
          Reset();
        else
          Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate loaded license - {agx.Runtime.instance().getStatus()}" );
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }
      return success;
    }

    /// <summary>
    /// Reset the current license loaded by AGX Dynamics.
    /// </summary>
    public static void Reset()
    {
      try {
        agx.Runtime.instance().clear();
      }
      catch ( Exception ) {
      }
      finally {
        LicenseInfo = new LicenseInfo();
      }
    }

    /// <summary>
    /// Finds license type given license file name. The filename may or may
    /// not contain path. If the license type isn't found LicenseType.Invalid
    /// is returned.
    /// </summary>
    /// <param name="licenseFilename">License filename (optionally including path).</param>
    /// <returns>License type if found, otherwise LicenseType.Invalid.</returns>
    public static LicenseInfo.LicenseType GetLicenseType( string licenseFilename )
    {
      var extension = Path.GetExtension( licenseFilename );
      var index = Array.IndexOf( s_licenseExtensions, extension );
      if ( index < 0 )
        return LicenseInfo.LicenseType.Unknown;
      return (LicenseInfo.LicenseType)index;
    }

    /// <summary>
    /// Searches all directories from application/project root for
    /// license files of given license type.
    /// </summary>
    /// <param name="type">License type to check for.</param>
    /// <returns>Array of license files (absolute path).</returns>
    public static string[] FindLicenseFiles( LicenseInfo.LicenseType type )
    {
      return Directory.GetFiles( ".",
                                 $"*{GetLicenseExtension( type )}",
                                 SearchOption.AllDirectories ).Select( filename => filename.PrettyPath() ).ToArray();
    }

    /// <summary>
    /// Searches all directories from application/project root for any
    /// type of AGX Dynamics license files.
    /// </summary>
    /// <returns>Array of all AGX Dynamics license files, service before legacy.</returns>
    public static string[] FindLicenseFiles()
    {
      return ( from licenseType in LicenseTypes
               from licenseFile in FindLicenseFiles( licenseType )
               select licenseFile ).ToArray();
    }

    /// <summary>
    /// Searches all directories from application/project root for encrypted runtime
    /// activation files. These files are used to activate runtime licenses on other
    /// computers and results in a service license file post successful activation.
    /// </summary>
    /// <returns>Array of all AGX Dynamics encrypted runtime activation files.</returns>
    public static string[] FindRuntimeActivationFiles()
    {
      return Directory.GetFiles( ".",
                                 $"*{s_runtimeActivationExtension}",
                                 SearchOption.AllDirectories ).Select( filename => filename.PrettyPath() ).ToArray();
    }

    /// <summary>
    /// Predefined license file name given license type.
    /// </summary>
    /// <param name="type">License type.</param>
    /// <returns>Predefined license filename for given license type.</returns>
    public static string GetLicenseExtension( LicenseInfo.LicenseType type )
    {
      return s_licenseExtensions[ (int)type ];
    }

    /// <summary>
    /// Runtime activation license file extension.
    /// </summary>
    /// <returns>The runtime activation license file extension.</returns>
    public static string GetRuntimeActivationExtension()
    {
      return s_runtimeActivationExtension;
    }

    /// <summary>
    /// Await active tasks to complete.
    /// </summary>
    public static void AwaitTasks()
    {
      if ( s_activationTask != null && s_activationTask.Status == TaskStatus.Running )
        s_activationTask.Wait();
      s_activationTask = null;
    }

    public enum LicenseContentType
    {
      Unknown,
      Service,
      EncryptedRuntimeService,
      Legacy,
      LegacyObfuscated
    }

    /// <summary>
    /// Finds license type given file/string content.
    /// </summary>
    /// <param name="licenseContent">License file/string content.</param>
    /// <returns>License content type.</returns>
    public static LicenseContentType FindLicenseContentType( string licenseContent )
    {
      if ( string.IsNullOrEmpty( licenseContent ) )
        return LicenseContentType.Unknown;
      else if ( licenseContent.StartsWith( @"<SoftwareKey>" ) )
        return LicenseContentType.Service;
      else if ( licenseContent.StartsWith( @"RT=" ) )
        return LicenseContentType.EncryptedRuntimeService;
      else if ( licenseContent.StartsWith( @"Key {" ) )
        return LicenseContentType.Legacy;
      // Assuming obfuscated if nothing else matches.
      else
        return LicenseContentType.LegacyObfuscated;
    }

    /// <summary>
    /// Delete license and its .meta file (if editor and exists).
    /// </summary>
    /// <param name="filename">License file to delete.</param>
    /// <returns>True if successfully deleted, otherwise false.</returns>
    public static bool DeleteFile( string filename )
    {
      try {
        File.Delete( filename );
#if UNITY_EDITOR
        if ( File.Exists( $"{filename}.meta" ) ) {
          File.Delete( $"{filename}.meta" );
          UnityEditor.AssetDatabase.Refresh();
        }
#endif
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }

      return !File.Exists( filename );
    }

    /// <summary>
    /// Load text file with context.
    /// </summary>
    /// <param name="filename">Filename, including path, to load.</param>
    /// <param name="context">Context of the call to this method.</param>
    /// <returns>True if successfully loaded, otherwise false.</returns>
    private static bool LoadFile( string filename, string context )
    {
      if ( string.IsNullOrEmpty( filename ) ) {
        IssueLoadWarning( "Filename is null or empty.", context );
        return false;
      }

      if ( !File.Exists( filename ) ) {
        IssueLoadWarning( $"Given filename: {filename} - doesn't exist.", context );
        return false;
      }

      var text = string.Empty;
      try {
        text = File.ReadAllText( filename );
      }
      catch ( Exception e ) {
        IssueLoadWarning( $"Caught exception reading text from file: {filename}.", context );
        Debug.LogException( e );
        return false;
      }

      var loadSuccess = Load( text, context );

      // If the license has been refreshed we have to write the
      // new license content to 'filename' independent of 'loadSuccess'.
      try {
        var isRefreshed = agx.Runtime.instance().isLicenseRefreshed();
        var isRefreshedAndCanWrite = isRefreshed &&
                                     IO.Environment.CanWriteToExisting( filename );
        if ( isRefreshedAndCanWrite ) {
          LoadInfo( $"The license has been refreshed - rewriting license file {filename}.", context );

          using ( var str = new StreamWriter( filename, false ) )
            str.WriteLine( agx.Runtime.instance().readEncryptedLicense() );

          LoadInfo( $"Successfully updated license file {filename}.", context );
        }
        else if ( isRefreshed ) {
          Debug.LogError( $"AGXUnity.LicenseManager: Unable to write updated license information to {filename} - write access is required." );
        }
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }

      return loadSuccess;
    }

    /// <summary>
    /// Load license string with context.
    /// </summary>
    /// <param name="licenseContent">License content.</param>
    /// <param name="context">Context description or target filename (including path) if runtime activation.</param>
    /// <returns>True if successful, otherwise false.</returns>
    private static bool Load( string licenseContent, string context )
    {
      if ( string.IsNullOrEmpty( licenseContent ) ) {
        IssueLoadWarning( "License content is null or empty.", context );
        return false;
      }

      var licenseContentType = FindLicenseContentType( licenseContent );
      if ( licenseContentType == LicenseContentType.Unknown ) {
        IssueLoadWarning( $"Unknown license content: \"{licenseContent}\".", context );
        return false;
      }

      try {
        // Service type.
        if ( licenseContentType == LicenseContentType.Service ) {
          agx.Runtime.instance().loadLicenseString( licenseContent );
          LoadInfo( $"Loading service license successful: {agx.Runtime.instance().isValid()}.",
                    context );
        }
        // Runtime license activation.
        else if ( licenseContentType == LicenseContentType.EncryptedRuntimeService ) {
          // Temporary fix until activateEncryptedRuntime has been fixed in AGX Dynamics
          // to support non-ASCII input paths.
          var cwd = Directory.GetCurrentDirectory();
          agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( cwd );
          agx.Runtime.instance().activateEncryptedRuntime( licenseContent, context );
          agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).removeFilePath( cwd );

          LoadInfo( $"Activating encrypted runtime license \"{context}\" successful: {agx.Runtime.instance().isValid()}.",
                    context );
        }
        // Legacy type.
        else if ( licenseContentType == LicenseContentType.Legacy ) {
          agx.Runtime.instance().unlock( licenseContent );
          LoadInfo( $"Loading legacy license successful: {agx.Runtime.instance().isValid()}.",
                    context );
        }
        // Assume obfuscated legacy.
        else {
          agx.Runtime.instance().verifyAndUnlock( licenseContent );
          LoadInfo( $"Loading obfuscated legacy license successful: {agx.Runtime.instance().isValid()}.",
                    context );
        }
      }
      catch ( Exception e ) {
        IssueLoadWarning( "Caught exception calling AGX Dynamics.", context );
        Debug.LogException( e );
        return false;
      }

      UpdateLicenseInformation();

      return LicenseInfo.IsValid;
    }

    private static void LoadInfo( string info, string context )
    {
      if ( !Application.isEditor )
        Debug.Log( $"AGXUnity.LicenseManager: {info} (Context: {context})" );
    }

    private static void IssueLoadWarning( string warning, string context )
    {
      if ( !Application.isEditor )
        Debug.LogWarning( $"AGXUnity.LicenseManager: {warning} (Context: {context})" );
    }


    private static string[] s_licenseExtensions = new string[] { null, ".lfx", ".lic" };
    private static string s_runtimeActivationExtension = ".rtlfx";
    private static LicenseInfo s_licenseInfo = new LicenseInfo();
    private static Task s_activationTask = null;
    private static Task s_refreshTask = null;
  }
}
