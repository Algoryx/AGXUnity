using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO
{
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#agx-dynamics-import" )]
  public class SavedPrefabLocalData : ScriptComponent
  {
    [SerializeField]
    private List<GroupPair> m_disabledGroups = new List<GroupPair>();

    [HideInInspector]
    public GroupPair[] DisabledGroups { get { return m_disabledGroups.ToArray(); } }

    public int NumSavedDisabledPairs { get { return m_disabledGroups.Count; } }

    public void AddDisabledPair( string group1, string group2 )
    {
      if ( m_disabledGroups.FindIndex( pair => ( pair.First == group1 && pair.Second == group2 ) || ( pair.Second == group1 && pair.First == group2 ) ) >= 0 )
        return;

      m_disabledGroups.Add( new GroupPair() { First = group1, Second = group2 } );
    }

    protected override bool Initialize()
    {
      m_disabledGroups.ForEach( gp => CollisionGroupsManager.Instance.SetEnablePair( gp.First, gp.Second, false ) );

      return true;
    }
  }
}
