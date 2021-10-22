﻿using System;
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

    private static Dictionary<string, UnityEngine.Mesh> m_meshes = new Dictionary<string, UnityEngine.Mesh>();
    private static Material m_gizmoMaterial = null;

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.InSelectionHierarchy)]
    public static void OnDrawGizmosConstraint(Constraint constraint, GizmoType gizmoType)
    {
      //if (constraint.Native != null)
      //  return;

      //TODO: this should be in some kind of settings object - where? DebugRenderManager (get instance or use defaults) or own object? ScriptableObject could be one solution, even though we are not currently using them...
      if (!m_settingsObject)
        m_settingsObject = GameObject.FindObjectOfType<DebugRenderManager>();
      if (m_settingsObject)
      {
        if (!m_settingsObject.ConstraintGizmos)
          return;

        m_scale = m_settingsObject.ConstraintGizmoScale;
      }

      bool inSelectionHierarchy = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
      bool selected = (gizmoType & GizmoType.Selected) != 0;

      Color transparentColor = new Color(0.5f, 0.5f, 1f, 0.1f);
      Color solidColor = new Color(0.5f, 0.5f, 1f, 0.7f);
      Color solidColorSelected = new Color(0.3f, 1f, 0.3f, 0.7f);
      Color currentColor = new Color(1f, 1f, 0f, 0.8f);
      Color lockActiveColor = new Color(1f, 0f, 0f, 0.8f);
      Color lockPassiveColor = new Color(1f, 0f, 0f, 0.2f);
      Color speedColor = new Color(0f, 1f, 0f, 0.8f);

      AttachmentPair pair = constraint.AttachmentPair;
      var frame = pair.ReferenceFrame;

      // TODO: do we want to visualize connected frame? Could be spheres...

      switch (constraint.Type)
      {
        case ConstraintType.Hinge:
          // TODO: this should be separated into a separate function so we can reuse it for other constraints that have rotational DOFs
          var range = constraint.GetController<RangeController>();
          var min = Mathf.Rad2Deg * range.Range.Min;
          var max = Mathf.Rad2Deg * range.Range.Max;
          var normal = frame.Rotation * Vector3.forward;
          var start = Quaternion.AngleAxis(min, normal) * (frame.Rotation * Vector3.up);
          var currentAngleDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * constraint.GetCurrentAngle(), normal) * (frame.Rotation * Vector3.up);
          var end = Quaternion.AngleAxis(max, normal) * (frame.Rotation * Vector3.up);
          var circleScale = m_scale * 1.5f;

          if (range.Enable)
          {
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

          var lockC = constraint.GetController<LockController>();
          Handles.color = lockC.Enable ? lockActiveColor : lockPassiveColor;
          var currentLockDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * lockC.Position, normal) * (frame.Rotation * Vector3.up);
          Handles.DrawLine(frame.Position, frame.Position + currentLockDirection * circleScale * 1.2f);

          // Current position
          Handles.color = currentColor;
          Handles.DrawLine(frame.Position, frame.Position + currentAngleDirection * circleScale * 1.1f);

          var speedC = constraint.GetController<TargetSpeedController>();
          if (speedC.Enable)
          {
            Handles.color = speedColor;
            var currentSpeedDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * constraint.GetCurrentAngle(), normal) * (frame.Rotation * Vector3.left);
            var pos = frame.Position + currentAngleDirection * circleScale;
            Handles.DrawLine(pos, pos + currentSpeedDirection * speedC.Speed * Time.fixedDeltaTime * 5f);
          }
          break;

        case ConstraintType.Prismatic:
          range = constraint.GetController<RangeController>();
          min = range.Range.Min;
          max = range.Range.Max;
          normal = frame.Rotation * Vector3.forward;
          var currentPosition = frame.Position + normal * constraint.GetCurrentAngle();

          var rectScale = m_scale / 10f;

          float length = Mathf.Max(max - min, 0.01f);
          float offset = (max - min) / 2 - max;
          if (length == Mathf.Infinity)  // TODO: handle this case by drawing one end if there is one, and the other as an arrow or something
          {
            length = 1f;
            offset = 0;
          }

          DrawMeshGizmo("Debug/CylinderRenderer", solidColor, frame.Position - normal * offset, frame.Rotation, new Vector3(rectScale, length / 2f, rectScale));
          Handles.color = solidColor;
          if (min != Mathf.NegativeInfinity)
            DrawSolidAndWireDisc(frame.Position + normal * min, normal, rectScale * 2f, transparentColor, solidColor);
          if (max != Mathf.Infinity)
            DrawSolidAndWireDisc(frame.Position + normal * max, normal, rectScale * 2f, transparentColor, solidColor);


          DrawSolidAndWireDisc(currentPosition, normal, rectScale * 2f, transparentColor, currentColor);

          lockC = constraint.GetController<LockController>();
          var lockPosition = frame.Position + normal * lockC.Position;
          DrawSolidAndWireDisc(lockPosition, normal, rectScale * 2.1f, transparentColor, lockC.Enable ? lockActiveColor : lockPassiveColor);

          speedC = constraint.GetController<TargetSpeedController>();
          if (speedC.Enable)
            DrawMeshGizmo("Debug/ConstraintRenderer", solidColorSelected, currentPosition + frame.Rotation * Vector3.up * rectScale * 1.8f, frame.Rotation, new Vector3(rectScale * 2, speedC.Speed / 2f, rectScale * 2));
          break;

        default:
          DrawMeshGizmo("Debug/ConstraintRenderer", inSelectionHierarchy ? solidColorSelected : solidColor, frame.Position, frame.Rotation, m_scale * Vector3.one);
          break;
      }

      if (!pair.Synchronized && inSelectionHierarchy)
      {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pair.ReferenceFrame.Position, pair.ConnectedFrame.Position);
      }
    }

    public static void DrawSolidAndWireDisc(Vector3 position, Vector3 normal, float radius, Color discColor, Color wireColor)
    {
      Handles.color = discColor;
      Handles.DrawSolidDisc(position, normal, radius);
      Handles.color = wireColor;
      Handles.DrawWireDisc(position, normal, radius);
    }

    public static UnityEngine.Mesh GetOrCreateGizmoMesh(string resourceName)
    {
      UnityEngine.Mesh mesh;
      if (m_meshes.TryGetValue(resourceName, out mesh))
      {
        if (mesh == null)
          m_meshes = new Dictionary<string, UnityEngine.Mesh>(); // Better luck next time
        return mesh;
      }

      GameObject tmp = Resources.Load<GameObject>(@resourceName);
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      CombineInstance[] combine = new CombineInstance[filters.Length];

      for (int i = 0; i < filters.Length; ++i)
      {
        combine[i].mesh = filters[i].sharedMesh;
        combine[i].transform = filters[i].transform.localToWorldMatrix;
      }

      mesh = new UnityEngine.Mesh();
      mesh.CombineMeshes(combine);

      m_meshes.Add(resourceName, mesh);

      return mesh;
    }

    // We want to render gizmo meshes with a material that has a shader that draws on top of other materials...
    public static Material GetOrCreateGizmoMaterial()
    {
      if (m_gizmoMaterial != null)
        return m_gizmoMaterial;

      GameObject tmp = Resources.Load<GameObject>(@"Debug/ConstraintRenderer");
      MeshRenderer[] renderers = tmp.GetComponentsInChildren<MeshRenderer>();
      m_gizmoMaterial = new Material(renderers[0].sharedMaterial); // We just want a copy of the material, in order to change the color but not make changes to the original material

      return m_gizmoMaterial;
    }

    private static void DrawMeshGizmo(string resourceName, Color color, Vector3 position, Quaternion rotation, Vector3 scale)
    {
      //Gizmos.color = color;
      //Gizmos.DrawMesh(GetOrCreateArrowGizmoMesh(),
      //                 attachmentPair.ReferenceFrame.Position,
      //                 attachmentPair.ReferenceFrame.Rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward),
      //                 m_scale * Vector3.one);// * Spawner.Utils.FindConstantScreenSizeScale(attachmentPair.ReferenceFrame.Position, Camera.current) * Vector3.one);

      Matrix4x4 matrixTRS = Matrix4x4.TRS(position, rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward), scale);
      UnityEngine.Mesh mesh = GetOrCreateGizmoMesh(resourceName);
      if (mesh == null)
        return;

      var material = GetOrCreateGizmoMaterial();
      material.color = color;
      material.SetPass(0);

      Graphics.DrawMeshNow(mesh, matrixTRS);
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
        Gizmos.DrawMesh( GetOrCreateGizmoMesh("Debug/ConstraintRenderer"),
                         contact.Point,
                         Quaternion.FromToRotation( Vector3.up, contact.Normal ),
                         manager.ContactScale * Spawner.Utils.FindConstantScreenSizeScale( contact.Point, Camera.current ) * Vector3.one );
      }
    }
  }
}
