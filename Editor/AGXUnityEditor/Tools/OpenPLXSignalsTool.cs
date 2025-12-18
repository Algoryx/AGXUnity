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

  public OpenPLXSignalsTool( Object[] targets )
      : base( targets )
  {
    IsSingleInstanceTool = true;
  }

  private void RenderSignalList( IEnumerable<SignalEndpoint> endpoints, string interfacePrefix = "" )
  {
    var style = new GUIStyle(InspectorGUISkin.Instance.Label);
    style.alignment = TextAnchor.MiddleRight;
    foreach ( var endpoint in endpoints ) {
      GUILayout.BeginHorizontal();
      var name = endpoint.Name;
      if ( interfacePrefix != "" ) {
        var interfaceName = interfacePrefix + ".";
        if ( !name.StartsWith( interfaceName ) )
          interfaceName = interfaceName[ ( interfaceName.IndexOf( "." ) + 1 ).. ];

        name = name.Replace( interfaceName, "" );
      }
      EditorGUILayout.LabelField( name );
      GUILayout.Label( endpoint.Type.Name, style, GUILayout.ExpandWidth( false ) );
      GUILayout.EndHorizontal();
      if ( endpoint is OutputSource output && output.HasSentSignal ) {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        var type = OpenPLXSignals.GetOpenPLXTypeEnum( endpoint.ValueTypeCode );
        switch ( type ) {
          case OpenPLXSignals.ValueType.Integer: GUILayout.Label( $"{output.GetValue<int>()}" ); break;
          case OpenPLXSignals.ValueType.Real: GUILayout.Label( $"{output.GetValue<double>()}" ); break;
          case OpenPLXSignals.ValueType.Vec3: GUILayout.Label( $"{output.GetValue<Vector3>()}" ); break;
          case OpenPLXSignals.ValueType.Vec2: GUILayout.Label( $"{output.GetValue<Vector2>()}" ); break;
          case OpenPLXSignals.ValueType.Boolean: GUILayout.Label( $"{output.GetValue<bool>()}" ); break;
          default: break;
        }
        GUILayout.EndHorizontal();
      }
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

    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "advanced_foldout", entry => entry.Bool = false ), AGXUnity.Utils.GUI.MakeLabel( "Advanced", true ) ) ) {
      using var indent = new InspectorGUI.IndentScope();
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "input_signal_foldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", false ) ) )
        RenderSignalList( OpenPLXSignals.Inputs );
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "output_signal_foldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", false ) ) )
        RenderSignalList( OpenPLXSignals.Outputs );
    }
  }
}
