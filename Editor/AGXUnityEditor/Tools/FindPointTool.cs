using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

namespace AGXUnityEditor.Tools
{
  public class FindPointTool : Tool
  {
    public class Result
    {
      public GameObject Target            = null;
      public Raycast.TriangleHit Triangle = Raycast.TriangleHit.Invalid;
      public Quaternion Rotation          = Quaternion.identity;
    }

    public Action<Result> OnPointFound = delegate { };

    public Utils.VisualPrimitiveSphere PointVisual { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "point", "GUI/Text Shader" ); } }

    public FindPointTool()
    {
      PointVisual.Pickable       = false;
      PointVisual.MouseOverColor = PointVisual.Color;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( m_collectedData == null ) {
        if ( GetChild<SelectGameObjectTool>() == null ) {
          SelectGameObjectTool selectGameObjectTool = new SelectGameObjectTool();
          selectGameObjectTool.OnSelect = go =>
          {
            m_collectedData = new CollectedData() { Target = go };
          };
          AddChild( selectGameObjectTool );
        }
      }
      else if ( !m_collectedData.TriangleGiven ) {
        // TODO: Handle world point?
        if ( m_collectedData.Target == null ) {
          PerformRemoveFromParent();
          return;
        }

        m_collectedData.Triangle = Raycast.Test( m_collectedData.Target, HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ) ).Triangle;

        // Done (next state) when the user left click and we've a valid triangle.
        m_collectedData.TriangleGiven = m_collectedData.Triangle.Valid && Manager.HijackLeftMouseClick();
      }
      else if ( !m_collectedData.RotationGiven ) {
        if ( GetChild<DirectionTool>() == null ) {
          DirectionTool directionTool = new DirectionTool( m_collectedData.Triangle.Point,
                                                           m_collectedData.Triangle.Normal,
                                                           m_collectedData.Triangle.ClosestEdge.Direction );

          directionTool.OnSelect += ( position, rotation ) =>
          {
            m_collectedData.Rotation      = rotation;
            m_collectedData.RotationGiven = true;
          };

          AddChild( directionTool );
        }
      }
      else {
        Result resultingData = new Result()
        {
          Target   = m_collectedData.Target,
          Triangle = m_collectedData.Triangle,
          Rotation = m_collectedData.Rotation
        };

        OnPointFound( resultingData );
        PerformRemoveFromParent();
      }

      PointVisual.Visible = m_collectedData != null && m_collectedData.Triangle.Valid && !m_collectedData.TriangleGiven;
      if ( PointVisual.Visible )
        PointVisual.SetTransform( m_collectedData.Triangle.Point, Quaternion.identity, 0.05f );
    }

    private class CollectedData
    {
      public GameObject Target            = null;
      public Raycast.TriangleHit Triangle = Raycast.TriangleHit.Invalid;
      public Quaternion Rotation          = Quaternion.identity;

      public bool TriangleGiven = false;
      public bool RotationGiven = false;
    }

    private CollectedData m_collectedData = null;
  }
}
