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
    var showDisabled = EditorData.Instance.GetData( OpenPLXSignals, "show_disabled_endpoints");
    foreach ( var endpoint in endpoints ) {
      if ( !showDisabled.Bool && !endpoint.Enabled )
        continue;
      GUILayout.BeginHorizontal();
      GUILayout.Label( endpoint.Name );
      GUILayout.FlexibleSpace();
      GUILayout.Label( endpoint.Type.Name );
      GUILayout.EndHorizontal();
      if ( endpoint is OutputSource output && output.HasSendSignal ) {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        var type = OpenPLXSignals.GetOpenPLXTypeEnum( endpoint.ValueTypeCode );
        switch ( type ) {
          case OpenPLXSignals.ValueType.Integer: GUILayout.Label( $"{output.GetValue<int>()}" ); break;
          case OpenPLXSignals.ValueType.Real: GUILayout.Label( $"{output.GetValue<double>()}" ); break;
          default: break;
        }
        GUILayout.EndHorizontal();
      }
    }
  }

  public override void OnPostTargetMembersGUI()
  {
    base.OnPostTargetMembersGUI();

    var showDisabled = EditorData.Instance.GetData( OpenPLXSignals, "show_disabled_endpoints", entry => entry.Bool = false );
    showDisabled.Bool = EditorGUILayout.Toggle( "Show Disabled Signals", showDisabled.Bool );

    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "input_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", true ) ) )
      RenderSignalList( OpenPLXSignals.Inputs );
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "output_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", true ) ) )
      RenderSignalList( OpenPLXSignals.Outputs );
  }
}
