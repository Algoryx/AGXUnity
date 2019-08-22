using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AGXUnity.Collide;
using AGXUnity.Utils;

namespace AGXUnityEditor.Tools
{
  /// <summary>
  /// Finds target, edge and position on an object.
  /// </summary>
  public class EdgeDetectionTool : Tool
  {
    /// <summary>
    /// Resulting data when this tool is done.
    /// </summary>
    public class Result
    {
      /// <summary>
      /// Target game object.
      /// </summary>
      public GameObject Target = null;

      /// <summary>
      /// Edge on the target.
      /// </summary>
      public AGXUnity.Edge Edge = new AGXUnity.Edge();

      /// <summary>
      /// Position on the edge.
      /// </summary>
      public Vector3 Position = Vector3.zero;

      /// <summary>
      /// Rotation of the edge.
      /// </summary>
      public Quaternion Rotation = Quaternion.identity;
    }
    
    /// <summary>
    /// Callback when all data has been collected.
    /// </summary>
    public Action<Result> OnEdgeFound = delegate { };

    /// <summary>
    /// Default constructor.
    /// </summary>
    public EdgeDetectionTool()
    {
      EdgeVisual.OnMouseClick += OnEdgeClick;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( EdgeVisual.Visible && EdgeVisual.MouseOver )
        return;

      // 1. Find target game object.
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
      // 2. Select edge on target game object.
      else if ( !m_collectedData.SelectedEdge.Valid ) {
        var result = Utils.Raycast.Intersect( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ),
                                              m_collectedData.Target );

        if ( result )
          m_collectedData.CurrentEdge = result.ClosestEdge;
      }
      // 3. Find point on edge - hold ctrl for "no-snap" mode.
      else if ( !m_collectedData.PointOnEdgeGiven ) {
        Vector3 pointOnEdge = FindClosestPointOnEdge( m_collectedData.SelectedEdge );

        if ( Event.current.control )
          m_collectedData.PointOnEdge = pointOnEdge;
        else {
          float snapValue            = 0.5f * HandleUtility.GetHandleSize( pointOnEdge );
          float closestDistance      = float.PositiveInfinity;
          Vector3 closestPoint       = pointOnEdge;
          Vector3[] predefinedPoints = FindPredefinedEdgePoints( m_collectedData.SelectedEdge ).ToArray();
          // Given set of predefined points along the edge, finds the
          // closest to the mouse ray (i.e., the actual point on the edge).
          foreach ( var point in predefinedPoints ) {
            float distanceToPoint = Vector3.Distance( pointOnEdge, point );
            if ( distanceToPoint < snapValue && distanceToPoint < closestDistance ) {
              closestPoint = point;
              closestDistance = distanceToPoint;
            }
          }

          m_collectedData.PointOnEdge = closestPoint;
        }
      }
      // 4. Find direction.
      else if ( !m_collectedData.DirectionGiven ) {
        if ( GetChild<DirectionTool>() == null ) {
          DirectionTool directionTool = new DirectionTool( m_collectedData.PointOnEdge,
                                                           m_collectedData.SelectedEdge.Direction,
                                                           m_collectedData.SelectedEdge.Normal );
          directionTool.OnSelect += ( position, rotation ) =>
          {
            m_collectedData.DirectionRotation = rotation;
            m_collectedData.DirectionGiven = true;
          };
          AddChild( directionTool );
        }
      }
      // 5. Done, fire callback with result and remove us.
      else {
        var orgEdge = m_collectedData.SelectedEdge;
        var resultingData = new Result()
        {
          Target   = m_collectedData.Target,
          Edge     = new AGXUnity.Edge()
          {
            Start  = m_collectedData.PointOnEdge + 0.5f * orgEdge.Length * ( m_collectedData.DirectionRotation * Vector3.back ),
            End    = m_collectedData.PointOnEdge + 0.5f * orgEdge.Length * ( m_collectedData.DirectionRotation * Vector3.forward ),
            Normal = m_collectedData.DirectionRotation * Vector3.up,
            Type   = AGXUnity.Edge.EdgeType.Triangle
          },
          Position = m_collectedData.PointOnEdge,
          Rotation = m_collectedData.DirectionRotation
        };

        OnEdgeFound( resultingData );

        PerformRemoveFromParent();

        return;
      }

