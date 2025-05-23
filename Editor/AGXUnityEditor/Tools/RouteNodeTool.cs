using AGXUnity;
using System;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  public class RouteNodeTool : Tool
  {
    private Func<RouteNode> m_getSelected = null;
    private Action<RouteNode> m_setSelected = null;
    private Predicate<RouteNode> m_hasNode = null;
    private Func<float> m_radius = null;
    private Func<RouteNode, Color> m_color = null;
    private ScriptComponent m_undoRecordObject = null;

    public ScriptComponent Parent { get; private set; }

    public RouteNode Node { get; private set; }

    public FrameTool FrameTool
    {
      get { return GetChild<FrameTool>(); }
    }

    public Utils.VisualPrimitiveSphere Visual { get { return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( "RouteNodeVisual" ); } }

    public bool Selected
    {
      get { return m_getSelected() == Node; }
      set { m_setSelected( value ? Node : null ); }
    }

    public RouteNodeTool( RouteNode node,
                          ScriptComponent parent,
                          ScriptComponent undoRedoRecordObject,
                          Func<RouteNode> getSelected,
                          Action<RouteNode> setSelected,
                          Predicate<RouteNode> hasNode,
                          Func<float> radius,
                          Func<RouteNode, Color> color )
      : base( isSingleInstanceTool: false )
    {
      Node = node;
      Parent = parent;
      m_undoRecordObject = undoRedoRecordObject;

      m_getSelected = getSelected;
      m_setSelected = setSelected;
      m_hasNode = hasNode;
      m_radius = radius ?? new Func<float>( () => { return 0.05f; } );
      m_color = color;

      Visual.Color = m_color( Node );
      Visual.MouseOverColor = new Color( 0.1f, 0.96f, 0.15f, 1.0f );
      Visual.OnMouseClick += OnClick;
    }

    public override void OnAdd()
    {
      AddChild( new FrameTool( Node )
      {
        OnChangeDirtyTarget = Parent,
        TransformHandleActive = false,
        UndoRedoRecordObject = m_undoRecordObject
      } );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( Parent == null || Node == null || !m_hasNode( Node ) ) {
        PerformRemoveFromParent();
        return;
      }

      float radius = ( Selected ? 3.01f : 3.0f ) * m_radius();
      Visual.Visible = !EditorApplication.isPlaying;
      Visual.Color = Selected ? Visual.MouseOverColor : m_color( Node );
      Visual.SetTransform( Node.Position, Node.Rotation, radius, true, 1.2f * m_radius(), Mathf.Max( 1.5f * m_radius(), 0.25f ) );
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( Node.NodeData is EyeNodeData end ) {
        EditorGUILayout.LabelField( "<b>Eye Node Data</b>", InspectorGUISkin.Instance.Label );
        using ( new InspectorGUI.IndentScope() ) {
          end.FrictionCoefficients = InspectorGUI.Vector2Field( AGXUnity.Utils.GUI.MakeLabel( "Friction Coefficients" ), end.FrictionCoefficients, "F,B" );
        }
      }
      else if ( Node.NodeData is BodyFixedData fixedData ) {
        using ( new InspectorGUI.IndentScope() ) {

          fixedData.RigidAttachment = EditorGUILayout.Toggle( AGXUnity.Utils.GUI.MakeLabel( "Rigid Attachment",
                                                                                            false,
                                                                                            "When enabled, the rotation of the attached cable segment will be locked to the body, " +
                                                                                            "otherwise, only the position will be locked" ),
                                                              fixedData.RigidAttachment );

          fixedData.IgnoreNodeRotation = EditorGUILayout.Toggle( AGXUnity.Utils.GUI.MakeLabel( "Ignore Node Rotation",
                                                                                               false,
                                                                                               "When enabled, the rotation of the created cable attachment will be derived from the routed cable " +
                                                                                               "rather than from the rotation of the node itself" ),
                                                                 fixedData.IgnoreNodeRotation );
        }
      }
      base.OnPostTargetMembersGUI();
    }

    private void OnClick( Utils.Raycast.Result result, Utils.VisualPrimitive primitive )
    {
      Selected = true;
    }
  }
}
