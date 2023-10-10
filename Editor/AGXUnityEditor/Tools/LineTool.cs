using AGXUnity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class LineTool : Tool
  {
    public enum ToolMode
    {
      Line,
      Direction
    }

    public Line Line { get; private set; }

    public ToolMode Mode { get; set; } = ToolMode.Line;

    public float DirectionArrowLength = 1.0f;

    public string Name { get; set; } = "Line";

    public UnityEngine.Object UndoRedoRecordObject { get; set; }

    public Func<EdgeDetectionTool.EdgeSelectResult, EdgeDetectionTool.EdgeSelectResult> TransformResult = null;

    public Color Color
    {
      get { return m_color; }
      set
      {
        m_color = value;
        LineVisual.Color = m_color;
        ArrowVisual.Color = m_color;
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
      ArrowVisual.Visible = false;
      StartVisual.Visible = false;
      EndVisual.Visible   = false;

      LineVisual.Pickable  = false;
      ArrowVisual.Pickable = false;
      StartVisual.Pickable = true;
      EndVisual.Pickable   = true;

      StartFrameToolEnable = Line.Valid;
      EndFrameToolEnable   = Line.Valid;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      var lineVisualRadius   = 0.005f;
      var sphereVisualRadius = 1.5f * lineVisualRadius;
      var renderOnSceneView  = !ConfigurationToolActive() &&
                                Line.Valid && ( 
                               !EditorApplication.isPlaying ||
                                EditorApplication.isPaused );
      var startEnabled = renderOnSceneView && GetFrameToggleEnable( StartFrameNameId );
      var endEnabled   = renderOnSceneView && GetFrameToggleEnable( EndFrameNameId );

      LineVisual.Visible = renderOnSceneView && Mode == ToolMode.Line;
      if ( LineVisual.Visible ) {
        LineVisual.SetTransformEx( Line.Start.Position,
                                   Line.End.Position,
                                   lineVisualRadius,
                                   RenderAsArrow );
      }

      ArrowVisual.Visible = renderOnSceneView && Mode == ToolMode.Direction;
      if ( ArrowVisual.Visible ) {
        ArrowVisual.SetTransform( Line.Start.Position,
                                  Line.End.Position - Mathf.Min( Line.Length, DirectionArrowLength ) * Line.Direction,
                                  lineVisualRadius );
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

      Synchronize();
    }

    public void OnInspectorGUI()
    {
      if ( Mode == ToolMode.Direction )
        StartFrameNameId = "Frame";

      StartFrameToolEnable = Line.Valid;
      EndFrameToolEnable   = Line.Valid;

      bool toggleCreateEdge    = false;
      bool toggleFlipDirection = false;
      bool toggleRenderAsArrow = false;
      bool showTools           = !EditorApplication.isPlaying;
      if ( showTools ) {
        var toolButtonData = new List<InspectorGUI.ToolButtonData>();
        toolButtonData.Add( InspectorGUI.ToolButtonData.Create( ToolIcon.VisualizeLineDirection,
                                                                RenderAsArrow,
                                                                "Visualize line direction.",
                                                                () => toggleRenderAsArrow = true,
                                                                Mode != ToolMode.Direction ) );
        toolButtonData.Add( InspectorGUI.ToolButtonData.Create( ToolIcon.FlipDirection,
                                                                false,
                                                                "Flip direction.",
                                                                () => toggleFlipDirection = true,
                                                                Line.Valid ) );
        toolButtonData.Add( InspectorGUI.ToolButtonData.Create( ToolIcon.FindTransformGivenEdge,
                                                                EdgeDetectionToolEnable,
                                                                "Find line given edge.",
                                                                () => toggleCreateEdge = true ) );
        InspectorGUI.ToolButtons( toolButtonData.ToArray() );
      }

      if ( toggleCreateEdge )
        EdgeDetectionToolEnable = !EdgeDetectionToolEnable;

      if ( toggleFlipDirection && EditorUtility.DisplayDialog( "Line direction",
                                                               "Flip direction of " + Name + "?",
                                                               "Yes",
                                                               "No" ) ) {
        StartFrameToolEnable = false;

        if ( Mode == ToolMode.Direction ) {
          Line.End.Position    = Line.End.Position - 2.0f * Line.Length * Line.Direction;
          // Rotate frames around local Y axis
          Line.Start.Rotation *= Quaternion.Euler( new Vector3( 0.0f, 180.0f, 0.0f ) );
          Line.End.Rotation   *= Quaternion.Euler( new Vector3( 0.0f, 180.0f, 0.0f ) );
        }
        else {
          var tmp    = Line.Start;
          Line.Start = Line.End;
          Line.End   = tmp;
        }
      }
      if ( toggleRenderAsArrow )
        RenderAsArrow = !RenderAsArrow;

      if ( !Line.Valid )
        InspectorGUI.WarningLabel( Name + " isn't created - use Tools to configure." );

      if ( StartFrameToolEnable ) {
        if ( InspectorGUI.Foldout( GetToggleData( StartFrameNameId ),
                          GUI.MakeLabel( StartFrameNameId, true ) ) ) {
          StartFrameTool.ForceDisableTransformHandle = EditorApplication.isPlaying;
          using ( new GUI.EnabledBlock( !EditorApplication.isPlaying ) )
            InspectorGUI.HandleFrame( StartFrameTool.Frame, 1 );
        }
      }
      if ( EndFrameToolEnable ) {
        if ( InspectorGUI.Foldout( GetToggleData( EndFrameNameId ),
                          GUI.MakeLabel( EndFrameNameId, true ) ) ) {
          EndFrameTool.ForceDisableTransformHandle = EditorApplication.isPlaying;
          using ( new GUI.EnabledBlock( !EditorApplication.isPlaying ) )
            InspectorGUI.HandleFrame( EndFrameTool.Frame, 1 );
        }
      }

      Synchronize();
    }

    /// <summary>
    /// Synchronize end frame given current transform of start frame
    /// when in direction mode.
    /// </summary>
    private void Synchronize()
    {
      if ( Mode != ToolMode.Direction || !Line.Valid )
        return;

      Line.End.Position = Line.Start.Position + Line.Start.Rotation * Vector3.back;
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
      if ( UndoRedoRecordObject != null )
        Undo.RecordObject( UndoRedoRecordObject, "Line Tool Edge Detect Result" );

      if ( TransformResult != null )
        result = TransformResult( result );

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
        // Direction tool doesn't have an end frame -
        // the direction is controlled with the start frame.
        if ( Mode == ToolMode.Direction )
          return;

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

    private Utils.VisualPrimitiveArrow LineVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveArrow>( "Line", "GUI/Text Shader" ); }
    }

    private Utils.VisualPrimitiveArrow ArrowVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveArrow>( "Arrow", "GUI/Text Shader" ); }
    }

    private Utils.VisualPrimitiveSphere StartVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "Start", "GUI/Text Shader" ); }
    }

    private Utils.VisualPrimitiveSphere EndVisual
    {
      get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "End", "GUI/Text Shader" ); }
    }

    private bool RenderAsArrow
    {
      get { return GetToggleData( "AsArrow" ).Bool; }
      set
      {
        GetToggleData( "AsArrow" ).Bool = value;
      }
    }

    private Quaternion CalculateRotation( Edge edge, AGXUnity.Utils.ShapeUtils.Direction direction )
    {
      return Quaternion.LookRotation( edge.Normal, edge.Direction ) *
             Quaternion.FromToRotation( Vector3.up, AGXUnity.Utils.ShapeUtils.GetLocalFaceDirection( direction ) );
    }

    private EditorDataEntry GetToggleData( string name )
    {
      return EditorData.Instance.GetData( UndoRedoRecordObject, Name + '_' + name );
    }

    private bool GetFrameToggleEnable( string name )
    {
      return GetToggleData( name ).Bool;
    }

    private Color m_color = Color.yellow;
    private string StartFrameNameId = "Start frame";
    private string EndFrameNameId = "End frame";
  }
}
