using System.Linq;
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
      var window = GetWindowWithRect<AboutWindow>( new Rect( 300, 300, 400, 330 ),
                                                   true,
                                                   "AGX Dynamics for Unity" );
      return window;
    }

    private struct AGXDynamicsInfo
    {
      public string Version;
      public System.DateTime EndDate;
      public string User;
      public string Contact;
      public string[] EnabledModules;

      public bool LicenseValid;
      public string LicenseStatus;

      public bool ValidEndDate { get { return !System.DateTime.Equals( EndDate, System.DateTime.MinValue ); } }
      public bool LicenseExpired { get { return !ValidEndDate || EndDate < System.DateTime.Now; } }

      public string DiffString
      {
        get
        {
          var diff = EndDate - System.DateTime.Now;
          var str = diff.Days != 0 ?
                      $"{System.Math.Abs( diff.Days )} day" + ( System.Math.Abs( diff.Days ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          str    += diff.Days == 0 && diff.Hours != 0 ?
                      $"{System.Math.Abs( diff.Hours )} hour" + ( System.Math.Abs( diff.Hours ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          str    += string.IsNullOrEmpty( str ) ?
                      $"{System.Math.Abs( diff.Minutes )} minute" + ( System.Math.Abs( diff.Minutes ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          return str;
        }
      }

      public bool IsLicenseAboutToBeExpired( int days )
      {
        var diff = EndDate - System.DateTime.Now;
        return System.Convert.ToInt32( diff.TotalDays + 0.5 ) < days;
      }
    }

    private void OnEnable()
    {
      try {
        s_agxInfo = new AGXDynamicsInfo();

        s_agxInfo.Version = agx.agxSWIG.agxGetVersion( false );
        if ( s_agxInfo.Version.ToLower().StartsWith( "agx-" ) )
          s_agxInfo.Version = s_agxInfo.Version.Remove( 0, 4 );

        try {
          s_agxInfo.EndDate = System.DateTime.Parse( agx.Runtime.instance().readValue( "EndDate" ) );
        }
        catch ( System.FormatException ) {
          s_agxInfo.EndDate = System.DateTime.MinValue;
        }

        s_agxInfo.LicenseValid  = agx.Runtime.instance().isValid();
        s_agxInfo.LicenseStatus = agx.Runtime.instance().getStatus();

        s_agxInfo.User    = agx.Runtime.instance().readValue( "User" );
        s_agxInfo.Contact = agx.Runtime.instance().readValue( "Contact" );

        s_agxInfo.EnabledModules = agx.Runtime.instance().getEnabledModules().ToArray();
      }
      catch ( System.Exception ) {
        s_agxInfo = new AGXDynamicsInfo();
      }
    }

    private void OnGUI()
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

      EditorGUILayout.LabelField( GUI.MakeLabel( "Thank you for using AGX Dynamics for Unity!", true ),
                                  InspectorEditor.Skin.LabelMiddleCenter );

      GUILayout.Space( 6 );

      var fieldColor = EditorGUIUtility.isProSkin ?
                         Color.white :
                         Color.black;
      var fieldErrorColor = Color.Lerp( Color.red,
                                        Color.black,
                                        0.25f );
      var fieldOkColor = Color.Lerp( Color.green,
                                     Color.black,
                                     0.35f );
      var fieldWarningColor = Color.Lerp( Color.yellow,
                                          Color.black,
                                          0.45f );

      string agxDynamicsVersion = s_agxInfo.Version;
      if ( string.IsNullOrEmpty( agxDynamicsVersion ) )
        agxDynamicsVersion = GUI.AddColorTag( "Unknown",
                                              fieldErrorColor );
      else
        agxDynamicsVersion = GUI.AddColorTag( agxDynamicsVersion,
                                              fieldColor );
      EditorGUILayout.LabelField( GUI.MakeLabel( "AGX Dynamics version" ),
                                  GUI.MakeLabel( agxDynamicsVersion ),
                                  InspectorEditor.Skin.Label );

      EditorGUILayout.LabelField( GUI.MakeLabel( s_agxInfo.LicenseExpired ?
                                                   "License expired" :
                                                   "License expires" ),
                                  s_agxInfo.ValidEndDate ?
                                    GUI.MakeLabel( s_agxInfo.EndDate.ToString( "yyyy-MM-dd" ) +
                                                   GUI.AddColorTag( $" ({s_agxInfo.DiffString} {(s_agxInfo.LicenseExpired ? "ago" : "remaining")})",
                                                                    s_agxInfo.LicenseExpired ?
                                                                      fieldErrorColor :
                                                                      s_agxInfo.IsLicenseAboutToBeExpired( 10 ) ?
                                                                        fieldWarningColor :
                                                                        fieldOkColor ),
                                                   fieldColor ) :
                                    GUI.MakeLabel( "License not found", fieldErrorColor ),
                                  InspectorEditor.Skin.Label );

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

    private AGXDynamicsInfo s_agxInfo = new AGXDynamicsInfo();
  }
}
