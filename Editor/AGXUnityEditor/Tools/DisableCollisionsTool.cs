using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnityEditor.Utils.GUI;

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
    {
      m_mainObject = mainObject;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( !SelectGameObjectTool )
        SelectGameObjectTool = true;
    }

    public void OnInspectorGUI()
    {
      var skin = InspectorEditor.Skin;

      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( GUI.MakeLabel( "Disable: ", true ), skin.label );
        GUILayout.Label( SelectGameObjectDropdownMenuTool.GetGUIContent( m_mainObject ), skin.textField );
      }
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      {
        GUILayout.FlexibleSpace();
        GUILayout.Label( GUI.MakeLabel( GUI.Symbols.Synchronized.ToString() ), skin.label );
        GUILayout.BeginVertical();
        {
          if ( m_selected.Count == 0 )
            GUILayout.Label( GUI.MakeLabel( "None", true ), skin.label, GUILayout.Width( 180 ) );
          else {
            int removeIndex = -1;
            for ( int i = 0; i < m_selected.Count; ++i ) {
              GUILayout.BeginHorizontal();
              {
                GUILayout.Label( SelectGameObjectDropdownMenuTool.GetGUIContent( m_selected[ i ] ), GUI.Align( skin.textField, TextAnchor.MiddleLeft ), GUILayout.Height( 20 ) );
                using ( GUI.NodeListButtonColor )
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString() ), skin.button, GUILayout.Width( 18 ), GUILayout.Height( 18 ) ) )
                    removeIndex = i;
              }
              GUILayout.EndHorizontal();
            }

            if ( removeIndex >= 0 )
              m_selected.RemoveAt( removeIndex );
          }
        }
        GUILayout.EndVertical();
      }
      GUILayout.EndHorizontal();

      GUILayout.Space( 12 );

      bool applyPressed = false;
      bool cancelPressed = false;
      GUILayout.BeginHorizontal();
      {
        GUILayout.FlexibleSpace();

        UnityEngine.GUI.enabled = m_selected.Count > 0;
        applyPressed = GUILayout.Button( GUI.MakeLabel( "Apply", true, "Apply current configuration" ), skin.button, GUILayout.Width( 86 ), GUILayout.Height( 26 ) );
        UnityEngine.GUI.enabled = true;

        GUILayout.BeginVertical();
        {
          GUILayout.Space( 11 );
          cancelPressed = GUILayout.Button( GUI.MakeLabel( "Cancel", false, "Cancel/reset" ), skin.button, GUILayout.Width( 64 ), GUILayout.Height( 18 ) );
        }
        GUILayout.EndVertical();
      }
      GUILayout.EndHorizontal();

      if ( applyPressed ) {
        string selectedGroupName = m_mainObject.GetInstanceID().ToString();
        string mainObjectGroupName = "";
        for ( int i = 0; i < m_selected.Count; ++i )
          mainObjectGroupName += m_selected[ i ].GetInstanceID().ToString() + ( i != m_selected.Count - 1 ? "_" : "" );

        m_mainObject.GetOrCreateComponent<CollisionGroups>().AddGroup( mainObjectGroupName, ShouldPropagateToChildren( m_mainObject ) );
        foreach ( var selected in m_selected )
          selected.GetOrCreateComponent<CollisionGroups>().AddGroup( selectedGroupName, ShouldPropagateToChildren( selected ) );

        CollisionGroupsManager.Instance.SetEnablePair( mainObjectGroupName, selectedGroupName, false );

        PerformRemoveFromParent();
      }
      else if ( cancelPressed )
        PerformRemoveFromParent();
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
             go.GetComponent<Cable>() == null;
    }
  }
}
