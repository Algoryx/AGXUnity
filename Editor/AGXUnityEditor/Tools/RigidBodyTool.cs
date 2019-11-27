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

          var constraintCreateTool = new ConstraintCreateTool( RigidBody.gameObject, false );
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
      var allConstraints = StageUtility.GetCurrentStageHandle().Contains( RigidBody.gameObject ) ?
                             StageUtility.GetCurrentStageHandle().FindComponentsOfType<Constraint>() :
                             Object.FindObjectsOfType<Constraint>();
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
        using ( new GUILayout.HorizontalScope() ) {
          GUI.ToolsLabel( skin );
          using ( GUI.ToolButtonData.ColorBlock ) {
            toggleFindTransformGivenPoint = GUI.ToolButton( GUI.Symbols.SelectPointTool,
                                                            FindTransformGivenPointTool,
                                                            "Find rigid body transform given point on object.",
                                                            skin );
            toggleFindTransformGivenEdge  = GUI.ToolButton( GUI.Symbols.SelectEdgeTool,
                                                            FindTransformGivenEdgeTool,
                                                            "Find rigid body transform given edge on object.",
                                                            skin );
            toggleShapeCreate             = GUI.ToolButton( GUI.Symbols.ShapeCreateTool,
                                                            ShapeCreateTool,
                                                            "Create shape from visual objects",
                                                            skin );
            toggleConstraintCreate        = GUI.ToolButton( GUI.Symbols.ConstraintCreateTool,
                                                            ConstraintCreateTool,
                                                            "Create constraint to this rigid body",
                                                            skin );
            toggleDisableCollisions       = GUI.ToolButton( GUI.Symbols.DisableCollisionsTool,
                                                            DisableCollisionsTool,
                                                            "Disable collisions against other objects",
                                                            skin );
            using ( new EditorGUI.DisabledGroupScope( !Tools.RigidBodyVisualCreateTool.ValidForNewShapeVisuals( RigidBody ) ) )
              toggleRigidBodyVisualCreate = GUI.ToolButton( GUI.Symbols.ShapeVisualCreateTool,
                                                            RigidBodyVisualCreateTool,
                                                            "Create visual representation of each physical shape in this body",
                                                            skin,
                                                            14 );

          }
        }
      }

      if ( ShapeCreateTool ) {
        GUI.Separator();

        GetChild<ShapeCreateTool>().OnInspectorGUI();
      }
      if ( ConstraintCreateTool ) {
        GUI.Separator();

        GetChild<ConstraintCreateTool>().OnInspectorGUI();
      }
      if ( DisableCollisionsTool ) {
        GUI.Separator();

        GetChild<DisableCollisionsTool>().OnInspectorGUI();
      }
      if ( RigidBodyVisualCreateTool ) {
        GUI.Separator();

        GetChild<RigidBodyVisualCreateTool>().OnInspectorGUI();
      }

      GUI.Separator();

      GUILayout.Label( GUI.MakeLabel( "Mass properties", true ), skin.label );
      using ( new GUI.Indent( 12 ) )
        InspectorEditor.DrawMembersGUI( GetTargets<RigidBody>().Select( rb => rb.MassProperties ).ToArray() );

      GUI.Separator();

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

      GUI.Separator();

      GUIStyle dragDropFieldStyle = new GUIStyle( skin.textArea );
      dragDropFieldStyle.alignment = TextAnchor.MiddleCenter;
      dragDropFieldStyle.richText = true;

      Rect dropArea = new Rect();
      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( GUI.MakeLabel( "Assign Shape Material [" + GUI.AddColorTag( "drop area", Color.Lerp( Color.green, Color.black, 0.4f ) ) + "]",
                                        false,
                                        "Assigns dropped shape material to all shapes in this rigid body." ),
                         dragDropFieldStyle,
                         GUILayout.Height( 22 ) );
        dropArea = GUILayoutUtility.GetLastRect();

        bool resetMaterials = GUILayout.Button( GUI.MakeLabel( "Reset",
                                              false,
                                              "Reset shapes material to null." ),
                               skin.button,
                               GUILayout.Width( 42 ) ) &&
                               EditorUtility.DisplayDialog( "Reset shape materials", "Reset all shapes material to default [null]?", "OK", "Cancel" );
        if ( resetMaterials )
          AssignShapeMaterialToAllShapes( null );
      }
      GUILayout.EndHorizontal();

      GUI.HandleDragDrop<ShapeMaterial>( dropArea, Event.current, ( shapeMaterial ) => { AssignShapeMaterialToAllShapes( shapeMaterial ); } );

      GUI.Separator();

      OnShapeListGUI( RigidBody.GetComponentsInChildren<Shape>(), this );

      GUI.Separator();

      OnConstraintListGUI( m_constraints.ToArray(), this );
    }

    public static void OnShapeListGUI( Shape[] shapes, CustomTargetTool context )
    {
      var skin = InspectorEditor.Skin;

      if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], "Shapes" ), GUI.MakeLabel( "Shapes", true ), skin ) ) {
        context.RemoveEditors( shapes );
        return;
      }

      if ( shapes.Length == 0 ) {
        using ( new GUI.Indent( 12 ) )
          GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.label );
        return;
      }

      using ( new GUI.Indent( 12 ) ) {
        foreach ( var shape in shapes ) {
          GUI.Separator();
          if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ],
                                                          shape.GetInstanceID().ToString() ),
                             GUI.MakeLabel( "[" + GUI.AddColorTag( shape.GetType().Name, Color.Lerp( Color.green, Color.black, 0.4f ) ) + "] " + shape.name ),
                             skin ) ) {
            context.RemoveEditor( shape );
            continue;
          }

          GUI.Separator();
          using ( new GUI.Indent( 12 ) ) {
            var editor = context.GetOrCreateEditor( shape );
            using ( new GUILayout.VerticalScope() )
              editor.OnInspectorGUI();
          }
        }
      }
    }

    public static void OnConstraintListGUI( Constraint[] constraints, CustomTargetTool context )
    {
      var skin = InspectorEditor.Skin;

      if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], "Constraints" ),
                         GUI.MakeLabel( "Constraints", true ), skin ) ) {
        context.RemoveEditors( constraints );
        return;
      }

      if ( constraints.Length == 0 ) {
        using ( new GUI.Indent( 12 ) )
          GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.label );
        return;
      }

      using ( new GUI.Indent( 12 ) ) {
        foreach ( var constraint in constraints ) {
          GUI.Separator();
          if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], constraint.GetInstanceID().ToString() ),
                             GUI.MakeLabel( "[" + GUI.AddColorTag( constraint.Type.ToString(), Color.Lerp( Color.magenta, Color.black, 0.4f ) ) + "] " + constraint.name ),
                             skin ) ) {
            context.RemoveEditor( constraint );
            continue;
          }

          GUI.Separator();
          using ( new GUI.Indent( 12 ) ) {
            var editor = context.GetOrCreateEditor( constraint );
            editor.OnInspectorGUI();
          }
        }
      }
    }

    public static void OnRigidBodyListGUI( RigidBody[] rigidBodies, CustomTargetTool context )
    {
      var skin = InspectorEditor.Skin;

      if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], "Rigid Bodies" ),
                         GUI.MakeLabel( "Rigid Bodies", true ), skin ) ) {
        context.RemoveEditors( rigidBodies );
        return;
      }

      if ( rigidBodies.Length == 0 ) {
        using ( new GUI.Indent( 12 ) )
          GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.label );
        return;
      }

      using ( new GUI.Indent( 12 ) ) {
        foreach ( var rb in rigidBodies ) {
          GUI.Separator();

          if ( !GUI.Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], rb.GetInstanceID().ToString() ),
                             GUI.MakeLabel( "[" + GUI.AddColorTag( "RigidBody", Color.Lerp( Color.blue, Color.white, 0.35f ) ) + "] " + rb.name ),
                             skin ) ) {
            context.RemoveEditor( rb );
            continue;
          }

          GUI.Separator();

          using ( new GUI.Indent( 12 ) ) {
            var editor = context.GetOrCreateEditor( rb );
            editor.OnInspectorGUI();
          }
        }
      }
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
