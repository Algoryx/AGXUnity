using AGXUnity.IO.OpenPLX;
using AGXUnityEditor;
using AGXUnityEditor.Tools;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomTool( typeof( OpenPLXSignals ) )]
public class OpenPLXSignalsTool : CustomTargetTool
{
  public OpenPLXSignals OpenPLXSignals => Targets[ 0 ] as OpenPLXSignals;

  private string m_rootDecl;

  public OpenPLXSignalsTool( Object[] targets )
      : base( targets )
  {
    IsSingleInstanceTool = true;
    m_rootDecl = OpenPLXSignals.GetComponentInChildren<OpenPLXObject>().SourceDeclarations[ 0 ];
  }

  private void RenderSignalList( IEnumerable<SignalEndpoint> endpoints, string interfacePrefix = "" )
  {
    var style = new GUIStyle(InspectorGUISkin.Instance.Label);
    style.alignment = TextAnchor.MiddleRight;

    var buttonStyle = InspectorGUISkin.Instance.Button;
    buttonStyle.border = new RectOffset( 0, 0, 0, 0 );
    buttonStyle.padding = new RectOffset( 4, 4, 0, 0 );
    var buttonContent = new GUIContent(EditorGUIUtility.FindTexture( "Clipboard" ), "Copy full signal name to clipboard");

    foreach ( var endpoint in endpoints ) {
      GUILayout.BeginHorizontal();

      var name = endpoint.Name;

      if ( interfacePrefix != "" ) {
        var interfaceName = interfacePrefix + ".";
        if ( !name.StartsWith( interfaceName ) )
          interfaceName = interfaceName[ ( interfaceName.IndexOf( "." ) + 1 ).. ];

        name = name.Replace( interfaceName, "" );
      }
      else if ( name.StartsWith( m_rootDecl + "." ) )
        name = name.Substring( m_rootDecl.Length + 1 );

      EditorGUILayout.LabelField( name );

      var typeName = endpoint.Type.Name;
      if ( typeName.EndsWith( "Input" ) )
        typeName = typeName.Substring( 0, typeName.Length - "Input".Length );
      if ( typeName.EndsWith( "Output" ) )
        typeName = typeName.Substring( 0, typeName.Length - "Output".Length );
      GUILayout.Label( typeName, GUILayout.ExpandWidth( false ) );

      if ( GUILayout.Button( buttonContent, buttonStyle, GUILayout.ExpandWidth( false ) ) )
        GUIUtility.systemCopyBuffer = endpoint.Name;

      GUILayout.EndHorizontal();
    }
  }

  public override void OnPostTargetMembersGUI()
  {
    base.OnPostTargetMembersGUI();

    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "signal_interfaces", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Signal Interfaces", true ) ) ) {
      using var indent = new InspectorGUI.IndentScope();
      foreach ( var sigInt in OpenPLXSignals.Interfaces ) {
        var signalInterfaceName = sigInt.Path + "." + sigInt.Name;
        var intPrefix = "signal_interfaces_" + signalInterfaceName;
        if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix, entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( signalInterfaceName, true ) ) ) {
          using var sigIntIndent = new InspectorGUI.IndentScope();
          if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix + "_inputs", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", false ) ) )
            RenderSignalList( sigInt.Inputs, signalInterfaceName );
          if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix + "_outputs", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", false ) ) )
            RenderSignalList( sigInt.Outputs, signalInterfaceName );
        }
      }
    }

    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "advanced_foldout", entry => entry.Bool = false ), AGXUnity.Utils.GUI.MakeLabel( "Full signal list", true ) ) ) {
      GUILayout.Label( $"Root object prefix omitted for brevity: <b>{m_rootDecl}</b>", InspectorGUISkin.Instance.Label );
      using var indent = new InspectorGUI.IndentScope();
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "input_signal_foldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", false ) ) )
        RenderSignalList( OpenPLXSignals.Inputs );
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "output_signal_foldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", false ) ) )
        RenderSignalList( OpenPLXSignals.Outputs );
    }
  }
}
