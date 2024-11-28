using AGXUnity.IO.OpenPLX;
using AGXUnityEditor;
using AGXUnityEditor.Tools;
using System.Collections.Generic;
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
    foreach ( var endpoint in endpoints ) {
      GUILayout.BeginHorizontal();
      GUILayout.Label( endpoint.Name );
      GUILayout.FlexibleSpace();
      GUILayout.Label( endpoint.Type.Name );
      GUILayout.EndHorizontal();
    }
  }

  public override void OnPostTargetMembersGUI()
  {
    base.OnPostTargetMembersGUI();
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "input_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", true ) ) )
      RenderSignalList( OpenPLXSignals.Inputs );
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( OpenPLXSignals, "output_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", true ) ) )
      RenderSignalList( OpenPLXSignals.Outputs );
  }
}
