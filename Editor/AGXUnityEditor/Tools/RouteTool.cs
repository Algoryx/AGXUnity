using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;
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
      // , Route<NodeT> route
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

      GUILayout.BeginHorizontal();
      {
        GUI.ToolsLabel( skin );

        using ( GUI.ToolButtonData.ColorBlock ) {
          toggleDisableCollisions = GUI.ToolButton( GUI.Symbols.DisableCollisionsTool,
                                                    DisableCollisionsTool,
                                                    "Disable collisions against other objects",
                                                    skin );
        }
      }
      GUILayout.EndHorizontal();

      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI();

        GUI.Separator();
      }

      if ( !EditorApplication.isPlaying )
        RouteGUI();

      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
    }

    protected virtual string GetNodeTypeString( RouteNode node ) { return string.Empty; }
    protected virtual Color GetNodeColor( RouteNode node ) { return Color.yellow; }
    protected virtual void OnPreFrameGUI( NodeT node, GUISkin skin ) { }
    protected virtual void OnPostFrameGUI( NodeT node, GUISkin skin ) { }
    protected virtual void OnNodeCreate( NodeT newNode, NodeT refNode, bool addPressed ) { }

    protected RouteNodeTool GetRouteNodeTool( NodeT node )
    {
      return FindActive<RouteNodeTool>( tool => tool.Node == node );
    }

    private void RouteGUI()
    {
      var skin                           = InspectorEditor.Skin;
      GUIStyle invalidNodeStyle          = new GUIStyle( skin.label );
      invalidNodeStyle.normal.background = GUI.CreateColoredTexture( 4, 4, Color.Lerp( UnityEngine.GUI.color, Color.red, 0.75f ) );

      bool addNewPressed        = false;
      bool insertBeforePressed  = false;
      bool insertAfterPressed   = false;
      bool erasePressed         = false;
      NodeT listOpNode          = null;

      Undo.RecordObject( Route, "Route changed" );

      GUI.Separator();

      if ( GUI.Foldout( EditorData.Instance.GetData( Parent, "Route", ( entry ) => { entry.Bool = true; } ), GUI.MakeLabel( "Route", true ), skin ) ) {
        GUI.Separator();

        Route<NodeT>.ValidatedRoute validatedRoute = Route.GetValidated();
        foreach ( var validatedNode in validatedRoute ) {
          var node = validatedNode.Node;
          using ( new GUI.Indent( 12 ) ) {
            if ( validatedNode.Valid )
              GUILayout.BeginVertical();
            else
              GUILayout.BeginVertical( invalidNodeStyle );

            if ( GUI.Foldout( GetFoldoutData( node ),
                              GUI.MakeLabel( GetNodeTypeString( node ) + " | " + SelectGameObjectDropdownMenuTool.GetGUIContent( node.Parent ).text,
                                             !validatedNode.Valid,
                                             validatedNode.ErrorString ),
                              skin,
                              newState =>
                              {
                                Selected = newState ? node : null;
                                EditorUtility.SetDirty( Parent );
                              } ) ) {

              OnPreFrameGUI( node, skin );

              GUI.HandleFrame( node, 12 );

              OnPostFrameGUI( node, skin );

              GUILayout.BeginHorizontal();
              {
                GUILayout.FlexibleSpace();

                using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) ) {
                  insertBeforePressed = GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementBefore.ToString(),
                                                                         16,
                                                                         false,
                                                                         "Insert a new node before this node" ),
                                                          skin.button,
                                                          GUILayout.Width( 20 ),
                                                          GUILayout.Height( 16 ) ) || insertBeforePressed;
                  insertAfterPressed  = GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementAfter.ToString(),
                                                                         16,
                                                                         false,
                                                                         "Insert a new node after this node" ),
                                                          skin.button,
                                                          GUILayout.Width( 20 ),
                                                          GUILayout.Height( 16 ) ) || insertAfterPressed;
                }
                using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
                  erasePressed        = GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(),
                                                                         16,
                                                                         false,
                                                                         "Erase this node" ),
                                                          skin.button,
                                                          GUILayout.Width( 20 ),
                                                          GUILayout.Height( 16 ) ) || erasePressed;

                if ( listOpNode == null && ( insertBeforePressed || insertAfterPressed || erasePressed ) )
                  listOpNode = node;
              }
              GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUI.Separator();
          }

          if ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) &&
               Event.current.type == EventType.MouseDown &&
               Event.current.button == 0 ) {
            Selected = node;
          }
        }

        GUILayout.BeginHorizontal();
        {
          GUILayout.FlexibleSpace();

          using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
            addNewPressed = GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementAfter.ToString(),
                                                             16,
                                                             false,
                                                             "Add new node to route" ),
                                              skin.button,
                                              GUILayout.Width( 20 ),
                                              GUILayout.Height( 16 ) );
          if ( listOpNode == null && addNewPressed )
            listOpNode = Route.LastOrDefault();
        }
        GUILayout.EndHorizontal();
      }

      GUI.Separator();

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
