using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Contact event handler of a simulation, enabling view and/or modification
  /// of contact data in the given simulation. It's also possible to view the
  /// forces applied by contacts given that the contact has been solved.
  /// 
  /// The callbacks are invoked within the stepping of the simulation for effective
  /// filtering of all the contacts in the simulation given the components registered
  /// for callbacks. This means that some data of the interacting components aren't
  /// synchronized yet. A simplified view into the simulation events:
  ///     1. preCollide events.
  ///     2. Collision detection.
  ///     3. Native contact events.
  ///     4. pre events.
  ///     5. Solve.
  ///     6. post events.
  /// The contacts are collected (on the native side) in (3) and the "OnContact" callbacks
  /// are invoked in (4). "OnForce" callbacks are invoked in (6). In "OnContact" it's
  /// possible to view and manipulate the given contact data while any modification is
  /// ignored in "OnForce" because the changes wont have any effect.
  /// </summary>
  public class ContactEventHandler
  {
    /// <summary>
    /// Signature of the contact callback.
    /// </summary>
    /// <param name="contactData">Matched contact data.</param>
    /// <returns>
    /// True if the given contactData has modifications and should be synchronized
    /// back to the native contact point. False if no modifications has been made.
    /// </returns>
    public delegate bool OnContactDelegate( ref ContactData contactData );

    /// <summary>
    /// Signature of the separation callback.
    /// </summary>
    /// <param name="separationData">Separation data.</param>
    public delegate void OnSeparationDelegate( SeparationData contactData );

    /// <summary>
    /// Finds callback name assuming it's one listener per delegate.
    /// </summary>
    /// <param name="callback">Callback.</param>
    /// <returns>namespace(s).ClassName.MethodName</returns>
    public static string FindCallbackName( OnContactDelegate callback )
    {
      if ( callback == null || callback.GetInvocationList().Length == 0 )
        return "null";

      var targetName = callback.GetInvocationList()[ 0 ].Target != null ?
                         callback.GetInvocationList()[ 0 ].Target.GetType().FullName :
                         "[static class]";
      return targetName + "." + callback.GetInvocationList()[ 0 ].Method.Name;
    }

    /// <summary>
    /// Finds callback name assuming it's one listener per delegate.
    /// </summary>
    /// <param name="callback">Callback.</param>
    /// <returns>namespace(s).ClassName.MethodName</returns>
    public static string FindCallbackName( OnSeparationDelegate callback )
    {
      if ( callback == null || callback.GetInvocationList().Length == 0 )
        return "null";

      var targetName = callback.GetInvocationList()[ 0 ].Target != null ?
                         callback.GetInvocationList()[ 0 ].Target.GetType().FullName :
                         "[static class]";
      return targetName + "." + callback.GetInvocationList()[ 0 ].Method.Name;
    }

    /// <summary>
    /// Geometry contact handler, collecting contact data given registered listeners.
    /// </summary>
    public GeometryContactHandler GeometryContactHandler { get; private set; } = new GeometryContactHandler();

    /// <summary>
    /// Register a contact callback, invoked before the contact has been solved,
    /// given zero or more components. If zero components are given, all contacts
    /// in the simulation will be filtered to the callback. If one component is
    /// given, any object interacting with that component will be filtered to the
    /// callback. If two or more components are given, any interaction between the
    /// given components are filtered to the callback.
    /// </summary>
    /// <param name="onContact">
    /// Callback that takes the contact data and returns true if modifications to
    /// the contact point data has been made. All contact point data is synchronized
    /// with its corresponding native instance which may affect performance if many
    /// contacts has modifications. This callback should normally return false.
    /// </param>
    /// <param name="components">
    /// Components (zero or more) defining which contacts that should be passed to
    /// the given callback. The contact filtering is made given number of components
    /// passed as:
    ///     0: All contacts in the simulation will be passed to the callback.
    ///     1: All contacts interacting with the given component will be passed to the callback.
    ///  >= 2: All contacts between the given components will be passed to the callback.
    /// </param>
    public void OnContact( OnContactDelegate onContact,
                           params ScriptComponent[] components )
    {
      Add( onContact,
           null,
           components,
           agxSDK.ContactEventListener.ActivationMask.IMPACT |
           agxSDK.ContactEventListener.ActivationMask.CONTACT );
    }

    /// <summary>
    /// Register a contact callback, invoked before AND after the contact has been solved,
    /// given zero or more components. If zero components are given, all contacts
    /// in the simulation will be filtered to the callback. If one component is
    /// given, any object interacting with that component will be filtered to the
    /// callback. If two or more components are given, any contacts between the
    /// given components are filtered to the callback.
    /// </summary>
    /// <param name="onContactAndForce">
    /// Callback that takes the contact data and returns true if modifications to
    /// the contact point data has been made. All contact point data is synchronized
    /// with its corresponding native instance which may affect performance if many
    /// contacts has modifications. This callback should normally return false.
    /// contactData.HasContactPointForceData is true the second time the callback
    /// is invoked during the step/frame.
    /// </param>
    /// <param name="components">
    /// Components (zero or more) defining which contacts that should be passed to
    /// the given callback. The contact filtering is made given number of components
    /// passed as:
    ///     0: All contacts in the simulation will be passed to the callback.
    ///     1: All contacts interacting with the given component will be passed to the callback.
    ///  >= 2: All contacts between the given components will be passed to the callback.
    /// </param>
    public void OnContactAndForce( OnContactDelegate onContactAndForce,
                                   params ScriptComponent[] components )
    {
      Add( onContactAndForce,
           null,
           components,
           agxSDK.ContactEventListener.ActivationMask.IMPACT |
           agxSDK.ContactEventListener.ActivationMask.CONTACT |
           agxSDK.ContactEventListener.ActivationMask.POST );
    }

    /// <summary>
    /// Register a contact force callback, invoked after the contact has been solved,
    /// given zero or more components. If zero components are given, all contacts
    /// in the simulation will be filtered to the callback. If one component is
    /// given, any object interacting with that component will be filtered to the
    /// callback. If two or more components are given, any contacts between the
    /// given components are filtered to the callback.
    /// </summary>
    /// <param name="onForce">
    /// Callback that takes the contact data containing the forces applied during
    /// the solve. The return value from the callback is ignored since the contact
    /// already has been solved.
    /// </param>
    /// <param name="components">
    /// Components (zero or more) defining which contacts that should be passed to
    /// the given callback. The contact filtering is made given number of components
    /// passed as:
    ///     0: All contacts in the simulation will be passed to the callback.
    ///     1: All contacts interacting with the given component will be passed to the callback.
    ///  >= 2: All contacts between the given components will be passed to the callback.
    /// </param>
    public void OnForce( OnContactDelegate onForce,
                         params ScriptComponent[] components )
    {
      Add( onForce,
           null,
           components,
           agxSDK.ContactEventListener.ActivationMask.POST );
    }

    /// <summary>
    /// Register a contact separation callback, invoked after a contact has been broken,
    /// given zero or more components. If zero components are given, all separations
    /// in the simulation will be filtered to the callback. If one component is
    /// given, any object separating from that component will be filtered to the
    /// callback. If two or more components are given, any separations between the
    /// given components are filtered to the callback.
    /// </summary>
    /// <param name="onSeparation">
    /// Callback that takes the separation data containing the components/geometries which
    /// are separating.
    /// </param>
    /// <param name="components">
    /// Components (zero or more) defining which separations that should be passed to
    /// the given callback. The separation filtering is made given number of components
    /// passed as:
    ///     0: All separations in the simulation will be passed to the callback.
    ///     1: All separations for the given component will be passed to the callback.
    ///  >= 2: All separations of the any of the components will be passed to the callback.
    /// </param>
    public void OnSeparation( OnSeparationDelegate onSeparation,
                         params ScriptComponent[] components )
    {
      Add( null,
           onSeparation,
           components,
           agxSDK.ContactEventListener.ActivationMask.SEPARATION );
    }

    /// <summary>
    /// Adds contact listener if valid.
    /// </summary>
    /// <param name="listener"></param>
    /// <returns>True if added, otherwise false.</returns>
    public bool Add( ContactListener listener )
    {
      if ( listener == null )
        return false;

      if ( listener.ContactCallback == null && listener.SeparationCallback == null ) {
        Debug.LogError( $"AGXUnity.ContactEventHandler: Invalid contact listener with null callbacks - ignoring listener." );
        return false;
      }

      if ( !m_listeners.Contains( listener ) )
        m_listeners.Add( listener );

      return true;
    }

    /// <summary>
    /// Removes all occurrences of the given callback, i.e., the given callback
    /// will not receive any more calls.
    /// </summary>
    /// <param name="callback">Callback to remove.</param>
    public void Remove( OnContactDelegate callback )
    {
      Remove( listener => listener.ContactCallback == callback );
    }

    /// <summary>
    /// Removes all occurrences of the given callback, i.e., the given callback
    /// will not receive any more calls.
    /// </summary>
    /// <param name="callback">Callback to remove.</param>
    public void Remove( OnSeparationDelegate callback )
    {
      Remove( listener => listener.SeparationCallback == callback );
    }

    /// <summary>
    /// Stop listening to contacts for <paramref name="component"/> in
    /// <paramref name="callbackId"/>.
    /// </summary>
    /// <param name="callbackId">Contact callback identifier.</param>
    /// <param name="component">Component to remove from contact listener <paramref name="callbackId"/>.</param>
    public void Remove( OnContactDelegate callbackId, ScriptComponent component )
    {
      var uuid = GetUuid( component );
      if ( uuid == 0u ) {
        Debug.LogWarning( $"AGXUnity.ContactEventHandler: Failed to remove component {component} from " +
                          $"{FindCallbackName( callbackId )}, native UUID isn't found." );
        return;
      }

      Remove( listener => listener.ContactCallback == callbackId && listener.Remove( uuid, component ) );
    }

    /// <summary>
    /// Stop listening to separations for <paramref name="component"/> in
    /// <paramref name="callbackId"/>.
    /// </summary>
    /// <param name="callbackId">Separation callback identifier.</param>
    /// <param name="component">Component to remove from contact listener <paramref name="callbackId"/>.</param>
    public void Remove( OnSeparationDelegate callbackId, ScriptComponent component )
    {
      var uuid = GetUuid( component );
      if ( uuid == 0u ) {
        Debug.LogWarning( $"AGXUnity.ContactEventHandler: Failed to remove component {component} from " +
                          $"{FindCallbackName( callbackId )}, native UUID isn't found." );
        return;
      }

      Remove( listener => listener.SeparationCallback == callbackId && listener.Remove( uuid, component ) );
    }

    /// <summary>
    /// Remove contact listener(s) given predicate. All matching listeners
    /// will be removed.
    /// </summary>
    /// <param name="predicate">Predicate to match contact listener(s) to remove.</param>
    public void Remove( System.Predicate<ContactListener> predicate )
    {
      if ( m_isPerformingCallbacks ) {
        var listenersToRemove = m_listeners.FindAll( listener => predicate( listener ) );
        listenersToRemove.ForEach( listener => listener.IsRemoved = true );
        m_listenersToRemove.AddRange( listenersToRemove );
      }
      else
        m_listeners.RemoveAll( listener => predicate( listener ) && listener.OnDestroy( this ) );
    }

    /// <summary>
    /// Removes the contact listener(s).
    /// </summary>
    /// <param name="listeners">Listener(s) to remove.</param>
    public void Remove( params ContactListener[] listeners )
    {
      Remove( listener => System.Array.IndexOf( listeners, listener ) >= 0 );
    }

    /// <summary>
    /// Tries to find component corresponding to the given geometry.
    /// </summary>
    /// <param name="geometry">Native geometry to find component for.</param>
    /// <returns>Component if found, otherwise null.</returns>
    public ScriptComponent GetComponent( agxCollide.Geometry geometry )
    {
      if ( geometry == null )
        return null;

      return GetComponent( agxSDK.UuidHashCollisionFilter.findCorrespondingUuid( geometry ) );
    }

    /// <summary>
    /// Checks if a component has be registered with the given native UUID.
    /// </summary>
    /// <param name="uuid">Native UUID.</param>
    /// <returns>Component if registered, otherwise null.</returns>
    public ScriptComponent GetComponent( uint uuid )
    {
      if ( uuid == 0u )
        return null;

      m_uuidComponentTable.TryGetValue( uuid, out var component );
      return component;
    }

    /// <summary>
    /// Finds UUID given component, if registered.
    /// </summary>
    /// <param name="component">Component to find UUID for.</param>
    /// <returns>UUID if registered, otherwise 0.</returns>
    public uint GetUuid( ScriptComponent component )
    {
      if ( component == null )
        return 0u;

      uint uuid = 0u;
      m_componentUuidTable.TryGetValue( component, out uuid );
      return uuid;
    }

    /// <summary>
    /// Maps component and UUID of the given component native instance, if found.
    /// </summary>
    /// <param name="component">Component to map.</param>
    /// <returns>Native UUID if found, otherwise 0.</returns>
    public uint Map( ScriptComponent component )
    {
      var isValid = component != null &&
                    !m_incompatibleTypes.Contains( component.GetType() );
      if ( !isValid )
        return 0u;

      object nativeInstance = null;
      System.Type incompatibleType = null;
      if ( component is Collide.Shape shape )
        nativeInstance = shape.NativeGeometry;
      else {
        var propertyInfo = component.GetType().GetProperty( "Native" );
        if ( propertyInfo != null && propertyInfo.GetGetMethod() != null )
          nativeInstance = propertyInfo.GetValue( component );
        else
          incompatibleType = component.GetType();
      }

      var hash = 0u;
      if ( nativeInstance != null ) {
        hash = agxSDK.UuidHashCollisionFilter.findUuid( nativeInstance as agx.Referenced );

        if ( hash > 0u ) {
          m_uuidComponentTable[ hash ] = component;
          m_componentUuidTable[ component ] = hash;
        }
      }
      else {
        if ( incompatibleType != null )
          m_incompatibleTypes.Add( incompatibleType );
      }

      return hash;
    }

    /// <summary>
    /// Removes the mapping between the given component and its native UUID.
    /// </summary>
    /// <param name="uuid">Native UUID.</param>
    public void Unmap( uint uuid )
    {
      if ( uuid == 0u )
        return;

      if ( m_uuidComponentTable.TryGetValue( uuid, out var component ) ) {
        // Trying to capture when the user hits stop in the editor.
        // isPlayingOrWillChangePlaymode == false by then.
        var isRuntimeDestroy =
#if UNITY_EDITOR
          UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
#else
          Application.isPlaying;
#endif
        if ( isRuntimeDestroy )
          m_listeners.RemoveAll( listener => listener.Remove( uuid, component ) );

        m_componentUuidTable.Remove( component );
        m_uuidComponentTable.Remove( uuid );
      }
    }

    /// <summary>
    /// Called from the Simulation when its native instance has been created.
    /// </summary>
    /// <param name="simulation">Simulation this contact handler belongs to.</param>
    public void OnInitialize( Simulation simulation )
    {
      GeometryContactHandler.OnInitialize( simulation.Native );

      m_getComponentFunc = GetComponent;

      simulation.StepCallbacks.PreStepForward += OnPreStepForward;
      simulation.StepCallbacks._Internal_PrePre += OnPreStep;
      simulation.StepCallbacks._Internal_PrePost += OnPostStep;
    }

    /// <summary>
    /// Called form the simulation when its native instance is about to be deleted.
    /// </summary>
    /// <param name="simulation"></param>
    public void OnDestroy( Simulation simulation )
    {
      foreach ( var listener in m_listeners )
        listener.OnDestroy( this );
      m_listeners.Clear();

      simulation.StepCallbacks.PreStepForward -= OnPreStepForward;
      simulation.StepCallbacks._Internal_PrePre -= OnPreStep;
      simulation.StepCallbacks._Internal_PrePost -= OnPostStep;

      GeometryContactHandler.OnDestroy( simulation.Native );
    }

    private void Add( OnContactDelegate contactCallback,
                      OnSeparationDelegate separationCallback,
                      ScriptComponent[] components,
                      agxSDK.ContactEventListener.ActivationMask activationMask )
    {
      Add( new ContactListener( contactCallback, separationCallback, components, activationMask ) );
    }

    private void OnBeginPerformingCallbacks()
    {
      m_isPerformingCallbacks = true;
    }

    private void OnEndPerformingCallbacks()
    {
      m_isPerformingCallbacks = false;

      foreach ( var listener in m_listenersToRemove )
        Remove( listener );

      m_listenersToRemove.Clear();
    }

    private void OnPreStepForward()
    {
      foreach ( var listener in m_listeners )
        listener.Initialize( this );
    }

    private void OnPreStep()
    {
      TriggerSeparationCallbacks();
      GenerateDataAndExecuteCallbacks( false );
    }

    private void OnPostStep()
    {
      GenerateDataAndExecuteCallbacks( true );
    }

    private void TriggerSeparationCallbacks()
    {
      OnBeginPerformingCallbacks();
      SeparationData separationData = new SeparationData();
      foreach ( var listener in m_listeners ) {
        if ( listener.IsRemoved || !listener.OnSeparationEnabled )
          continue;

        GeometryContactHandler.Native.collectSeparations( listener.Filter, m_separations );
        for ( int i = 0; i < m_separations.Count; i++ ) {
          var sep = m_separations[i];
          var g1 = sep.first;
          var g2 = sep.second;
          separationData.Component1 = GetComponent( g1 );
          separationData.Component2 = GetComponent( g2 );
          separationData.Geometry1 = g1;
          separationData.Geometry2 = g2;
          listener.SeparationCallback( separationData );
          g1.ReturnToPool();
          g2.ReturnToPool();
          sep.ReturnToPool();
        }
      }
      OnEndPerformingCallbacks();
    }

    private void GenerateDataAndExecuteCallbacks( bool hasForce )
    {
      GeometryContactHandler.GenerateContactData( m_getComponentFunc, hasForce );
      if ( GeometryContactHandler.ContactData.Count == 0 )
        return;

      OnBeginPerformingCallbacks();
      var isOnContact = !hasForce;
      foreach ( var listener in m_listeners ) {
        var isListening = ( isOnContact && listener.OnContactEnabled ) ||
                          ( hasForce && listener.OnForceEnabled );
        if ( listener.IsRemoved || !isListening )
          continue;

        var contactIndices = GeometryContactHandler.GetContactIndices( listener.Filter );
        for ( int i = 0; i < contactIndices.Count; ++i ) {
          uint contactIndex = contactIndices[ i ];
          if ( listener.IsRemoved )
            break;

          ref var contactData = ref GeometryContactHandler.ContactData[ (int)contactIndex ];
          var hasModifications = listener.ContactCallback( ref contactData );

          // Ignoring synchronization of any modifications when we are
          // in post, when the changes won't have any effect.
          if ( hasForce || !hasModifications )
            continue;

          var gc = GeometryContactHandler.Native.getGeometryContact( contactIndex );

          gc.setEnable( contactData.Enabled );

          var gcPoints = gc.points();
          for ( int pointIndex = 0; pointIndex < contactData.Points.Count; ++pointIndex ) {
            var gcPoint = gcPoints.at( (uint)pointIndex );
            contactData.Points[ pointIndex ].Synchronize( gcPoint );
            gcPoint.ReturnToPool();
          }
          gcPoints.ReturnToPool();
          gc.ReturnToPool();
        }
      }
      OnEndPerformingCallbacks();
    }

    private List<ContactListener> m_listeners = new List<ContactListener>();
    private List<ContactListener> m_listenersToRemove = new List<ContactListener>();

    private System.Func<agxCollide.Geometry, ScriptComponent> m_getComponentFunc = null;
    private bool m_isPerformingCallbacks = false;
    private agxCollide.GeometryPairVector m_separations = new agxCollide.GeometryPairVector();

    private Dictionary<uint, ScriptComponent> m_uuidComponentTable = new Dictionary<uint, ScriptComponent>();
    private Dictionary<ScriptComponent, uint> m_componentUuidTable = new Dictionary<ScriptComponent, uint>();
    private HashSet<System.Type> m_incompatibleTypes = new HashSet<System.Type>()
    {
      typeof( Utils.OnSelectionProxy ),
      typeof( AttachmentPair ),
      typeof( MassProperties ),
      typeof( CollisionGroups ),
      typeof( WireRoute ),
      typeof( CableRoute ),
      typeof( WireWinch ),
      typeof( HydrodynamicsParameters ),
      typeof( MergeSplitProperties ),
      typeof( Simulation ),
      typeof( ContactMaterialManager ),
      typeof( CollisionGroupsManager ),
      typeof( ScriptAssetManager ),
      typeof( RuntimeObjects ),
      typeof( Rendering.TrackRenderer ),
      typeof( Rendering.WireRenderer ),
      typeof( Rendering.CableRenderer ),
      typeof( Rendering.ShapeVisualBox ),
      typeof( Rendering.ShapeVisualSphere ),
      typeof( Rendering.ShapeVisualCylinder ),
      typeof( Rendering.ShapeVisualCapsule ),
      typeof( Rendering.ShapeVisualPlane ),
      typeof( Rendering.ShapeVisualMesh ),
      typeof( Rendering.ShapeVisualCone ),
      typeof( Rendering.ShapeVisualHollowCone ),
      typeof( Rendering.ShapeVisualHollowCylinder ),
      typeof( Rendering.ShapeDebugRenderData )
    };
  }
}
