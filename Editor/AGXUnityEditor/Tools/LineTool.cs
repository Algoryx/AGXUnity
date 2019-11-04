using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class LineTool : Tool
  {
    public Line Line { get; private set; }

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
      LineVisual.Visible   =
      StartVisual.Visible  =
      EndVisual.Visible    = false;
      LineVisual.Pickable  = false;
      StartVisual.Pickable = true;
      EndVisual.Pickable   = true;

      StartFrameToolEnable = true;
      EndFrameToolEnable   = true;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      var lineVisualRadius   = 0.015f;
      var sphereVisualRadius = 1.5f * lineVisualRadius;

      LineVisual.Visible = true;
      LineVisual.SetTransform( Line.Start.Position + lineVisualRadius * Line.Direction,
                               Line.End.Position - lineVisualRadius * Line.Direction,
                               lineVisualRadius,
                               false );
      StartVisual.Visible = true;
      StartVisual.SetTransform( Line.Start.Position,
                                Quaternion.identity,
                                sphereVisualRadius,
                                false );
      EndVisual.Visible = true;
      EndVisual.SetTransform( Line.End.Position,
                              Quaternion.identity,
                              sphereVisualRadius,
                              false );
    }

    public void OnInspectorGUI()
    {
      if ( StartFrameToolEnable )
        StartFrameTool.OnPreTargetMembersGUI();
      if ( EndFrameToolEnable )
        EndFrameTool.OnPreTargetMembersGUI();
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
          AddChild( new FrameTool( Line.Start ) );
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
          AddChild( new FrameTool( Line.End ) );
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

    private Color m_color = Color.yellow;
  }
}
