using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Contact material list data.
  /// </summary>
  [Serializable]
  public class ContactMaterialEntry
  {
    public ContactMaterial ContactMaterial = null;
    // Continue on this. Hook on ToolArrayGUI with pre/post
    // item GUI.
    //public bool IsOriented = false;
    //public GameObject ReferenceObject = null;
  }

  /// <summary>
  /// Contact material manager which enables the user to manage contact materials.
  /// </summary>
  [AddComponentMenu( "" )]
  public class ContactMaterialManager : UniqueGameObject<ContactMaterialManager>
  {
    [SerializeField]
    private List<ContactMaterialEntry> m_contactMaterials = new List<ContactMaterialEntry>();

    [HideInInspector]
    public ContactMaterialEntry[] ContactMaterialEntries { get { return m_contactMaterials.ToArray(); } }

    [HideInInspector]
    public ContactMaterial[] ContactMaterials
    {
      get
      {
        return ( from entry in m_contactMaterials
                 where entry.ContactMaterial != null
                 select entry.ContactMaterial ).ToArray();
      }
    }

    public void Add( ContactMaterial contactMaterial )
    {
      if ( contactMaterial == null || ContactMaterials.Contains( contactMaterial ) )
        return;

      m_contactMaterials.Add( new ContactMaterialEntry() { ContactMaterial = contactMaterial } );
    }

    public void Remove( ContactMaterial contactMaterial )
    {
      int index = -1;
      while ( ( index = Array.FindIndex( ContactMaterials, cm => { return cm == contactMaterial; } ) ) >= 0 )
        m_contactMaterials.RemoveAt( index );
    }

    public void RemoveNullEntries()
    {
      int index = 0;
      while ( index < m_contactMaterials.Count ) {
        if ( m_contactMaterials[ index ].ContactMaterial == null )
          m_contactMaterials.RemoveAt( index );
        else
          ++index;
      }
    }

    protected override bool Initialize()
    {
      RemoveNullEntries();

      foreach ( var entry in m_contactMaterials ) {
        ContactMaterial contactMaterial = entry.ContactMaterial.GetInitialized<ContactMaterial>();
        if ( contactMaterial != null && contactMaterial.Native != null )
          GetSimulation().getMaterialManager().add( contactMaterial.Native );
      }
      return base.Initialize();
    }
  }
}
