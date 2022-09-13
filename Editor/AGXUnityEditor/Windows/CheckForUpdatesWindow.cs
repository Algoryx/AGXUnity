using System.IO;
using UnityEngine;
using UnityEditor;

using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class CheckForUpdatesWindow : EditorWindow
  {
    public static CheckForUpdatesWindow Open()
    {
      var window = GetWindowWithRect<CheckForUpdatesWindow>( new Rect( 300,
                                                                       300,
                                                                       400,
                                                                       360 ),
                                                            true,
                                                            "AGX Dynamics for Unity update" );
      return window;
    }

    private string Target
    {
      get
      {
        return string.IsNullOrEmpty( m_sourceFilename ) ?
                 string.Empty :
                 m_downloadDirectory + Path.DirectorySeparatorChar + m_sourceFilename;
      }
      set
      {
        var fi = new FileInfo( value );
        m_downloadDirectory = fi.Directory.FullName;
        m_sourceFilename = fi.Name;
      }
    }

    private string ServerVersionRequest
    {
      get
      {
        return $"https://us.download.algoryx.se/AGXUnity/latest.php?platform={m_currentVersion.Platform}";
      }
    }

    private string ServerDownloadRequest
    {
      get
      {
        return $"https://us.download.algoryx.se/AGXUnity/packages/{m_currentVersion.Platform}/{m_sourceFilename}";
      }
    }

    private void OnEnable()
    {
      m_status            = Status.Passive;
      m_serverVersion     = VersionInfo.Invalid;
      m_currentVersion    = PackageUpdateHandler.FindCurrentVersion();
      m_sourceFilename    = string.Empty;
      m_downloadDirectory = Path.GetTempPath();
      m_downloadProgress  = 0.0f;
    }

    private void OnDisable()
    {
      Web.RequestHandler.Abort( OnPackageNameRequest );
      Web.RequestHandler.Abort( OnDownloadComplete );
    }

    private void OnGUI()
    {
      AboutWindow.AGXDynamicsForUnityLogoGUI();

      EditorGUILayout.LabelField( GUI.MakeLabel( "Current version" ),
                                  GUI.MakeLabel( m_currentVersion.IsValid ?
                                                   m_currentVersion.VersionStringShort :
                                                   "git checkout",
                                                 Color.white ) );
      using ( new EditorGUILayout.HorizontalScope() ) {
        var newVersionAvailable = m_serverVersion.IsValid && m_serverVersion > m_currentVersion;
        EditorGUILayout.LabelField( GUI.MakeLabel( "Latest version" ),
                                    GUI.MakeLabel( m_serverVersion.IsValid ?
                                                     m_serverVersion.VersionStringShort :
                                                     "...",
                                                   newVersionAvailable ?
                                                     Color.Lerp( Color.green, Color.black, 0.35f ) :
                                                     Color.white ),
                                    InspectorEditor.Skin.Label,
                                    GUILayout.Width( 280 ) );
        GUILayout.FlexibleSpace();
        if ( newVersionAvailable && InspectorGUI.Link( GUI.MakeLabel( "Changelog" ) ) )
          Application.OpenURL( TopMenu.AGXUnityChangelogURL );
        else if ( !newVersionAvailable )
          GUILayout.Label( "", InspectorEditor.Skin.Label );
      }

      InspectorGUI.Separator( 1, 4 );

      var isUpToDate = m_currentVersion.IsValid &&
                       m_serverVersion.IsValid &&
                       m_currentVersion >= m_serverVersion;
      if ( isUpToDate ) {
        EditorGUILayout.LabelField( GUI.MakeLabel( "The version of AGX Dynamics for Unity is up to date.",
                                                   Color.Lerp( Color.green, Color.black, 0.35f ) ),
                                    InspectorEditor.Skin.TextAreaMiddleCenter );
      }
      else if ( m_status == Status.Passive ) {
        if ( Web.RequestHandler.Get( ServerVersionRequest,
                                     OnPackageNameRequest ) )
          m_status = Status.CheckingForUpdate;
      }
      else if ( m_status == Status.CheckingForUpdate ) {
        Repaint();
      }
      else {
        if ( m_serverVersion.IsValid ) {
          HandleDownloadInstall();
          if ( m_status == Status.Downloading )
            Repaint();
        }
      }

      var manualPackageButtonSize = new Vector2( 110, EditorGUIUtility.singleLineHeight );
      var manualPackageRect = new Rect( maxSize - manualPackageButtonSize - new Vector2( 2.0f * EditorGUIUtility.standardVerticalSpacing,
                                                                                         2.0f * EditorGUIUtility.standardVerticalSpacing ),
                                        manualPackageButtonSize );
      var manualSelectPressed = false;
      using ( new GUI.EnabledBlock( m_status != Status.Installing && m_status != Status.Downloading ) )
        manualSelectPressed = UnityEngine.GUI.Button( manualPackageRect,
                                                      GUI.MakeLabel( "Manual select..." ),
                                                      InspectorEditor.Skin.Button );
      if ( manualSelectPressed ) {
        if ( !Directory.Exists( GetManualPackageDirectoryData().String ) )
          GetManualPackageDirectoryData().String = "Assets";
        var manualPackageFilename = EditorUtility.OpenFilePanelWithFilters( "AGX Dynamics for Unity package",
                                                                             GetManualPackageDirectoryData().String,
                                                                             new string[]
                                                                             {
                                                                               "AGXUnity package",
                                                                               "*.*.*unitypackage"
                                                                             } );
        if ( !string.IsNullOrEmpty( manualPackageFilename ) ) {
          var manualTargetFileInfo = new FileInfo( manualPackageFilename );
          GetManualPackageDirectoryData().String = manualTargetFileInfo.Directory.FullName;

          if ( !manualTargetFileInfo.Exists )
            Debug.LogWarning( $"The target package \"{manualTargetFileInfo.FullName}\" doesn't exist. Aborting." );
          else if ( !VersionInfo.Parse( manualTargetFileInfo.Name ).IsValid )
            Debug.LogWarning( $"Unable to parse version from package name \"{manualTargetFileInfo.Name}\". Aborting." );
          else if ( !manualTargetFileInfo.Name.StartsWith( "AGXDynamicsForUnity-" ) )
            Debug.LogWarning( $"Package name \"{manualTargetFileInfo.Name}\" doesn't seems to be an AGX Dynamics for Unity package. Aborting." );
          else if ( EditorUtility.DisplayDialog( "AGX Dynamics for Unity update",
                                                 "AGX Dynamics for Unity is about to be updated/downgraded " +
                                                 "to version " +
                                                 VersionInfo.Parse( manualTargetFileInfo.Name ).VersionString +
                                                 ".\n\nDo you want to continue with the update/downgrade?",
                                                 "Continue",
                                                 "Cancel" ) ) {
            Target = manualTargetFileInfo.FullName;
            m_status = Status.AwaitInstall;
            InstallTarget();
          }
        }
      }
    }

    private void HandleDownloadInstall()
    {
      var skin = InspectorEditor.Skin;
      var downloadOrInstallPressed = false;

      var rect     = EditorGUILayout.GetControlRect();
      var orgWidth = rect.width;
      rect.width   = 74;

      var buttonText = m_status == Status.AwaitInstall ? "Install" : "Download";
      using ( new GUI.EnabledBlock( m_status == Status.AwaitDownload ||
                                    ( m_currentVersion.IsValid && m_status == Status.AwaitInstall ) ) )
        downloadOrInstallPressed = UnityEngine.GUI.Button( rect,
                                                           GUI.MakeLabel( buttonText ),
                                                           skin.Button );
      rect.x += rect.width + EditorGUIUtility.standardVerticalSpacing;
      rect.width = orgWidth - rect.x;
      using ( new GUI.EnabledBlock( m_status == Status.Downloading ) )
        EditorGUI.ProgressBar( rect,
                               m_downloadProgress,
                               m_serverVersion.VersionStringShort +
                                 ( m_status == Status.Downloading ? $": { (int)(100.0f * m_downloadProgress + 0.5f) }%" : "" ) );

      if ( m_status == Status.AwaitInstall ) {
        InspectorGUI.Separator( 1, 4 );

        EditorGUILayout.LabelField( GUI.MakeLabel( $"AGXDynamicsForUnity-{m_serverVersion.VersionStringShort} is ready to be installed!\n\n" +
                                                   GUI.AddColorTag( "During the installation Unity will be restarted.",
                                                                    Color.Lerp( Color.red, Color.white, 0.25f ) ) ),
                                    skin.TextAreaMiddleCenter );
      }

      if ( downloadOrInstallPressed ) {
        if ( m_status == Status.AwaitDownload ) {
          if ( File.Exists( Target ) )
            File.Delete( Target );

          Web.RequestHandler.Get( ServerDownloadRequest,
                                  new FileInfo( Target ).Directory,
                                  OnDownloadComplete,
                                  OnDownloadProgress );
          m_status = Status.Downloading;
        }
        else if ( m_status == Status.AwaitInstall )
          InstallTarget();
      }
    }

    private void OnPackageNameRequest( string packageName, Web.RequestHandler.Status status )
    {
      if ( status == Web.RequestHandler.Status.Error )
        m_serverVersion = VersionInfo.Invalid;
      else {
        m_serverVersion = VersionInfo.Parse( packageName );
        m_sourceFilename = packageName;
      }

      m_status = Status.AwaitDownload;
    }

    private void OnDownloadComplete( FileInfo file,
                                     Web.RequestHandler.Status status )
    {
      if ( status == Web.RequestHandler.Status.Error ) {
        m_status = Status.AwaitDownload;
        return;
      }

      m_status = Status.AwaitInstall;
    }

    private void OnDownloadProgress( float progress )
    {
      m_downloadProgress = progress;
    }

    private void InstallTarget()
    {
      // Double/triple verifying so that we don't install to a git
      // checkout of AGXUnity.
      if ( !m_currentVersion.IsValid ) {
        Debug.LogWarning( "It's to possible to update AGXUnity to git checkout." );
        return;
      }

      m_status = Status.Installing;

      PackageUpdateHandler.Install( new FileInfo( Target ) );
    }

    private static EditorDataEntry GetManualPackageDirectoryData()
    {
      return EditorData.Instance.GetStaticData( "CheckForUpdatesWindow.ManualPackageDirectory",
                                                entry => entry.String = "Assets" );
    }

    private enum Status
    {
      Passive,
      CheckingForUpdate,
      AwaitDownload,
      Downloading,
      AwaitInstall,
      Installing
    }

    private Status m_status = Status.Passive;
    private VersionInfo m_currentVersion = VersionInfo.Invalid;
    private VersionInfo m_serverVersion = VersionInfo.Invalid;
    private string m_downloadDirectory = string.Empty;
    private string m_sourceFilename = string.Empty;
    private float m_downloadProgress = 0.0f;
  }
}
