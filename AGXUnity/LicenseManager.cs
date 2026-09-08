using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AGXUnity
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#license-manager" )]
  public static class LicenseManager
  {
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void CacheEditorProcessState()
    {
      try {
        s_isAssetImportWorkerProcess = UnityEditor.AssetDatabase.IsAssetImportWorkerProcess();
      }
      catch ( UnityEngine.UnityException ) {
        s_isAssetImportWorkerProcess = LooksLikeAssetImportWorkerProcess();
      }
    }
#endif

    public static bool CanAccessRuntime => !IsAssetImportWorkerProcess;

    internal static agx.Runtime Runtime
    {
      get
      {
        if ( !CanAccessRuntime )
          throw new AGXUnity.Exception( "Runtime may not be accessed from a Unity asset import worker process." );

        return agx.Runtime.instance();
      }
    }

    public static bool IsAssetImportWorkerProcess
    {
      get
      {
#if UNITY_EDITOR
        if ( s_isAssetImportWorkerProcess.HasValue )
          return s_isAssetImportWorkerProcess.Value;

        try {
          s_isAssetImportWorkerProcess = UnityEditor.AssetDatabase.IsAssetImportWorkerProcess();
          return s_isAssetImportWorkerProcess.Value;
        }
        catch ( UnityEngine.UnityException ) {
          return LooksLikeAssetImportWorkerProcess();
        }
#else
        return false;
#endif
      }
    }

#if UNITY_EDITOR
    private static bool LooksLikeAssetImportWorkerProcess()
    {
      try {
        var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        if ( processName.IndexOf( "AssetImportWorker", StringComparison.OrdinalIgnoreCase ) >= 0 )
          return true;

        foreach ( var arg in System.Environment.GetCommandLineArgs() )
          if ( arg.IndexOf( "AssetImportWorker", StringComparison.OrdinalIgnoreCase ) >= 0 )
            return true;
      }
      catch {
      }

      return false;
    }

    private static bool? s_isAssetImportWorkerProcess = null;
#endif

    /// <summary>
    /// Current license information loaded in AGX Dynamics.
    /// </summary>
    public static LicenseInfo LicenseInfo
    {
      get
      {
        if ( !CanAccessRuntime )
          return new LicenseInfo();
        if ( !s_licenseInfo.IsParsed )
          LicenseInfo = LicenseInfo.Create();
        return s_licenseInfo;
      }
      private set
      {
        s_licenseInfo = value;
        if ( !value.IsFloating )
          s_activeFloatingLicenseFilename = null;

        // We could end up here during instantiation of the
        // NativeHandler. Only validate license in the native
        // handler if it has an instance.
        if ( CanAccessRuntime && NativeHandler.HasInstance )
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
    public static bool IsRefreshing => IsOperationRunning( LicenseOperation.Refresh );

    /// <summary>
    /// True if a license is being activated.
    /// </summary>
    public static bool IsActivating => IsOperationRunning( LicenseOperation.Activate );

    public static bool IsConnecting => IsOperationRunning( LicenseOperation.Connect );

    public static bool IsReturning => IsOperationRunning( LicenseOperation.Return );

    /// <summary>
    /// True while any asynchronous license operation or its callback is running.
    /// </summary>
    public static bool IsBusy
    {
      get { lock ( s_operationLock ) return s_operationTask != null && !s_operationTask.IsCompleted; }
    }

    /// <summary>
    /// Whether AGX has a floating session, including one opened outside the plugin.
    /// </summary>
    public static bool HasFloatingSession => CanAccessRuntime && Runtime.isFloatingLicense();

    /// <summary>
    /// Normalized absolute source path of a floating session opened by this plugin.
    /// Null when no session is held or its source is unknown. License IDs cannot
    /// identify a floating file: they may be empty or shared by multiple files.
    /// </summary>
    public static string ActiveFloatingLicenseFilename
    {
      get
      {
        if ( !IsBusy && !HasFloatingSession )
          s_activeFloatingLicenseFilename = null;
        return s_activeFloatingLicenseFilename;
      }
    }

    /// <summary>
    /// Failure details captured before querying license information changes native status.
    /// </summary>
    public static string LastOperationError { get; private set; }

    /// <summary>
    /// Manual return disables automatic checkout for the remainder of this editor
    /// session, until an explicit Connect succeeds. Standalone defaults are unchanged.
    /// </summary>
    public static bool AutomaticFloatingCheckoutEnabled
    {
      get
      {
#if UNITY_EDITOR
        var pending = Volatile.Read( ref s_pendingFloatingSessionState );
        return pending == 2 || ( pending == 0 && !UnityEditor.SessionState.GetBool( s_floatingReturnedKey, false ) );
#else
        return true;
#endif
      }
    }

    /// <summary>
    /// Keep an already valid native license, or load a license file (service
    /// or legacy) located under application/project root. Service (*.lfx) is
    /// searched for before legacy (*.lic). The first valid file found is loaded.
    /// </summary>
    /// <returns>
    /// True if an existing valid license was retained or a valid file was loaded,
    /// otherwise false.
    /// </returns>
    public static bool LoadFile()
    {
      return LoadFile( allowFloating: true );
    }

    /// <summary>
    /// Keep an already valid native license, otherwise search for a license,
    /// optionally excluding floating checkout and its fallback. An existing
    /// floating session is preserved; return it before replacing it.
    /// </summary>
    public static bool LoadFile( bool allowFloating )
    {
      if ( !CanAccessRuntime || IsBusy )
        return false;

      // Scripts or native initialization may have already loaded a license.
      // Preserve the actual native license instead of clearing it and later
      // restoring only a snapshot of its information.
      var currentLicense = UpdateLicenseInformation();
      if ( currentLicense.IsValid || HasFloatingSession )
        return currentLicense.IsValid;

      Reset();

      var licenseFiles = FindLicenseFiles();
      foreach ( var licenseFile in licenseFiles ) {
        var file = licenseFile.PrettyPath();
        if ( LoadFile( file,
                       $"License file \"{file}\" found in search from application root.",
                       allowFloating ) )
          return true;
      }

      UpdateLicenseInformation();
      return false;
    }

    public static LicenseInfo QueryInfo( string filename )
    {
      var info = new LicenseInfo()
      {
        IsValid = false,
        Type = LicenseInfo.LicenseType.Unknown,
        TypeDescription = "Unknown",
      };

      if ( !CanAccessRuntime )
        return info;

      if ( !File.Exists( filename ) ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to query license info for license {filename} - file doesn't exist." );
        return info;
      }

      var licenseType = GetLicenseType( filename );
      if ( licenseType == LicenseInfo.LicenseType.Unknown ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to query license info for license {filename} - unknown file extension." );
        return info;
      }

      var licenseContent = File.ReadAllText(filename);
      if ( licenseType == LicenseInfo.LicenseType.Legacy )
        return LicenseInfo.FromLegacy( licenseContent );

      return LicenseInfo.FromNative( Runtime.queryLicenseInformation( licenseContent ) );
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
      if ( !CanAccessRuntime )
        return false;

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
      if ( !CanAccessRuntime )
        return false;

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
        var encrypted = Runtime.encryptRuntimeActivation( runtimeLicenseId,
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
                        $"encryption failed with status: {Runtime.getStatus()}" );
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
      if ( !CanAccessRuntime )
        return false;
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
      if ( !CanAccessRuntime )
        return false;
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
      if ( !CanAccessRuntime )
        return false;

      var success = false;

      try {
        var activationText = Runtime.generateOfflineActivationRequest( licenseId, licensePassword );
        if ( !string.IsNullOrEmpty( Runtime.getStatus() ) )
          throw new AGXUnity.Exception( Runtime.getStatus() );

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
      if ( !CanAccessRuntime )
        return false;

      var success = false;

      try {
        if ( IsBusy )
          throw new AGXUnity.Exception( "The license manager is busy." );
        RequireNoFloatingSession();
        if ( Path.GetExtension( licenseFilename ) != GetLicenseExtension( LicenseInfo.LicenseType.Service ) )
          licenseFilename += GetLicenseExtension( LicenseInfo.LicenseType.Service );

        if ( File.Exists( webResponseFilenameOrContent ) )
          webResponseFilenameOrContent = File.ReadAllText( webResponseFilenameOrContent );

        if ( !Runtime.processOfflineActivationRequest( webResponseFilenameOrContent ) )
          throw new AGXUnity.Exception( Runtime.getStatus() );

        File.WriteAllText( licenseFilename, Runtime.readEncryptedLicense() );

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
      if ( !CanAccessRuntime )
        return ( LicenseInfo = new LicenseInfo() );

      return ( LicenseInfo = LicenseInfo.Create() );
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
      StartOperation( LicenseOperation.Activate, () => {
        RequireNoFloatingSession();
        var licenseFilename = IO.Environment.FindUniqueFilename( $"{targetDirectory}/agx{GetLicenseExtension( LicenseInfo.LicenseType.Service )}" );
        return Runtime.activateAgxLicense( licenseId, licensePassword, licenseFilename );
      }, onDone );
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
      Task task;
      lock ( s_operationLock ) {
        if ( !CanAccessRuntime || IsBusy )
          return false;
        ActivateAsync( licenseId, licensePassword, targetDirectory, result => success = result );
        task = s_operationTask;
      }
      task.GetAwaiter().GetResult();
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
      StartOperation( LicenseOperation.Refresh, () => {
        RequireNoFloatingSession();
        var info = QueryInfo( filename );
        if ( info.IsFloating )
          throw new AGXUnity.Exception( "Use Connect to check out a floating license seat." );
        if ( GetLicenseType( filename ) != LicenseInfo.LicenseType.Service || !File.Exists( filename ) )
          throw new AGXUnity.Exception( "The license file doesn't support refresh or doesn't exist." );
        return Runtime.loadLicenseFile( filename, true );
      }, onDone );
    }

    /// <summary>
    /// Refresh given license with updated information from the license server.
    /// Note that the given license will be loaded after a successful refresh.
    /// </summary>
    /// <param name="filename">License file to refresh and load.</param>
    public static bool Refresh( string filename )
    {
      var success = false;
      Task task;
      lock ( s_operationLock ) {
        if ( !CanAccessRuntime || IsBusy )
          return false;
        RefreshAsync( filename, result => success = result );
        task = s_operationTask;
      }
      task.GetAwaiter().GetResult();
      return success;
    }

    /// <summary>
    /// Explicitly connect using floating file metadata, without activation or refresh.
    /// The callback runs on the worker thread (or immediately if busy).
    /// </summary>
    public static void ConnectFloatingAsync( string filename, Action<bool> onDone )
    {
      StartOperation( LicenseOperation.Connect, () => {
        RequireNoFloatingSession();
        if ( !QueryInfo( filename ).IsFloating )
          throw new AGXUnity.Exception( "The selected file isn't an identified floating license." );
        return OpenFloatingSession( filename );
      }, onDone );
    }

    /// <summary>
    /// Return the current floating seat, including sessions whose source is unknown.
    /// The callback runs on the worker thread (or immediately if busy).
    /// </summary>
    public static void ReturnFloatingAsync( Action<bool> onDone )
    {
      StartOperation( LicenseOperation.Return, () => {
        if ( !HasFloatingSession )
          throw new AGXUnity.Exception( "There is no floating license seat to return." );
        return CloseFloatingSession();
      }, onDone );
    }

    /// <summary>
    /// Deactivates the license and deletes the license file. Legacy licenses
    /// are only deleted.
    /// </summary>
    /// <param name="filename">License file to deactivate.</param>
    /// <returns>True if the license were successfully deactivated and deleted.</returns>
    public static bool DeactivateAndDelete( string filename )
    {
      if ( !CanAccessRuntime || IsBusy )
        return false;

      if ( !File.Exists( filename ) ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - file doesn't exist." );
        return false;
      }

      var licenseType = GetLicenseType( filename );
      if ( licenseType == LicenseInfo.LicenseType.Unknown ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - unknown file extension." );
        return false;
      }

      // Inspect the file before loading it: a failed checkout must never turn
      // into deactivation of a different, currently loaded license.
      try {
        var info = QueryInfo( filename );
        if ( info.IsFloating || !info.IsParsed || HasFloatingSession ) {
          Debug.LogWarning( "AGXUnity.LicenseManager: Cannot deactivate an unidentified or floating license, or replace a held floating seat." );
          return false;
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
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
      if ( !CanAccessRuntime )
        return false;
      if ( IsBusy || HasFloatingSession || UpdateLicenseInformation().Type != LicenseInfo.LicenseType.Service ) {
        Debug.LogWarning( "AGXUnity.LicenseManager: Only an identified non-floating service license can be deactivated." );
        return false;
      }

      var success = false;
      try {
        success = Runtime.deactivateAgxLicense();

        if ( success )
          Reset();
        else
          Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate loaded license - {Runtime.getStatus()}" );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }
      return success;
    }

    /// <summary>
    /// Reset the current license loaded by AGX Dynamics.
    /// </summary>
    public static void Reset()
    {
      if ( !CanAccessRuntime || IsBusy || HasFloatingSession )
        return;
      try {
        Runtime.clear();
      }
      catch ( System.Exception ) {
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
      Task task;
      lock ( s_operationLock )
        task = s_operationTask;
      task?.GetAwaiter().GetResult();
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
        if ( !CanAccessRuntime || IsBusy )
          return false;
        if ( HasFloatingSession && QueryInfo( filename ).IsFloating ) {
          var active = ActiveFloatingLicenseFilename;
          if ( string.IsNullOrEmpty( active ) ||
               string.Equals( Path.GetFullPath( filename ).Replace( '\\', '/' ), active,
                              Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal ) ) {
            Debug.LogWarning( "AGXUnity.LicenseManager: Return the current floating seat before deleting this file." );
            return false;
          }
        }
        File.Delete( filename );
#if UNITY_EDITOR
        if ( File.Exists( $"{filename}.meta" ) ) {
          File.Delete( $"{filename}.meta" );
          UnityEditor.AssetDatabase.Refresh();
        }
#endif
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        return false;
      }

      return !File.Exists( filename );
    }

    /// <summary>
    /// Load text file with context.
    /// </summary>
    /// <param name="filename">Filename, including path, to load.</param>
    /// <param name="context">Context of the call to this method.</param>
    /// <returns>True if successfully loaded, otherwise false.</returns>
    private static bool LoadFile( string filename, string context, bool allowFloating = true )
    {
      if ( !CanAccessRuntime )
        return false;
      if ( IsBusy || HasFloatingSession ) {
        IssueLoadWarning( "Return the floating seat and wait for any pending operation before loading a license.", context );
        return false;
      }
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
        if ( GetLicenseType( filename ) == LicenseInfo.LicenseType.Service && QueryInfo( filename ).IsFloating )
          return allowFloating && LoadFloating( filename, context );
      }
      catch ( System.Exception e ) {
        IssueLoadWarning( $"Caught exception reading text from file: {filename}.", context );
        Debug.LogException( e );
        return false;
      }

      var loadSuccess = Load( text, context );

      // Runtime activation owns its output file. Its request must survive a
      // failure without being rewritten or used for a floating checkout.
      if ( FindLicenseContentType( text ) == LicenseContentType.EncryptedRuntimeService )
        return loadSuccess;

      // If the license has been refreshed we have to write the
      // new license content to 'filename' independent of 'loadSuccess'.
      try {
        var isRefreshed = Runtime.isLicenseRefreshed();
        var isRefreshedAndCanWrite = isRefreshed &&
                                     IO.Environment.CanWriteToExisting( filename );
        if ( isRefreshedAndCanWrite ) {
          LoadInfo( $"The license has been refreshed - rewriting license file {filename}.", context );

          using ( var str = new StreamWriter( filename, false ) )
            str.WriteLine( Runtime.readEncryptedLicense() );

          LoadInfo( $"Successfully updated license file {filename}.", context );
        }
        else if ( isRefreshed ) {
          Debug.LogError( $"AGXUnity.LicenseManager: Unable to write updated license information to {filename} - write access is required." );
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }

      // Compatibility fallback for native versions that cannot query the file.
      // Never use it when automatic floating checkout has been disabled.
      if ( allowFloating && !loadSuccess &&
           ( LicenseInfo.Status?.IndexOf( "floating", StringComparison.OrdinalIgnoreCase ) ?? -1 ) >= 0 ) {
        loadSuccess = LoadFloating( filename, context );
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
      if ( !CanAccessRuntime )
        return false;
      if ( IsBusy || HasFloatingSession ) {
        IssueLoadWarning( "Return the floating seat and wait for any pending operation before loading a license.", context );
        return false;
      }
      if ( string.IsNullOrEmpty( licenseContent ) ) {
        IssueLoadWarning( "License content is null or empty.", context );
        return false;
      }

      var licenseContentType = FindLicenseContentType( licenseContent );
      if ( licenseContentType == LicenseContentType.Unknown ) {
        IssueLoadWarning( $"Unknown license content: \"{licenseContent}\".", context );
        return false;
      }

      var success = false;
      try {
        s_activeFloatingLicenseFilename = null;
        // Service type.
        if ( licenseContentType == LicenseContentType.Service ) {
          success = Runtime.loadLicenseString( licenseContent );
          LoadInfo( $"Loading service license successful: {success && Runtime.isValid()}.",
                    context );
        }
        // Runtime license activation.
        else if ( licenseContentType == LicenseContentType.EncryptedRuntimeService ) {
          // Temporary fix until activateEncryptedRuntime has been fixed in AGX Dynamics
          // to support non-ASCII input paths.
          var cwd = Directory.GetCurrentDirectory();
          agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( cwd );
          try {
            success = Runtime.activateEncryptedRuntime( licenseContent, context );
          }
          finally {
            agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).removeFilePath( cwd );
          }

          LoadInfo( $"Activating encrypted runtime license \"{context}\" successful: {success && Runtime.isValid()}.",
                    context );
        }
        // Legacy type.
        else if ( licenseContentType == LicenseContentType.Legacy ) {
          success = Runtime.unlock( licenseContent );
          LoadInfo( $"Loading legacy license successful: {success && Runtime.isValid()}.",
                    context );
        }
        // Assume obfuscated legacy.
        else {
          success = Runtime.verifyAndUnlock( licenseContent );
          LoadInfo( $"Loading obfuscated legacy license successful: {success && Runtime.isValid()}.",
                    context );
        }
      }
      catch ( System.Exception e ) {
        IssueLoadWarning( "Caught exception calling AGX Dynamics.", context );
        Debug.LogException( e );
        success = false;
      }
      finally {
        UpdateInformationAfterOperation();
      }

      return success && LicenseInfo.IsValid;
    }

    private static bool LoadFloating( string file, string context )
    {
      if ( !CanAccessRuntime )
        return false;
      var success = false;
      try {
        success = OpenFloatingSession( file );
        LastOperationError = success ? null : GetNativeOperationError( LicenseOperation.Connect );
        LoadInfo( $"Loading floating license successful: {success}.", context );
      }
      catch ( System.Exception e ) {
        LastOperationError = e.Message;
        IssueLoadWarning( "Caught exception calling AGX Dynamics.", context );
        Debug.LogException( e );
      }
      finally {
        UpdateInformationAfterOperation();
      }
      return success;
    }

    public static bool ReturnFloating( string context )
    {
      if ( !CanAccessRuntime || IsBusy )
        return false;
      var success = false;
      try {
        success = CloseFloatingSession();
        LastOperationError = success ? null : GetNativeOperationError( LicenseOperation.Return );
        LoadInfo( $"Returning floating license successful: {success}.", context );
      }
      catch ( System.Exception e ) {
        LastOperationError = e.Message;
        IssueLoadWarning( "Caught exception calling AGX Dynamics.", context );
        Debug.LogException( e );
      }
      finally {
        UpdateInformationAfterOperation();
      }
      return success;
    }

    private static bool OpenFloatingSession( string filename )
    {
      var normalized = Path.GetFullPath( filename ).Replace( '\\', '/' );
      var success = Runtime.openNetworkSession( normalized );
      if ( success )
        s_activeFloatingLicenseFilename = normalized;
      return success;
    }

    private static bool CloseFloatingSession()
    {
      var success = Runtime.closeNetworkSession();
      if ( success )
        s_activeFloatingLicenseFilename = null;
      return success;
    }

    private static void RequireNoFloatingSession()
    {
      if ( HasFloatingSession )
        throw new AGXUnity.Exception( "Return the current floating license seat before connecting or loading another license." );
    }

    private enum LicenseOperation { Activate, Refresh, Connect, Return }

    private static bool IsOperationRunning( LicenseOperation operation )
    {
      lock ( s_operationLock )
        return IsBusy && s_operation == operation;
    }

    private static void StartOperation( LicenseOperation operation, Func<bool> execute, Action<bool> onDone )
    {
      // Check on the calling thread so no license task is started by an asset
      // import worker, and normal editor tasks use the cached process state.
      if ( !CanAccessRuntime ) {
        onDone?.Invoke( false );
        return;
      }
      lock ( s_operationLock ) {
        if ( !IsBusy ) {
          s_operation = operation;
          // Publish the task before scheduling so two callers cannot both start.
          s_operationTask = new Task( () => {
            var success = false;
            LastOperationError = null;
            try {
              success = execute();
              if ( !success )
                LastOperationError = GetNativeOperationError( operation );
#if UNITY_EDITOR
              if ( success && ( operation == LicenseOperation.Connect || operation == LicenseOperation.Return ) )
                Interlocked.Exchange( ref s_pendingFloatingSessionState, operation == LicenseOperation.Return ? 1 : 2 );
#endif
            }
            catch ( System.Exception e ) {
              LastOperationError = $"{operation} failed. {e.Message}";
              Debug.LogException( e );
            }
            finally {
              UpdateInformationAfterOperation();
            }
            if ( !success )
              Debug.LogWarning( $"AGXUnity.LicenseManager: {LastOperationError}" );
            onDone?.Invoke( success );
          } );
          s_operationTask.Start( TaskScheduler.Default );
          return;
        }
      }
      Debug.LogWarning( "AGXUnity.LicenseManager: Another license operation is in progress." );
      onDone?.Invoke( false );
    }

    private static void UpdateInformationAfterOperation()
    {
      try {
        UpdateLicenseInformation();
      }
      catch ( System.Exception e ) {
        // Deliver the native result even if shutdown races with information
        // refresh. ReturnFloating is also called by NativeHandler's finalizer.
        Debug.LogException( e );
      }
    }

    private static string GetNativeOperationError( LicenseOperation operation )
    {
      var status = Runtime.getStatus();
      var extended = Runtime.getExtendedStatus();
      return $"{operation} failed." +
             ( string.IsNullOrWhiteSpace( status ) ? string.Empty : $"\n\n{status.Trim()}" ) +
             ( string.IsNullOrWhiteSpace( extended ) || extended == status ? string.Empty : $"\n\n{extended.Trim()}" );
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitializeEditorSessionHandling()
    {
      if ( !CanAccessRuntime )
        return;
      UnityEditor.EditorApplication.update += ApplyFloatingSessionState;
      UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => {
        AwaitTasks();
        ApplyFloatingSessionState();
      };
      UnityEditor.EditorApplication.playModeStateChanged += state => {
        if ( state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
             state == UnityEditor.PlayModeStateChange.ExitingPlayMode ) {
          AwaitTasks();
          ApplyFloatingSessionState();
        }
      };
    }

    // Runs even if the License Manager window was closed during the request.
    private static void ApplyFloatingSessionState()
    {
      var pending = Interlocked.Exchange( ref s_pendingFloatingSessionState, 0 );
      if ( pending != 0 )
        UnityEditor.SessionState.SetBool( s_floatingReturnedKey, pending == 1 );
    }

    private const string s_floatingReturnedKey = "AGXUnity.FloatingLicenseReturned";
    private static int s_pendingFloatingSessionState;
#endif

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
    private static readonly object s_operationLock = new object();
    private static Task s_operationTask;
    private static LicenseOperation s_operation;
    private static string s_activeFloatingLicenseFilename;
  }
}
