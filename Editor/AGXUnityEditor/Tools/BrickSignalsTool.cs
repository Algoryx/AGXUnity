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

  private void RenderSignalList( IEnumerable<string> signals )
  {
    foreach ( var signal in signals ) {
      GUILayout.BeginHorizontal();
      GUILayout.Label( signal );
      GUILayout.FlexibleSpace();
      GUILayout.Label( BrickSignals.GetMetadata( signal ).Value.type.Name );
      GUILayout.EndHorizontal();
    }
  }

  public override void OnPostTargetMembersGUI()
  {
    base.OnPostTargetMembersGUI();
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( BrickSignals, "input_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Inputs", true ) ) )
      RenderSignalList(BrickSignals.Signals.Where( s => BrickSignals.GetMetadata( s ).Value.input ) ) ;
    if ( InspectorGUI.Foldout( EditorData.Instance.GetData( BrickSignals, "output_signal_fouldout", entry => entry.Bool = true ), AGXUnity.Utils.GUI.MakeLabel( "Outputs", true ) ) )
      RenderSignalList( BrickSignals.Signals.Where( s => !BrickSignals.GetMetadata( s ).Value.input ) );
  }
}
