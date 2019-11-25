using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnityEditor.Utils.GUI;
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
      AddChild( new LineTool( Shovel.CuttingDirection )
      {
        Color                = Color.green,
        Name                 = "Cutting Direction",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel,
        TransformResult      = OnCuttingDirectionResult,
        Mode                 = LineTool.ToolMode.Direction,
        DirectionArrowLength = 0.5f
      } );
    }

    public override void OnRemove()
    {
      SceneView.RepaintAll();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      GUI.Separator();

      HandleLineToolInspectorGUI( TopEdgeLineTool, "Top Edge" );
      GUI.Separator();

      HandleLineToolInspectorGUI( CuttingEdgeLineTool, "Cutting Edge" );
      GUI.Separator();

      HandleLineToolInspectorGUI( CuttingDirectionLineTool, "Cutting Direction" );
      GUI.Separator();
    }

    private void HandleLineToolInspectorGUI( LineTool lineTool, string name )
    {
      var backgroundStyle = new GUIStyle( InspectorEditor.Skin.label );
      backgroundStyle.normal.background = GUI.CreateColoredTexture( 1,
                                                                    1,
                                                                    Color.Lerp( EditorGUIUtility.isProSkin ?
                                                                                  GUI.ProBackgroundColor :
                                                                                  GUI.IndieBackgroundColor,
                                                                                lineTool.Color,
                                                                                0.15f ) );
      using ( new GUILayout.VerticalScope( backgroundStyle ) ) {
        if ( !GUI.Foldout( GetLineToggleData( name ),
                           GUI.MakeLabel( name, true ),
                           InspectorEditor.Skin ) )
          return;
        GUI.Separator();
        using ( new GUI.Indent( 24 ) )
          lineTool.OnInspectorGUI();
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

    private LineTool CuttingDirectionLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.CuttingDirection ); }
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

    private EdgeDetectionTool.EdgeSelectResult OnCuttingDirectionResult( EdgeDetectionTool.EdgeSelectResult result )
    {
      var refCamera = FindDirectionReferenceCamera();
      if ( refCamera == null )
        return result;
      if ( Vector3.Dot( result.Edge.Direction, refCamera.transform.forward ) > 0.0 )
        AGXUnity.Utils.Math.Swap( ref result.Edge.Start, ref result.Edge.End );
      result.Edge.End = result.Edge.Start + 0.5f * result.Edge.Direction;
      return result;
    }
  }
}
