using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  public class RouteTool<ParentT, NodeT> : CustomTargetTool
    where ParentT : ScriptComponent
    where NodeT : RouteNode, new()
  {
    public Func<float> NodeVisualRadius = null;

    public ParentT Parent
    {
      get
      {
        return Targets[ 0 ] as ParentT;
      }
    }

    public Route<NodeT> Route { get; private set; }

    private NodeT m_selected = null;
    public NodeT Selected
    {
      get { return m_selected; }
      set
      {
        if ( value == m_selected )
          return;

        if ( m_selected != null ) {
          GetFoldoutData( m_selected ).Bool = false;
          SelectedTool.FrameTool.TransformHandleActive = false;
          SelectedTool.FrameTool.InactivateTemporaryChildren();
          // Not selected anymore - enable picking (OnMouseClick callback).
          SelectedTool.Visual.Pickable = true;
        }

        m_selected = value;

        if ( m_selected != null ) {
          GetFoldoutData( m_selected ).Bool = true;
          SelectedTool.FrameTool.TransformHandleActive = true;
          // This flags that we don't expect OnMouseClick when the
          // node is already selected. This solves transform handles
          // completely inside the visual (otherwise Manager will
          // swallow the mouse click and it's not possible to move
          // the nodes).
          SelectedTool.Visual.Pickable = false;
          EditorUtility.SetDirty( Parent );
        }
      }
    }

    public RouteNodeTool SelectedTool { get { return FindActive<RouteNodeTool>( tool => tool.Node == m_selected ); } }

    /// <summary>
    /// Not visual in scene view when the editor is playing or selected in project (asset).
    /// </summary>
    public bool VisualInSceneView { get; private set; }

    public bool DisableCollisionsTool
    {
      get { return GetChild<DisableCollisionsTool>() != null; }
      set
      {
        if ( value && !DisableCollisionsTool ) {
          var disableCollisionsTool = new DisableCollisionsTool( Parent.gameObject );
          AddChild( disableCollisionsTool );
        }
        else if ( !value )
          RemoveChild( GetChild<DisableCollisionsTool>() );
      }
    }

    public RouteTool( Object[] targets )
      : base( targets )
    {
      Route = (Route<NodeT>)Parent.GetType().GetProperty( "Route", System.Reflection.BindingFlags.Instance |
                                                                   System.Reflection.BindingFlags.Public ).GetGetMethod().Invoke( Parent, null );

      VisualInSceneView = true;
    }

    public override void OnAdd()
    {
      VisualInSceneView = !EditorApplication.isPlaying &&
                          !AssetDatabase.Contains( Parent.gameObject );

      HideDefaultHandlesEnableWhenRemoved();

      if ( VisualInSceneView ) {
        foreach ( var node in Route ) {
          CreateRouteNodeTool( node );
          if ( GetFoldoutData( node ).Bool )
            Selected = node;
        }
      }
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( !VisualInSceneView )
        return;

      // Something happens to our child tools when Unity is performing
      // undo and redo. Try to restore the tools.
      if ( GetChildren().Length == 0 ) {
        m_selected = null;
        foreach ( var node in Route ) {
          if ( GetRouteNodeTool( node ) == null )
            CreateRouteNodeTool( node );
          if ( GetFoldoutData( node ).Bool )
            Selected = node;
        }
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      if ( IsMultiSelect ) {
        if ( VisualInSceneView ) {
          foreach ( var node in Route )
            RemoveChild( GetRouteNodeTool( node ) );
          VisualInSceneView = false;
        }
        return;
      }

      bool toggleDisableCollisions = false;
      var skin = InspectorEditor.Skin;

      InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.DisableCollisions,
                                                                    DisableCollisionsTool,
                                                                    "Disable collisions against other objects",
                                                                    () => toggleDisableCollisions = true ) );

      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI();
      }

      if ( !EditorApplication.isPlaying )
        RouteGUI();

      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
    }

    protected virtual string GetNodeTypeString( RouteNode node ) { return string.Empty; }
    protected virtual Color GetNodeColor( RouteNode node ) { return Color.yellow; }
    protected virtual void OnPreFrameGUI( NodeT node ) { }
    protected virtual void OnPostFrameGUI( NodeT node ) { }
    protected virtual void OnNodeCreate( NodeT newNode, NodeT refNode, bool addPressed ) { }

    protected RouteNodeTool GetRouteNodeTool( NodeT node )
    {
      return FindActive<RouteNodeTool>( tool => tool.Node == node );
    }

    private static GUIStyle s_invalidNodeStyle = null;

    struct NodeFoldoutState
    {
      public bool Foldout;
      public bool InsertAfter;
      public bool InsertBefore;
      public bool Erase;

      public bool ButtonPressed { get { return InsertAfter || InsertBefore || Erase; } }
    }

    private NodeFoldoutState NodeFoldout( Route<NodeT>.ValidatedNode validatedNode )
    {
      if ( s_invalidNodeStyle == null ) {
        s_invalidNodeStyle = new GUIStyle( InspectorEditor.Skin.Label );
        s_invalidNodeStyle.normal.background = GUI.CreateColoredTexture( 1,
                                                                         1,
                                                                         Color.Lerp( UnityEngine.GUI.color,
                                                                                     Color.red,
                                                                                     0.75f ) );
      }

      var state = new NodeFoldoutState();
      var node  = validatedNode.Node;

      var verticalScope = !validatedNode.Valid ?
                            new EditorGUILayout.VerticalScope( s_invalidNodeStyle ) :
                            null;
      var horizontalScope = node == Selected ?
                              new EditorGUILayout.HorizontalScope( InspectorEditor.Skin.Label ) :
                              new EditorGUILayout.HorizontalScope( InspectorEditor.Skin.TextArea );
      state.Foldout = InspectorGUI.Foldout( GetFoldoutData( node ),
                                            GUI.MakeLabel( GetNodeTypeString( node ) + ' ' +
                                                            SelectGameObjectDropdownMenuTool.GetGUIContent( node.Parent ).text,
                                                            !validatedNode.Valid,
                                                            validatedNode.ErrorString ),
                                            newState =>
                                            {
                                              Selected = newState ? node : null;
                                              EditorUtility.SetDirty( Parent );
                                            } );

      state.InsertBefore = InspectorGUI.Button( MiscIcon.EntryInsertBefore,
                                                true,
                                                "Insert a new node before this node.",
                                                0.85f,
                                                GUILayout.Width( 18 ) );
      state.InsertAfter = InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                               true,
                                               "Insert a new node after this node.",
                                               0.85f,
                                               GUILayout.Width( 18 ) );
      state.Erase = InspectorGUI.Button( MiscIcon.EntryRemove,
                                         true,
                                         "Remove this node from the route.",
                                         GUILayout.Width( 18 ) );
      horizontalScope?.Dispose();
      verticalScope?.Dispose();

      return state;
    }

    private void RouteGUI()
    {
      var addNewPressed = false;
      var insertBeforePressed = false;
      var insertAfterPressed = false;
      var erasePressed = false;

      NodeT listOpNode   = null;

      Undo.RecordObject( Route, "Route changed" );

      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( Parent,
                                                              "Route",
                                                              entry => { entry.Bool = true; } ),
                                 GUI.MakeLabel( "Route" ) ) ) {
        var validatedRoute = Route.GetValidated();
        foreach ( var validatedNode in validatedRoute ) {
          var node = validatedNode.Node;
          using ( InspectorGUI.IndentScope.Single ) {
            var foldoutState = NodeFoldout( validatedNode );
            if ( foldoutState.Foldout ) {
              OnPreFrameGUI( node );

              InspectorGUI.HandleFrame( node, 1 );

              OnPostFrameGUI( node );
            }

            if ( listOpNode == null && foldoutState.ButtonPressed )
              listOpNode = node;

            insertBeforePressed = insertBeforePressed || foldoutState.InsertBefore;
            insertAfterPressed  = insertAfterPressed  || foldoutState.InsertAfter;
            erasePressed        = erasePressed        || foldoutState.Erase;
          }

          if ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) &&
               Event.current.type == EventType.MouseDown &&
               Event.current.button == 0 ) {
            Selected = node;
          }
        }

        GUILayout.BeginHorizontal();
        {
          InspectorGUI.Separator( 1, EditorGUIUtility.singleLineHeight );

          addNewPressed = InspectorGUI.Button( MiscIcon.EntryAdd,
                                               true,
                                               "Add new node to the route.",
                                               GUILayout.Width( 18 ) );

          if ( listOpNode == null && addNewPressed )
            listOpNode = Route.LastOrDefault();
        }
        GUILayout.EndHorizontal();
      }
      else
        InspectorGUI.Separator( 1, 3 );

      if ( addNewPressed || insertBeforePressed || insertAfterPressed ) {
        NodeT newRouteNode = null;
        // Clicking "Add" will not copy data from last node.
        newRouteNode = listOpNode != null ?
                         addNewPressed ?
                           RouteNode.Create<NodeT>( null, listOpNode.Position, listOpNode.Rotation ) :
                           RouteNode.Create<NodeT>( listOpNode.Parent, listOpNode.LocalPosition, listOpNode.LocalRotation ) :
                         RouteNode.Create<NodeT>();
        OnNodeCreate( newRouteNode, listOpNode, addNewPressed );

        if ( addNewPressed )
          Route.Add( newRouteNode );
        if ( insertBeforePressed )
          Route.InsertBefore( newRouteNode, listOpNode );
        if ( insertAfterPressed )
          Route.InsertAfter( newRouteNode, listOpNode );

        if ( newRouteNode != null ) {
          CreateRouteNodeTool( newRouteNode );
          Selected = newRouteNode;
        }
      }
      else if ( listOpNode != null && erasePressed ) {
        Selected = null;
        Route.Remove( listOpNode );
      }
    }

    private void CreateRouteNodeTool( NodeT node )
    {
      AddChild( new RouteNodeTool( node,
                                   Parent,
                                   Route,
                                   () => { return Selected; },
                                   ( selected ) => { Selected = selected as NodeT; },
                                   ( n ) => { return Route.Contains( n as NodeT ); },
                                   NodeVisualRadius,
                                   GetNodeColor ) );
    }

    private EditorDataEntry GetData( NodeT node, string identifier, Action<EditorDataEntry> onCreate = null )
    {
      return EditorData.Instance.GetData( Route, identifier + "_" + Route.IndexOf( node ), onCreate );
    }

    private EditorDataEntry GetFoldoutData( NodeT node )
    {
      return GetData( node, "foldout", ( entity ) => { entity.Bool = false; } );
    }
  }
}
