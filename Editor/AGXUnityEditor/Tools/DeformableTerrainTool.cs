using UnityEngine;
using UnityEditor;
using AGXUnity.Model;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrain ) )]
  public class DeformableTerrainTool : CustomTargetTool
  {
    public DeformableTerrain DeformableTerrain { get { return Targets[ 0 ] as DeformableTerrain; } }

    public DeformableTerrainTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      DeformableTerrain.RemoveInvalidShovels();
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

      Undo.RecordObject( DeformableTerrain, "Shovel add/remove." );

      InspectorGUI.ToolListGUI( this,
                                DeformableTerrain.Shovels,
                                "Shovels",
                                shovel => DeformableTerrain.Add( shovel ),
                                shovel => DeformableTerrain.Remove( shovel ) );
    }
  }
}