      EdgeVisual.Visible = m_collectedData != null && m_collectedData.CurrentEdge.Valid;
      if ( EdgeVisual.Visible ) {
        const float edgeRadius     = 0.035f;
        const float defaultAlpha   = 0.25f;
        const float mouseOverAlpha = 0.65f;

        EdgeVisual.SetTransform( m_collectedData.CurrentEdge.Start, m_collectedData.CurrentEdge.End, edgeRadius );

        if ( m_collectedData.CurrentEdge.Type == AGXUnity.Edge.EdgeType.Triangle ) {
          EdgeVisual.Color = new Color( Color.yellow.r, Color.yellow.g, Color.yellow.b, defaultAlpha );
          EdgeVisual.MouseOverColor = new Color( Color.yellow.r, Color.yellow.g, Color.yellow.b, mouseOverAlpha );
        }
        else if ( m_collectedData.CurrentEdge.Type == AGXUnity.Edge.EdgeType.Principal ) {
          EdgeVisual.Color = new Color( Color.red.r, Color.red.g, Color.red.b, defaultAlpha );
          EdgeVisual.MouseOverColor = new Color( Color.red.r, Color.red.g, Color.red.b, mouseOverAlpha );
        }
      }

      NodeVisual.Visible = EdgeVisual.Visible && m_collectedData.SelectedEdge.Valid;
      if ( NodeVisual.Visible ) {
        const float nodeRadius = 0.040f;

        NodeVisual.SetTransform( m_collectedData.PointOnEdge, Quaternion.identity, nodeRadius );

        // The user doesn't have to hit the node sphere.
        if ( Manager.HijackLeftMouseClick() )
          OnPointClick( null, NodeVisual );
      }
    }

    /// <summary>
    /// Object holding the current state of the process.
    /// </summary>
    private class CollectedData
    {
      public GameObject Target                   = null;
      public AGXUnity.Edge SelectedEdge;

      public Vector3 PointOnEdge                 = Vector3.zero;
      public Quaternion DirectionRotation        = Quaternion.identity;

      public AGXUnity.Edge CurrentEdge;
      public bool PointOnEdgeGiven               = false;
      public bool DirectionGiven                 = false;
    }

    private CollectedData m_collectedData = null;

    /// <summary>
    /// Visual representation of the edge.
    /// </summary>
    private Utils.VisualPrimitiveCylinder EdgeVisual { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "edgeVisual", "GUI/Text Shader" ); } }

    /// <summary>
    /// Visual representation of the node on the edge.
    /// </summary>
    private Utils.VisualPrimitiveSphere NodeVisual { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "nodeVisual", "GUI/Text Shader" ); } }

    /// <summary>
    /// Callback when the edge has been picked.
    /// </summary>
    private void OnEdgeClick( Utils.Raycast.Result result, Utils.VisualPrimitive primitive )
    {
      m_collectedData.SelectedEdge = m_collectedData.CurrentEdge;
      EdgeVisual.Pickable          = false;
      NodeVisual.Pickable          = false;
    }

    /// <summary>
    /// Callback when the node/position has been picked.
    /// </summary>
    private void OnPointClick( Raycast.Hit hit, Utils.VisualPrimitive primitive )
    {
      m_collectedData.PointOnEdgeGiven = true;
    }

    /// <summary>
    /// Finds point on edge given mouse ray.
    /// </summary>
    private Vector3 FindClosestPointOnEdge( AGXUnity.Edge edge )
    {
      var ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );
      return ShapeUtils.ShortestDistanceSegmentSegment( ray.origin,
                                                        ray.GetPoint( 500f ),
                                                        edge.Start,
                                                        edge.End ).PointOnSegment2;
    }

    /// <summary>
    /// Finds a set of predefined points on an edge. Normally it's only
    /// three - start, middle and end. If the edge type is "principal"
    /// and the target has a Collide.Shape, additional points at the surface
    /// of the shape may appear.
    /// </summary>
    /// <param name="edge">Edge to find predefined points on.</param>
    /// <returns>Iterator to point on the edge.</returns>
    private IEnumerable<Vector3> FindPredefinedEdgePoints( AGXUnity.Edge edge )
    {
      yield return edge.Start;
      yield return edge.Center;
      yield return edge.End;

      if ( edge.Type == AGXUnity.Edge.EdgeType.Triangle || m_collectedData == null || m_collectedData.Target == null || m_collectedData.Target.GetComponent<Shape>() == null )
        yield break;

      var utils = m_collectedData.Target.GetComponent<Shape>().GetUtils();
      if ( utils == null )
        yield break;

      var edgeDirections = ShapeUtils.ToDirection( ShapeUtils.ToPrincipal( utils.FindDirectionGivenWorldEdge( edge ) ) );
      yield return utils.GetWorldFace( edgeDirections[ 0 ] );
      yield return utils.GetWorldFace( edgeDirections[ 1 ] );
    }
  }
}
