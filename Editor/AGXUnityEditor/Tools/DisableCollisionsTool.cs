using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class DisableCollisionsTool : Tool
  {
    private GameObject m_mainObject = null;
    private List<GameObject> m_selected = new List<GameObject>();

    public bool SelectGameObjectTool
    {
      get { return GetChild<SelectGameObjectTool>() != null; }
      set
      {
        if ( value && !SelectGameObjectTool ) {
          RemoveAllChildren();

          var selectGameObjectTool = new SelectGameObjectTool()
          {
            OnSelect = go =>
            {
              HandleSelectedObject( go );
            }
          };

          AddChild( selectGameObjectTool );
        }
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
    }

    public DisableCollisionsTool( GameObject mainObject )
      : base( isSingleInstanceTool: true )
    {
      m_mainObject = mainObject;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( !SelectGameObjectTool )
        SelectGameObjectTool = true;

      InspectorEditor.RequestConstantRepaint = true;
    }

    public void OnInspectorGUI()
    {
      InspectorGUI.OnDropdownToolBegin( $"Disable collisions between {m_mainObject.name} and other objects selected in Scene View." );

      var skin = InspectorEditor.Skin;
      var emptyContent = GUI.MakeLabel( " " );

      EditorGUILayout.LabelField( GUI.MakeLabel( "Disable: ", true ),
                                  SelectGameObjectDropdownMenuTool.GetGUIContent( m_mainObject ),
                                  skin.TextArea );


      EditorGUILayout.LabelField( emptyContent,
                                  GUI.MakeLabel( GUI.Symbols.ArrowLeftRight.ToString() ) );

      if ( m_selected.Count == 0 ) {
        EditorGUILayout.LabelField( emptyContent,
                                    GUI.MakeLabel( "Select object(s) in scene view" + AwaitingUserActionDots() ),
                                    skin.TextArea );
      }
      else {
        int removeIndex = -1;
        for ( int i = 0; i < m_selected.Count; ++i ) {
          GUILayout.BeginHorizontal();
          {
            EditorGUILayout.LabelField( emptyContent,
                                        SelectGameObjectDropdownMenuTool.GetGUIContent( m_selected[ i ] ),
                                        skin.TextArea );
            if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                      true,
                                      "Remove pair.",
                                      GUILayout.Width( 14 ) ) )
              removeIndex = i;
          }
          GUILayout.EndHorizontal();
        }

        if ( removeIndex >= 0 )
          m_selected.RemoveAt( removeIndex );
      }

      var applyCancelState = InspectorGUI.PositiveNegativeButtons( m_selected.Count > 0,
                                                                   "Apply",
                                                                   "Apply current configuration.",
                                                                   "Cancel" );

      if ( applyCancelState == InspectorGUI.PositiveNegativeResult.Positive ) {
        string selectedGroupName = m_mainObject.GetInstanceID().ToString();
        string mainObjectGroupName = "";
        for ( int i = 0; i < m_selected.Count; ++i )
          mainObjectGroupName += m_selected[ i ].GetInstanceID().ToString() +
                                 ( i != m_selected.Count - 1 ? "_" : "" );

        Undo.SetCurrentGroupName( "Disabling collisions" );
        var undoGroupId = Undo.GetCurrentGroup();

        if ( m_mainObject.GetComponent<CollisionGroups>() == null )
          Undo.AddComponent<CollisionGroups>( m_mainObject );

        Undo.RecordObject( m_mainObject.GetComponent<CollisionGroups>(), "Adding collision group" );
        m_mainObject.GetComponent<CollisionGroups>().AddGroup( mainObjectGroupName,
                                                               ShouldPropagateToChildren( m_mainObject ) );
        foreach ( var selected in m_selected ) {
          if ( selected.GetComponent<CollisionGroups>() == null )
            Undo.AddComponent<CollisionGroups>( selected );
          Undo.RecordObject( selected.GetComponent<CollisionGroups>(), "Adding collision group" );
          selected.GetComponent<CollisionGroups>().AddGroup( selectedGroupName,
                                                             ShouldPropagateToChildren( selected ) );
        }

        // TopMenu.GetOrCreate works with Undo.
        Undo.RecordObject( TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>(),
                           "Adding collision group to manager." );

        CollisionGroupsManager.Instance.SetEnablePair( mainObjectGroupName, selectedGroupName, false );

        Undo.CollapseUndoOperations( undoGroupId );

        PerformRemoveFromParent();
      }
      else if ( applyCancelState == InspectorGUI.PositiveNegativeResult.Negative )
        PerformRemoveFromParent();

      InspectorGUI.OnDropdownToolEnd();
    }

    private void HandleSelectedObject( GameObject selected )
    {
      if ( selected == null )
        return;

      if ( !m_selected.Contains( selected ) )
        m_selected.Add( selected );

      EditorUtility.SetDirty( m_mainObject );
    }

    private bool ShouldPropagateToChildren( GameObject go )
    {
      return go.GetComponent<RigidBody>() == null &&
             go.GetComponent<AGXUnity.Collide.Shape>() == null &&
             go.GetComponent<Wire>() == null &&
             go.GetComponent<Cable>() == null &&
             go.GetComponent<AGXUnity.Model.Track>() == null &&
             go.GetComponent<AGXUnity.Model.DeformableTerrain>() == null;
    }
  }
}
