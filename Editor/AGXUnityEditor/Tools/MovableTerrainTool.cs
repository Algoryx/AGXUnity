using AGXUnity.Collide;
using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using static AGXUnityEditor.InspectorGUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( MovableTerrain ) )]
  public class MovableTerrainTool : DeformableTerrainBaseTool
  {
    public MovableTerrain MovableTerrain { get { return Targets[ 0 ] as MovableTerrain; } }

    public MovableTerrainTool( Object[] targets )
      : base( targets )
    {
    }

    private Shape[] m_availableBedGeometries;

    public override void OnAdd()
    {
      m_availableBedGeometries = null;
      if ( !EditorApplication.isPlayingOrWillChangePlaymode )
        m_availableBedGeometries = GameObject.FindObjectsByType<Shape>( FindObjectsSortMode.None );
    }

    public enum SizeUnit
    {
      Meters, CellCount
    };

    public SizeUnit sizeToUse;

    public override void OnPreTargetMembersGUI()
    {
      EditorGUILayout.Space();

      DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.PlacementMode ) );

      if ( MovableTerrain.PlacementMode == MovableTerrain.Placement.Automatic ) {
        using var _ = new InspectorGUI.IndentScope();
        InspectorGUI.ToolListGUI( this, MovableTerrain.BedGeometries, "Bed Geometries", m_availableBedGeometries, geom => MovableTerrain.AddBedGeometry( geom ), geom => MovableTerrain.RemoveBedGeometry( geom ) );
        DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.Resolution ) );

        EditorGUILayout.LabelField( new GUIContent(
              $"Terrain size is {MovableTerrain.SizeCells.x} x {MovableTerrain.SizeCells.y} cells sized {MovableTerrain.ElementSize:F2} m",
              $"Cell size is {MovableTerrain.ElementSize:F5} m"
              ) );

        DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.MaxDepthAsInitialHeight ) );

        DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.TerrainBedMargin ) );
        DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.TerrainBedHeightOffset ) );

        if ( GUILayout.Button( "Recompute" ) )
          MovableTerrain.RecalculateAutomaticBed();
      }
      else {
        sizeToUse = (SizeUnit)EditorGUILayout.EnumPopup( "Size Units", sizeToUse );

        using var _ = new InspectorGUI.IndentScope();
        if ( sizeToUse == SizeUnit.Meters ) {
          DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.SizeMeters ) );
          DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.Resolution ) );
          EditorGUILayout.LabelField( new GUIContent(
            $"Terrain size is {MovableTerrain.SizeCells.x} x {MovableTerrain.SizeCells.y} cells sized {MovableTerrain.ElementSize:F2} m",
            $"Cell size is {MovableTerrain.ElementSize} m"
            ) );
        }
        else {
          DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.SizeCells ) );
          DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.ElementSize ) );
          EditorGUILayout.LabelField( new GUIContent(
            $"Terrain size is {MovableTerrain.SizeMeters.x:F3} x {MovableTerrain.SizeMeters.y:F3} m",
            $"Terrain size is {MovableTerrain.SizeMeters.x} x {MovableTerrain.SizeMeters.y} m"
            ) );
        }

        DefaultInspector<MovableTerrain>( Targets, nameof( MovableTerrain.MaxDepthAsInitialHeight ) );
      }
    }
  }
}
