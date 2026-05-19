using AGXUnity.Model;
using AGXUnityEditor.Utils;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainShovel ) )]
  public class DeformableTerrainShovelTool : CustomTargetTool
  {
    public DeformableTerrainShovel Shovel { get { return Targets[ 0 ] as DeformableTerrainShovel; } }

    public DeformableTerrainShovelTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      AddChild( new LineTool( Shovel.TopEdge )
      {
        Color                = Color.yellow,
        Name                 = "Top Edge",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel,
        TransformResult      = OnEdgeResult
      } );
      AddChild( new LineTool( Shovel.CuttingEdge )
      {
        Color                = Color.red,
        Name                 = "Cutting Edge",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel,
        TransformResult      = OnEdgeResult
      } );
      AddChild( new LineTool( Shovel.ToothDirection )
      {
        Color                = Color.green,
        Name                 = "Teeth Direction",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel,
        TransformResult      = OnToothDirectionResult,
        Mode                 = LineTool.ToolMode.Direction,
        DirectionArrowLength = 0.5f
      } );

      m_requestEdgeValidate = true;
    }

    public override void OnRemove()
    {
      SceneView.RepaintAll();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      var shouldValidateEdges = !EditorApplication.isPlaying &&
                                m_requestEdgeValidate &&
                                TopEdgeLineTool.Line.Valid &&
                                CuttingEdgeLineTool.Line.Valid;
      if ( shouldValidateEdges ) {
        m_edgeIssues.Clear();

        var cuttingDir   = CuttingEdgeLineTool.Line.Direction;
        var cuttingToTop = Vector3.Normalize( TopEdgeLineTool.Line.Start.Position - CuttingEdgeLineTool.Line.Start.Position );
        var rayCenter    = 0.5f * ( CuttingEdgeLineTool.Line.Center + TopEdgeLineTool.Line.Center );
        var rayDir       = Vector3.Cross( cuttingDir, cuttingToTop ).normalized;


        var absDot = Mathf.Abs(Vector3.Dot( cuttingDir, TopEdgeLineTool.Line.Direction ));
        if ( absDot < 0.95f )
          m_edgeIssues.Add( "\u2022 " +
                            GUI.AddColorTag( "Top", Color.Lerp( Color.yellow, Color.white, 0.35f ) ) +
                            " and " +
                            GUI.AddColorTag( "Cutting", Color.Lerp( Color.red, Color.white, 0.35f ) ) +
                            " edge direction expected to be approximately parallel with (absolute) dot product > 0.95, currently: " +
                            GUI.AddColorTag( absDot.ToString(), Color.red ) );
        if ( !Utils.Raycast.Intersect( new Ray( rayCenter, rayDir ), Shovel.GetComponentsInChildren<MeshFilter>() ).Hit )
          m_edgeIssues.Add( "\u2022 " +
                            GUI.AddColorTag( "Top", Color.Lerp( Color.yellow, Color.white, 0.35f ) ) +
                            " and " +
                            GUI.AddColorTag( "Cutting", Color.Lerp( Color.red, Color.white, 0.35f ) ) +
                            " edges appears to be directed in the wrong way - raycast from center bucket plane into the bucket didn't hit the bucket." );
        if ( ToothDirectionLineTool.Line != null ) {
          var teethAbsDot  = Mathf.Abs(Vector3.Dot( CuttingEdgeLineTool.Line.Direction, ToothDirectionLineTool.Line.Direction ));
          if ( Shovel.HasTeeth && teethAbsDot > 0.05f )
            m_edgeIssues.Add( "\u2022 " +
                              GUI.AddColorTag( "Tooth Direction", Color.Lerp( Color.green, Color.white, 0.35f ) ) +
                              " expected to be aproximately orthogonal to currting direction with (absolute) dot product < 0.05, currently: " +
                              GUI.AddColorTag( teethAbsDot.ToString(), Color.red ) );
        }
        m_requestEdgeValidate = false;
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      if ( m_edgeIssues.Count > 0 ) {
        foreach ( var issue in m_edgeIssues )
          InspectorGUI.WarningLabel( issue );
      }

      HandleLineToolInspectorGUI( TopEdgeLineTool, "Top Edge" );

      HandleLineToolInspectorGUI( CuttingEdgeLineTool, "Cutting Edge" );

      Shovel.HasTeeth = EditorGUILayout.Toggle( "Has Teeth", Shovel.HasTeeth );
      ToothDirectionLineTool.EnableRendering = Shovel.HasTeeth;
      if ( Shovel.HasTeeth )
        HandleLineToolInspectorGUI( ToothDirectionLineTool, "Tooth Direction" );

      m_requestEdgeValidate = true;
    }

    private void HandleLineToolInspectorGUI( LineTool lineTool, string name )
    {
      using ( new DirtyOnLineChangeScope( Shovel, lineTool.Line ) ) {
        // If visible, the vertical maker starts under the foldout, otherwise
        // render the marker through the fouldout label.
        var isVisible = GetLineToggleData( name ).Bool;
        var color     = Color.Lerp( lineTool.Color, InspectorGUI.BackgroundColor, 0.25f );
        using ( new InspectorGUI.VerticalScopeMarker( color ) ) {
          if ( !InspectorGUI.Foldout( GetLineToggleData( name ),
                                      GUI.MakeLabel( name, true ) ) )
            return;
          //using ( new InspectorGUI.VerticalScopeMarker( color ) )
          using ( InspectorGUI.IndentScope.Single )
            lineTool.OnInspectorGUI();
        }
      }
    }

    private LineTool TopEdgeLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.TopEdge ); }
    }

    private LineTool CuttingEdgeLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.CuttingEdge ); }
    }

    private LineTool ToothDirectionLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.ToothDirection ); }
    }

    private EditorDataEntry GetLineToggleData( string name )
    {
      return EditorData.Instance.GetData( Shovel, name );
    }

    private Camera FindDirectionReferenceCamera()
    {
      return SceneView.lastActiveSceneView != null ?
               SceneView.lastActiveSceneView.camera :
               null;
    }

    private EdgeDetectionTool.EdgeSelectResult OnEdgeResult( EdgeDetectionTool.EdgeSelectResult result )
    {
      var refCamera = FindDirectionReferenceCamera();
      if ( refCamera == null )
        return result;

      // Assuming the user is viewing "into" the bucket, i.e.,
      // the camera is located in front of the bucket. The
      // edge should go from left to right on screen.
      var startX = refCamera.WorldToViewportPoint( result.Edge.Start ).x;
      var endX = refCamera.WorldToViewportPoint( result.Edge.End ).x;
      if ( startX > endX )
        AGXUnity.Utils.Math.Swap( ref result.Edge.Start, ref result.Edge.End );
      return result;
    }

    private EdgeDetectionTool.EdgeSelectResult OnToothDirectionResult( EdgeDetectionTool.EdgeSelectResult result )
    {
      var refCamera = FindDirectionReferenceCamera();
      if ( refCamera == null )
        return result;
      if ( Vector3.Dot( result.Edge.Direction, refCamera.transform.forward ) > 0.0 )
        AGXUnity.Utils.Math.Swap( ref result.Edge.Start, ref result.Edge.End );
      // If a principal axis was picked, move start of direction to center of axis
      if ( result.Edge.Type == AGXUnity.Edge.EdgeType.Principal )
        result.Edge.Start = result.Edge.Center;
      result.Edge.End = result.Edge.Start + 0.5f * result.Edge.Direction;
      return result;
    }

    private bool m_requestEdgeValidate = false;
    private List<string> m_edgeIssues = new List<string>();
  }
}
