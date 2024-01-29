using AGXUnity.Model;
using AGXUnity.Rendering;
using UnityEditor;
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
      if ( TerrainMaterialPatch.RenderLayer != null &&
          TerrainMaterialPatch.GetComponentInParent<TerrainPatchRenderer>() == null ) {
        EditorGUILayout.HelpBox( "This patch has an associated Render Layer but the parent terrain does not contain a renderer", MessageType.Warning );
        if ( GUILayout.Button( AGXUnity.Utils.GUI.MakeLabel( "Add a renderer to parent" ) ) ) {
          TerrainMaterialPatch.transform.parent.gameObject.AddComponent<TerrainPatchRenderer>();
        }
      }
    }
  }

}