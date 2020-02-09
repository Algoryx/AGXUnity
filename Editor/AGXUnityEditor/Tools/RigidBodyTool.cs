using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AGXUnity;
using AGXUnity.Collide;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RigidBody ) )]
  public class RigidBodyTool : CustomTargetTool
  {
    private List<Constraint> m_constraints = new List<Constraint>();

    public RigidBody RigidBody
    {
      get
      {
        return Targets[ 0 ] as RigidBody;
      }
    }

    public bool FindTransformGivenPointTool
    {
      get { return GetChild<FindPointTool>() != null; }
      set
      {
        if ( value && !FindTransformGivenPointTool ) {
          RemoveAllChildren();

          var pointTool = new FindPointTool();
          pointTool.OnPointFound = data =>
          {
            Undo.RecordObject( RigidBody.transform, "Rigid body transform" );

            RigidBody.transform.position = data.RaycastResult.Point;
            RigidBody.transform.rotation = data.Rotation;

            EditorUtility.SetDirty( RigidBody );
          };

          AddChild( pointTool );
        }
        else if ( !value )
          RemoveChild( GetChild<FindPointTool>() );
      }
    }

    public bool FindTransformGivenEdgeTool
    {
      get { return GetChild<EdgeDetectionTool>() != null; }
      set
      {
        if ( value && !FindTransformGivenEdgeTool ) {
          RemoveAllChildren();

          var edgeTool = new EdgeDetectionTool();
          edgeTool.OnEdgeFound = data =>
          {
            Undo.RecordObject( RigidBody.transform, "Rigid body transform" );

            RigidBody.transform.position = data.Position;
            RigidBody.transform.rotation = data.Rotation;

            EditorUtility.SetDirty( RigidBody );
          };

          AddChild( edgeTool );
        }
        else if ( !value )
          RemoveChild( GetChild<EdgeDetectionTool>() );
      }
    }

    public bool ShapeCreateTool
    {
      get { return GetChild<ShapeCreateTool>() != null; }
      set
      {
        if ( value && !ShapeCreateTool ) {
          RemoveAllChildren();

          var shapeCreateTool = new ShapeCreateTool( RigidBody.gameObject );
          AddChild( shapeCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ShapeCreateTool>() );
      }
    }

    public bool ConstraintCreateTool
    {
      get { return GetChild<ConstraintCreateTool>() != null; }
      set
      {
        if ( value && !ConstraintCreateTool ) {
          RemoveAllChildren();

          var constraintCreateTool = new ConstraintCreateTool( RigidBody.gameObject,
                                                               false,
                                                               newConstraint => m_constraints.Add( newConstraint ) );
          AddChild( constraintCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ConstraintCreateTool>() );
      }
    }

    public bool DisableCollisionsTool
    {
      get { return GetChild<DisableCollisionsTool>() != null; }
      set
      {
        if ( value && !DisableCollisionsTool ) {
          RemoveAllChildren();

          var disableCollisionsTool = new DisableCollisionsTool( RigidBody.gameObject );
          AddChild( disableCollisionsTool );
        }
        else if ( !value )
          RemoveChild( GetChild<DisableCollisionsTool>() );
      }
    }

    public bool RigidBodyVisualCreateTool
    {
      get { return GetChild<RigidBodyVisualCreateTool>() != null; }
      set
      {
        if ( value && !RigidBodyVisualCreateTool ) {
          RemoveAllChildren();

          var createRigidBodyVisualTool = new RigidBodyVisualCreateTool( RigidBody );
          AddChild( createRigidBodyVisualTool );
        }
        else if ( !value )
          RemoveChild( GetChild<RigidBodyVisualCreateTool>() );
      }
    }

    public bool ToolsActive = true;

    public RigidBodyTool( Object[] targets )
      : base( targets )
    {
#if UNITY_2019_1_OR_NEWER
      var allConstraints = StageUtility.GetCurrentStageHandle().Contains( RigidBody.gameObject ) ?
                             StageUtility.GetCurrentStageHandle().FindComponentsOfType<Constraint>() :
                             Object.FindObjectsOfType<Constraint>();
#else
      var allConstraints = Object.FindObjectsOfType<Constraint>();
#endif
      foreach ( var constraint in allConstraints ) {
        foreach ( var rb in GetTargets<RigidBody>() )
          if ( constraint.AttachmentPair.Contains( rb ) )
            m_constraints.Add( constraint );
      }
    }

    public override void OnAdd()
    {
      foreach ( var rb in GetTargets<RigidBody>() ) {
        var forceUpdateMassProperties = ( rb.MassProperties.Mass.UseDefault &&
                                          rb.MassProperties.Mass.Value == 1.0f ) ||
                                        ( rb.MassProperties.InertiaDiagonal.UseDefault &&
                                          rb.MassProperties.InertiaDiagonal.Value == Vector3.one );
        if ( forceUpdateMassProperties )
          rb.MassProperties.OnForcedMassInertiaUpdate();
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      bool toggleFindTransformGivenPoint = false;
      bool toggleFindTransformGivenEdge  = false;
      bool toggleShapeCreate             = false;
      bool toggleConstraintCreate        = false;
      bool toggleDisableCollisions       = false;
      bool toggleRigidBodyVisualCreate   = false;

      if ( !IsMultiSelect && ToolsActive ) {
        InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( "agx_unity_given point 1",
                                                                      FindTransformGivenPointTool,
                                                                      "Find rigid body transform given point on object.",
                                                                      () => toggleFindTransformGivenPoint = true ),
                                  InspectorGUI.ToolButtonData.Create( "agx_unity_given edge 1",
                                                                      FindTransformGivenEdgeTool,
                                                                      "Find rigid body transform given edge on object.",
                                                                      () => toggleFindTransformGivenEdge = true ),
                                  InspectorGUI.ToolButtonData.Create( "agx_unity_represent shape 1",
                                                                      ShapeCreateTool,
                                                                      "Create shape from child visual object.",
                                                                      () => toggleShapeCreate = true ),
                                  InspectorGUI.ToolButtonData.Create( "agx_unity_hinge 2",
                                                                      ConstraintCreateTool,
                                                                      "Create new constraint to this rigid body.",
                                                                      () => toggleConstraintCreate = true ),
                                  InspectorGUI.ToolButtonData.Create( "agx_unity_disable collision 1",
                                                                      DisableCollisionsTool,
                                                                      "Disable collisions against other objects.",
                                                                      () => toggleDisableCollisions = true ),
                                  InspectorGUI.ToolButtonData.Create( "agx_unity_represent shape 3",
                                                                      RigidBodyVisualCreateTool,
                                                                      "Create visual representation of each physical shape in this body.",
                                                                      () => toggleRigidBodyVisualCreate = true,
                                                                      Tools.RigidBodyVisualCreateTool.ValidForNewShapeVisuals( RigidBody ) ) );
      }

      if ( ShapeCreateTool ) {
        GetChild<ShapeCreateTool>().OnInspectorGUI();
      }
      if ( ConstraintCreateTool ) {
        GetChild<ConstraintCreateTool>().OnInspectorGUI();
      }
      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI();
      }
      if ( RigidBodyVisualCreateTool ) {
        GetChild<RigidBodyVisualCreateTool>().OnInspectorGUI();
      }

      EditorGUILayout.LabelField( GUI.MakeLabel( "Mass properties", true ), skin.Label );
      using ( InspectorGUI.IndentScope.Single )
        InspectorEditor.DrawMembersGUI( GetTargets<RigidBody>().Select( rb => rb.MassProperties ).ToArray() );

      if ( toggleFindTransformGivenPoint )
        FindTransformGivenPointTool = !FindTransformGivenPointTool;
      if ( toggleFindTransformGivenEdge )
        FindTransformGivenEdgeTool = !FindTransformGivenEdgeTool;
      if ( toggleShapeCreate )
        ShapeCreateTool = !ShapeCreateTool;
      if ( toggleConstraintCreate )
        ConstraintCreateTool = !ConstraintCreateTool;
      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
      if ( toggleRigidBodyVisualCreate )
        RigidBodyVisualCreateTool = !RigidBodyVisualCreateTool;
    }

    public override void OnPostTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      InspectorGUI.ToolArrayGUI( this, RigidBody.GetComponentsInChildren<Shape>(), "Shapes" );

      InspectorGUI.ToolArrayGUI( this, m_constraints.ToArray(), "Constraints" );
    }

    private void AssignShapeMaterialToAllShapes( ShapeMaterial shapeMaterial )
    {
      Shape[] shapes = RigidBody.GetComponentsInChildren<Shape>();
      foreach ( var shape in shapes )
        shape.Material = shapeMaterial;

      RigidBody.UpdateMassProperties();
    }
  }
}
