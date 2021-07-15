using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class GenerateRuntimeLicenseActivationWindow : EditorWindow
  {
    public static GenerateRuntimeLicenseActivationWindow Open()
    {
      var window = GetWindowWithRect<GenerateRuntimeLicenseActivationWindow>( new Rect( 300, 300, 500, 352 ),
                                                                              true,
                                                                              "AGX Dynamics for Unity" );
      return window;
    }

    /// <summary>
    /// Absolute path to application/build.
    /// </summary>
    public string BuildDirectory
    {
      get { return m_buildDirectory; }
      set
      {
        if ( string.IsNullOrEmpty( value ) ) {
          Debug.LogWarning( "Invalid build directory - directory is null or empty." );
          return;
        }

        if ( !Path.IsPathRooted( value ) )
          value = $"{Directory.GetCurrentDirectory()}/{value}";

        if ( !Directory.Exists( value ) ) {
          Debug.LogWarning( $"Invalid build directory - directory \"{value}\" doesn't exist." );
          return;
        }

        m_buildDirectory = value.Replace( '\\', '/' );
      }
    }

    /// <summary>
    /// Reference binary file in build.
    /// </summary>
    public string ReferenceFileInBuild
    {
      get { return m_referenceFileInBuild; }
      set
      {
        var tmp = value == null ? string.Empty : value.Replace( '\\', '/' );

        if ( tmp.StartsWith( BuildDirectory ) )
          tmp = tmp.MakeRelative( BuildDirectory, false );
        else if ( Path.IsPathRooted( tmp ) ) {
          Debug.LogWarning( $"Reference file \"{tmp}\" doesn't seems to be in the build directory \"{BuildDirectory}\". " +
                            "Make sure the build directory is correct before reference file is set." );
          return;
        }

        m_referenceFileInBuild = tmp;
      }
    }

    /// <summary>
    /// Reference binary file in build including build directory path.
    /// </summary>
    public string ReferenceFileInBuildFull => $"{BuildDirectory}/{ReferenceFileInBuild}";

    public void Initialize( string buildDirectory, string referenceFile )
    {
      BuildDirectory = buildDirectory;
      ReferenceFileInBuild = referenceFile;
    }

    private void OnGUI()
    {
      using ( GUI.AlignBlock.Center )
        GUILayout.Box( IconManager.GetAGXUnityLogo(),
                       GUI.Skin.customStyles[ 3 ],
                       GUILayout.Width( 400 ),
                       GUILayout.Height( 100 ) );

      EditorGUILayout.LabelField( "© " + System.DateTime.Now.Year + " Algoryx Simulation AB",
                                  InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator( 1, 6 );

      var agxLfxColor = Color.Lerp( Color.green, Color.black, 0.35f );
      var encryptedFilename = $"agx{AGXUnity.LicenseManager.GetRuntimeActivationExtension()}".Color( agxLfxColor );
      var serviceFilename = $"agx{AGXUnity.LicenseManager.GetLicenseExtension( AGXUnity.LicenseInfo.LicenseType.Service )}".Color( agxLfxColor );
      InspectorGUI.ToolDescription( "Generate an AGX Dynamics for Unity runtime activation file containing " +
                                    "encrypted License Id and Activation Code bound to the application. The " +
                                    "generated runtime activation file (" + encryptedFilename + ") will be replaced " +
                                    "with a hardware locked " + serviceFilename + " if the activation is successful.\n\n" +
                                    "<b>Internet access is required during the activation on the target hardware.</b>" );

      InspectorGUI.Separator( 1, 6 );

      InspectorGUI.SelectFolder( GUI.MakeLabel( "Build directory" ),
                                 BuildDirectory,
                                 "Build directory",
                                 newBuildDirectory =>
                                 {
                                   if ( !Directory.Exists( newBuildDirectory ) ) {
                                     Debug.LogWarning( $"Ignoring given build path {newBuildDirectory} doesn't exist." );
                                     return;
                                   }
                                   BuildDirectory = newBuildDirectory;
                                   // Reset reference file if it doesn't exist in the new build directory hierarchy.
                                   if ( !File.Exists( ReferenceFileInBuildFull ) )
                                     m_referenceFileInBuild = string.Empty;
                                 } );
      InspectorGUI.SelectFile( GUI.MakeLabel( "Reference file" ),
                               ReferenceFileInBuild,
                               "Select reference file in build",
                               BuildDirectory,
                               newFilename =>
                               {
                                 ReferenceFileInBuild = newFilename;
                               } );

      m_idPassword.Id = EditorGUILayout.TextField( GUI.MakeLabel( "Runtime License Id" ),
                                                   m_idPassword.Id,
                                                   InspectorEditor.Skin.TextField );
      if ( m_idPassword.Id.Any( c => !char.IsDigit( c ) ) )
        m_idPassword.Id = new string( m_idPassword.Id.Where( c => char.IsDigit( c ) ).ToArray() );
      m_idPassword.Password = EditorGUILayout.PasswordField( GUI.MakeLabel( "Runtime Activation Code" ),
                                                             m_idPassword.Password );

      var generateToolTip = string.Empty;
      using ( new GUI.EnabledBlock( ValidateGenerate( ref generateToolTip ) ) ) {
        GUILayout.Space( 3 );
        if ( GUILayout.Button( GUI.MakeLabel( "Generate", false, generateToolTip ) ) ) {
          var generatedFilename = string.Empty;
          if ( AGXUnity.LicenseManager.GenerateEncryptedRuntime( System.Convert.ToInt32( m_idPassword.Id ),
                                                                 m_idPassword.Password,
                                                                 BuildDirectory,
                                                                 ReferenceFileInBuild,
                                                                 filename => generatedFilename = filename ) ) {
            EditorUtility.DisplayDialog( "Encrypted Runtime License",
                                         $"Encrypted runtime successfully written to: {generatedFilename}",
                                         "Ok" );
            Close();
          }
        }
      }
    }

    private bool ValidateGenerate( ref string toolTip )
    {
      var idGiven = !string.IsNullOrEmpty( m_idPassword.Id ) && m_idPassword.Id.Length > 1;
      var passwordGiven = !string.IsNullOrEmpty( m_idPassword.Password ) && m_idPassword.Password.Length > 1;
      var directoryValid = Directory.Exists( BuildDirectory );
      var fileExist = File.Exists( ReferenceFileInBuildFull );
      if ( !idGiven )
        toolTip += "Missing license id.\n";
      if ( !passwordGiven )
        toolTip += "Missing license password.\n";
      if ( !directoryValid )
        toolTip += "Missing build directory.\n";
      if ( !fileExist )
        toolTip += "Missing reference file.\n";

      if ( toolTip.Length > 0 && toolTip.EndsWith( "\n" ) )
        toolTip = toolTip.Remove( toolTip.Length - 1 );
      else if ( string.IsNullOrEmpty( toolTip ) )
        toolTip = "Generate encrypted runtime license.";

      return idGiven &&
             passwordGiven &&
             directoryValid &&
             fileExist;
    }

    private LicenseManagerWindow.IdPassword m_idPassword = LicenseManagerWindow.IdPassword.Empty();
    private string m_buildDirectory = string.Empty;
    private string m_referenceFileInBuild = string.Empty;
  }
}