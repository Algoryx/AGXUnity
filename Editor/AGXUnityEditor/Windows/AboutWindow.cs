using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class AboutWindow : EditorWindow
  {
    public static AboutWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindowWithRect<AboutWindow>( new Rect( 300, 300, 400, 350 ),
                                                   true,
                                                   "AGX Dynamics for Unity" );
      return window;
    }

    public static void AGXDynamicsForUnityLogoGUI()
    {
      GUILayout.BeginHorizontal( GUILayout.Width( 570 ) );
      GUILayout.Box( IconManager.GetAGXUnityLogo(),
                     GUI.Skin.customStyles[ 3 ],
                     GUILayout.Width( 400 ),
                     GUILayout.Height( 100 ) );
      GUILayout.EndHorizontal();

      EditorGUILayout.LabelField( "© " + System.DateTime.Now.Year + " Algoryx Simulation AB",
                                  InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator( 1, 6 );
    }

    private void OnEnable()
    {
      s_agxInfo = AGXUnity.LicenseInfo.Create();
    }

    private void OnGUI()
    {
      AGXDynamicsForUnityLogoGUI();

      EditorGUILayout.LabelField( GUI.MakeLabel( "Thank you for using AGX Dynamics for Unity!", true ),
                                  InspectorEditor.Skin.LabelMiddleCenter );

      GUILayout.Space( 6 );

      var fieldColor = EditorGUIUtility.isProSkin ?
                         Color.white :
                         Color.black;
      var fieldErrorColor = Color.Lerp( Color.red,
                                        Color.black,
                                        0.25f );

      var versionInfo = PackageUpdateHandler.FindCurrentVersion();
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Version" ),
                                        ( versionInfo.IsValid ?
                                            versionInfo.VersionString :
                                            "git checkout" ).Color( fieldColor ),
                                        InspectorEditor.Skin.Label );

      string agxDynamicsVersion = s_agxInfo.Version;
      if ( string.IsNullOrEmpty( agxDynamicsVersion ) )
        agxDynamicsVersion = GUI.AddColorTag( "Unknown",
                                              fieldErrorColor );
      else
        agxDynamicsVersion = GUI.AddColorTag( agxDynamicsVersion,
                                              fieldColor );

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "AGX Dynamics version" ),
                                        agxDynamicsVersion,
                                        InspectorEditor.Skin.Label );

      InspectorGUI.LicenseEndDateField( s_agxInfo );

      InspectorGUI.BrandSeparator( 1, 8 );

      GUILayout.Label( GUI.MakeLabel( "Online Documentation", true ), InspectorEditor.Skin.Label );

      using ( new EditorGUILayout.HorizontalScope( GUILayout.Width( 200 ) ) ) {
        if ( InspectorGUI.Link( GUI.MakeLabel( "AGX Dynamics for Unity" ) ) )
          Application.OpenURL( TopMenu.AGXDynamicsForUnityManualURL );
        GUILayout.Label( " - ", InspectorEditor.Skin.Label );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Examples" ) ) )
          Application.OpenURL( TopMenu.AGXDynamicsForUnityExamplesURL );
      }

      using ( new EditorGUILayout.HorizontalScope( GUILayout.Width( 200 ) ) ) {
        if ( InspectorGUI.Link( GUI.MakeLabel( "AGX Dynamics user manual" ) ) )
          Application.OpenURL( TopMenu.AGXUserManualURL );
        GUILayout.Label( " - ", InspectorEditor.Skin.Label );
        if ( InspectorGUI.Link( GUI.MakeLabel( "AGX Dynamics API Reference" ) ) )
          Application.OpenURL( TopMenu.AGXAPIReferenceURL );
      }

      InspectorGUI.BrandSeparator( 1, 8 );

      GUILayout.Label( "Support", EditorStyles.boldLabel );
      EditorGUILayout.SelectableLabel( "Please refer to the information received when purchasing your license for support contact information.",
                                       InspectorEditor.Skin.LabelWordWrap );
    }

    private AGXUnity.LicenseInfo s_agxInfo = new AGXUnity.LicenseInfo();
  }
}
