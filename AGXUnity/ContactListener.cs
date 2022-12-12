using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Contact listener data used by the ContactEventHandler.
  /// </summary>
  public class ContactListener
  {
    /// <summary>
    /// Components this contact listener is listening to.
    /// </summary>
    public ScriptComponent[] Components { get; protected set; } = null;

    /// <summary>
    /// Callback taking ContactData for a matching component.
    /// </summary>
    public ContactEventHandler.OnContactDelegate ContactCallback { get; protected set; } = null;

    /// <summary>
    /// Callback taking SeparationData for a matching component.
    /// </summary>
    public ContactEventHandler.OnSeparationDelegate SeparationCallback { get; protected set; } = null;

    /// <summary>
    /// Native filter matching contacts in the simulation.
    /// </summary>
    public agxSDK.ExecuteFilter Filter { get; protected set; } = null;

    /// <summary>
    /// True if removed, otherwise false.
    /// </summary>
    public bool IsRemoved { get; set; } = false;

    /// <summary>
    /// True if this listener is listening to contacts before the contact
    /// is solved.
    /// </summary>
    public bool OnContactEnabled
    {
      get
      {
        return ( m_activationMask & agxSDK.ContactEventListener.ActivationMask.CONTACT ) != 0;
      }
    }

    /// <summary>
    /// True if this listener is listening to contact forces of matching contacts.
    /// </summary>
    public bool OnForceEnabled
    {
      get
      {
        return ( m_activationMask & agxSDK.ContactEventListener.ActivationMask.POST ) != 0;
      }
    }

    /// <summary>
    /// True if this listener is listening to separation events of contacts.
    /// </summary>
    public bool OnSeparationEnabled
    {
      get
      {
        return ( m_activationMask & agxSDK.ContactEventListener.ActivationMask.SEPARATION ) != 0;
      }
    }

    /// <summary>
    /// Construct given callbacks, components and activation mask. If this listener
    /// is listening to OnContact, activation mask should be:
    ///     agxSDK.ContactEventListener.ActivationMask.IMPACT | agxSDK.ContactEventListener.ActivationMask.CONTACT
    /// If this listener is listening to contact forces only, activation mask should be:
    ///     agxSDK.ContactEventListener.ActivationMask.POST
    /// If this listener is listening to separations only, activation mask should be:
    ///     agxSDK.ContactEventListener.ActivationMask.SEPARATION
    /// </summary>
    /// <param name="contactCallback">Callback when a ContactData is available for a matching component.</param>
    /// <param name="separationCallback">Callback when a contact separation is available for a matching component.</param>
    /// <param name="components">Components to match for contacts.</param>
    /// <param name="activationMask">Activation mask.</param>
    public ContactListener( ContactEventHandler.OnContactDelegate contactCallback,
                            ContactEventHandler.OnSeparationDelegate separationCallback,
                            ScriptComponent[] components,
                            agxSDK.ContactEventListener.ActivationMask activationMask )
    {
      Components = components ?? throw new ArgumentNullException( "components" );

      const int contactMask = agxSDK.ContactEventListener.ActivationMask.ALL - agxSDK.ContactEventListener.ActivationMask.SEPARATION;
      if ( contactCallback == null && ( (int)activationMask & contactMask ) != 0 )
        throw new ArgumentNullException( "contactCallback" );
      ContactCallback = contactCallback;

      if ( separationCallback == null && ( activationMask & agxSDK.ContactEventListener.ActivationMask.SEPARATION ) != 0 )
        throw new ArgumentNullException( "separationCallback" );
      SeparationCallback = separationCallback;

      m_activationMask = activationMask;
      m_filteringMode = Components.Length == 0 ?
                          agxSDK.UuidHashCollisionFilter.Mode.MATCH_ALL :
                        Components.Length == 1 ?
                          agxSDK.UuidHashCollisionFilter.Mode.MATCH_OR :
                          agxSDK.UuidHashCollisionFilter.Mode.MATCH_AND;
    }

    /// <summary>
    /// Constructor when subclassed. It's important that Callback is assigned before
    /// this listener is added to the event handler, otherwise it will be ignored.
    /// </summary>
    /// <param name="components">Components to listen to.</param>
    /// <param name="activationMask">Activation mask, default listening to IMPACT + CONTACT.</param>
    protected ContactListener( ScriptComponent[] components = null,
                               agxSDK.ContactEventListener.ActivationMask activationMask = agxSDK.ContactEventListener.ActivationMask.IMPACT |
                                                                                           agxSDK.ContactEventListener.ActivationMask.CONTACT )
    {
      Components = components ?? new ScriptComponent[] { };
      m_activationMask = activationMask;
    }

    /// <summary>
    /// Cast the agxSDK.ExecuteFilter to given type.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <returns>Filter as <typeparamref name="T"/>.</returns>
    public T GetFilter<T>()
      where T : agxSDK.ExecuteFilter
    {
      return Filter as T;
    }

    /// <summary>
    /// Remove the given component with the given UUID from this listener.
    /// </summary>
    /// <param name="uuid">Native UUID of the given component.</param>
    /// <param name="component">Component to remove.</param>
    /// <param name="notifyOnRemove">True to log info about remove status.</param>
    /// <returns>
    /// True if this listener becomes invalid after successfully removing
    /// the component and should be removed.
    /// </returns>
    virtual public bool Remove( uint uuid, ScriptComponent component, bool notifyOnRemove = true )
    {
      var filter = GetFilter<agxSDK.UuidHashCollisionFilter>();
      if ( filter != null ) {
        if ( !filter.contains( uuid ) )
          return false;
        filter.remove( uuid );
      }

      var index = Array.IndexOf( Components, component );
      if ( index < 0 )
        return false;

      Components = Array.FindAll( Components, c => c != component );

      // Remove us if we had one component and now zero, which disqualifies us from MATCH_OR.
      // Remove us if we had two or more components and now less than two, which disqualifies us from MATCH_AND.
      var removeMe = Filter == null ||
                     ( filter != null && filter.getMode() == agxSDK.UuidHashCollisionFilter.Mode.MATCH_OR && Components.Length == 0 ) ||
                     ( filter != null && filter.getMode() == agxSDK.UuidHashCollisionFilter.Mode.MATCH_AND && Components.Length < 2 );

      if ( removeMe && notifyOnRemove )
        Debug.Log( $"AGXUnity.ContactListener: Removing callback {ContactEventHandler.FindCallbackName( ContactCallback )} due " +
                   $"to remove of component {component} with UUID {uuid}." );

      return removeMe;
    }

    /// <summary>
    /// Finds UUIDs of our components and registers the filter to the simulation.
    /// </summary>
    /// <param name="handler">A contact event handler.</param>
    virtual public void Initialize( ContactEventHandler handler )
    {
      if ( Filter != null )
        return;

      var filter = new agxSDK.UuidHashCollisionFilter();
      filter.setMode( m_filteringMode );
      foreach ( var component in Components ) {
        // TODO: This should be a GetOrCreate where handler is assigning UUID.
        var uuid = handler.GetUuid( component );
        if ( uuid == 0u ) {
          Debug.LogWarning( $"AGXUnity.ContactEventHandler: Unknown unique simulation id for component of type {component.GetType().FullName} - " +
                            $"it's not possible match contacts without identifier, ignoring component.",
                            component );
          continue;
        }

        filter.add( uuid );
      }

      Filter = filter;
      handler.GeometryContactHandler.Native.add( Filter, (int)m_activationMask );
    }

    /// <summary>
    /// Disposes the filter and removes listener from the simulation.
    /// </summary>
    /// <param name="handler">The contact event handler this listener was added to.</param>
    /// <returns>True.</returns>
    virtual public bool OnDestroy( ContactEventHandler handler )
    {
      if ( Filter != null && handler.GeometryContactHandler.Native != null )
        handler.GeometryContactHandler.Native.remove( Filter );

      Filter?.Dispose();
      Filter = null;

      return true;
    }

    /// <summary>
    /// Set new activation mask for our filter.
    /// </summary>
    /// <param name="handler">Native contact handler.</param>
    /// <param name="activationMask">New activation mask.</param>
    virtual public void SetActivationMask( agxSDK.MultiCollisionFilterContactHandler handler,
                                           agxSDK.ContactEventListener.ActivationMask activationMask )
    {
      m_activationMask = activationMask;
      if ( handler != null && Filter != null )
        handler.setActivationMask( Filter, (int)m_activationMask );
    }

    protected agxSDK.ContactEventListener.ActivationMask m_activationMask = agxSDK.ContactEventListener.ActivationMask.IMPACT |
                                                                            agxSDK.ContactEventListener.ActivationMask.CONTACT;
    protected agxSDK.UuidHashCollisionFilter.Mode m_filteringMode = agxSDK.UuidHashCollisionFilter.Mode.MATCH_ALL;
  }
}
