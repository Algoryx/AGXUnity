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
    }

    private void OnEnable()
    {
      var packageJsonFile = IO.Utils.AGXUnityPackageDirectory +
                            Path.DirectorySeparatorChar +
                            "package.json";
      if ( File.Exists( packageJsonFile ) ) {
        var data = new PackageData();
        EditorJsonUtility.FromJsonOverwrite( File.ReadAllText( packageJsonFile ),
                                             data );
        m_currentVersion = VersionInfo.Parse( data.version );
      }
      else
        m_currentVersion = VersionInfo.Invalid;

      m_downloadDirectory = Path.GetTempPath();
    }

    private void OnDisable()
    {
      Web.HttpRequestHandler.Abort( OnPackageNameRequest );
      Web.DownloadHandler.Abort( OnDownloadComplete );
    }

    private void OnGUI()
    {
      GUILayout.BeginHorizontal( GUILayout.Width( 570 ) );
      GUILayout.Box( IconManager.GetAGXUnityLogo(),
                     GUI.Skin.customStyles[ 3 ],
                     GUILayout.Width( 400 ),
                     GUILayout.Height( 100 ) );
      GUILayout.EndHorizontal();

      InspectorGUI.Separator( 1, 4 );

      EditorGUILayout.LabelField( GUI.MakeLabel( "Current version" ),
                                  GUI.MakeLabel( m_currentVersion.IsValid ?
                                                   m_currentVersion.VersionStringShort :
                                                   "git checkout",
                                                 Color.white ) );
      EditorGUILayout.LabelField( GUI.MakeLabel( "Latest version" ),
                                  GUI.MakeLabel( m_serverVersion.IsValid ?
                                                   m_serverVersion.VersionStringShort :
                                                   "...",
                                                 m_serverVersion.IsValid && m_serverVersion > m_currentVersion ?
                                                   Color.Lerp( Color.green, Color.black, 0.35f ) :
                                                   Color.white ),
                                  InspectorEditor.Skin.Label );

      InspectorGUI.Separator( 1, 4 );

      var isUpToDate = m_currentVersion.IsValid &&
                       m_serverVersion.IsValid &&
                       m_currentVersion >= m_serverVersion;
      if ( isUpToDate ) {
        EditorGUILayout.LabelField( GUI.MakeLabel( "The version of AGX Dynamics for Unity is up to date.",
                                                   Color.Lerp( Color.green, Color.black, 0.35f ) ),
                                    InspectorEditor.Skin.TextAreaMiddleCenter );
        return;
      }

      if ( m_status == Status.Passive ) {
        if ( Web.HttpRequestHandler.Create( @"https://us.download.algoryx.se/AGXUnity/latest.php",
                                            OnPackageNameRequest ) )
          m_status = Status.CheckingForUpdate;
      }
      else if ( m_status == Status.CheckingForUpdate ) {
        Repaint();
      }
      else {
        if ( /*m_currentVersion.IsValid &&*/ m_serverVersion.IsValid ) {
          HandleDownloadInstall();
          if ( m_status == Status.Downloading )
            Repaint();
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

          Web.DownloadHandler.Create( $"https://us.download.algoryx.se/AGXUnity/packages/{m_sourceFilename}",
                                      Target,
                                      OnDownloadComplete,
                                      OnDownloadProgress );
          m_status = Status.Downloading;
        }
        else if ( m_status == Status.AwaitInstall ) {
          // Double/triple verifying so that we don't install to a git
          // checkout of AGXUnity.
          if ( !m_currentVersion.IsValid ) {
            Debug.LogWarning( "It's to possible to update AGXUnity to git checkout." );
            return;
          }

          m_status = Status.Installing;

          PackageUpdateHandler.Install( new FileInfo( Target ) );
        }
      }
    }

    private void OnPackageNameRequest( string packageName, Web.HttpRequestHandler.RequestStatus status )
    {
      if ( status == Web.HttpRequestHandler.RequestStatus.Error )
        m_serverVersion = VersionInfo.Invalid;
      else {
        m_serverVersion = VersionInfo.Parse( packageName );
        m_sourceFilename = packageName;
      }

      m_status = Status.AwaitDownload;
    }

    private void OnDownloadComplete( object sender,
                                     System.ComponentModel.AsyncCompletedEventArgs e )
    {
      if ( sender != null && e.Error != null ) {
        Debug.LogException( e.Error );
        m_status = Status.AwaitDownload;
        return;
      }

      m_status = Status.AwaitInstall;
    }

    private void OnDownloadProgress( object sender,
                                     System.ComponentModel.ProgressChangedEventArgs e )
    {
      m_downloadProgress = (float)e.ProgressPercentage / 100.0f;
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

    private class PackageData
    {
      public string version = string.Empty;
    }

    private Status m_status = Status.Passive;
    private VersionInfo m_currentVersion = VersionInfo.Invalid;
    private VersionInfo m_serverVersion = VersionInfo.Invalid;
    private string m_downloadDirectory = string.Empty;
    private string m_sourceFilename = string.Empty;
    private float m_downloadProgress = 0.0f;
  }
}
