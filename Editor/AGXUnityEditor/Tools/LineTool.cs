using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class LineTool : Tool
  {
    public Line Line { get; private set; }

    public string Name { get; set; } = "Line";

    public UnityEngine.Object UndoRedoRecordObject { get; set; }

    public Color Color
    {
      get { return m_color; }
      set
      {
        m_color = value;
        LineVisual.Color = m_color;
        StartVisual.Color = m_color;
        EndVisual.Color = m_color;
      }
    }

    public LineTool( Line line )
      : base( isSingleInstanceTool: true )
    {
      Line = line;
    }

    public override void OnAdd()
    {
      LineVisual.Visible  = false;
      StartVisual.Visible = false;
      EndVisual.Visible   = false;

      LineVisual.Pickable  = false;
      StartVisual.Pickable = true;
      EndVisual.Pickable   = true;

      StartFrameToolEnable = Line.Valid;
      EndFrameToolEnable   = Line.Valid;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      var lineVisualRadius   = 0.015f;
      var sphereVisualRadius = 1.5f * lineVisualRadius;
      var renderOnSceneView  = !ConfigurationToolActive() &&
                                Line.Valid && ( 
                               !EditorApplication.isPlaying ||
                                EditorApplication.isPaused );
      var startEnabled = renderOnSceneView && GetFrameToggleEnable( StartFrameNameId );
      var endEnabled   = renderOnSceneView && GetFrameToggleEnable( EndFrameNameId );

      LineVisual.Visible = renderOnSceneView;
      if ( LineVisual.Visible ) {
        LineVisual.SetTransform( Line.Start.Position + lineVisualRadius * Line.Direction,
                                 Line.End.Position - lineVisualRadius * Line.Direction,
                                 lineVisualRadius,
                                 false );
      }

      StartVisual.Visible = startEnabled;
      if ( StartVisual.Visible ) {
        StartVisual.SetTransform( Line.Start.Position,
                                  Quaternion.identity,
                                  sphereVisualRadius,
                                  false );
      }

      EndVisual.Visible = endEnabled;
      if ( EndVisual.Visible ) {
        EndVisual.SetTransform( Line.End.Position,
                                Quaternion.identity,
                                sphereVisualRadius,
                                false );
      }

      if ( StartFrameTool != null )
        StartFrameTool.TransformHandleActive = startEnabled;
      if ( EndFrameTool != null )
        EndFrameTool.TransformHandleActive = endEnabled;
    }

    public void OnInspectorGUI()
    {
      bool toggleCreateEdge = false;
      using ( new GUILayout.HorizontalScope() ) {
        GUI.ToolsLabel( InspectorEditor.Skin );

        using ( GUI.ToolButtonData.ColorBlock ) {
          toggleCreateEdge = GUI.ToolButton( GUI.Symbols.SelectEdgeTool,
                                             EdgeDetectionToolEnable,
                                             "Find line given edge.",
                                             InspectorEditor.Skin );
        }
      }

      GUI.Separator();

      if ( toggleCreateEdge )
        EdgeDetectionToolEnable = !EdgeDetectionToolEnable;

      if ( !Line.Valid )
        GUI.WarningLabel( Name + " isn't created - use Tools to configure.", InspectorEditor.Skin );

      if ( StartFrameToolEnable ) {
        if ( GUI.Foldout( GetFrameToggleData( StartFrameNameId ),
                          GUI.MakeLabel( StartFrameNameId, true ),
                          InspectorEditor.Skin ) ) {
          using ( new GUI.Indent( 12 ) )
            StartFrameTool.OnPreTargetMembersGUI();
        }
      }
      if ( EndFrameToolEnable ) {
        if ( GUI.Foldout( GetFrameToggleData( EndFrameNameId ),
                          GUI.MakeLabel( EndFrameNameId, true ),
                          InspectorEditor.Skin ) ) {
          using ( new GUI.Indent( 12 ) )
            EndFrameTool.OnPreTargetMembersGUI();
        }
      }
    }

    private bool ConfigurationToolActive()
    {
      return EdgeDetectionToolEnable;
    }

    private EdgeDetectionTool EdgeDetectionTool
    {
      get { return GetChild<EdgeDetectionTool>(); }
    }

    private bool EdgeDetectionToolEnable
    {
      get { return EdgeDetectionTool != null; }
      set
      {
        if ( value && !EdgeDetectionToolEnable )
          AddChild( new EdgeDetectionTool()
          {
            OnEdgeSelect = OnEdgeSelect
          } );
        else if ( !value )
          RemoveChild( EdgeDetectionTool );
      }
    }

    private void OnEdgeSelect( EdgeDetectionTool.EdgeSelectResult result )
    {
      Line.Start.SetParent( result.Target );
      Line.Start.Position = result.Edge.Start;
      Line.Start.Rotation = CalculateRotation( result.Edge, AGXUnity.Utils.ShapeUtils.Direction.Positive_Z );

      Line.End.SetParent( result.Target );
      Line.End.Position = result.Edge.End;
      Line.End.Rotation = CalculateRotation( result.Edge, AGXUnity.Utils.ShapeUtils.Direction.Negative_Z );

      StartFrameToolEnable = true;
      EndFrameToolEnable   = true;

      EdgeDetectionToolEnable = false;
    }

    private FrameTool StartFrameTool
    {
      get { return FindActive<FrameTool>( tool => tool.Frame == Line.Start ); }
    }

    private bool StartFrameToolEnable
    {
      get { return StartFrameTool != null; }
      set
      {
        if ( value && !StartFrameToolEnable )
          AddChild( new FrameTool( Line.Start )
          {
            IsSingleInstanceTool = IsSingleInstanceTool,
            UndoRedoRecordObject = UndoRedoRecordObject
          } );
        else if ( !value )
          RemoveChild( StartFrameTool );
      }
    }

    private FrameTool EndFrameTool
    {
      get { return FindActive<FrameTool>( tool => tool.Frame == Line.End ); }
    }

    private bool EndFrameToolEnable
    {
      get { return EndFrameTool != null; }
      set
      {
        if ( value && !EndFrameToolEnable )
          AddChild( new FrameTool( Line.End )
          {
            IsSingleInstanceTool = IsSingleInstanceTool,
            UndoRedoRecordObject = UndoRedoRecordObject
          } );
        else if ( !value )
          RemoveChild( EndFrameTool );
      }
    }

    private Utils.VisualPrimitiveCylinder LineVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "Line", "GUI/Text Shader" ); }
    }

    private Utils.VisualPrimitiveSphere StartVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "Start", "GUI/Text Shader" ); }
    }

    private Utils.VisualPrimitiveSphere EndVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "End", "GUI/Text Shader" ); }
    }

    private Quaternion CalculateRotation( Edge edge, AGXUnity.Utils.ShapeUtils.Direction direction )
    {
      return Quaternion.LookRotation( edge.Normal, edge.Direction ) *
             Quaternion.FromToRotation( Vector3.up, AGXUnity.Utils.ShapeUtils.GetLocalFaceDirection( direction ) );
    }

    private EditorDataEntry GetFrameToggleData( string name )
    {
      return EditorData.Instance.GetData( UndoRedoRecordObject, Name + '_' + name );
    }

    private bool GetFrameToggleEnable( string name )
    {
      return GetFrameToggleData( name ).Bool;
    }

    private Color m_color = Color.yellow;
    private static readonly string StartFrameNameId = "Start frame";
    private static readonly string EndFrameNameId = "End frame";
  }
}
