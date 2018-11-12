using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

namespace AGXUnityEditor.Tools
{
  public class PickHandlerTool : Tool
  {
    public GameObject ConstraintGameObject { get; private set; }
    public Constraint Constraint { get { return ConstraintGameObject != null ? ConstraintGameObject.GetComponent<Constraint>() : null; } }
    public PickHandler.DofTypes ConstrainedDofTypes { get; private set; }

    public PickHandlerTool( PickHandler.DofTypes constrainedDofTypes, Predicate<Event> removePredicate )
    {
      if ( removePredicate == null )
        throw new ArgumentNullException( "removePredicate", "When to remove callback is null - undefined." );

      ConstrainedDofTypes = constrainedDofTypes;
      m_removePredicate = removePredicate;
    }

    public override void OnAdd()
    {
      Initialize();
    }

    public override void OnRemove()
    {
      if ( ConstraintGameObject != null )
        GameObject.DestroyImmediate( ConstraintGameObject );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      // Remove us if the constraint never were created or at the mouse up event.
      if ( ConstraintGameObject == null || m_removePredicate( Event.current ) ) {
        PerformRemoveFromParent();
        return;
      }

      Constraint constraint = Constraint;
      UpdateVisual( constraint );

      // NOTE: camera.ScreenToWorldPoint is not stable during all types of events. Pick one!
      if ( !Event.current.isMouse )
        return;

      constraint.AttachmentPair.ConnectedFrame.Position = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ).GetPoint( m_distanceFromCamera );

      PickHandler.SetComplianceDamping( constraint );
    }

    private float m_distanceFromCamera = -1f;
    private Predicate<Event> m_removePredicate = null;

    private Utils.VisualPrimitiveSphere VisualSphereReference { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "reference", "Standard" ); } }
    private Utils.VisualPrimitiveSphere VisualSphereConnected { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "connected", "Standard" ); } }
    private Utils.VisualPrimitiveCylinder VisualCylinder { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( "cylinder", "Standard" ); } }

    private void Initialize()
    {
      if ( Manager.MouseOverObject == null )
        return;

      ConstraintGameObject = PickHandler.TryCreateConstraint( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ),
                                                              Manager.MouseOverObject,
                                                              ConstrainedDofTypes,
                                                              "PickHandlerToolConstraint" );
      if ( ConstraintGameObject == null )
        return;

      m_distanceFromCamera = PickHandler.FindDistanceFromCamera( SceneView.currentDrawingSceneView.camera, Constraint.AttachmentPair.ReferenceFrame.Position );

      Constraint.DrawGizmosEnable = false;

      VisualSphereReference.Color = PickHandler.ReferenceSphereColor;
      VisualSphereConnected.Color = PickHandler.ConnectedSphereColor;
      VisualCylinder.Color        = PickHandler.ConnectingCylinderColor;

      VisualSphereReference.Pickable = false;
      VisualSphereConnected.Pickable = false;
      VisualCylinder.Pickable        = false;

      PickHandler.SetComplianceDamping( Constraint );
    }

    private void UpdateVisual( Constraint constraint )
    {
      if ( constraint.Type == ConstraintType.AngularLockJoint )
        return;

      const float sphereRadius   = 0.05f;
      const float cylinderRadius = 0.5f * sphereRadius;

      VisualSphereReference.Visible = true;
      VisualSphereConnected.Visible = true;
      VisualCylinder.Visible        = true;

      VisualSphereReference.SetTransform( constraint.AttachmentPair.ReferenceFrame.Position, Quaternion.identity, sphereRadius );
      VisualSphereConnected.SetTransform( constraint.AttachmentPair.ConnectedFrame.Position, Quaternion.identity, sphereRadius );
      VisualCylinder.SetTransform( constraint.AttachmentPair.ReferenceFrame.Position, constraint.AttachmentPair.ConnectedFrame.Position, cylinderRadius );
    }
  }
}
