using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class ConstraintCreateTool : Tool
  {
    public GameObject Parent { get; private set; }

    public bool MakeConstraintChildToParent { get; set; }

    public ConstraintAttachmentFrameTool AttachmentFrameTool
    {
      get { return GetChild<ConstraintAttachmentFrameTool>(); }
      set
      {
        RemoveChild( AttachmentFrameTool );
        AddChild( value );
      }
    }

    public ConstraintCreateTool( GameObject parent,
                                 bool makeConstraintChildToParent,
                                 Action<Constraint> onCreate = null )
      : base( isSingleInstanceTool: true )
    {
      Parent = parent;
      MakeConstraintChildToParent = makeConstraintChildToParent;
      m_onCreate = onCreate;
    }

    public override void OnAdd()
    {
      m_createConstraintData.CreateInitialState( Parent.name );
      AttachmentFrameTool = new ConstraintAttachmentFrameTool( new AttachmentPair[] { m_createConstraintData.AttachmentPair }, Parent );
    }

    public override void OnRemove()
    {
      m_createConstraintData.Reset();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( Parent == null ) {
        PerformRemoveFromParent();
        return;
      }
    }

    public void OnInspectorGUI()
    {
      if ( AttachmentFrameTool == null || AttachmentFrameTool.AttachmentPairs[ 0 ] == null ) {
        PerformRemoveFromParent();
        return;
      }

      var skin = InspectorEditor.Skin;

      using ( new GUI.Indent( 16 ) ) {
        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( GUI.MakeLabel( "Name", true ), skin.Label, GUILayout.Width( 64 ) );
          m_createConstraintData.Name = GUILayout.TextField( m_createConstraintData.Name, skin.TextField, GUILayout.ExpandWidth( true ) );
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( GUI.MakeLabel( "Type", true ), skin.Label, GUILayout.Width( 64 ) );
          using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.yellow, 0.1f ) ) )
            m_createConstraintData.ConstraintType = (ConstraintType)EditorGUILayout.EnumPopup( m_createConstraintData.ConstraintType,
                                                                                               skin.Button,
                                                                                               GUILayout.ExpandWidth( true ),
                                                                                               GUILayout.Height( 18 ) );
        }
        GUILayout.EndHorizontal();
      }

      GUI.Separator3D();

      AttachmentFrameTool.OnPreTargetMembersGUI();
      AttachmentFrameTool.AttachmentPairs[ 0 ].Synchronize();

      m_createConstraintData.CollisionState = ConstraintTool.ConstraintCollisionsStateGUI( m_createConstraintData.CollisionState );
      m_createConstraintData.SolveType = ConstraintTool.ConstraintSolveTypeGUI( m_createConstraintData.SolveType );

      GUI.Separator3D();

      var createCancelState = GUI.CreateCancelButtons( m_createConstraintData.AttachmentPair.ReferenceObject != null &&
                                                       m_createConstraintData.AttachmentPair.ReferenceObject.GetComponentInParent<RigidBody>() != null,
                                                       "Create the constraint" );

      GUI.Separator3D();

      if ( createCancelState == GUI.CreateCancelState.Create ) {
        GameObject constraintGameObject = Factory.Create( m_createConstraintData.ConstraintType,
                                                          m_createConstraintData.AttachmentPair );
        Constraint constraint           = constraintGameObject.GetComponent<Constraint>();
        constraintGameObject.name       = m_createConstraintData.Name;
        constraint.CollisionsState      = m_createConstraintData.CollisionState;

        if ( MakeConstraintChildToParent )
          constraintGameObject.transform.SetParent( Parent.transform );

        Undo.RegisterCreatedObjectUndo( constraintGameObject, "New constraint '" + constraintGameObject.name + "' created" );

        m_onCreate?.Invoke( constraint );

        m_createConstraintData.Reset();
      }

      if ( createCancelState != GUI.CreateCancelState.Nothing )
        PerformRemoveFromParent();
    }

    private class CreateConstraintData
    {
      public ConstraintType ConstraintType
      {
        get { return (ConstraintType)EditorData.Instance.GetStaticData( "CreateConstraintData.ConstraintType" ).Int; }
        set { EditorData.Instance.GetStaticData( "CreateConstraintData.ConstraintType" ).Int = (int)value; }
      }

      private Action<EditorDataEntry> m_defaultCollisionState = new Action<EditorDataEntry>( entry => { entry.Int = (int)Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2; } );
      public Constraint.ECollisionsState CollisionState
      {
        get { return (Constraint.ECollisionsState)EditorData.Instance.GetStaticData( "CreateConstraintData.CollisionState", m_defaultCollisionState ).Int; }
        set { EditorData.Instance.GetStaticData( "CreateConstraintData.CollisionState", m_defaultCollisionState ).Int = (int)value; }
      }

      private Action<EditorDataEntry> m_defaultSolveType = new Action<EditorDataEntry>( entry => { entry.Int = (int)Constraint.ESolveType.Direct; } );
      public Constraint.ESolveType SolveType
      {
        get { return (Constraint.ESolveType)EditorData.Instance.GetStaticData( "CreateConstraintData.SolveType", m_defaultSolveType ).Int; }
        set { EditorData.Instance.GetStaticData( "CreateConstraintData.SolveType", m_defaultSolveType ).Int = (int)value; }
      }

      public string Name                   = string.Empty;
      public AttachmentPair AttachmentPair = null;
      public GameObject TempGameObject     = null;

      public void CreateInitialState( string name )
      {
        if ( AttachmentPair != null ) {
          Debug.LogError( "Attachment pair already created. Make sure to clean any previous state before initializing a new one.", AttachmentPair );
          return;
        }

        TempGameObject           = new GameObject();
        TempGameObject.hideFlags = HideFlags.HideAndDontSave;
        AttachmentPair           = AttachmentPair.Create( TempGameObject );
        Name                     = Factory.CreateName( name + "_constraint" );
      }

      public void Reset()
      {
        if ( TempGameObject != null )
          GameObject.DestroyImmediate( TempGameObject );
        AttachmentPair = null;
      }
    }

    private CreateConstraintData m_createConstraintData = new CreateConstraintData();
    private Action<Constraint> m_onCreate = null;
  }
}
