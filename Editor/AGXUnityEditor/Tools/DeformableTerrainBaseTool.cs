using AGXUnity;
using AGXUnity.Model;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static AGXUnityEditor.InspectorGUI;
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

      ToolListGUI( this,
                    DeformableTerrainBase.Shovels,
                    "Shovels",
                    shovel => DeformableTerrainBase.Add( shovel ),
                    shovel => DeformableTerrainBase.Remove( shovel ) );

      RenderMaterialPatchesGUI();
      
      if ( DeformableTerrainBase.Shovels.Any( shovel => !shovel.isActiveAndEnabled ) ) {
        EditorGUILayout.HelpBox( "Terrain contains disabled shovels. This is not supported and they will be removed on play. Disabled shovels must be added manually to the terrain when enabled", MessageType.Warning );
        if ( GUILayout.Button( "Remove disabled shovels" ) )
          DeformableTerrainBase.RemoveInvalidShovels( true, false );
      }
    }

    protected void RenderMaterialPatchesGUI()
    {
      var items = DeformableTerrainBase.Materials;
      var displayItemsList = Foldout( GetTargetToolArrayGUIData( Targets[ 0 ], "Materials" ),
                                      GUI.MakeLabel( $"Materials [{items.Length}]" ) );
      if ( displayItemsList ) {
        DeformableTerrainMaterial itemToRemove = null;
        using ( IndentScope.Single ) {
          for ( int itemIndex = 0; itemIndex < items.Length; ++itemIndex ) {
            var item = items[ itemIndex ];
            bool displayItem;
            using ( new GUILayout.HorizontalScope() ) {
              displayItem = Foldout( EditorData.Instance.GetData( Targets[ 0 ], $"Material Patches_{itemIndex}" ),
                                       GUI.MakeLabel( InspectorEditor.Skin.TagTypename( "Material" ) +
                                                      ' ' +
                                                      item.name ) );

              if ( Button( MiscIcon.EntryRemove,
                             true,
                             $"Remove {item.name} from Materials.",
                             GUILayout.Width( 18 ) ) )
                itemToRemove = item;

              GUILayout.Space( 3.0f );
            }
            if ( displayItem ) {
              using ( IndentScope.Single ) {
                var newItem = FoldoutObjectField( GUI.MakeLabel( "Terrain Material" ),
                                                            item,
                                                            typeof( DeformableTerrainMaterial ),
                                                            EditorData.Instance.GetData( Targets[ 0 ], $"Material Patches_{itemIndex}_Material" ),
                                                            false
                                                           ) as DeformableTerrainMaterial;
                if ( newItem != item )
                  DeformableTerrainBase.Replace( item, newItem );

                var materialHandle = DeformableTerrainBase.GetAssociatedMaterial(item);
                var newMaterialHandle = FoldoutObjectField( GUI.MakeLabel( "Material Handle" ),
                                                          materialHandle,
                                                          typeof( ShapeMaterial ),
                                                          EditorData.Instance.GetData( Targets[ 0 ], $"Material Patches_{itemIndex}_MaterialHandle" ),
                                                          false
                                                          ) as ShapeMaterial;

                if ( materialHandle != newMaterialHandle )
                  DeformableTerrainBase.SetAssociatedMaterial( item, newMaterialHandle );

                foreach(var shape in DeformableTerrainBase.GetMaterialShapes( item ) )
                  if(shape == null ) {
                    DeformableTerrainBase.RemoveMaterialShape(item, shape );
                  }

                ToolListGUI( this,
                              DeformableTerrainBase.GetMaterialShapes( item ).ToArray(),
                              $"{item.name} Shapes",
                              "Shapes",
                              shape => DeformableTerrainBase.AddMaterialShape( item, shape ),
                              shape => DeformableTerrainBase.RemoveMaterialShape( item, shape )
                              );
              }
            }
          }

          DeformableTerrainMaterial itemToAdd = null;
          GUILayout.Space( 2.0f * EditorGUIUtility.standardVerticalSpacing );
          using ( new GUILayout.VerticalScope( FadeNormalBackground( InspectorEditor.Skin.Label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add item", true ), InspectorEditor.Skin.Label );
            var rect = EditorGUILayout.GetControlRect();
            var xMax = rect.xMax;
            rect.xMax = rect.xMax - EditorGUIUtility.standardVerticalSpacing;
            itemToAdd = EditorGUI.ObjectField( rect, null, typeof( DeformableTerrainMaterial ), true ) as DeformableTerrainMaterial;
          }

          if ( itemToAdd != null )
            DeformableTerrainBase.Add( itemToAdd );
          if ( itemToRemove != null )
            DeformableTerrainBase.Remove( itemToRemove );
        }
      }
    }

    protected void RenderMaterialHandles()
    {
      if ( Foldout( EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainBaseMaterialHandles" ), GUI.MakeLabel( "Internal Material Handles" ) ) ) {
        EditorGUI.indentLevel = 1;

        WarningLabel( "The parameters of these materials will be overrwritten depending on the TerrainMaterial set. " +
                      "The purpose of these materials is to have a handle to create contact materials between terrain objects and external objects." );

        Undo.RecordObject( DeformableTerrainBase, "Set Surface Material" );
        var surfaceMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainSurfaceMaterial" );
        var surfaceMatRes = FoldoutObjectField( GUI.MakeLabel( "Surface Material" ),
                                                DeformableTerrainBase.Material,
                                                typeof( ShapeMaterial ),
                                                surfaceMatData,
                                                false ) as ShapeMaterial;
        if ( surfaceMatRes != DeformableTerrainBase.Material )
          DeformableTerrainBase.Material = surfaceMatRes;


        Undo.RecordObject( DeformableTerrainBase, "Set Particle Material" );
        var particleMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainParticleMaterial" );
        var particleMatRes = FoldoutObjectField(  GUI.MakeLabel( "Particle Material" ),
                                                  DeformableTerrainBase.ParticleMaterial,
                                                  typeof( ShapeMaterial ),
                                                  particleMatData,
                                                  false ) as ShapeMaterial;

        if ( particleMatRes != DeformableTerrainBase.ParticleMaterial )
          DeformableTerrainBase.ParticleMaterial = particleMatRes;
        EditorGUI.indentLevel = 0;
      }
    }
  }
}
