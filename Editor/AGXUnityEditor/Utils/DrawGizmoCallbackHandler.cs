using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Rendering;
using AGXUnity.Collide;
using Tool = AGXUnityEditor.Tools.Tool;

namespace AGXUnityEditor.Utils
{
  public static class DrawGizmoCallbackHandler
  {
    private static ObjectsGizmoColorHandler m_colorHandler = new ObjectsGizmoColorHandler();

    [DrawGizmo( GizmoType.Active | GizmoType.Selected )]
    public static void OnDrawGizmosCable( Cable cable, GizmoType gizmoType )
    {
      // Do not render initialized cables.
      if ( cable.Native != null )
        return;

      cable.TraverseRoutePoints( routePointData =>
      {
        var lineLength = 2.5f * cable.Radius;

        Gizmos.color = Color.red;
        Gizmos.DrawLine( routePointData.Position,
                         routePointData.Position + lineLength * ( routePointData.Rotation * Vector3.right ) );

        Gizmos.color = Color.green;
        Gizmos.DrawLine( routePointData.Position,
                         routePointData.Position + lineLength * ( routePointData.Rotation * Vector3.up ) );

        Gizmos.color = Color.blue;
        Gizmos.DrawLine( routePointData.Position,
                         routePointData.Position + lineLength * ( routePointData.Rotation * Vector3.forward ) );
      } );
    }

    [DrawGizmo( GizmoType.Active | GizmoType.Selected | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable )]
    public static void OnDrawGizmosWire( Wire wire, GizmoType gizmoType )
    {
      if ( wire.Native != null )
        return;

      var nodes = wire.Route.ToArray();
      Gizmos.color = (gizmoType & GizmoType.Selected) != 0 ? Color.green : Color.red;
      for ( int i = 1; i < nodes.Length; ++i )
        Gizmos.DrawLine( nodes[ i - 1 ].Position, nodes[ i ].Position );
    }

    [DrawGizmo( GizmoType.Active | GizmoType.NotInSelectionHierarchy )]
    public static void OnDrawGizmosDebugRenderManager( DebugRenderManager manager, GizmoType gizmoType )
    {
      if ( !manager.isActiveAndEnabled )
        return;

      HandleColorizedBodies( manager );
      HandleContacts( manager );
    }

    private static void HandleColorizedBodies( DebugRenderManager manager )
    {
      // List containing active tools decisions of what could be considered selected.
      // TODO HIGHLIGHT: Fix.
      //var toolsSelections = new List<Tool.VisualizedSelectionData>();

      // Active assembly tool has special rendering needs.
      Tools.AssemblyTool assemblyTool = null;

      ToolManager.Traverse<Tool>( activeTool =>
      {
        if ( assemblyTool == null && activeTool is Tools.AssemblyTool )
          assemblyTool = activeTool as Tools.AssemblyTool;

        // TODO HIGHLIGHT: Fix.
        //if ( activeTool.VisualizedSelection != null && !toolsSelections.Contains( activeTool.VisualizedSelection ) )
        //  toolsSelections.Add( activeTool.VisualizedSelection );
      } );

      bool highlightMouseOverObject = manager.HighlightMouseOverObject;

      // Find if we've any active selections.
      bool selectionActive = ( highlightMouseOverObject && Manager.MouseOverObject != null ) ||
                             ( assemblyTool != null && assemblyTool.HasActiveSelections() ) ||
                             Array.Exists( Selection.gameObjects, go =>
                                                                  {
                                                                    return go.GetComponent<Shape>() != null || go.GetComponent<RigidBody>() != null;
                                                                  } );
      if ( !selectionActive )
        m_colorHandler.TimeInterpolator.Reset();

      // Early exist if we're not visualizing the bodies nor have active selections.
      bool active = manager.ColorizeBodies || selectionActive || assemblyTool != null;
      if ( !active )
        return;

      try {
        using ( m_colorHandler.BeginEndScope() ) {
          // Create unique colors for each rigid body in the scene.
          {
            var bodies = UnityEngine.Object.FindObjectsOfType<RigidBody>();
            Array.Sort( bodies, ( b1, b2 ) => { return b1.GetInstanceID() > b2.GetInstanceID() ? -1 : 1; } );

            foreach ( var body in bodies ) {
              // Create the color for all bodies for the colors to be consistent.
              m_colorHandler.GetOrCreateColor( body );

              if ( manager.ColorizeBodies && body.enabled && body.gameObject.activeInHierarchy )
                m_colorHandler.Colorize( body );
            }
          }

          // An active assembly tool will (atm) render objects in a different
          // way and, e.g., render colorized bodies despite manager.VisualizeBodies.
          if ( assemblyTool != null )
            assemblyTool.OnRenderGizmos( m_colorHandler );

          // Handling objects selected in the editor.
          {
            GameObject[] editorSelections = Selection.gameObjects;
            foreach ( var editorSelection in editorSelections )
              HandleSelectedGameObject( editorSelection, ObjectsGizmoColorHandler.SelectionType.ConstantColor );
          }

          // Handling objects selected in our tools.
          //{
          //  foreach ( var toolSelection in toolsSelections )
          //    HandleSelectedGameObject( toolSelection.Object, ObjectsGizmoColorHandler.SelectionType.VaryingIntensity );
          //}

          if ( highlightMouseOverObject )
            HandleSelectedGameObject( Manager.MouseOverObject, ObjectsGizmoColorHandler.SelectionType.VaryingIntensity );

          foreach ( var filterColorPair in m_colorHandler.ColoredMeshFilters ) {
            Gizmos.color = filterColorPair.Value;
            Gizmos.matrix = filterColorPair.Key.transform.localToWorldMatrix;
            Gizmos.DrawWireMesh( filterColorPair.Key.sharedMesh );
          }

          Gizmos.matrix = Matrix4x4.identity;
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }
    }

    private static void HandleSelectedGameObject( GameObject selected, ObjectsGizmoColorHandler.SelectionType selectionType )
    {
      if ( selected == null || !selected.activeInHierarchy )
        return;

      RigidBody rb      = null;
      Shape shape       = null;
      MeshFilter filter = null;
      if ( ( rb = selected.GetComponent<RigidBody>() ) != null ) {
        if ( rb.isActiveAndEnabled )
          m_colorHandler.Highlight( rb, selectionType );
      }
      else if ( ( shape = selected.GetComponent<Shape>() ) != null ) {
        if ( shape.IsEnabledInHierarchy )
          m_colorHandler.Highlight( shape, selectionType );
      }
      else if ( ( filter = selected.GetComponent<MeshFilter>() ) != null ) {
        m_colorHandler.Highlight( filter, selectionType );
      }
    }

    private static void HandleContacts( DebugRenderManager manager )
    {
      if ( !manager.RenderContacts )
        return;

      Gizmos.color = manager.ContactColor;
      foreach ( var contact in manager.ContactList ) {
        Gizmos.DrawMesh( Constraint.GetOrCreateGizmosMesh(),
                         contact.Point,
                         Quaternion.FromToRotation( Vector3.up, contact.Normal ),
                         manager.ContactScale * Spawner.Utils.FindConstantScreenSizeScale( contact.Point, Camera.current ) * Vector3.one );
      }
    }
  }
}
