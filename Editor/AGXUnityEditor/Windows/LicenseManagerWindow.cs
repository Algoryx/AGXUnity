using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class LicenseManagerWindow : EditorWindow
  {
    [System.Flags]
    public enum Module
    {
      None             = 0,
      AGX              = 1 << 0,
      AGXParticles     = 1 << 1,
      AGXCable         = 1 << 2,
      AGXCableDamage   = 1 << 3,
      AGXDriveTrain    = 1 << 4,
      AGXGranular      = 1 << 5,
      AGXHydraulics    = 1 << 6,
      AGXHydrodynamics = 1 << 7,
      AGXSimulink      = 1 << 8,
      AGXTerrain       = 1 << 9,
      AGXTires         = 1 << 10,
      AGXTracks        = 1 << 11,
      AGXWireLink      = 1 << 12,
      AGXWires         = 1 << 13
    }

    public static LicenseManagerWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindowWithRect<LicenseManagerWindow>( new Rect( 300, 300, 400, 250 ),
                                                            true,
                                                            "License Manager - AGX Dynamics for Unity" );
      return window;
    }

    /// <summary>
    /// License file directory with Assets as root and
    /// / as directory separator.
    /// </summary>
    public string LicenseDirectory
    {
      get
      {
        return GetLicenseDirectoryData().String;
      }
      private set
      {
        GetLicenseDirectoryData().String = value;
      }
    }

    /// <summary>
    /// License file including relative path with Assets as root
    /// and / as directory separator.
    /// </summary>
    public string LicenseFilename
    {
      get { return LicenseDirectory + "/agx.lfx"; }
    }

    private void OnEnable()
    {
      ValidateFolder();

      UpdateLicenseInformation();
    }

    private void OnDisable()
    {
      if ( m_licenseActivateTask != null )
        m_licenseActivateTask.Wait();
      m_licenseActivateTask = null;
    }

    private void OnGUI()
    {
      ValidateFolder();

      AboutWindow.AGXDynamicsForUnityLogoGUI();

      if ( !m_licenseInfo.LicenseValid ) {
        InspectorGUI.SelectFolder( GUI.MakeLabel( "License file directory" ),
                                   LicenseDirectory,
                                   "License file directory",
                                   newFolder =>
                                   {
                                     newFolder = newFolder.Replace( '\\', '/' );
                                     if ( System.IO.Path.IsPathRooted( newFolder ) )
                                       newFolder = IO.Utils.MakeRelative( newFolder,
                                                                          Application.dataPath );
                                     if ( string.IsNullOrEmpty( newFolder ) )
                                       newFolder = "Assets";

                                     // If the folder was created inside the Folder Panel.
                                     AssetDatabase.Refresh();

                                     if ( !AssetDatabase.IsValidFolder( newFolder ) ) {
                                       Debug.LogWarning( $"Invalid license folder: {newFolder}" );
                                       return;
                                     }
                                     LicenseDirectory = newFolder;
                                   } );

        InspectorGUI.Separator( 1, 6 );

        GUILayout.Label( GUI.MakeLabel( "Activate license", true ), InspectorEditor.Skin.Label );
        using ( new GUI.EnabledBlock( m_licenseActivateTask == null ) )
        using ( InspectorGUI.IndentScope.Single ) {
          m_licenseActivateData.Id = EditorGUILayout.TextField( GUI.MakeLabel( "Id" ),
                                                                m_licenseActivateData.Id,
                                                                InspectorEditor.Skin.TextField );
          if ( m_licenseActivateData.Id.Any( c => !char.IsDigit( c ) ) )
            m_licenseActivateData.Id = new string( m_licenseActivateData.Id.Where( c => char.IsDigit( c ) ).ToArray() );
          m_licenseActivateData.Password = EditorGUILayout.PasswordField( GUI.MakeLabel( "Password" ),
                                                                          m_licenseActivateData.Password );

          using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled &&
                                        m_licenseActivateData.Id.Length > 0 &&
                                        m_licenseActivateData.Password.Length > 0 ) ) {
            // It isn't possible to press this button during activation.
            if ( GUILayout.Button( GUI.MakeLabel( m_licenseActivateTask == null ?
                                                    "Activate" :
                                                    "Activating..." ),
                                                  InspectorEditor.Skin.Button ) ) {
              m_licenseActivateTask = ActiveLicense( m_licenseActivateData,
                                                     LicenseFilename );
            }
          }
        }
      }
      else {
        InspectorGUI.SelectableTextField( GUI.MakeLabel( "License file" ), m_licenseFilename );

        InspectorGUI.Separator( 1, 6 );

        InspectorGUI.LicenseEndDateField( m_licenseInfo );

        EditorGUILayout.EnumFlagsField( GUI.MakeLabel( "Enabled modules" ),
                                        m_enabledModules,
                                        false,
                                        InspectorEditor.Skin.Popup );

        InspectorGUI.SelectableTextField( GUI.MakeLabel( "User" ), m_licenseInfo.User );

        InspectorGUI.SelectableTextField( GUI.MakeLabel( "Contact" ), m_licenseInfo.Contact );
      }

      if ( m_licenseActivateTask != null )
        Repaint();
    }

    private Task<bool> ActiveLicense( IdPassword idPassword, string filename )
    {
      EditorApplication.update += OnUpdate;
      return Task.Run( () =>
      {
        try {
          return agx.Runtime.instance().activateAgxLicense( System.Convert.ToInt32( idPassword.Id ),
                                                            idPassword.Password,
                                                            filename );
        }
        catch ( System.Exception e ) {
          Debug.LogException( e );
          return false;
        }
      } );
    }

    private void OnUpdate()
    {
      if ( m_licenseActivateTask == null ) {
        EditorApplication.update -= OnUpdate;
        return;
      }
      else if ( !m_licenseActivateTask.IsCompleted )
        return;

      var activateSuccess = m_licenseActivateTask.Result;
      if ( activateSuccess ) {
        AssetDatabase.Refresh();
        if ( agx.Runtime.instance().loadLicenseFile( LicenseFilename ) )
          Debug.Log( $"{LicenseFilename.Color( Color.green ) } activated and loaded successfully." );
        else
          Debug.LogError( $"Failed to load {LicenseFilename}: {agx.Runtime.instance().getStatus().Color( Color.red )}" );
      }
      // Activate failed.
      else
        Debug.LogError( $"Failed to activate {LicenseFilename}: {agx.Runtime.instance().getStatus().Color( Color.red )}" );

      m_licenseActivateTask = null;
      UpdateLicenseInformation();
    }

    private static Module FindModules( string[] modules )
    {
      var enabledModules = Module.None;
      if ( modules == null )
        return enabledModules;

      foreach ( var module in modules ) {
        if ( module == "AgX" ) {
          enabledModules |= Module.AGX;
          continue;
        }
        else if ( !module.StartsWith( "AgX-" ) )
          continue;
        var enabledModule = module.Replace( "AgX-", "AGX" );
        if ( System.Enum.TryParse<Module>( enabledModule, out var enumModule ) )
          enabledModules |= enumModule;
      }
      return enabledModules;
    }

    private static EditorDataEntry GetLicenseDirectoryData()
    {
      return EditorData.Instance.GetStaticData( "LicenseManagerWindow_LicenseFilename",
                                                entry => entry.String = "Assets" );
    }

    private void ValidateFolder()
    {
      if ( !AssetDatabase.IsValidFolder( LicenseDirectory ) )
        LicenseDirectory = "Assets";
    }

    private void UpdateLicenseInformation()
    {
      m_licenseInfo = AGXUnity.LicenseManager.LicenseInfo.Create();
      m_licenseFilename = AGXUnity.LicenseManager.FindLicenseFile( AGXUnity.LicenseManager.LicenseType.Legacy ) ??
                          AGXUnity.LicenseManager.FindLicenseFile( AGXUnity.LicenseManager.LicenseType.Service ) ??
                          string.Empty;
      if ( !string.IsNullOrEmpty( m_licenseFilename ) ) {
        if ( System.IO.Path.IsPathRooted( m_licenseFilename ) )
          m_licenseFilename = IO.Utils.MakeRelative( m_licenseFilename, Application.dataPath );
        m_licenseFilename = m_licenseFilename.Replace( '\\', '/' );
        if ( m_licenseFilename.StartsWith( "./" ) )
          m_licenseFilename = m_licenseFilename.Remove( 0, 2 );
      }
      m_enabledModules = FindModules( m_licenseInfo.EnabledModules );
    }

    private struct IdPassword
    {
      public string Id;
      public string Password;
    }

    private AGXUnity.LicenseManager.LicenseInfo m_licenseInfo = new AGXUnity.LicenseManager.LicenseInfo();
    private string m_licenseFilename = string.Empty;
    private Module m_enabledModules = Module.AGX;
    private IdPassword m_licenseActivateData = new IdPassword() { Id = string.Empty, Password = string.Empty };
    private Task<bool> m_licenseActivateTask = null;
  }
}
