using UnityEngine;
using UnityEditor;
using AGXUnity.Model;

using GUI = AGXUnityEditor.Utils.GUI;

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
      if ( NumTargets > 1 )
        return;

      Undo.RecordObject( DeformableTerrain, "Shovel add/remove." );

      GUI.Separator();

      InspectorGUI.ToolArrayGUI( this,
                                 DeformableTerrain.Shovels,
                                 "Shovels",
                                 Color.Lerp( Color.red, Color.black, 0.25f ),
                                 shovel => DeformableTerrain.Add( shovel ),
                                 shovel => DeformableTerrain.Remove( shovel ) );
    }
  }
}
