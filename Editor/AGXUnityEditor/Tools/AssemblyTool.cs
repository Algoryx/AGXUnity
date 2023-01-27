using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;
using Assembly = AGXUnity.Assembly;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Assembly ) )]
  public class AssemblyTool : CustomTargetTool
  {
    private class SelectionEntry
    {
      public GameObject Object { get; private set; }

      public SelectionEntry( GameObject gameObject )
      {
        if ( gameObject == null )
          throw new ArgumentNullException( "Game object is null." );

        Object = gameObject;
      }
    }

    private class RigidBodySelection
    {
      public RigidBody RigidBody { get; private set; }

      public RigidBodySelection( RigidBody rb )
      {
        if ( rb == null ) {
          Debug.LogError( "Rigid body component is null - ignoring selection." );
          return;
        }

        RigidBody = rb;
      }
    }

    private enum Mode
    {
      None,
      RigidBody,
      Shape,
      Constraint
    }

    private enum SubMode
    {
      None,
      SelectRigidBody
    }

    private Mode m_mode       = Mode.None;
    private SubMode m_subMode = SubMode.None;

    private List<SelectionEntry> m_selection = new List<SelectionEntry>();
    private RigidBodySelection m_rbSelection = null;

    private CreateOrientedShapeTool CreateOrientedShapeTool
    {
      get { return GetChild<CreateOrientedShapeTool>(); }
      set
      {
        RemoveChild( GetChild<CreateOrientedShapeTool>() );
        AddChild( value );
      }
    }

    private ConstraintCreateTool ConstraintCreateTool
    {
      get { return GetChild<ConstraintCreateTool>(); }
      set
      {
        RemoveChild( ConstraintCreateTool );
        AddChild( value );
      }
    }

    public Assembly Assembly
    {
      get
      {
        return Targets[ 0 ] as Assembly;
      }
    }

    public AssemblyTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Renderer[] renderers = Assembly.GetComponentsInChildren<Renderer>();
      for ( int i = 0; i < renderers.Length; ++i )
        EditorUtility.SetSelectedRenderState( renderers[ i ], EditorSelectedRenderState.Wireframe );
    }

    public override void OnRemove()
    {
      if ( Assembly != null ) {
        Renderer[] renderers = Assembly.GetComponentsInChildren<Renderer>();
        for ( int i = 0; i < renderers.Length; ++i )
          EditorUtility.SetSelectedRenderState( renderers[ i ], EditorSelectedRenderState.Hidden );
      }

      ChangeMode( Mode.None );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      // TODO: This is not responsive.
      if ( Manager.KeyEscapeDown ) {
        ChangeMode( Mode.None );
        EditorUtility.SetDirty( Assembly );
      }

      if ( m_mode == Mode.RigidBody ) {
        if ( Manager.HijackLeftMouseClick() ) {
          Predicate<GameObject> filter = m_subMode == SubMode.None            ? new Predicate<GameObject>( obj => { return obj != null && obj.GetComponent<Shape>() == null; } ) :
                                         m_subMode == SubMode.SelectRigidBody ? new Predicate<GameObject>( obj => { return obj != null && obj.GetComponentInParent<RigidBody>() != null; } ) :
                                                                                null;

          if ( filter == null ) {
            Debug.LogError( "Unknown sub-mode in assembly tool.", Assembly );
            return;
          }

          var hitResults = Utils.Raycast.IntersectChildren( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ),
                                                            Assembly.gameObject,
                                                            filter );
          if ( hitResults.Length > 0 ) {
            // TODO: If count > 1 - the user should be able to chose which object to select.
            GameObject selected = hitResults[ 0 ].Target;
            if ( m_subMode == SubMode.SelectRigidBody ) {
              if ( m_rbSelection != null && m_rbSelection.RigidBody == selected.GetComponentInParent<RigidBody>() )
                m_rbSelection = null;
              else
                m_rbSelection = new RigidBodySelection( selected.GetComponentInParent<RigidBody>() );
            }
            else {
              int selectedIndex = m_selection.FindIndex( entry => { return entry.Object == selected; } );
              // New entry, add it.
              if ( selectedIndex < 0 )
                m_selection.Add( new SelectionEntry( selected ) );
              // Remove selected entry if it already exist.
              else
                m_selection.RemoveAt( selectedIndex );
            }

            EditorUtility.SetDirty( Assembly );
          }
        }
      }
      else if ( m_mode == Mode.Shape ) {
        // ShapeCreateTool on scene view handles this.
      }
      else if ( m_mode == Mode.Constraint ) {
        // ConstraintCreateTool on scene view handles this.
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      // TODO: Improvements.
      //   - "Copy-paste" shape.
      //       1. Select object with primitive shape(s)
      //       2. Select object to copy the shape(s) to
      //   - Move from-to existing bodies or create a new body.
      //   - Mesh object operations.
      //       * Simplify assembly
      //       * Multi-select to create meshes
      //   - Inspect element (hold 'i').

      if ( !AGXUnity.Utils.Math.IsUniform( Assembly.transform.lossyScale, 1.0E-3f ) )
        Debug.LogWarning( "Scale of AGXUnity.Assembly transform isn't uniform. If a child rigid body is moving under this transform the (visual) behavior is undefined.", Assembly );

      var skin = InspectorEditor.Skin;
      if ( !IsMultiSelect ) {
        bool rbButtonPressed         = false;
        bool shapeButtonPressed      = false;
        bool constraintButtonPressed = false;

        InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.CreateRigidBody,
                                                                      m_mode == Mode.RigidBody,
                                                                      "Create/manage rigid bodies in this assembly.",
                                                                      () => rbButtonPressed = true ),
                                  InspectorGUI.ToolButtonData.Create( ToolIcon.CreateShapeGivenVisual,
                                                                      m_mode == Mode.Shape,
                                                                      "Create shapes given visual representations.",
                                                                      () => shapeButtonPressed = true ),
                                  InspectorGUI.ToolButtonData.Create( ToolIcon.CreateConstraint,
                                                                      m_mode == Mode.Constraint,
                                                                      "Create new constraint.",
                                                                      () => constraintButtonPressed = true ) );

        HandleModeGUI();

        if ( rbButtonPressed )
          ChangeMode( Mode.RigidBody );
        if ( shapeButtonPressed )
          ChangeMode( Mode.Shape );
        if ( constraintButtonPressed )
          ChangeMode( Mode.Constraint );
      }
      else {
        GUILayout.Label( GUI.MakeLabel( "Assemblies", 24, true ),
                         skin.LabelMiddleCenter );
      }

      OnObjectListsGUI( this );
    }

    public static void OnObjectListsGUI( CustomTargetTool context )
    {
      if ( context == null )
        return;

      InspectorGUI.ToolArrayGUI( context,
                                 context.CollectComponentsInChildred<RigidBody>().ToArray(),
                                 "Rigid Bodies" );

      InspectorGUI.ToolArrayGUI( context,
                                 context.CollectComponentsInChildred<Constraint>().ToArray(),
                                 "Constraints" );

      InspectorGUI.ToolArrayGUI( context,
                                 context.CollectComponentsInChildred<Shape>().ToArray(),
                                 "Shapes" );
    }

    private void HandleModeGUI()
    {
      if ( m_mode == Mode.RigidBody )
        HandleModeRigidBodyGUI();
      else if ( m_mode == Mode.Shape )
        HandleModeShapeGUI();
      else if ( m_mode == Mode.Constraint )
        HandleModeConstraintGUI();
    }

    private void HandleModeRigidBodyGUI()
    {
      var skin = InspectorEditor.Skin;

      using ( GUI.AlignBlock.Center ) {
        if ( m_subMode == SubMode.SelectRigidBody )
          GUILayout.Label( GUI.MakeLabel( "Select rigid body object in scene view.", true ), skin.Label );
        else
          GUILayout.Label( GUI.MakeLabel( "Select object(s) in scene view.", true ), skin.Label );
      }

      bool selectionHasRigidBody         = m_selection.Find( entry => entry.Object.GetComponentInParent<RigidBody>() != null ) != null;
      bool createNewRigidBodyPressed     = false;
      bool addToExistingRigidBodyPressed = false;
      bool moveToNewRigidBodyPressed     = false;
      GUILayout.BeginHorizontal();
      {
        GUILayout.Space( 12 );
        GUILayout.BeginVertical();
        {
          UnityEngine.GUI.enabled = m_selection.Count == 0 || !selectionHasRigidBody;
          createNewRigidBodyPressed = GUILayout.Button( GUI.MakeLabel( "Create new" + ( m_selection.Count == 0 ? " (empty)" : "" ),
                                                                       false,
                                                                       "Create new rigid body with selected objects" ),
                                                        skin.Button,
                                                        GUILayout.Width( 128 ) );
          UnityEngine.GUI.enabled = m_selection.Count > 0 && Assembly.GetComponentInChildren<RigidBody>() != null;
          addToExistingRigidBodyPressed = GUILayout.Toggle( m_subMode == SubMode.SelectRigidBody,
                                                            GUI.MakeLabel( "Add to existing",
                                                                           false,
                                                                           "Add selected objects to existing rigid body" ),
                                                            skin.Button,
                                                            GUILayout.Width( 100 ) ) != ( m_subMode == SubMode.SelectRigidBody );
          UnityEngine.GUI.enabled = selectionHasRigidBody;
          moveToNewRigidBodyPressed = GUILayout.Button( GUI.MakeLabel( "Move to new",
                                                                       false,
                                                                       "Move objects that already contains a rigid body to a new rigid body" ),
                                                        skin.Button,
                                                        GUILayout.Width( 85 ) );
          UnityEngine.GUI.enabled = true;
        }
        GUILayout.EndVertical();
      }
      GUILayout.EndHorizontal();

      // Creates new rigid body and move selected objects to it (as children).
      if ( createNewRigidBodyPressed || moveToNewRigidBodyPressed ) {
        CreateOrMoveToRigidBodyFromSelectionEntries( m_selection );
        m_selection.Clear();
      }
      // Toggle to select a rigid body in scene view to move the current selection to.
      else if ( addToExistingRigidBodyPressed ) {
        // This will toggle if sub-mode already is SelectRigidBody.
        ChangeSubMode( SubMode.SelectRigidBody );
      }

      // The user has chosen a rigid body to move the current selection to.
      if ( m_rbSelection != null ) {
        CreateOrMoveToRigidBodyFromSelectionEntries( m_selection, m_rbSelection.RigidBody.gameObject );
        m_selection.Clear();
        ChangeSubMode( SubMode.None );
      }
    }

    private void HandleModeShapeGUI()
    {
      if ( CreateOrientedShapeTool == null ) {
        ChangeMode( Mode.None );
        return;
      }

      CreateOrientedShapeTool.OnInspectorGUI();
    }

    private void HandleModeConstraintGUI()
    {
      if ( ConstraintCreateTool == null ) {
        ChangeMode( Mode.None );
        return;
      }

      ConstraintCreateTool.OnInspectorGUI();
    }

    private void CreateOrMoveToRigidBodyFromSelectionEntries( List<SelectionEntry> selectionEntries, GameObject rbGameObject = null )
    {
      if ( rbGameObject != null && rbGameObject.GetComponent<RigidBody>() == null ) {
        Debug.LogError( "Mandatory AGXUnity.RigidBody component not present in game object. Ignoring 'move to'.", rbGameObject );
        return;
      }

      foreach ( var selection in selectionEntries ) {
        if ( selection.Object == null ) {
          Debug.LogError( "Unable to create rigid body - selection contains null object(s)." );
          return;
        }
      }

      if ( rbGameObject == null ) {
        rbGameObject                    = Factory.Create<RigidBody>();
        rbGameObject.transform.position = Assembly.transform.position;
        rbGameObject.transform.rotation = Assembly.transform.rotation;
        rbGameObject.transform.parent   = Assembly.transform;

        Undo.RegisterCreatedObjectUndo( rbGameObject, "New assembly rigid body" );
      }

      foreach ( var entry in selectionEntries ) {
        // Collecting selected objects, non selected children, to be moved to
        // a new parent.
        List<Transform> orphans = new List<Transform>();
        foreach ( Transform child in entry.Object.transform ) {
          // Do not add shapes to our orphans since they've PROBABLY/HOPEFULLY
          // been created earlier by this tool. This implicit state probably has
          // to be revised.
          bool inSelectedList = child.GetComponent<Shape>() != null || selectionEntries.FindIndex( selectedEntry => { return selectedEntry.Object == child.gameObject; } ) >= 0;
          if ( !inSelectedList )
            orphans.Add( child );
        }

        // Moving selected parents (NON-selected) children to a new parent.
        Transform parent = entry.Object.transform.parent;
        foreach ( var orphan in orphans )
          Undo.SetTransformParent( orphan, parent, "Moving non-selected child to selected parent" );

        Undo.SetTransformParent( entry.Object.transform, rbGameObject.transform, "Parent of mesh is rigid body" );
      }
    }

    private void ChangeMode( Mode mode )
    {
      // Assembly reference may be lost here when called from OnRemove.

      // Toggle mode.
      if ( mode == m_mode )
        mode = Mode.None;

      m_selection.Clear();
      RemoveAllChildren();

      m_mode = mode;
      m_subMode = SubMode.None;

      if ( m_mode == Mode.Shape )
        CreateOrientedShapeTool = new CreateOrientedShapeTool( Assembly.gameObject );
      else if ( m_mode == Mode.Constraint )
        ConstraintCreateTool = new ConstraintCreateTool( Assembly.gameObject, true );
    }

    private void ChangeSubMode( SubMode subMode )
    {
      // Toggle sub-mode.
      if ( subMode == m_subMode )
        subMode = SubMode.None;

      m_rbSelection = null;
      m_subMode = subMode;
    }

    public bool HasActiveSelections()
    {
      return m_selection.Count > 0 || m_rbSelection != null;
    }

    public void OnRenderGizmos( Utils.ObjectsGizmoColorHandler colorHandler )
    {
      if ( Assembly == null )
        return;

      RigidBody[] bodies = Assembly.GetComponentsInChildren<RigidBody>();
      foreach ( var rb in bodies ) {
        colorHandler.Colorize( rb );

        // Mesh filters are not colorized by default - give the color (similar/same as body).
        // NOTE: Shapes debug rendering are not included in these mesh filters.
        colorHandler.ColorizeMeshFilters( rb );
      }

      foreach ( var selected in m_selection ) {
        MeshFilter filter = selected.Object.GetComponent<MeshFilter>();
        colorHandler.Highlight( filter, Utils.ObjectsGizmoColorHandler.SelectionType.VaryingIntensity );
      }

      if ( m_rbSelection != null )
        colorHandler.Highlight( m_rbSelection.RigidBody, Utils.ObjectsGizmoColorHandler.SelectionType.VaryingIntensity );
    }
  }
}
