using AGXUnityEditor.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AGXUnityEditor.Windows
{
  /// <summary>
  /// Container class for loading example data from the download server. 
  /// Since Unity's built in JSON-deserializer can't load lists at the top level
  /// we need to wrap the list in an object.
  /// </summary>
  [Serializable]
  public struct LoaderExamples
  {
    public List<ExampleMeta> examples;
  }

  /// <summary>
  /// Contains metadata for a single example including the name of the examples as well
  /// as URLs for the example thumbnail as well as the documentation.
  /// Furthermore, this metadata includes metadata about the package itself.
  /// </summary>
  [Serializable]
  public struct ExampleMeta
  {
    public string name;
    public string thumbnail;
    public string documentation;
    public PackageMeta package;
  }

  /// <summary>
  /// Contains metadata for an example unitypackage including the URL for the
  /// package download as well as the size of the package. This metadata also
  /// includes a list of dependencies which are required by the example package
  /// and whether or not the package requires the legacy input system to be enabled.
  /// </summary>
  [Serializable]
  public struct PackageMeta
  {
    public string download;
    public ulong size;
    public List<string> dependencies;
    public bool requiresLegacyInput;
  }


  /// <summary>
  /// Manager handling download and import of examples and their
  /// optional dependencies, such as ML Agents, Input System and/or
  /// legacy Input Manager. On initialization the examples.html
  /// (of AGX Dynamics for Unity documentation) is parsed for
  /// download links and the current project is scanned for already
  /// installed examples. The status of dependent packages is also
  /// checked.
  /// </summary>
  public static class ExamplesManager
  {

    /// <summary>
    /// Data of each example - may be null if the examples
    /// page hasn't been parsed (IsInitializing == true) or
    /// if the example has been removed.
    /// </summary>
    public class ExampleData
    {
      /// <summary>
      /// States of this example.
      /// </summary>
      public enum State
      {
        /// <summary>
        /// Unknown when the example exists but hasn't been
        /// checked whether it's installed.
        /// </summary>
        Unknown,
        /// <summary>
        /// The example is available for download but isn't
        /// installed in the project.
        /// </summary>
        NotInstalled,
        /// <summary>
        /// The example package is currently being downloaded.
        /// </summary>
        Downloading,
        /// <summary>
        /// The example package is available in the temporary
        /// directory but hasn't been imported.
        /// </summary>
        ReadyToInstall,
        /// <summary>
        /// The example is being imported into the project.
        /// </summary>
        Installing,
        /// <summary>
        /// The example is installed and valid in the project.
        /// </summary>
        Installed
      }

      /// <summary>
      /// The raw metadata for the example as downloaded from the download server.
      /// </summary>
      private ExampleMeta m_metadata;

      /// <summary>
      /// Directory where the example is installed, null if not installed.
      /// </summary>
      public DirectoryInfo InstalledDirectory = null;

      /// <summary>
      /// Download progress [0 .. 1] if Status == State.Downloading, otherwise 0.
      /// </summary>
      public float DownloadProgress = 0.0f;

      /// <summary>
      /// Internal. Callback when the package has been downloaded used
      /// to abort current downloads.
      /// </summary>
      public Action<FileInfo, RequestHandler.Status> OnDownloadCompleteCallback = null;

      /// <summary>
      /// Internal. Reference to package to be imported.
      /// </summary>
      public FileInfo DownloadedPackage = null;

      /// <summary>
      /// Dependencies of the example.
      /// </summary>
      public string[] Dependencies => m_metadata.package.dependencies.ToArray();

      /// <summary>
      /// True if the example requires legacy input manager to be enabled.
      /// Some examples either use it for input or has EventSystem for other
      /// reasons. If True and disabled in Player Settings, Unity will throw
      /// exceptions.
      /// </summary>
      public bool RequiresLegacyInputManager => m_metadata.package.requiresLegacyInput;

      /// <summary>
      /// Current status of the example.
      /// </summary>
      public State Status { get; private set; } = State.Unknown;

      /// <summary>
      /// Download URL of the example package.
      /// </summary>
      public string DownloadUrl => m_metadata.package.download;

      /// <summary>
      /// Documentation URL of the example package.
      /// </summary>
      public string DocumentationUrl => m_metadata.documentation;

      /// <summary>
      /// Directory name (in project) of the example.
      /// </summary>
      public string DirectoryName => $"AGXUnity_{PackageName}";

      /// <summary>
      /// The name of the the raw package, parsed from the donwload URL.
      /// </summary>
      public string PackageName
      {
        get
        {
          var exampleMatch = Regex.Match( m_metadata.package.download, @"AGXUnity_(\w+).unitypackage" );
          return exampleMatch.Groups[ 1 ].ToString();
        }
      }

      /// <summary>
      /// The name of the example.
      /// </summary>
      public string Name => m_metadata.name;

      public string SizeString
      {
        get
        {
          var size = m_metadata.package.size;
          return
            size >= Mathf.Pow( 2, 10 * 3 ) ? $"{size / Mathf.Pow( 2, 10 * 3 ):G3} GiB" :
            size >= Mathf.Pow( 2, 10 * 2 ) ? $"{size / Mathf.Pow( 2, 10 * 2 ):G3} MiB" :
            size >= Mathf.Pow( 2, 10 * 1 ) ? $"{size / Mathf.Pow( 2, 10 * 1 ):G3} KiB" : $"{size} B";
        }
      }

      /// <summary>
      /// A fallback texture which is used when the actual thumbnail texture fails to load.
      /// </summary>
      public static Texture2D FallbackTexture;

      private Texture2D m_thumbnail;

      /// <summary>
      /// The thumbnail used to showcase the example.
      /// </summary>
      public Texture2D Thumbnail
      {
        get
        {
          if ( m_thumbnail != null ) return m_thumbnail;
          if ( webRequest.isDone && webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success ) {
            m_thumbnail = new Texture2D( 2, 2 );
            m_thumbnail.LoadImage( webRequest.downloadHandler.data );
          }
          return m_thumbnail != null ? m_thumbnail : FallbackTexture;
        }
      }

      private UnityEngine.Networking.UnityWebRequest webRequest;

      /// <summary>
      /// Scene file name including relative path to the project.
      /// </summary>
      public string Scene
      {
        get
        {
          if ( InstalledDirectory == null )
            return string.Empty;

          var sceneFileInfo = InstalledDirectory.EnumerateFiles( "*.unity",
                                                                 SearchOption.TopDirectoryOnly ).FirstOrDefault();
          if ( sceneFileInfo == null )
            return string.Empty;
          return IO.Utils.MakeRelative( sceneFileInfo.FullName,
                                                       Application.dataPath ).Replace( '\\', '/' );
        }
      }

      /// <summary>
      /// Creates a new ExampleData based on the specified metadata.
      /// </summary>
      /// <param name="meta">The metadata used to create the example data</param>
      public ExampleData( ExampleMeta meta )
      {
        m_metadata = meta;
        if ( m_metadata.thumbnail != null ) {
          webRequest = UnityEngine.Networking.UnityWebRequest.Get( m_metadata.thumbnail );
          webRequest.SendWebRequest();
        }
      }

      /// <summary>
      /// Internal. Update status of this example.
      /// </summary>
      /// <param name="status">New status.</param>
      public void UpdateStatus( State status )
      {
        Status = status;
      }
    }

    /// <summary>
    /// State of this dependency.
    /// </summary>
    public enum DependencyState
    {
      /// <summary>
      /// Unknown until the request has been received
      /// whether the dependency is installed or not.
      /// </summary>
      Unknown,
      /// <summary>
      /// The dependency is not installed.
      /// </summary>
      NotInstalled,
      /// <summary>
      /// The dependency is currently being installed.
      /// </summary>
      Installing,
      /// <summary>
      /// The dependency is installed.
      /// </summary>
      Installed
    }

    /// <summary>
    /// Enumerate examples.
    /// </summary>
    public static IEnumerable<ExampleData> Examples => s_exampleData.AsEnumerable();

    /// <summary>
    /// True when data is being fetched, otherwise false.
    /// </summary>
    public static bool IsInitializing { get; private set; } = false;

    /// <summary>
    /// True if dependencies are being installed, otherwise false.
    /// </summary>
    public static bool IsInstallingDependencies => s_addPackageRequests.Count > 0;

    /// <summary>
    /// True if (new) Input System is enabled in the project settings,
    /// otherwise false.
    /// </summary>
    public static bool InputSystemEnabled
    {
      get
      {
        var property = InputSystemProperty;
        return property != null &&
               ( ( property.type == "bool" && property.boolValue ) ||
                 ( property.type == "int" && property.intValue >= 1 ) );
      }
    }

    /// <summary>
    /// True if (legacy) Input Manager is enabled in the project settings,
    /// otherwise false.
    /// </summary>
    public static bool LegacyInputManagerEnabled
    {
      get
      {
        var property = LegacyInputManagerDisabledProperty;
        return property == null ||
              ( property.type == "bool" && !property.boolValue ) ||
              ( property.type == "int" && property.intValue != 1 );
      }
    }

    /// <summary>
    /// Scraps old data and fetch new information about examples
    /// and dependencies.
    /// </summary>
    public static void Initialize()
    {
      IsInitializing = true;

      ExampleData.FallbackTexture = IconManager.GetIcon( "algoryx_white_shadow_icon.png" );

      AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
      AssetDatabase.importPackageCancelled += OnImportPackageCanceled;
      AssetDatabase.importPackageFailed += OnImportPackageFailed;

      RequestHandler.Get( s_metadataURL,
                          TempDirectory,
                          OnMetadata );
    }

    /// <summary>
    /// Abort any downloads and handling of currently importing dependencies.
    /// </summary>
    public static void Uninitialize()
    {
      AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
      AssetDatabase.importPackageCancelled -= OnImportPackageCanceled;
      AssetDatabase.importPackageFailed -= OnImportPackageFailed;

      foreach ( var data in s_exampleData )
        RequestHandler.Abort( data?.OnDownloadCompleteCallback );

      s_exampleData.Clear();
      s_dependencyData.Clear();
    }

    /// <summary>
    /// Resolve input settings to work with all examples. If any setting
    /// has been changed, the user will be asked to restart the editor.
    /// For all examples to work, both legacy input manager and new input
    /// system has to be enabled: Edit -> Project Settings -> Player ->
    /// Active Input Handling -> Both. This method is doing that for the caller.
    /// </summary>
    public static void ResolveInputSystemSettings()
    {
      // From ProjectSettings.asset:
      //     Old enabled:
      //       enableNativePlatformBackendsForNewInputSystem: 0
      //       disableOldInputManagerSupport: 0
      //     New enabled:
      //       enableNativePlatformBackendsForNewInputSystem: 1
      //       disableOldInputManagerSupport: 1
      //     Both:
      //       enableNativePlatformBackendsForNewInputSystem: 1
      //       disableOldInputManagerSupport: 0
      // In 2020.2 and later it's an int:
      //     activeInputHandler = 0: old, 1: new, 2 both
      // We want "Both".

      if ( InputSystemPropertyAsInt != null ) {
        if ( InputSystemPropertyAsInt.intValue == 2 )
          return;
        InputSystemPropertyAsInt.intValue = 2;
        InputSystemPropertyAsInt.serializedObject.ApplyModifiedPropertiesWithoutUndo();
      }
      else if ( InputSystemProperty != null && LegacyInputManagerDisabledProperty != null ) {
        if ( InputSystemProperty.boolValue && !LegacyInputManagerDisabledProperty.boolValue )
          return;
        InputSystemProperty.boolValue = true;
        LegacyInputManagerDisabledProperty.boolValue = false;

        InputSystemProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
      }
      else
        return;

      var restartNow = EditorUtility.DisplayDialog( "Input System",
                                                    "Both Input System and (legacy) Input Manager must be enabled for " +
                                                    "all examples to work.\n\n" +
                                                    "This change has been applied to the Player Settings but requires restart of the editor. " +
                                                    "Restart the editor now?",
                                                    "Yes",
                                                    "Later" );
      if ( restartNow ) {
        var postRestartMethod = typeof( ExamplesManager ).FullName + ".PostInputSystemSettingsRestart";
        Uninitialize();
        EditorApplication.OpenProject( Path.Combine( Application.dataPath, ".." ),
                                        "-executeMethod",
                                        postRestartMethod );
      }
    }

    /// <summary>
    /// Handle import queue where import of dependencies are
    /// prioritized. All examples has to be downloaded before
    /// the examples are imported.
    /// </summary>
    public static void HandleImportQueue()
    {
      if ( IsInitializing )
        return;

      if ( s_exampleData.Any( data => data != null &&
                                      ( data.Status == ExampleData.State.Installing ||
                                        data.Status == ExampleData.State.Downloading ) ) )
        return;

      // If we're installing dependencies we will not import any example.
      if ( s_addPackageRequests.Count > 0 ) {
        foreach ( var addRequest in s_addPackageRequests ) {
          if ( !addRequest.IsCompleted )
            continue;

          if ( addRequest.Status == StatusCode.Success ) {
            s_dependencyData[ addRequest.Result.name ] = DependencyState.Installed;
            if ( addRequest.Result.name == "com.unity.inputsystem" )
              ResolveInputSystemSettings();
          }
          else
            Debug.LogError( addRequest.Error.message );
        }

        s_addPackageRequests.RemoveAll( addRequest => addRequest.IsCompleted );

        return;
      }

      try {
        // Important for Unity to not start compilation of any
        // imported script. If Unity compiles, the data of the
        // downloaded packages will be deleted during Initialize
        // post-compile.
        AssetDatabase.DisallowAutoRefresh();

        foreach ( var data in s_exampleData ) {
          if ( data == null || data.DownloadedPackage == null )
            continue;

          if ( data.DownloadedPackage.Exists ) {
            data.UpdateStatus( ExampleData.State.Installing );
            AssetDatabase.ImportPackage( IO.Utils.MakeRelative( data.DownloadedPackage.FullName,
                                                                Application.dataPath ).Replace( '\\', '/' ),
                                         false );
          }
          else
            data.DownloadedPackage = null;
        }
      }
      finally {
        AssetDatabase.AllowAutoRefresh();
      }
    }

    /// <summary>
    /// True if there are issues with the current example. E.g.,
    /// unresolved dependencies or the project settings has to
    /// be changed.
    /// </summary>
    /// <param name="example">Example to check for issues with.</param>
    /// <returns>True if there are issues with the example, otherwise false.</returns>
    public static bool HasUnresolvedIssues( ExampleData example )
    {
      return HasUnresolvedDependencies( example ) ||
             example == null ||
             ( example.RequiresLegacyInputManager && !LegacyInputManagerEnabled ) ||
             ( example.Dependencies.Contains( "com.unity.inputsystem" ) && !InputSystemEnabled );
    }

    /// <summary>
    /// True if there are dependencies that hasn't been installed
    /// for the given example.
    /// </summary>
    /// <param name="example">Example to verify dependencies for.</param>
    /// <returns>True if there are unresolved dependencies for the example, otherwise false.</returns>
    public static bool HasUnresolvedDependencies( ExampleData example )
    {
      return example != null &&
             example.Dependencies.Length > 0 &&
             example.Dependencies.Any( dependency => GetDependencyState( dependency ) != DependencyState.Installed );
    }

    /// <summary>
    /// Install the given dependency. Does nothing of the dependency
    /// is already available.
    /// </summary>
    /// <param name="dependency">Dependency to install.</param>
    public static void InstallDependency( string dependency )
    {
      var depData = GetDependencyState(dependency);
      if ( depData != DependencyState.NotInstalled )
        return;
      s_addPackageRequests.Add( Client.Add( dependency ) );
    }

    public static void Download( ExampleData example )
    {
      if ( example == null ) {
        Debug.LogWarning( $"Unable to download: {example} - data for the example isn't available." );
        return;
      }
      else if ( example.Status == ExampleData.State.Installed ) {
        Debug.LogWarning( $"Example {example} is already installed - ignoring download." );
        return;
      }
      else if ( example.Status == ExampleData.State.Downloading )
        return;

      if ( RequestHandler.Get( example.DownloadUrl,
                               TempDirectory,
                               OnDownloadComplete( example ),
                               OnDownloadProgress( example ) ) )
        example.UpdateStatus( ExampleData.State.Downloading );
    }

    /// <summary>
    /// Cancel download of the given example.
    /// </summary>
    /// <param name="example">Example to cancel download for.</param>
    public static void CancelDownload( ExampleData example )
    {
      if ( example == null || example.OnDownloadCompleteCallback == null )
        return;

      RequestHandler.Abort( example.OnDownloadCompleteCallback );
      example.UpdateStatus( ExampleData.State.NotInstalled );
    }

    /// <summary>
    /// Temporary directory "Temp" in the parent directory
    /// of "Assets".
    /// </summary>
    private static DirectoryInfo TempDirectory
    {
      get
      {
        if ( s_tempDirectory == null ) {
          s_tempDirectory = new DirectoryInfo( "./Temp" );
          if ( !s_tempDirectory.Exists )
            s_tempDirectory.Create();
        }
        return s_tempDirectory;
      }
    }

    /// <summary>
    /// Player Settings as a serialized object.
    /// </summary>
    private static SerializedObject PlayerSettings
    {
      get
      {
        if ( s_playerSettings == null )
          s_playerSettings = new SerializedObject( Unsupported.GetSerializedAssetInterfaceSingleton( "PlayerSettings" ) );
        return s_playerSettings;
      }
    }

    /// <summary>
    /// New from 2020.2: activeInputHandler = (0: old, 1: new, 2: both).
    /// </summary>
    private static SerializedProperty InputSystemPropertyAsInt => PlayerSettings?.FindProperty( "activeInputHandler" );

    /// <summary>
    /// Player Settings (new) Input System property where
    /// boolValue may be changed.
    /// </summary>
    private static SerializedProperty InputSystemProperty => PlayerSettings?.FindProperty( "enableNativePlatformBackendsForNewInputSystem" ) ??
                                                              InputSystemPropertyAsInt;

    /// <summary>
    /// Player Settings (legacy) Input Manager property where
    /// boolValue may be changed.
    /// </summary>
    /// <remarks>
    /// The value is inverted: "disableOldInputManagerSupport".
    /// </remarks>
    private static SerializedProperty LegacyInputManagerDisabledProperty => PlayerSettings?.FindProperty( "disableOldInputManagerSupport" ) ??
                                                                            InputSystemPropertyAsInt;

    /// <summary>
    /// Closure of current download complete callback. When the
    /// download is complete, this method will call OnDownloadComplete
    /// with ExampleData, FileInfo and request status.
    /// </summary>
    /// <param name="data">Example data.</param>
    /// <returns>Callback used by the download request.</returns>
    private static Action<FileInfo, RequestHandler.Status> OnDownloadComplete( ExampleData data )
    {
      Action<FileInfo, RequestHandler.Status> onComplete = ( fi, status ) =>
      {
        data.OnDownloadCompleteCallback = null;
        OnDownloadComplete( data, fi, status );
      };
      data.OnDownloadCompleteCallback = onComplete;
      return onComplete;
    }

    /// <summary>
    /// Callback when an example package has been downloaded (or failed).
    /// </summary>
    /// <param name="data">Example data.</param>
    /// <param name="fi">File info to the downloaded package.</param>
    /// <param name="status">Request status.</param>
    private static void OnDownloadComplete( ExampleData data, FileInfo fi, RequestHandler.Status status )
    {
      lock ( s_downloadCompleteLock ) {
        if ( status == RequestHandler.Status.Success ) {
          data.UpdateStatus( ExampleData.State.ReadyToInstall );
          data.DownloadedPackage = fi;
        }
        else
          data.UpdateStatus( ExampleData.State.NotInstalled );
        data.DownloadProgress = 0.0f;
      }
    }

    /// <summary>
    /// Closure updating the ExampleData.DownloadProgress of the given
    /// example data during downloads.
    /// </summary>
    /// <param name="data">Example data of the example being downloaded.</param>
    /// <returns>The request OnProgress callback.</returns>
    private static Action<float> OnDownloadProgress( ExampleData data )
    {
      Action<float> onProgress = progress =>
      {
        data.DownloadProgress = progress;
      };
      return onProgress;
    }

    /// <summary>
    /// Callback hook for successfully imported packages.
    /// </summary>
    /// <param name="packageName">Any successfully imported package.</param>
    private static void OnImportPackageCompleted( string packageName )
    {
      OnImportPackageDone( packageName, "Success" );
    }

    /// <summary>
    /// Callback hook for imports being canceled.
    /// </summary>
    /// <param name="packageName">Any canceled package import.</param>
    private static void OnImportPackageCanceled( string packageName )
    {
      OnImportPackageDone( packageName, "Canceled" );
    }

    /// <summary>
    /// Callback hook for failed imports.
    /// </summary>
    /// <param name="packageName">Any failed package.</param>
    /// <param name="error">Error of the import (ignored, assumed to be printed in the Console).</param>
    private static void OnImportPackageFailed( string packageName, string error )
    {
      OnImportPackageDone( packageName, "Failed" );
    }

    /// <summary>
    /// Updating status of the example being imported to either
    /// Installed (if status == "Success") or NotInstalled for
    /// any other <paramref name="status"/>.
    /// </summary>
    /// <param name="packageName">Name of the package being handled.</param>
    /// <param name="status">Status "Success", "Failed" or "Canceled".</param>
    private static void OnImportPackageDone( string packageName, string status )
    {
      var exampleMatch = Regex.Match( packageName, @"AGXUnity_(\w+)" );
      if ( exampleMatch.Success ) {
        // Data can be null here if Unity has compiled scripts from the example.
        var data = s_exampleData.Find( d => d.PackageName == exampleMatch.Groups[1].ToString());
        if ( data != null ) {
          if ( status == "Success" ) {
            data.InstalledDirectory = new DirectoryInfo( $"Assets/{packageName}" );
            data.UpdateStatus( ExampleData.State.Installed );
          }
          else
            data.UpdateStatus( ExampleData.State.NotInstalled );
        }

        DeleteTemporaryPackage( data );
      }
    }

    /// <summary>
    /// Callback when Unity has been restarted due to changes in
    /// the Player Settings (that requires restart). This method
    /// is triggering a recompile of all scripts.
    /// </summary>
    private static void PostInputSystemSettingsRestart()
    {
      // Note: This define symbol isn't used, it's only used to
      //       trigger scripts to be recompiled when AGXUnity
      //       uses ENABLE_INPUT_SYSTEM define symbol.
      if ( AGXUnityEditor.Build.DefineSymbols.Contains( "AGXUNITY_INPUT_SYSTEM" ) )
        AGXUnityEditor.Build.DefineSymbols.Remove( "AGXUNITY_INPUT_SYSTEM" );
      else
        AGXUnityEditor.Build.DefineSymbols.Add( "AGXUNITY_INPUT_SYSTEM" );
    }

    /// <summary>
    /// Callback when the Metadata.json has been downloaded to the
    /// temporary directory. This method parses the file for download
    /// links and checks the project for installed examples. Last,
    /// this method fires a request for a list of installed packages
    /// in the project.
    /// </summary>
    /// <param name="file">Downloaded file.</param>
    /// <param name="status">Request status.</param>
    private static void OnMetadata( FileInfo file, RequestHandler.Status status )
    {
      if ( status != RequestHandler.Status.Success )
        return;

      try {
        string text = File.ReadAllText(file.FullName);

        LoaderExamples? examples = JsonUtility.FromJson<LoaderExamples>(text);

        foreach ( var example in examples.Value.examples ) {
          var data = new ExampleData( example );
          s_exampleData.Add( data );
          foreach ( var dependency in data.Dependencies )
            if ( !s_dependencyData.ContainsKey( dependency ) )
              s_dependencyData.Add( dependency, DependencyState.Unknown );
        }
      }
      finally {
        file.Delete();
      }

      for ( int i = 0; i < s_exampleData.Count; ++i ) {
        var data = s_exampleData[ i ];

        var directories = Directory.GetDirectories( "Assets",
                                                    data.DirectoryName,
                                                    SearchOption.AllDirectories );
        data.InstalledDirectory = ( from dir in directories
                                    let di = new DirectoryInfo( dir )
                                    where di.EnumerateFiles( $"{data.DirectoryName}.unity",
                                                             SearchOption.TopDirectoryOnly ).FirstOrDefault() != null
                                    select di ).FirstOrDefault();
        if ( data.InstalledDirectory != null )
          data.UpdateStatus( ExampleData.State.Installed );
        else
          data.UpdateStatus( ExampleData.State.NotInstalled );

        // We don't want downloaded packages in the temp folder. E.g., if
        // the user deletes some example folder, it's not desired to automatically
        // install the package again when opening the examples window.
        DeleteTemporaryPackage( data );
      }

      s_listPackagesRequest = Client.List( true );
      EditorApplication.update += InitializeDependencies;
    }

    /// <summary>
    /// An EditorApplication.update callback waiting for the list of
    /// currently installed packages in the project. The callback is
    /// removed when the request is completed.
    /// </summary>
    private static void InitializeDependencies()
    {
      if ( s_listPackagesRequest == null ) {
        EditorApplication.update -= InitializeDependencies;
        return;
      }

      if ( s_listPackagesRequest.IsCompleted ) {
        if ( s_listPackagesRequest.Status == StatusCode.Success ) {
          foreach ( var package in s_listPackagesRequest.Result )
            s_dependencyData[ package.name ] = DependencyState.Installed;
        }
        else
          Debug.LogError( s_listPackagesRequest.Error.message );

        var keys = s_dependencyData.Keys.ToArray();
        foreach ( var depData in keys )
          if ( s_dependencyData[ depData ] == DependencyState.Unknown )
            s_dependencyData[ depData ] = DependencyState.NotInstalled;


        EditorApplication.update -= InitializeDependencies;
        s_listPackagesRequest = null;

        IsInitializing = false;
      }
    }

    /// <summary>
    /// Delete downloaded example package from the temporary directory.
    /// </summary>
    /// <param name="example">Example to delete the package for.</param>
    private static void DeleteTemporaryPackage( ExampleData data )
    {
      try {
        if ( data != null ) {
          data.DownloadedPackage?.Delete();
          data.DownloadedPackage = null;
        }
        else
          File.Delete( $"{TempDirectory.FullName}/{data.DirectoryName}.unitypackage" );
      }
      catch ( Exception ) {
      }
    }

    public static DependencyState GetDependencyState( string packageName )
    {
      return s_dependencyData.ContainsKey(packageName) ? s_dependencyData[packageName] : DependencyState.Unknown;
    }

    private static string s_metadataURL = @"https://us.download.algoryx.se/AGXUnity/examples/current/ExampleMetadata.json";
    private static ListRequest s_listPackagesRequest = null;
    private static List<AddRequest> s_addPackageRequests = new List<AddRequest>();
    private static List<ExampleData> s_exampleData = new List<ExampleData>();
    private static Dictionary<string, DependencyState> s_dependencyData = new Dictionary<string, DependencyState>();
    private static DirectoryInfo s_tempDirectory = null;
    private static SerializedObject s_playerSettings = null;
    private static readonly object s_downloadCompleteLock = new object();
  }
}
