using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using System.Linq;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainBase ) )]
  public class DeformableTerrainBaseTool : CustomTargetTool
  {
    public DeformableTerrainBase DeformableTerrainBase { get { return Targets[ 0 ] as DeformableTerrainBase; } }

    public DeformableTerrainBaseTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      DeformableTerrainBase.RemoveInvalidShovels( false );
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      RenderMaterialHandles();

      Undo.RecordObject( DeformableTerrainBase, "Shovel add/remove." );

      InspectorGUI.ToolListGUI( this,
                                DeformableTerrainBase.Shovels,
                                "Shovels",
                                shovel => DeformableTerrainBase.Add( shovel ),
                                shovel => DeformableTerrainBase.Remove( shovel ) );

      if ( DeformableTerrainBase.Shovels.Any( shovel => !shovel.isActiveAndEnabled ) ) {
        EditorGUILayout.HelpBox( "Terrain contains disabled shovels. This is not supported and they will be removed on play. Disabled shovels must be added manually to the terrain when enabled", MessageType.Warning );
        if ( GUILayout.Button( "Remove disabled shovels" ) )
          DeformableTerrainBase.RemoveInvalidShovels( true, false );
      }
    }

    protected void RenderMaterialHandles()
    {
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainBaseMaterialHandles" ), GUI.MakeLabel( "Internal Material Handles" ) ) ) {
        EditorGUI.indentLevel = 1;

        InspectorGUI.WarningLabel( "The parameters of these materials will be overrwritten depending on the TerrainMaterial set. " +
                                   "The purpose of these materials is to have a handle to create contact materials between terrain objects and external objects." );

        Undo.RecordObject( DeformableTerrainBase, "Set Surface Material" );
        var surfaceMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainSurfaceMaterial" );
        var surfaceMatRes = InspectorGUI.FoldoutObjectField( GUI.MakeLabel( "Surface Material" ),
                                                      DeformableTerrainBase.Material,
                                                      typeof( AGXUnity.ShapeMaterial ),
                                                      surfaceMatData,
                                                      false ) as AGXUnity.ShapeMaterial;
        if ( surfaceMatRes != DeformableTerrainBase.Material )
          DeformableTerrainBase.Material = surfaceMatRes;


        Undo.RecordObject( DeformableTerrainBase, "Set Particle Material" );
        var particleMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainParticleMaterial" );
        var particleMatRes = InspectorGUI.FoldoutObjectField( GUI.MakeLabel( "Particle Material" ),
                                                      DeformableTerrainBase.ParticleMaterial,
                                                      typeof( AGXUnity.ShapeMaterial ),
                                                      particleMatData,
                                                      false ) as AGXUnity.ShapeMaterial;

        if ( particleMatRes != DeformableTerrainBase.ParticleMaterial )
          DeformableTerrainBase.ParticleMaterial = particleMatRes;
        EditorGUI.indentLevel = 0;
      }
    }
  }
}
