using AGXUnity.Model;
using UnityEngine;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( AGXUnity.Model.TerrainMaterialPatch ) )]
  public class TerrainMaterialPatchTool : CustomTargetTool
  {
    public TerrainMaterialPatch TerrainMaterialPatch { get { return Targets[ 0 ] as TerrainMaterialPatch; } }

    public TerrainMaterialPatchTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      InspectorGUI.ToolArrayGUI( this, TerrainMaterialPatch.Shapes, "Shapes" );
    }
  }

}