using UnityEngine;
using UnityEditor;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class AboutWindow : EditorWindow
  {
    public static AboutWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindowWithRect<AboutWindow>( new Rect( 300, 300, 400, 360 ),
                                                   true,
                                                   "AGX Dynamics for Unity" );
      return window;
    }

    private void OnGUI()
    {
      GUILayout.BeginHorizontal( GUILayout.Width( 570 ) );
      GUILayout.Box( IconManager.GetAGXUnityLogo(),
                     GUI.Skin.customStyles[ 3 ],
                     GUILayout.Width( 400 ),
                     GUILayout.Height( 100 ) );
      GUILayout.EndHorizontal();

      EditorGUILayout.SelectableLabel( "© " + System.DateTime.Now.Year + " Algoryx Simulation AB",
                                       InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

      string agxDynamicsVersion = string.Empty;
      try {
        agxDynamicsVersion = agx.agxSWIG.agxGetVersion( false );
        if ( agxDynamicsVersion.ToLower().StartsWith( "agx-" ) )
          agxDynamicsVersion = agxDynamicsVersion.Remove( 0, 4 );
        agxDynamicsVersion = GUI.AddColorTag( agxDynamicsVersion,
                                              EditorGUIUtility.isProSkin ?
                                                Color.white :
                                                Color.black );
      }
      catch ( System.Exception ) {
      }
      EditorGUILayout.SelectableLabel( "Thank you for using AGX Dynamics for Unity!\n\nAGX Dynamics version: " +
                                       agxDynamicsVersion,
                                       GUILayout.Height( 45 ) );

      GUILayout.Space( 10 );
      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

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

      GUILayout.Space( 10 );
      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

      GUILayout.Label( "Support", EditorStyles.boldLabel );
      EditorGUILayout.SelectableLabel( "Please refer to the information received when purchasing your license for support contact information.",
                                       InspectorEditor.Skin.LabelWordWrap );
    }
  }
}
