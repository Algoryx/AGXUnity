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
      public GameObject Target   = null;
      public Utils.Raycast.Result RaycastResult;
      public Quaternion Rotation = Quaternion.identity;
    }

    public Action<Result> OnPointFound = delegate { };

    public Utils.VisualPrimitiveSphere PointVisual { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "point", "GUI/Text Shader" ); } }

    public Utils.VisualPrimitiveCylinder[] TriangleVisual
    {
      get
      {
        return new Utils.VisualPrimitiveCylinder[]
        {
          GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "v1", "GUI/Text Shader" ),
          GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "v2", "GUI/Text Shader" ),
          GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "v3", "GUI/Text Shader" )
        };
      }
    }

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

        m_collectedData.RaycastResult = Utils.Raycast.Intersect( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ), m_collectedData.Target );

        // Done (next state) when the user left click and we've a valid triangle.
        m_collectedData.TriangleGiven = m_collectedData.RaycastResult && Manager.HijackLeftMouseClick();
      }
      else if ( !m_collectedData.RotationGiven ) {
        if ( GetChild<DirectionTool>() == null ) {
          DirectionTool directionTool = new DirectionTool( m_collectedData.RaycastResult.Point,
                                                           m_collectedData.RaycastResult.Triangle.Normal,
                                                           m_collectedData.RaycastResult.ClosestEdge.Direction );

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
          Target        = m_collectedData.Target,
          RaycastResult = m_collectedData.RaycastResult,
          Rotation      = m_collectedData.Rotation
        };

        OnPointFound( resultingData );
        PerformRemoveFromParent();
      }

      PointVisual.Visible = m_collectedData != null && m_collectedData.RaycastResult && !m_collectedData.TriangleGiven;
      if ( PointVisual.Visible ) {
        PointVisual.SetTransform( m_collectedData.RaycastResult.Point, Quaternion.identity, 0.05f );
        TriangleVisual[ 0 ].SetTransform( m_collectedData.RaycastResult.Triangle.Vertex1,
                                          m_collectedData.RaycastResult.Triangle.Vertex2,
                                          0.05f );
        TriangleVisual[ 1 ].SetTransform( m_collectedData.RaycastResult.Triangle.Vertex2,
                                          m_collectedData.RaycastResult.Triangle.Vertex3,
                                          0.05f );
        TriangleVisual[ 2 ].SetTransform( m_collectedData.RaycastResult.Triangle.Vertex3,
                                          m_collectedData.RaycastResult.Triangle.Vertex1,
                                          0.05f );
      }
      foreach ( var cyl in TriangleVisual ) {
        cyl.Visible = PointVisual.Visible;
        cyl.Pickable = false;
        cyl.Color = Color.green;
      }
    }

    private class CollectedData
    {
      public GameObject Target   = null;
      public Utils.Raycast.Result RaycastResult;
      public Quaternion Rotation = Quaternion.identity;

      public bool TriangleGiven = false;
      public bool RotationGiven = false;
    }

    private CollectedData m_collectedData = null;
  }
}
