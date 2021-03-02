using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class LicenseManagerWindow : EditorWindow
  {
    public static LicenseManagerWindow Open()
    {
      return GetWindow<LicenseManagerWindow>( false,
                                              "License Manager - AGX Dynamics for Unity",
                                              true );
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
        GetLicenseDirectoryData().String = value.PrettyPath();
      }
    }

    public bool IsUpdatingLicenseInformation { get { return m_updateLicenseInfoTask != null; } }

    private void OnEnable()
    {
      ValidateLicenseDirectory();

      StartUpdateLicenseInformation();
    }

    private void OnDisable()
    {
      AGXUnity.LicenseManager.AwaitTasks();
      m_updateLicenseInfoTask?.Wait( 0 );
    }

    private void OnGUI()
    {
      ValidateLicenseDirectory();

      AboutWindow.AGXDynamicsForUnityLogoGUI();

      m_scroll = EditorGUILayout.BeginScrollView( m_scroll );

      if ( IsUpdatingLicenseInformation )
        ShowNotification( GUI.MakeLabel( "Reading..." ) );

      using ( new GUI.EnabledBlock( !IsUpdatingLicenseInformation ) ) {
        for ( int i = 0; i < m_licenseData.Count; ++i ) {
          var data = m_licenseData[ i ];
          LicenseDataGUI( data );
          if ( i + 1 < m_licenseData.Count )
            InspectorGUI.Separator( 2, 6 );
        }

        if ( m_licenseData.Count > 0 )
          InspectorGUI.BrandSeparator( 1, 6 );

        ActivateLicenseGUI();
      }

      EditorGUILayout.EndScrollView();

      if ( AGXUnity.LicenseManager.IsActivating || IsUpdatingLicenseInformation )
        Repaint();
    }

    private void ActivateLicenseGUI()
    {
      InspectorGUI.SelectFolder( GUI.MakeLabel( "License file directory" ),
                                 LicenseDirectory,
                                 "License file directory",
                                 newDirectory =>
                                 {
                                   newDirectory = newDirectory.Replace( '\\', '/' );
                                   if ( Path.IsPathRooted( newDirectory ) ) {
                                     newDirectory = IO.Utils.MakeRelative( newDirectory,
                                                                           Application.dataPath );
                                   }

                                   if ( string.IsNullOrEmpty( newDirectory ) )
                                     newDirectory = "Assets";

                                   if ( !Directory.Exists( newDirectory ) ) {
                                     Debug.LogWarning( $"Invalid license directory: {newDirectory} - directory doesn't exist." );
                                     return;
                                   }
                                   LicenseDirectory = newDirectory;
                                 } );

      InspectorGUI.Separator( 1, 6 );

      GUILayout.Label( GUI.MakeLabel( "Activate license", true ), InspectorEditor.Skin.Label );
      using ( new GUI.EnabledBlock( !AGXUnity.LicenseManager.IsActivating ) )
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
          if ( GUILayout.Button( GUI.MakeLabel( AGXUnity.LicenseManager.IsActivating ?
                                                  "Activating..." :
                                                  "Activate" ),
                                                InspectorEditor.Skin.Button ) ) {
            AGXUnity.LicenseManager.ActivateAsync( System.Convert.ToInt32( m_licenseActivateData.Id ),
                                                   m_licenseActivateData.Password,
                                                   LicenseDirectory,
                                                   success =>
                                                   {
                                                     if ( success )
                                                       m_licenseActivateData = IdPassword.Empty();
                                                     StartUpdateLicenseInformation();
                                                   } );
          }
        }
      }
    }

    private void LicenseDataGUI( LicenseData data )
    {
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "License file" ),
                                        data.Filename,
                                        MiscIcon.EntryRemove,
                                        () =>
                                        {
                                          var deactivateDelete = EditorUtility.DisplayDialog( "Deactivate and erase license.",
                                                                                              "Would you like to deactivate the current license " +
                                                                                              "and remove the license file from this project?\n\n" +
                                                                                              "It's possible to activate the license again in this " +
                                                                                              "License Manager and/or download the license file again " +
                                                                                              "from the license portal.",
                                                                                              "Yes",
                                                                                              "Cancel" );
                                          if ( deactivateDelete ) {
                                            AGXUnity.LicenseManager.DeactivateAndDelete( data.Filename );
                                            StartUpdateLicenseInformation();
                                            GUIUtility.ExitGUI();
                                          }
                                        },
                                        "Deactivate and erase license file from project." );
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "License type" ), data.LicenseInfo.Type );

      InspectorGUI.Separator( 1, 6 );

      InspectorGUI.LicenseEndDateField( data.LicenseInfo );

      EditorGUILayout.EnumFlagsField( GUI.MakeLabel( "Enabled modules" ),
                                      data.LicenseInfo.EnabledModules,
                                      false,
                                      InspectorEditor.Skin.Popup );

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "User" ), data.LicenseInfo.User );

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Contact" ), data.LicenseInfo.Contact );
    }

    private static EditorDataEntry GetLicenseDirectoryData()
    {
      return EditorData.Instance.GetStaticData( "LicenseManagerWindow_LicenseFilename",
                                                entry => entry.String = "Assets" );
    }

    private void ValidateLicenseDirectory()
    {
      if ( !Directory.Exists( LicenseDirectory ) )
        LicenseDirectory = "Assets";
    }

    private void StartUpdateLicenseInformation()
    {
      if ( IsUpdatingLicenseInformation )
        return;

      var licenseData = new List<LicenseData>();
      m_updateLicenseInfoTask = Task.Run( () =>
      {
        foreach ( var licenseFile in AGXUnity.LicenseManager.FindLicenseFiles() ) {
          AGXUnity.LicenseManager.LoadFile( licenseFile );
          licenseData.Add( new LicenseData()
          {
            Filename = licenseFile,
            LicenseInfo = AGXUnity.LicenseManager.LicenseInfo
          } );
        }

        // Default behavior - unsure how we know which license that's loaded before update.
        AGXUnity.LicenseManager.LoadFile();

        return licenseData;
      } );

      EditorApplication.update += OnUpdateLicenseInformation;
    }

    private void OnUpdateLicenseInformation()
    {
      if ( m_updateLicenseInfoTask != null && m_updateLicenseInfoTask.IsCompleted ) {
        m_licenseData = m_updateLicenseInfoTask.Result;
        m_updateLicenseInfoTask = null;
      }

      if ( m_updateLicenseInfoTask == null )
        EditorApplication.update -= OnUpdateLicenseInformation;
    }

    private struct IdPassword
    {
      public static IdPassword Empty() { return new IdPassword() { Id = string.Empty, Password = string.Empty }; }
      public string Id;
      public string Password;
    }

    private struct LicenseData
    {
      public string Filename;
      public AGXUnity.LicenseInfo LicenseInfo;
    }

    private IdPassword m_licenseActivateData = IdPassword.Empty();
    private Vector2 m_scroll = Vector2.zero;
    [System.NonSerialized]
    private List<LicenseData> m_licenseData = new List<LicenseData>();
    private Task<List<LicenseData>> m_updateLicenseInfoTask = null;
  }
}
