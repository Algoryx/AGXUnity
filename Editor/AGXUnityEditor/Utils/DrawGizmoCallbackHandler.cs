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

    // TODO temporary, see below
    private static DebugRenderManager m_settingsObject = null;
    private static float m_scale = 0.3f;

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.InSelectionHierarchy)]
    public static void OnDrawGizmosConstraint(Constraint constraint, GizmoType gizmoType)
    {
      if (constraint.Native != null)
        return;

      //TODO: this should be in some kind of settings object - where? DebugRenderManager (get instance or use defaults) or own object? ScriptableObject could be one solution, even though we are not currently using them...
      if (!m_settingsObject)
        m_settingsObject = GameObject.FindObjectOfType<DebugRenderManager>();
      if (m_settingsObject)
        m_scale = m_settingsObject.ConstraintGizmoScale;

      bool inSelectionHierarchy = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
      bool selected = (gizmoType & GizmoType.Selected) != 0;

      Color transparentColor = new Color(0.5f, 0.5f, 1f, 0.1f);
      Color solidColor = new Color(0.5f, 0.5f, 1f, 0.7f);

      AttachmentPair pair = constraint.AttachmentPair;
      var frame = pair.ReferenceFrame;

      switch (constraint.Type)
      {
        case ConstraintType.Hinge:
          var range = constraint.GetController<RangeController>();
          var min = Mathf.Rad2Deg * range.Range.Min;
          var max = Mathf.Rad2Deg * range.Range.Max;

          var circleScale = m_scale * 1.5f;

          if (range.Enable)
          {
            var normal = frame.Rotation * Vector3.forward;
            var start = Quaternion.AngleAxis(min, normal) * (frame.Rotation * Vector3.up);
            var end = Quaternion.AngleAxis(max, normal) * (frame.Rotation * Vector3.up);
            if (max - min > 360f)
            {
              Handles.color = transparentColor;
              Handles.DrawSolidDisc(frame.Position, frame.Rotation * Vector3.forward, circleScale);
              Handles.color = solidColor;
              Handles.DrawWireDisc(frame.Position, frame.Rotation * Vector3.forward, circleScale);
              if (min != Mathf.NegativeInfinity)
                Handles.DrawLine(frame.Position, frame.Position + start * circleScale);
              if (max != Mathf.Infinity)
                Handles.DrawLine(frame.Position, frame.Position + end * circleScale);
            }
            else
            {
              Handles.color = transparentColor;
              Handles.DrawSolidArc(frame.Position, normal, start, max - min, circleScale);
              Handles.color = solidColor;
              Handles.DrawWireArc(frame.Position, normal, start, max - min, circleScale);
              Handles.DrawLine(frame.Position, frame.Position + start * circleScale);
              Handles.DrawLine(frame.Position, frame.Position + end * circleScale);
            }
          }
          else
          {
            Handles.color = transparentColor;
            Handles.DrawSolidDisc(frame.Position, frame.Rotation * Vector3.forward, circleScale);
            Handles.color = solidColor;
            Handles.DrawWireDisc(frame.Position, frame.Rotation * Vector3.forward, circleScale);
          }
          break;
      }

      DrawArrowGizmo(inSelectionHierarchy ? Color.green : solidColor, pair, inSelectionHierarchy);
    }

    private static UnityEngine.Mesh m_gizmoMesh = null;
    private static Material m_gizmoMaterial = null;
    public static UnityEngine.Mesh GetOrCreateArrowGizmoMesh()
    {
      if (m_gizmoMesh != null)
        return m_gizmoMesh;

      GameObject tmp = Resources.Load<GameObject>(@"Debug/ConstraintRenderer");
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      MeshRenderer[] renderers = tmp.GetComponentsInChildren<MeshRenderer>();
      CombineInstance[] combine = new CombineInstance[filters.Length];

      for (int i = 0; i < filters.Length; ++i)
      {
        combine[i].mesh = filters[i].sharedMesh;
        combine[i].transform = filters[i].transform.localToWorldMatrix;
      }

      m_gizmoMesh = new UnityEngine.Mesh();
      m_gizmoMesh.CombineMeshes(combine);

      m_gizmoMaterial = renderers[0].sharedMaterial;

      return m_gizmoMesh;
    }

    private static void DrawArrowGizmo(Color color, AttachmentPair attachmentPair, bool selected)
    {
      Gizmos.color = color;
      //Gizmos.DrawMesh(GetOrCreateArrowGizmoMesh(),
      //                 attachmentPair.ReferenceFrame.Position,
      //                 attachmentPair.ReferenceFrame.Rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward),
      //                 m_scale * Vector3.one);// * Spawner.Utils.FindConstantScreenSizeScale(attachmentPair.ReferenceFrame.Position, Camera.current) * Vector3.one);

      Matrix4x4 matrixTRS = Matrix4x4.TRS(attachmentPair.ReferenceFrame.Position, attachmentPair.ReferenceFrame.Rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward), m_scale * Vector3.one);
      GetOrCreateArrowGizmoMesh();
      m_gizmoMaterial.SetPass(0);
      Graphics.DrawMeshNow(m_gizmoMesh, matrixTRS);

      if (!attachmentPair.Synchronized && selected)
      {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(attachmentPair.ReferenceFrame.Position, attachmentPair.ConnectedFrame.Position);
      }
    }

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

      // Early exit if we're not visualizing the bodies nor have active selections.
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
        if ( rb.IsEnabled )
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
        Gizmos.DrawMesh( GetOrCreateArrowGizmoMesh(),
                         contact.Point,
                         Quaternion.FromToRotation( Vector3.up, contact.Normal ),
                         manager.ContactScale * Spawner.Utils.FindConstantScreenSizeScale( contact.Point, Camera.current ) * Vector3.one );
      }
    }
  }
}
