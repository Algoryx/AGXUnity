using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Utils;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( DeformableTerrainPager ) )]
  public class DeformableTerrainPagerTool : DeformableTerrainBaseTool
  {
    public DeformableTerrainPager TerrainPager { get { return Targets[ 0 ] as DeformableTerrainPager; } }

    public enum SizeUnit
    {
      Meters, Elements
    };
    public SizeUnit sizeToUse;

    public DeformableTerrainPagerTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      TerrainPager.RemoveInvalidShovels( false );

      if ( GetTargets<DeformableTerrainPager>().Any( pager => !TerrainUtils.IsValid( pager ) ) ) {
        InspectorGUI.WarningLabel( "INVALID CONFIGURATION\n\n" +
                                   "One or more AGXUnity.Model.DeformableTerrain and/or " +
                                   "AGXUnity.Model.DeformableTerrainPager component(s) found in " +
                                   "the connected tiles. <b>A Deformable Terrain Pager has to be unique " +
                                   "to the UnityEngine.Terrain and its tiles.</b>" );

        if ( !IsMultiSelect ) {
          var deformableTerrains = ( from terrain in TerrainUtils.CollectTerrains( TerrainPager.Terrain )
                                     let deformableTerrain = terrain.GetComponent<DeformableTerrain>()
                                     where deformableTerrain != null
                                     select deformableTerrain ).ToArray();
          var deformableTerrainPagers = ( from terrain in TerrainUtils.CollectTerrains( TerrainPager.Terrain )
                                          let otherPager = terrain.GetComponent<DeformableTerrainPager>()
                                          where otherPager != null && otherPager != TerrainPager
                                          select otherPager ).Distinct().ToArray();

          InspectorGUI.Separator( 2.0f, 4.0f );

          InvalidTileInstancesGUI( deformableTerrains );
          InvalidTileInstancesGUI( deformableTerrainPagers );
        }
      }
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
        if ( !DeformableTerrainPager.IsValidSize( TerrainPager.TileSize ) ) {
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

      RenderMaterialHandles();

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

      if ( TerrainPager.Shovels.Any( shovel => !shovel.isActiveAndEnabled ) || TerrainPager.RigidBodies.Any( rb => !rb.isActiveAndEnabled ) ) {
        EditorGUILayout.HelpBox( "Terrain contains disabled objects. This is not supported and they will be removed on play. Disabled objects must be added manually to the terrain when enabled", MessageType.Warning );
        if ( GUILayout.Button( "Remove disabled objects" ) )
          TerrainPager.RemoveInvalidShovels( true, false );
      }
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      base.OnSceneViewGUI( sceneView );

      if ( TerrainPager.Native != null )
        foreach ( var t in TerrainPager.Native.getActiveTileAttachments() )
          RenderTileAttachmentOutlines( t );
    }

    private void InvalidTileInstancesGUI<T>( T[] instances )
      where T : ScriptComponent
    {
      if ( instances.Length > 0 ) {
        GUILayout.Label( GUI.MakeLabel( $"Reduntant {typeof( T ).FullName}(s)", true ), InspectorGUISkin.Instance.LabelMiddleCenter );
        foreach ( var instance in instances ) {
          using ( new EditorGUILayout.HorizontalScope() ) {
            using ( new GUI.BackgroundColorBlock( Color.Lerp( Color.white, Color.red, 0.55f ) ) ) {
              if ( GUILayout.Button( SelectGameObjectDropdownMenuTool.GetGUIContent( instance.gameObject ),
                                     InspectorGUISkin.Instance.TextAreaMiddleCenter ) )
                EditorGUIUtility.PingObject( instance );
            }
            if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                      true,
                                      $"Remove {instance.GetType().FullName} component from game object {instance.name}.",
                                      GUILayout.Width( EditorGUIUtility.singleLineHeight ) ) ) {
              if ( EditorUtility.DisplayDialog( $"Remove {instance.GetType().FullName} component?",
                                                $"Are you sure you want to remove the {instance.GetType().FullName} " +
                                                $"component from {instance.name}?",
                                                "Yes",
                                                "Cancel" ) ) {
                using ( new Utils.UndoCollapseBlock( "Destroy " + instance.GetType().FullName ) ) {
                  var renderer = instance.GetComponent<AGXUnity.Rendering.DeformableTerrainParticleRenderer>();
                  Undo.DestroyObjectImmediate( instance );
                  // Avoid destroy of renderer belonging to the selected pager if 'instance'
                  // is a deformable terrain on our pager game object.
                  if ( renderer != null && renderer.gameObject != TerrainPager.gameObject )
                    Undo.DestroyObjectImmediate( renderer );
                }
                GUIUtility.ExitGUI();
              }
            }
          }
        }

        InspectorGUI.Separator( 2.0f, 4.0f );
      }
    }

    private void RadiiEditor<T>( T obj, int index )
      where T : ScriptComponent
    {
      using ( InspectorGUI.IndentScope.Single ) {
        GUILayout.Space( 2 );

        var isShovel = obj is DeformableTerrainShovel;
        PagingBody<T> pagingObject = isShovel ?
                                       TerrainPager.PagingShovels[index] as PagingBody<T> :
                                       TerrainPager.PagingRigidBodies[index] as PagingBody<T> ;
        float requiredRadius = EditorGUILayout.FloatField( "Required radius", pagingObject.requiredRadius );
        float preloadRadius = EditorGUILayout.FloatField( "Preload radius", pagingObject.preloadRadius );

        if ( isShovel )
          TerrainPager.SetTileLoadRadius( obj as DeformableTerrainShovel, requiredRadius, preloadRadius );
        else
          TerrainPager.SetTileLoadRadius( obj as RigidBody, requiredRadius, preloadRadius );

        GUILayout.Space( 6 );
      }
    }

    private void RenderTileAttachmentOutlines( agxTerrain.TerrainPager.TileAttachments terr )
    {
      Vector3 basePos = terr.m_terrainTile.getPosition().ToHandedVector3();
      var size = terr.m_terrainTile.getSize() / 2;

      Vector3 v0 = basePos + new Vector3( (float)size.x,  0.1f - basePos.y, (float)size.y );
      Vector3 v1 = basePos + new Vector3( (float)size.x,  0.1f - basePos.y, (float)-size.y );
      Vector3 v2 = basePos + new Vector3( (float)-size.x, 0.1f - basePos.y, (float)-size.y );
      Vector3 v3 = basePos + new Vector3( (float)-size.x, 0.1f - basePos.y, (float)size.y );

      Debug.DrawLine( v0, v1 );
      Debug.DrawLine( v1, v2 );
      Debug.DrawLine( v2, v3 );
      Debug.DrawLine( v3, v0 );
    }
  }
}
