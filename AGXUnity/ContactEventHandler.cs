using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnity
{
  public class ContactEventHandler
  {
    public delegate bool OnContactDelegate( ref ContactData contactData );

    public agxSDK.Simulation NativeSimulation { get; private set; } = null;

    public GeometryContactHandler GeometryContactHandler { get; private set; } = new GeometryContactHandler();

    public void OnContact( OnContactDelegate onContact,
                           params ScriptComponent[] components )
    {
      Add( onContact,
           components,
           agxSDK.ContactEventListener.ActivationMask.IMPACT |
           agxSDK.ContactEventListener.ActivationMask.CONTACT );
    }

    public void OnContactAndForce( OnContactDelegate onContactAndForce,
                                   params ScriptComponent[] components )
    {
      Add( onContactAndForce,
           components,
           agxSDK.ContactEventListener.ActivationMask.IMPACT |
           agxSDK.ContactEventListener.ActivationMask.CONTACT |
           agxSDK.ContactEventListener.ActivationMask.POST );
    }

    public void OnForce( OnContactDelegate onForce,
                         params ScriptComponent[] components )
    {
      Add( onForce,
           components,
           agxSDK.ContactEventListener.ActivationMask.POST );
    }

    public void Remove( OnContactDelegate onContact )
    {
      if ( m_isPerformingCallbacks ) {
        m_listeners.FindAll( l => l.Callback == onContact ).ForEach( l => l.IsRemoved = true );
        m_callbacksToRemove.Add( onContact );
      }
      else
        m_listeners.RemoveAll( l => l.Callback == onContact && l.OnDestroy( NativeSimulation ) );
    }

    public ScriptComponent GetComponent( agxCollide.Geometry geometry )
    {
      if ( geometry == null )
        return null;

      return GetComponent( agxSDK.UuidHashCollisionFilter.findCorrespondingUuid( geometry ) );
    }

    public ScriptComponent GetComponent( uint uuid )
    {
      if ( uuid == 0u )
        return null;

      m_uuidComponentTable.TryGetValue( uuid, out var component );
      return component;
    }

    public uint GetUuid( ScriptComponent component )
    {
      uint uuid = 0u;
      m_componentUuidTable.TryGetValue( component, out uuid );
      return uuid;
    }

    // TODO: Remove
    private static agx.Timer s_timer = null;
    private static uint s_numCalls = 0u;

    public uint Map( ScriptComponent component )
    {
      if ( component == null )
        return 0u;

      ++s_numCalls;

      if ( s_timer == null )
        s_timer = new agx.Timer( true );
      else
        s_timer.start();

      object nativeInstance = null;
      if ( component is Collide.Shape shape )
        nativeInstance = shape.NativeGeometry;
      else
        nativeInstance = component.GetType().GetProperty( "Native" )?.GetValue( component );

      if ( nativeInstance == null ) {
        s_timer.stop();
        return 0u;
      }

      var hash = agxSDK.UuidHashCollisionFilter.findUuid( nativeInstance as agx.Referenced );
      if ( hash == 0u ) {
        s_timer.stop();
        return 0u;
      }

      m_uuidComponentTable[ hash ] = component;
      m_componentUuidTable[ component ] = hash;

      s_timer.stop();

      return hash;
    }

    public void Print()
    {
      Debug.Log( $"Initialization took: {s_timer.getTime().ToString("0.00")} ms, components " +
                 $"in table: {m_uuidComponentTable.Count}, called: {s_numCalls} -> {s_timer.getTime() / s_numCalls} ms per call." );
    }

    public void Unmap( uint uuidHash )
    {
      if ( uuidHash == 0u )
        return;

      if ( m_uuidComponentTable.TryGetValue( uuidHash, out var component ) ) {
        // TODO: The ScriptComponent is still accessible given this method
        //       is called from OnDestroy. Remove the component from any
        //       listener as well.

        m_componentUuidTable.Remove( component );
        m_uuidComponentTable.Remove( uuidHash );
      }
    }

    public void OnInitialize( agxSDK.Simulation simulation )
    {
      NativeSimulation = simulation;
      Simulation.Instance.StepCallbacks.PreStepForward += OnPreStepForward;
      Simulation.Instance.StepCallbacks._Internal_PrePre += OnPreStep;
      Simulation.Instance.StepCallbacks._Internal_PrePost += OnPostStep;
    }

    public void OnDestroy( agxSDK.Simulation simulation )
    {
      foreach ( var listener in m_listeners )
        listener.OnDestroy( simulation );
      m_listeners.Clear();

      NativeSimulation = null;
    }

    private void Add( OnContactDelegate callback,
                      ScriptComponent[] components,
                      agxSDK.ContactEventListener.ActivationMask activationMask )
    {
      m_listeners.Add( new ContactListener( callback, components, activationMask ) );
    }

    private void OnBeginPerformingCallbacks()
    {
      m_isPerformingCallbacks = true;
    }

    private void OnEndPerformingCallbacks()
    {
      m_isPerformingCallbacks = false;

      foreach ( var callbackToRemove in m_callbacksToRemove )
        Remove( callbackToRemove );
    }

    private void OnPreStepForward()
    {
      foreach ( var listener in m_listeners )
        listener.Initialize( this );
    }

    private void OnPreStep()
    {
      GenerateDataAndExecuteCallbacks( false );
    }

    private void OnPostStep()
    {
      GenerateDataAndExecuteCallbacks( true );
    }

    private void GenerateDataAndExecuteCallbacks( bool hasForce )
    {
      if ( NativeSimulation == null )
        return;

      TimerBlock timer = null; // new TimerBlock( $"Copy {GeometryContactHandler.GeometryContacts.Count} contacts, {numPoints} points" );

      OnBeginPerformingCallbacks();
      using ( GeometryContactHandler.GenerateContactData( GetComponent, hasForce ) ) {
        foreach ( var listener in m_listeners ) {
          if ( listener.NumGeometryContacts == 0 || listener.IsRemoved )
            continue;

          foreach ( var contactIndex in listener.Native.ContactIndices ) {
            if ( listener.IsRemoved )
              break;

            ref var contactData = ref GeometryContactHandler.ContactData[ contactIndex ];
            var hasModifications = listener.Callback( ref contactData );
            // Ignoring synchronization of any modifications when we are
            // in post, when the changes won't have any effect.
            if ( hasForce || !hasModifications )
              continue;

            var gcPoints = GeometryContactHandler.GeometryContacts[ contactIndex ].points();
            for ( int pointIndex = 0; pointIndex < contactData.Points.Count; ++pointIndex ) {
              var gcPoint = gcPoints.at( (uint)pointIndex );
              contactData.Points[ pointIndex ].To( gcPoint );
              gcPoint.ReturnToPool();
            }
            gcPoints.ReturnToPool();
          }
          listener.Native.Reset();
        }
      }
      OnEndPerformingCallbacks();

      timer?.Dispose();
    }

    private class ContactListener
    {
      public ScriptComponent[] Components { get; private set; } = null;

      public OnContactDelegate Callback { get; private set; } = null;

      public NativeUuidContactEvent Native { get; private set; } = null;

      public int NumGeometryContacts { get { return Native != null ? Native.ContactIndices.Count : 0; } }

      public bool IsRemoved { get; set; } = false;

      public ContactListener( OnContactDelegate callback,
                              ScriptComponent[] components,
                              agxSDK.ContactEventListener.ActivationMask activationMask )
      {
        Callback = callback;
        Components = components;
        m_activationMask = activationMask;
      }

      public void Initialize( ContactEventHandler handler )
      {
        if ( Native != null )
          return;

        Native = new NativeUuidContactEvent( handler.GeometryContactHandler, m_activationMask );
        Native.Filter.setMode( Components.Length == 0 ?
                                 agxSDK.UuidHashCollisionFilter.Mode.MATCH_ALL :
                               Components.Length == 1 ?
                                 agxSDK.UuidHashCollisionFilter.Mode.MATCH_OR :
                                 agxSDK.UuidHashCollisionFilter.Mode.MATCH_AND );
        handler.NativeSimulation.add( Native );

        foreach ( var component in Components ) {
          var uuid = handler.GetUuid( component );
          if ( uuid == 0u ) {
            Debug.LogWarning( $"AGXUnity.ContactEventHandler: Unknown unique simulation id for component of type {component.GetType().FullName} - " +
                              $"it's not possible match contacts without identifier, ignoring component.",
                              component );
            continue;
          }

          Native.Filter.add( uuid );
        }
      }

      public bool OnDestroy( agxSDK.Simulation simulation )
      {
        if ( Native == null )
          return true;

        simulation.remove( Native );
        Native.Dispose();
        Native.Filter.Dispose();
        Native = null;

        return true;
      }

      private agxSDK.ContactEventListener.ActivationMask m_activationMask;
    }

    private List<ContactListener> m_listeners = new List<ContactListener>();
    private List<OnContactDelegate> m_callbacksToRemove = new List<OnContactDelegate>();

    private bool m_isPerformingCallbacks = false;

    private Dictionary<uint, ScriptComponent> m_uuidComponentTable = new Dictionary<uint, ScriptComponent>();
    private Dictionary<ScriptComponent, uint> m_componentUuidTable = new Dictionary<ScriptComponent, uint>();
  }

  public class NativeUuidContactEvent : agxSDK.ContactEventListener
  {
    public agxSDK.UuidHashCollisionFilter Filter { get; private set; }

    public List<int> ContactIndices { get; private set; } = new List<int>();

    public NativeUuidContactEvent( GeometryContactHandler geometryContactHandler,
                                   ActivationMask activationMask )
      : base( (int)activationMask )
    {
      Filter = new agxSDK.UuidHashCollisionFilter();
      setFilter( Filter );
      m_geometryContactHandler = geometryContactHandler;
    }

    public void Reset()
    {
      ContactIndices.Clear();
    }

    public override KeepContactPolicy impact( double time, agxCollide.GeometryContact geometryContact )
    {
      ContactIndices.Add( m_geometryContactHandler.GetIndex( geometryContact ) );
      return KeepContactPolicy.KEEP_CONTACT;
    }

    public override KeepContactPolicy contact( double time, agxCollide.GeometryContact geometryContact )
    {
      ContactIndices.Add( m_geometryContactHandler.GetIndex( geometryContact ) );
      return KeepContactPolicy.KEEP_CONTACT;
    }

    public override void post( double time, agxCollide.GeometryContact geometryContact )
    {
      ContactIndices.Add( m_geometryContactHandler.GetIndex( geometryContact ) );
    }

    private GeometryContactHandler m_geometryContactHandler = null;
  }
}
