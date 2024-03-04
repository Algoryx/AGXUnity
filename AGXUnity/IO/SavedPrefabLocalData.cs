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

    [SerializeField]
    private List<ContactMaterial> m_contactMaterials = new List<ContactMaterial>();

    [HideInInspector]
    public GroupPair[] DisabledGroups => m_disabledGroups.ToArray();

    [HideInInspector]
    public ContactMaterial[] ContactMaterials => m_contactMaterials.ToArray();

    public int NumSavedDisabledPairs => m_disabledGroups.Count;

    public int NumSavedContactMaterials => m_contactMaterials.Count;

    public void AddDisabledPair( string group1, string group2 )
    {
      if ( m_disabledGroups.FindIndex( pair => ( pair.First == group1 && pair.Second == group2 ) || ( pair.Second == group1 && pair.First == group2 ) ) >= 0 )
        return;

      m_disabledGroups.Add( new GroupPair() { First = group1, Second = group2 } );
    }

    public void AddContactMaterial( ContactMaterial contactMaterial )
    {
      if ( m_contactMaterials.Contains( contactMaterial ) )
        return;

      m_contactMaterials.Add( contactMaterial );
    }

    protected override bool Initialize()
    {
      m_disabledGroups.ForEach( gp => CollisionGroupsManager.Instance.SetEnablePair( gp.First, gp.Second, false ) );
      m_contactMaterials.ForEach( cm => ContactMaterialManager.Instance.Add( cm ) );

      return true;
    }
  }
}
