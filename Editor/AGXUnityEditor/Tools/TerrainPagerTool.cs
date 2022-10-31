using AGXUnity;
using AGXUnity.Model;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( TerrainPager ) )]
  public class TerrainPagerTool : CustomTargetTool
  {
    public TerrainPager TerrainPager { get { return Targets[ 0 ] as TerrainPager; } }

    public enum SizeUnit
    {
      Meters, Elements
    };
    public SizeUnit sizeToUse;

    public TerrainPagerTool( UnityEngine.Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      TerrainPager.RemoveInvalidBodies();
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      EditorGUILayout.Space();
      sizeToUse = (SizeUnit)EditorGUILayout.EnumPopup( "Tile Size Unit", sizeToUse );

      EditorGUI.indentLevel = 1;
      if ( sizeToUse == SizeUnit.Meters ) {
        TerrainPager.TileSizeMeters = EditorGUILayout.FloatField( "Tile Size", TerrainPager.TileSizeMeters );
        TerrainPager.TileOverlapMeters = EditorGUILayout.FloatField( "Tile Overlap", TerrainPager.TileOverlapMeters );
      }
      else {
        TerrainPager.TileSize = EditorGUILayout.IntField( "Tile Size", TerrainPager.TileSize );
        TerrainPager.TileOverlap = EditorGUILayout.IntField( "Tile Overlap", TerrainPager.TileOverlap );
      }
      TerrainPager.AutoTileOnPlay = EditorGUILayout.Toggle( "Auto Tile On Play", TerrainPager.AutoTileOnPlay );
      EditorGUI.indentLevel = 0;

      if ( !TerrainPager.AutoTileOnPlay ) {
        Undo.RecordObject( TerrainPager, "Recalculate parameters" );
        bool validParams = true;

        if ( !TerrainPager.ValidateParameters() ) {
          validParams = false;
          InspectorGUI.WarningLabel( "Current TileSize and TileOverlap parameters does not tile the underlying Unity Terrain" );
        }

        // This check is required due to how heightmaps are offset in AGX
        if ( !TerrainPager.IsValidSize( TerrainPager.TileSize ) ) {
          InspectorGUI.WarningLabel( "Current only odd TileSize values are allowed" );
          validParams = false;
        }

        if ( !validParams ) {
          if ( GUILayout.Button( "Recalculate pager parameters" ) ) {
            TerrainPager.RecalculateParameters();
            EditorUtility.SetDirty( TerrainPager );
          }
        }
      }

      EditorGUILayout.Space();

      Undo.RecordObject( TerrainPager, "Shovel add/remove." );

      InspectorGUI.ToolListGUI( this,
                                TerrainPager.Shovels,
                                "Shovels",
                                shovel => TerrainPager.Add( shovel ),
                                shovel => TerrainPager.Remove( shovel ),
                                RadiiEditor,
                                null
                                );

      Undo.RecordObject( TerrainPager, "RigidBody add/remove." );

      InspectorGUI.ToolListGUI( this,
                                TerrainPager.RigidBodies,
                                "RigidBodies",
                                rb => TerrainPager.Add( rb ),
                                rb => TerrainPager.Remove( rb ),
                                RadiiEditor,
                                null
                                );
    }

    private void RadiiEditor( DeformableTerrainShovel shovel, int index )
    {
      var pagingShovel = TerrainPager.PagingShovels[index];
      float requiredRadius = EditorGUILayout.FloatField( "Required radius", pagingShovel.requiredRadius );
      float preloadRadius = EditorGUILayout.FloatField( "Preload radius", pagingShovel.preloadRadius );

      TerrainPager.SetTileLoadRadius( shovel, requiredRadius, preloadRadius );
    }

    private void RadiiEditor( RigidBody body, int index )
    {
      var pagingRB = TerrainPager.PagingRigidBodies[index];
      float requiredRadius = EditorGUILayout.FloatField( "Required radius", pagingRB.requiredRadius);
      float preloadRadius = EditorGUILayout.FloatField( "Preload radius", pagingRB.preloadRadius);

      TerrainPager.SetTileLoadRadius( body, requiredRadius, preloadRadius );
    }
  }
}
