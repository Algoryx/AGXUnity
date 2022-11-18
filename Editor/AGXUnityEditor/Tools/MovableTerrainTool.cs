using UnityEngine;
using UnityEditor;
using AGXUnity.Model;

using GUI = AGXUnity.Utils.GUI;

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
      //var patchTerrainData = GUILayout.Button( GUI.MakeLabel( "Patch terrain data" ), InspectorEditor.Skin.Button );
      //if ( patchTerrainData ) {
      //  foreach ( var terrain in GetTargets<DeformableTerrain>() )
      //    terrain.PatchTerrainData();
      //}

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
