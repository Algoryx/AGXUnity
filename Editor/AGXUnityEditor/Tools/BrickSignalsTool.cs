using AGXUnity.IO.BrickIO;
using AGXUnityEditor;
using AGXUnityEditor.Tools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CustomTool( typeof( BrickSignals ) )]
public class BrickSignalsTool : CustomTargetTool
{
  public BrickSignals BrickSignals => Targets[ 0 ] as BrickSignals;

  public BrickSignalsTool( Object[] targets )
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
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( BrickSignals, "input_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", true ) ) )
      RenderSignalList( BrickSignals.Inputs );
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( BrickSignals, "output_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", true ) ) )
      RenderSignalList( BrickSignals.Outputs );
  }
}
