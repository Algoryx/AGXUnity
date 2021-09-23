using System;

namespace AGXUnity
{
  /// <summary>
  /// Geometry contact handler generating and managing the contact data
  /// for all matching contacts.
  /// </summary>
  public class GeometryContactHandler
  {
    /// <summary>
    /// Native instance if initialized.
    /// </summary>
    public agxSDK.MultiCollisionFilterContactHandler Native { get; private set; }

    /// <summary>
    /// Generated contact data, available after GenerateContactData has been called.
    /// </summary>
    public RefArraySegment<ContactData> ContactData { get; private set; }

    /// <summary>
    /// Contact indices for a given filter. The geometry contact is the
    /// accessible by Native.getGeometryContact( index ).
    /// </summary>
    /// <param name="filter">Filter to get contact indices for.</param>
    /// <returns>Shared vector with contact indices. Do not keep a reference to this vector.</returns>
    public agx.UInt32Vector GetContactIndices( agxSDK.ExecuteFilter filter )
    {
      if ( Native == null )
        return null;

      if ( m_contactIndicies == null )
        m_contactIndicies = new agx.UInt32Vector();

      Native.collectContactIndices( filter, m_contactIndicies );

      return m_contactIndicies;
    }

    /// <summary>
    /// Set new activation mask for the given contact listener.
    /// </summary>
    /// <param name="listener">Contact listener.</param>
    /// <param name="activationMask">New activation mask.</param>
    public void SetActivationMask( ContactListener listener,
                                   agxSDK.ContactEventListener.ActivationMask activationMask )
    {
      if ( listener == null )
        return;

      listener.SetActivationMask( Native, activationMask );
    }

    /// <summary>
    /// Initializes native and adds it to the given simulation.
    /// </summary>
    /// <param name="simulation">Simulation to add the native instance to.</param>
    public void OnInitialize( agxSDK.Simulation simulation )
    {
      if ( Native != null ) {
        if ( Native.getSimulation() == simulation )
          return;
        if ( Native.getSimulation() != null )
          Native.getSimulation().remove( Native );
        Native.Dispose();
      }

      Native = new agxSDK.MultiCollisionFilterContactHandler();
      simulation.add( Native, (int)agxSDK.EventManager.ExecutePriority.LOWEST_PRIORITY );
    }

    /// <summary>
    /// Destroys native instance and removes it from the given simulation.
    /// </summary>
    /// <param name="simulation">Current simulation.</param>
    public void OnDestroy( agxSDK.Simulation simulation )
    {
      if ( Native == null )
        return;

      simulation.remove( Native );
      Native.Dispose();
      Native = null;
    }

    /// <summary>
    /// Generates contact data from the current state of registered contacts in the simulation.
    /// </summary>
    /// <param name="geometryToComponent">Maps agxCollide.Geometry to ScriptComponent.</param>
    /// <param name="hasForce">True if contact force data is available in the simulation.</param>
    public void GenerateContactData( Func<agxCollide.Geometry, ScriptComponent> geometryToComponent,
                                     bool hasForce )
    {
      var numGeometryContacts = Native != null ?
                                  (int)Native.getNumGeometryContacts() :
                                  0;
      var numContactPoints = Native != null ?
                               (int)Native.getNumContactPoints() :
                               0;
      if ( m_contactDataCache == null || numGeometryContacts > m_contactDataCache.Length )
        m_contactDataCache = new ContactData[ numGeometryContacts ];
      if ( m_contactPointDataCache == null || numContactPoints > m_contactPointDataCache.Length )
        m_contactPointDataCache = new ContactPointData[ numContactPoints ];

      ContactData = new RefArraySegment<ContactData>( m_contactDataCache,
                                                      0,
                                                      numGeometryContacts );

      int contactPointStartIndex = 0;
      for ( uint contactIndex = 0u; contactIndex < numGeometryContacts; ++contactIndex ) {
        var gc = Native.getGeometryContact( contactIndex );
        ref var contactData = ref m_contactDataCache[ contactIndex ];

        var g1 = gc.geometry( 0u );
        contactData.Component1 = geometryToComponent( g1 );

        var g2 = gc.geometry( 1u );
        contactData.Component2 = geometryToComponent( g2 );

        contactData.Enabled = gc.isEnabled();

        contactData.Geometry1 = g1;
        contactData.Geometry2 = g2;

        var gcPoints = gc.points();
        var gcNumPoints = (int)gcPoints.size();
        for ( int pointIndex = 0; pointIndex < gcNumPoints; ++pointIndex ) {
          var gcPoint = gcPoints.at( (uint)pointIndex );

          m_contactPointDataCache[ contactPointStartIndex + pointIndex ].From( gcPoint, hasForce );

          gcPoint.ReturnToPool();
        }

        contactData.Points = new RefArraySegment<ContactPointData>( m_contactPointDataCache,
                                                                    contactPointStartIndex,
                                                                    gcNumPoints );
        contactPointStartIndex += gcNumPoints;

        g2.ReturnToPool();
        g1.ReturnToPool();
        gcPoints.ReturnToPool();
        gc.ReturnToPool();
      }
    }

    private ContactData[] m_contactDataCache = null;
    private ContactPointData[] m_contactPointDataCache = null;
    private agx.UInt32Vector m_contactIndicies = null;
  }
}
