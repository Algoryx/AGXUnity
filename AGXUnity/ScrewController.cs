using System;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class ScrewController : ElementaryConstraintController
  {
    [SerializeField]
    private float m_lead = 0f;
    public float Lead
    {
      get { return m_lead; }
      set
      {
        m_lead = value;
        if ( Native != null )
          agx.ScrewController.safeCast( Native ).setLead( m_lead );
      }
    }

    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_lead = Convert.ToSingle( agx.ScrewController.safeCast( tmpEc ).getLead() );
    }

    protected override void Construct( ElementaryConstraint source )
    {
      base.Construct( source );

      m_lead = ( source as ScrewController ).m_lead;
    }
  }
}
