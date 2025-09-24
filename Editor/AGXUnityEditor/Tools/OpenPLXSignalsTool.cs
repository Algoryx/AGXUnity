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

  private void RenderSignalList( IEnumerable<SignalEndpoint> endpoints )
  {
    var style = InspectorGUISkin.Instance.Label;
    style.alignment = TextAnchor.MiddleRight;
    foreach ( var endpoint in endpoints ) {
      GUILayout.BeginHorizontal();
      EditorGUILayout.LabelField( endpoint.Name );
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
        var intPrefix = "signal_interfaces_" + sigInt.Path + sigInt.Name;
        if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix, entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( sigInt.Path + "." + sigInt.Name, true ) ) ) {
          using var sigIntIndent = new InspectorGUI.IndentScope();
          if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix + "_inputs", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", false ) ) )
            RenderSignalList( sigInt.Inputs );
          if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, intPrefix + "_outputs", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", false ) ) )
            RenderSignalList( sigInt.Outputs );
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
