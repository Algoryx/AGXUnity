using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;

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
      Undo.RecordObject( DeformableTerrain, "Shovel add/remove." );

      GUI.Separator();

      var displayShovelList = GUI.Foldout( EditorData.Instance.GetData( DeformableTerrain,
                                                                        "Shovels" ),
                                           GUI.MakeLabel( "Shovels" ),
                                           InspectorEditor.Skin );
      if ( displayShovelList ) {
        DeformableTerrainShovel shovelToRemove = null;
        using ( new GUI.Indent( 12 ) ) {
          foreach ( var shovel in DeformableTerrain.Shovels ) {
            GUI.Separator();

            var displayShovel = false;
            using ( new GUILayout.HorizontalScope() ) {
              displayShovel = GUI.Foldout( EditorData.Instance.GetData( DeformableTerrain,
                                                                        shovel.GetInstanceID().ToString() ),
                                           GUI.MakeLabel( "[" + GUI.AddColorTag( "Shovel", Color.Lerp( Color.red, Color.black, 0.25f ) ) + "] " + shovel.name ),
                                           InspectorEditor.Skin );

              using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
                if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(), false, "Remove shovel from terrain." ),
                     InspectorEditor.Skin.button,
                     GUILayout.Width( 18 ),
                     GUILayout.Height( 14 ) ) )
                  shovelToRemove = shovel;
            }
            if ( !displayShovel ) {
              HandleShovelEditorDisable( shovel );
              continue;
            }
            using ( new GUI.Indent( 12 ) ) {
              var editor = GetOrCreateEditor( shovel );
              editor.OnInspectorGUI();
            }
          }

          GUI.Separator( 3 );

          DeformableTerrainShovel shovelToAdd = null;
          var addButtonPressed = false;
          using ( new GUILayout.VerticalScope( GUI.FadeNormalBackground( InspectorEditor.Skin.label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add shovel", true ), InspectorEditor.Skin.label );
            using ( new GUILayout.HorizontalScope() ) {
              shovelToAdd = EditorGUILayout.ObjectField( "", null, typeof( DeformableTerrainShovel ), true ) as DeformableTerrainShovel;
              addButtonPressed = GUILayout.Button( GUI.MakeLabel( "+" ), InspectorEditor.Skin.button, GUILayout.Width( 24 ), GUILayout.Height( 14 ) );
            }
          }

          if ( addButtonPressed ) {
            var shovels = Object.FindObjectsOfType<DeformableTerrainShovel>();
            GenericMenu addShovelMenu = new GenericMenu();
            addShovelMenu.AddDisabledItem( GUI.MakeLabel( "Shovels in scene" ) );
            addShovelMenu.AddSeparator( string.Empty );
            foreach ( var shovel in shovels ) {
              if ( DeformableTerrain.Contains( shovel ) )
                continue;
              addShovelMenu.AddItem( GUI.MakeLabel( shovel.name ),
                                     false,
                                     () =>
                                     {
                                       DeformableTerrain.Add( shovel );
                                     } );
            }
            addShovelMenu.ShowAsContext();
          }

          if ( shovelToAdd != null )
            DeformableTerrain.Add( shovelToAdd );

          GUI.Separator( 3 );
        }

        if ( shovelToRemove != null ) {
          DeformableTerrain.Remove( shovelToRemove );
          HandleShovelEditorDisable( shovelToRemove );
          shovelToRemove = null;
        }
      }
      else {
        foreach ( var shovel in DeformableTerrain.Shovels )
          HandleShovelEditorDisable( shovel );
      }
    }

    private void HandleShovelEditorDisable( DeformableTerrainShovel shovel )
    {
      if ( HasEditor( shovel ) ) {
        RemoveEditor( shovel );
        SceneView.RepaintAll();
      }
    }
  }
}
