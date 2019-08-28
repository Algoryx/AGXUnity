using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class LineTool : Tool
  {
    public Line Line { get; private set; }

    public LineTool( Line line )
    {
      Line = line;
    }

    public override void OnAdd()
    {
      LineVisual.Visible = StartVisual.Visible = EndVisual.Visible = false;
      LineVisual.Pickable  = false;
      StartVisual.Pickable = true;
      EndVisual.Pickable   = true;
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

    private Color m_color;
  }
}
