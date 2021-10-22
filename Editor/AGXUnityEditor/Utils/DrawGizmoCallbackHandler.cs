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

    private static Color m_transparentColor = new Color(0.5f, 0.5f, 1f, 0.1f);
    private static Color m_solidColor = new Color(0.5f, 0.5f, 1f, 0.7f);
    private static Color m_solidColorSelected = new Color(0.3f, 1f, 0.3f, 0.7f);
    private static Color m_currentColor = new Color(1f, 1f, 0f, 0.8f);
    private static Color m_lockActiveColor = new Color(1f, 0f, 0f, 0.8f);
    private static Color m_lockPassiveColor = new Color(1f, 0f, 0f, 0.3f);
    private static Color m_speedColor = new Color(0f, 1f, 0f, 0.8f);

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

      AttachmentPair pair = constraint.AttachmentPair;
      var frame = pair.ReferenceFrame;

      // TODO: do we want to visualize connected frame? Could be spheres...

      switch (constraint.Type)
      {
        case ConstraintType.Hinge:
          DrawRotationalDofGizmos(constraint, frame);
          break;

        case ConstraintType.Prismatic:
          DrawTranslationalDofGizmos(constraint, frame);
          break;

        case ConstraintType.CylindricalJoint:
          DrawTranslationalDofGizmos(constraint, frame);
          DrawRotationalDofGizmos(constraint, frame);
          break;

        case ConstraintType.BallJoint:
          DrawMeshGizmo("Debug/SphereRenderer", inSelectionHierarchy ? m_solidColorSelected : m_solidColor, frame.Position, frame.Rotation, m_scale * Vector3.one / 2f);
          break;

        default:
          DrawMeshGizmo("Debug/ConstraintRenderer", inSelectionHierarchy ? m_solidColorSelected : m_solidColor, frame.Position, frame.Rotation, m_scale * Vector3.one);
          break;
      }

      if (!pair.Synchronized && inSelectionHierarchy)
      {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pair.ReferenceFrame.Position, pair.ConnectedFrame.Position);
      }
    }

    public static void DrawRotationalDofGizmos(Constraint constraint, ConstraintFrame frame)
    {
      var rangeC = constraint.GetController<RangeController>(Constraint.ControllerType.Rotational);
      var min = Mathf.Rad2Deg * rangeC.Range.Min;
      var max = Mathf.Rad2Deg * rangeC.Range.Max;
      var normal = frame.Rotation * Vector3.forward;
      var start = Quaternion.AngleAxis(min, normal) * (frame.Rotation * Vector3.up);
      var currentAngleDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * constraint.GetCurrentAngle(Constraint.ControllerType.Rotational), normal) * (frame.Rotation * Vector3.up);
      var end = Quaternion.AngleAxis(max, normal) * (frame.Rotation * Vector3.up);
      var circleScale = m_scale * 1f;

      if (rangeC.Enable)
      {
        if (max - min > 360f)
        {
          DrawSolidAndWireDisc(frame.Position, normal, circleScale, m_transparentColor, m_solidColor);

          if (min != Mathf.NegativeInfinity)
            Handles.DrawLine(frame.Position, frame.Position + start * circleScale);
          if (max != Mathf.Infinity)
            Handles.DrawLine(frame.Position, frame.Position + end * circleScale);
        }
        else
        {
          Handles.color = m_transparentColor;
          Handles.DrawSolidArc(frame.Position, normal, start, max - min, circleScale);
          Handles.color = m_solidColor;
          Handles.DrawWireArc(frame.Position, normal, start, max - min, circleScale);

          Handles.DrawLine(frame.Position, frame.Position + start * circleScale);
          Handles.DrawLine(frame.Position, frame.Position + end * circleScale);
        }
      }
      else
        DrawSolidAndWireDisc(frame.Position, normal, circleScale, m_transparentColor, m_solidColor);

      var lockC = constraint.GetController<LockController>(Constraint.ControllerType.Rotational);
      Handles.color = lockC.Enable ? m_lockActiveColor : m_lockPassiveColor;
      var currentLockDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * lockC.Position, normal) * (frame.Rotation * Vector3.up);
      Handles.DrawLine(frame.Position, frame.Position + currentLockDirection * circleScale * 1.2f);

      Handles.color = m_currentColor;
      Handles.DrawLine(frame.Position, frame.Position + currentAngleDirection * circleScale * 1.1f);

      var speedC = constraint.GetController<TargetSpeedController>(Constraint.ControllerType.Rotational);
      if (speedC.Enable)
      {
        Handles.color = m_speedColor;
        var currentSpeedDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * constraint.GetCurrentAngle(Constraint.ControllerType.Rotational), normal) * (frame.Rotation * Vector3.left);
        var pos = frame.Position + currentAngleDirection * circleScale;
        Handles.DrawLine(pos, pos + currentSpeedDirection * speedC.Speed * Time.fixedDeltaTime * 5f);
      }

    }

    public static void DrawTranslationalDofGizmos(Constraint constraint, ConstraintFrame frame)
    {
      var rangeC = constraint.GetController<RangeController>(Constraint.ControllerType.Translational);
      var min = rangeC.Range.Min;
      var max = rangeC.Range.Max;
      var normal = frame.Rotation * Vector3.forward;
      var currentPosition = frame.Position + normal * constraint.GetCurrentAngle(Constraint.ControllerType.Translational);

      var rectScale = m_scale / 5f;

      bool drawMax = true;
      bool drawMin = true;
      if (max == Mathf.Infinity && min == Mathf.NegativeInfinity)
      {
        max = 0.5f;
        min = -0.5f;
        drawMax = drawMin = false;
      }
      else if (max == Mathf.Infinity)
      {
        max = min + 1f;
        drawMax = false;
      }
      else if (min == Mathf.NegativeInfinity)
      {
        min = max - 1f;
        drawMin = false;
      }

      float length = Mathf.Max(max - min, 0.01f);
      float offset = (max - min) / 2 - max;

      DrawMeshGizmo("Debug/CylinderRenderer", m_solidColor, frame.Position - normal * offset, frame.Rotation, new Vector3(rectScale, length / 2f, rectScale));
      Handles.color = m_solidColor;
      if (drawMin)
        DrawSolidAndWireDisc(frame.Position + normal * min, normal, rectScale * 2f, m_transparentColor, m_solidColor);
      if (drawMax)
        DrawSolidAndWireDisc(frame.Position + normal * max, normal, rectScale * 2f, m_transparentColor, m_solidColor);


      DrawSolidAndWireDisc(currentPosition, normal, rectScale * 2f, m_transparentColor, m_currentColor);

      var lockC = constraint.GetController<LockController>(Constraint.ControllerType.Translational);
      var lockPosition = frame.Position + normal * lockC.Position;
      DrawSolidAndWireDisc(lockPosition, normal, rectScale * 2.1f, m_transparentColor, lockC.Enable ? m_lockActiveColor : m_lockPassiveColor);

      var speedC = constraint.GetController<TargetSpeedController>(Constraint.ControllerType.Translational);
      if (speedC.Enable)
        DrawMeshGizmo("Debug/ConstraintRenderer", m_solidColorSelected, currentPosition + frame.Rotation * Vector3.up * rectScale * 1.8f, frame.Rotation, new Vector3(rectScale * 2, speedC.Speed / 2f, rectScale * 2));
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
      if (m_meshes.TryGetValue(resourceName, out UnityEngine.Mesh mesh))
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
