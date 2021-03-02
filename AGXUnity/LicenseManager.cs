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
    /// License types that AGX Dynamics supports.
    /// </summary>
    public enum LicenseType
    {
      /// <summary>
      /// An agx.lfx file or encrypted string.
      /// </summary>
      Service,
      /// <summary>
      /// An agx.lic file or obfuscated string.
      /// </summary>
      Legacy,
      /// <summary>
      /// Unidentified license type.
      /// </summary>
      Invalid
    }

    /// <summary>
    /// Current license information loaded in AGX Dynamics.
    /// </summary>
    public static LicenseInfo LicenseInfo
    {
      get
      {
        if ( !s_licenseInfo.IsParsed )
          s_licenseInfo = LicenseInfo.Create();
        return s_licenseInfo;
      }
      private set
      {
        s_licenseInfo = value;
      }
    }

    /// <summary>
    /// Enumerate known license types.
    /// </summary>
    public static IEnumerable<LicenseType> LicenseTypes
    {
      get
      {
        foreach ( LicenseType licenseType in Enum.GetValues( typeof( LicenseType ) ) ) {
          if ( licenseType == LicenseType.Invalid )
            yield break;
          else
            yield return licenseType;
        }
      }
    }

    /// <summary>
    /// True if the activation task is running, otherwise false.
    /// </summary>
    public static bool IsActivating { get { return s_activationTask != null && !s_activationTask.IsCompleted; } }

    /// <summary>
    /// Load license file (service or legacy) located in any directory
    /// under application/project root. Service (agx.lfx) is searched
    /// for before legacy (agx.lic).
    /// </summary>
    /// <returns>
    /// True if successful, false if license files weren't found
    /// or if the loaded license isn't valid.
    /// </returns>
    public static bool LoadFile()
    {
      var filename = FindLicenseFiles( LicenseType.Service ).FirstOrDefault() ??
                     FindLicenseFiles( LicenseType.Legacy ).FirstOrDefault();
      return LoadFile( filename?.PrettyPath(),
                       $"Searching for license service or legacy license file from application root: \"{filename?.PrettyPath()}\"." );
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
      return Load( licenseContent,
                   "Explicit license content." );
    }

    /// <summary>
    /// Update license information of the license loaded
    /// into AGX Dynamics.
    /// </summary>
    public static void UpdateLicenseInformation()
    {
      LicenseInfo = LicenseInfo.Create();
    }

    /// <summary>
    /// Activate license with given id and password.
    /// </summary>
    /// <param name="licenseId">License id.</param>
    /// <param name="licensePassword">License password.</param>
    /// <param name="targetDirectory">Target directory of the license file.</param>
    /// <param name="onDone">Callback when the request has been done.</param>
    public static void ActivateAsync( int licenseId,
                                      string licensePassword,
                                      string targetDirectory,
                                      Action<bool> onDone )
    {
      if ( IsActivating ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to activate license with id {licenseId} - activation is still in progress." );
        return;
      }

      s_activationTask = Task.Run( () =>
      {
        try {
          var success = agx.Runtime.instance().activateAgxLicense( licenseId,
                                                                   licensePassword,
                                                                   $"{targetDirectory}/agx{GetLicenseExtension( LicenseType.Service )}" );
          UpdateLicenseInformation();
          onDone?.Invoke( success );
        }
        catch ( Exception e ) {
          Debug.LogException( e );
        }
      } );
    }

    public static bool Activate( int licenseId,
                                 string licensePassword,
                                 string targetDirectory )
    {
      var success = false;
      ActivateAsync( licenseId, licensePassword, targetDirectory, isSuccess => success = isSuccess );
      s_activationTask?.Wait();
      return success;
    }

    public static bool DeactivateAndDelete( string filename )
    {
      if ( !File.Exists( filename ) ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - file doesn't exist." );
        return false;
      }

      var licenseType = GetLicenseType( filename );
      if ( licenseType == LicenseType.Invalid ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - unknown file extension." );
        return false;
      }

      if ( licenseType == LicenseType.Legacy ) {
        Reset();
        return DeleteFile( filename );
      }

      // If we're not able to load the license we cannot deactivate it because
      // we don't know if we're deactivating the given file or some other
      // license loaded.
      if ( !LoadFile( filename, $"Deactivating license: \"{filename}\"." ) ) {
        Debug.LogWarning( $"AGXUnity.LicenseManager: Unable to deactivate and delete license {filename} - the license has to be valid." );
        return false;
      }

      // Do not delete file if deactivation failed.
      return DeactivateLoaded() && DeleteFile( filename );
    }

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
        agx.Runtime.instance().unlock( null );
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
    public static LicenseType GetLicenseType( string licenseFilename )
    {
      var extension = Path.GetExtension( licenseFilename );
      var index = Array.IndexOf( s_licenseExtensions, extension );
      if ( index < 0 )
        return LicenseType.Invalid;
      return (LicenseType)index;
    }

    /// <summary>
    /// Searches all directories from application/project root for
    /// license files of given license type.
    /// </summary>
    /// <param name="type">License type to check for.</param>
    /// <returns>Array of license files (absolute path).</returns>
    public static string[] FindLicenseFiles( LicenseType type )
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
    /// Predefined license file name given license type.
    /// </summary>
    /// <param name="type">License type.</param>
    /// <returns>Predefined license filename for given license type.</returns>
    public static string GetLicenseExtension( LicenseType type )
    {
      return s_licenseExtensions[ (int)type ];
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

      return Load( text, context );
    }

    private static bool Load( string licenseContent, string context )
    {
      if ( string.IsNullOrEmpty( licenseContent ) ) {
        IssueLoadWarning( "License content is null or empty.", context );
        return false;
      }

      try {
        // Service type.
        if ( licenseContent.StartsWith( @"<SoftwareKey>" ) ) {
          agx.Runtime.instance().loadLicenseString( licenseContent );
          LoadInfo( $"Loading service license successful: {agx.Runtime.instance().isValid()}.",
                    context );
        }
        // Legacy type.
        else if ( licenseContent.StartsWith( @"Key {" ) ) {
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

      return LicenseInfo.LicenseValid;
    }

    /// <summary>
    /// Delete license and its .meta file (if editor and exists).
    /// </summary>
    /// <param name="filename">License file to delete.</param>
    /// <returns>True if successfully deleted, otherwise false.</returns>
    private static bool DeleteFile( string filename )
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

    private static void LoadInfo( string info, string context )
    {
      //if ( !Application.isEditor )
        Debug.Log( $"AGXUnity.LicenseManager: {info} (Context: {context})" );
    }

    private static void IssueLoadWarning( string warning, string context )
    {
      //if ( !Application.isEditor )
        Debug.LogWarning( $"AGXUnity.LicenseManager: {warning} (Context: {context})" );
    }


    private static string[] s_licenseExtensions = new string[] { ".lfx", ".lic", null };
    private static LicenseInfo s_licenseInfo = new LicenseInfo();
    private static Task s_activationTask = null;
  }
}
