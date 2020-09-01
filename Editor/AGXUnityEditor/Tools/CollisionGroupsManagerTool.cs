using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( CollisionGroupsManager ) )]
  public class CollisionGroupsManagerTool : CustomTargetTool
  {
    private List<string> m_groups = new List<string>();
    private CollisionGroupEntry m_findActiveGroupNameEntry = null;
    private class StringLowerComparer : IComparer<string> { public int Compare( string a, string b ) { return a.ToLower().CompareTo( b.ToLower() ); } }
    private CollisionGroupEntryPair m_groupEntryPairToAdd = new CollisionGroupEntryPair();

    public CollisionGroupsManager Manager
    {
      get
      {
        return Targets[ 0 ] as CollisionGroupsManager;
      }
    }

    public CollisionGroupsManagerTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin                          = InspectorEditor.Skin;
      var disabledPairs                 = Manager.DisabledPairs;
      bool clearPressed                 = false;
      bool addPressed                   = false;
      CollisionGroupEntryPair erasePair = null;

      GUILayout.Label( GUI.MakeLabel( "Add pair",
                                      true ),
                       skin.LabelMiddleCenter );

      GUILayout.BeginVertical( skin.TextArea );
      {
        HandleCollisionGroupEntryPair( m_groupEntryPairToAdd );

        var buttonState = InspectorGUI.PositiveNegativeButtons( m_groupEntryPairToAdd.First.Tag.Length > 0 ||
                                                                m_groupEntryPairToAdd.Second.Tag.Length > 0,
                                                                "Add",
                                                                "Add pair to disabled pairs.",
                                                                "Clear" );
        addPressed   = buttonState == InspectorGUI.PositiveNegativeResult.Positive;
        clearPressed = buttonState == InspectorGUI.PositiveNegativeResult.Negative;
      }
      GUILayout.EndVertical();

      if ( InspectorGUI.Foldout( FoldoutDataEntry, GUI.MakeLabel( "Disabled Pairs [" + disabledPairs.Length + "]" ) ) ) {
        using ( InspectorGUI.IndentScope.Single ) {
          foreach ( var disabledPair in disabledPairs ) {
            GUILayout.BeginHorizontal();
            {
              InspectorGUI.Separator( 1, EditorGUIUtility.singleLineHeight );

              if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                        true,
                                        "Remove pair from list.",
                                        GUILayout.Width( 18 ) ) )
                erasePair = disabledPair;
            }
            GUILayout.EndHorizontal();

            HandleCollisionGroupEntryPair( disabledPair );

            // TODO GUI: Maybe unnecessary space with correct separator.
            GUILayout.Space( 6.0f );
          }
        }
      }

      if ( clearPressed )
        m_groupEntryPairToAdd.First.Tag = m_groupEntryPairToAdd.Second.Tag = string.Empty;
      if ( addPressed ) {
        Manager.SetEnablePair( m_groupEntryPairToAdd.First.Tag, m_groupEntryPairToAdd.Second.Tag, false );
        m_groupEntryPairToAdd.First.Tag = m_groupEntryPairToAdd.Second.Tag = string.Empty;
        FoldoutDataEntry.Bool = true;
      }
      if ( erasePair != null ) {
        if ( EditorUtility.DisplayDialog( "Remove pair",
                                          "Erase disabled pair: " + erasePair.First.Tag + " and " + erasePair.Second.Tag + "?",
                                          "Yes",
                                          "No" ) )
          Manager.SetEnablePair( erasePair.First.Tag, erasePair.Second.Tag, true );
      }
    }

    private EditorDataEntry FoldoutDataEntry { get { return EditorData.Instance.GetData( Manager, "CollisionGroups" ); } }

    private void HandleCollisionGroupEntryPair( CollisionGroupEntryPair entryPair )
    {
      GUILayout.BeginHorizontal();
      {
        GUILayout.BeginVertical( GUILayout.Width( 12 ) );
        {
          GUILayout.Space( 4 );
          GUILayout.Label( GUI.MakeLabel( "[", 20 ), InspectorEditor.Skin.Label, GUILayout.Height( 32 ), GUILayout.Width( 12 ) );
        }
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        {
          HandleCollisionGroupEntry( entryPair.First );
          HandleCollisionGroupEntry( entryPair.Second );
        }
        GUILayout.EndVertical();
      }
      GUILayout.EndHorizontal();
    }

    private void HandleCollisionGroupEntry( CollisionGroupEntry entry )
    {
      bool buttonPressed = false;
      GUILayout.BeginHorizontal();
      {
        entry.Tag = GUILayout.TextField( entry.Tag, InspectorEditor.Skin.TextField );
        buttonPressed = InspectorGUI.Button( MiscIcon.ContextDropdown,
                                             true,
                                             "Add collision group.",
                                             1.1f,
                                             GUILayout.Width( 18 ) );
      }
      GUILayout.EndHorizontal();

      if ( buttonPressed ) {
        m_findActiveGroupNameEntry = m_findActiveGroupNameEntry == entry ? null : entry;

        if ( m_findActiveGroupNameEntry != null ) {
          m_groups = ( from cg in Object.FindObjectsOfType<CollisionGroups>()
                       from cgEntry in cg.Groups
                       select cgEntry.Tag ).Distinct().ToList();
          m_groups.Sort( new StringLowerComparer() );
        }
      }

      if ( m_findActiveGroupNameEntry == entry && buttonPressed ) {
        GenericMenu groupNameMenu = new GenericMenu();
        groupNameMenu.AddDisabledItem( GUI.MakeLabel( "Groups in scene" ) );
        groupNameMenu.AddSeparator( string.Empty );
        foreach ( var groupName in m_groups )
          groupNameMenu.AddItem( GUI.MakeLabel( groupName ), groupName == m_findActiveGroupNameEntry.Tag, () =>
          {
            m_findActiveGroupNameEntry.Tag = groupName;
            m_findActiveGroupNameEntry = null;
          } );

        groupNameMenu.ShowAsContext();
      }
    }
  }
}
