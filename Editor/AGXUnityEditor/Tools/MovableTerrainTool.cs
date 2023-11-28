using AGXUnity.Model;
using UnityEditor;
using UnityEngine;

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

    public enum SizeUnit
    {
      Meters, CellCount
    };

    public SizeUnit sizeToUse;

    public override void OnPreTargetMembersGUI()
    {
      MovableTerrain.RemoveInvalidShovels();

      EditorGUILayout.Space();
      sizeToUse = (SizeUnit)EditorGUILayout.EnumPopup( "Size Units", sizeToUse );

      using ( new InspectorGUI.IndentScope() ) {
        if ( sizeToUse == SizeUnit.Meters ) {
          var newSize = InspectorGUI.Vector2Field( GUI.MakeLabel("Size"), MovableTerrain.SizeMeters );
          newSize = new Vector2( Mathf.Clamp( newSize.x, 0.1f, float.MaxValue ), Mathf.Clamp( newSize.y, 0.1f, float.MaxValue ) );
          MovableTerrain.SizeMeters = newSize;
          MovableTerrain.Resolution = Mathf.Clamp( EditorGUILayout.IntField( "Resolution", MovableTerrain.Resolution ), 1, int.MaxValue );
          EditorGUILayout.LabelField( $"Terrain size is {MovableTerrain.SizeCells.x} x {MovableTerrain.SizeCells.y} cells sized {MovableTerrain.ElementSize:F2} m" );
        }
        else {
          var newSize = InspectorGUI.Vector2IntField( GUI.MakeLabel("Count"), MovableTerrain.SizeCells );
          newSize = new Vector2Int( Mathf.Clamp( newSize.x, 1, int.MaxValue ), Mathf.Clamp( newSize.y, 1, int.MaxValue ) );
          MovableTerrain.SizeCells = newSize;
          MovableTerrain.ElementSize = Mathf.Clamp( EditorGUILayout.FloatField( "Element Size", MovableTerrain.ElementSize ), 0.001f, float.MaxValue );
          EditorGUILayout.LabelField( $"Terrain size is {MovableTerrain.SizeMeters.x:F1} x {MovableTerrain.SizeMeters.y:F1} m" );
        }
      }
    }

    public override void OnPostTargetMembersGUI()
    {
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
