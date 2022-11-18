using AGXUnity.Model;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( MovableTerrain ) )]
  public class MovableTerrainTool : CustomTargetTool
  {
    public MovableTerrain MovableTerrain { get { return Targets[ 0 ] as MovableTerrain; } }

    public MovableTerrainTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      MovableTerrain.RemoveInvalidShovels();
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      Undo.RecordObject( MovableTerrain, "Shovel add/remove." );

      InspectorGUI.ToolListGUI( this,
                                MovableTerrain.Shovels,
                                "Shovels",
                                shovel => MovableTerrain.Add( shovel ),
                                shovel => MovableTerrain.Remove( shovel ) );
    }
  }
}
