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

    [SerializeField]
    private bool m_isOriented = false;

    public bool IsOriented
    {
      get { return m_isOriented; }
      set
      {
        var update = m_isOriented != value &&
                     ContactMaterial != null &&
                     ContactMaterial.Native != null;
        m_isOriented = value;
        if ( update )
          UpdateInitializedContactMaterial();
      }
    }

    [SerializeField]
    private GameObject m_referenceObject = null;

    public GameObject ReferenceObject
    {
      get { return m_referenceObject; }
      set
      {
        var update = m_referenceObject != value &&
                     ContactMaterial != null &&
                     ContactMaterial.Native != null;
        m_referenceObject = value;
        if ( update )
          UpdateInitializedContactMaterial();
      }
    }

    [SerializeField]
    private FrictionModel.PrimaryDirection m_primaryDirection = FrictionModel.PrimaryDirection.X;

    public FrictionModel.PrimaryDirection PrimaryDirection
    {
      get { return m_primaryDirection; }
      set
      {
        var update = m_primaryDirection != value &&
                     ContactMaterial != null &&
                     ContactMaterial.Native != null;
        m_primaryDirection = value;
        if ( update )
          UpdateInitializedContactMaterial();
      }
    }

    private void UpdateInitializedContactMaterial()
    {
      ContactMaterial.InitializeOrientedFriction( IsOriented,
                                                  ReferenceObject,
                                                  PrimaryDirection );
    }
  }

  /// <summary>
  /// Contact material manager which enables the user to manage contact materials.
  /// </summary>
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#contact-material-manager" )]
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

      foreach ( var entry in m_contactMaterials )
        Initialize( entry );

      return true;
    }

    private void Initialize( ContactMaterialEntry entry )
    {
      var contactMaterial = entry.ContactMaterial.GetInitialized<ContactMaterial>();
      if ( contactMaterial == null )
        return;

      contactMaterial.InitializeOrientedFriction( entry.IsOriented, entry.ReferenceObject, entry.PrimaryDirection );

      GetSimulation().getMaterialManager().add( contactMaterial.Native );
    }
  }
}
