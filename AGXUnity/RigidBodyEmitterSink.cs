using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Rigid Body Emitter Sink" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sink" )]
  public class RigidBodyEmitterSink : ScriptComponent
  {
    [SerializeField]
    private Collide.Shape m_shape = null;

    /// <summary>
    /// Sink shape. When an emitted template collides with this shape
    /// the rigid body is removed.
    /// </summary>
    [IgnoreSynchronization]
    public Collide.Shape Shape
    {
      get { return m_shape; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: Invalid to change shape during runtime.", this );
          return;
        }

        m_shape = value;
      }
    }

    [SerializeField]
    private bool m_sinkAll = true;

    /// <summary>
    /// Sink all emitted templates when in contact with our
    /// sink shape. Default: true
    /// </summary>
    [IgnoreSynchronization]
    [HideInInspector]
    public bool SinkAll
    {
      get { return m_sinkAll; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: Invalid to change sink all during runtime.", this );
          return;
        }

        m_sinkAll = value;
      }
    }

    /// <summary>
    /// Emitter templates being removed by this sink.
    /// </summary>
    [HideInInspector]
    public RigidBody[] Templates { get { return m_templates.ToArray(); } }

    /// <summary>
    /// Add emitter template to this sink. Instances of this
    /// template will be removed when in contact with our shape.
    /// </summary>
    /// <param name="template">Template to add.</param>
    /// <returns>True if added, otherwise false.</returns>
    public bool AddTemplate( RigidBody template )
    {
      if ( template == null || ContainsTemplate( template ) )
        return false;

      m_templates.Add( template );

      return true;
    }

    /// <summary>
    /// Remove template from this sink.
    /// </summary>
    /// <param name="template">Template to remove.</param>
    /// <returns>True if removed, otherwise false.</returns>
    public bool RemoveTemplate( RigidBody template )
    {
      if ( template == null )
        return false;

      return m_templates.Remove( template );
    }

    /// <summary>
    /// Checks whether given template is part of this sink.
    /// </summary>
    /// <param name="template">Template to check.</param>
    /// <returns>True if associated to this sink, otherwise false.</returns>
    public bool ContainsTemplate( RigidBody template )
    {
      return m_templates.Contains( template );
    }

    /// <summary>
    /// Removes templates that has been deleted.
    /// </summary>
    public void RemoveInvalidTemplates()
    {
      m_templates.RemoveAll( template => template == null );
    }

    protected override void OnEnable()
    {
      if ( m_contactListener != null )
        m_contactListener.setEnable( true );
    }

    protected override void OnDisable()
    {
      if ( m_contactListener != null ) {
        m_contactListener.setEnable( false );
        m_contactListener.InstancesToRemove.Clear();
      }
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.Native.remove( m_contactListener );
        Simulation.Instance.StepCallbacks.SimulationPre -= OnPreStep;
      }
      m_contactListener?.Dispose();
      m_contactListener = null;

      base.OnDestroy();
    }

    protected override bool Initialize()
    {
      RemoveInvalidTemplates();

      if ( Shape == null ) {
        Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: No shape registered to this sink.", this );
        return false;
      }
      if ( Shape.GetInitialized<Collide.Shape>() == null ) {
        Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: Sink shape failed to initialize.", Shape );
        return false;
      }

      m_emitters = FindObjectsIncudingDisabledOfType<RigidBodyEmitter>();

      if ( m_emitters.Length == 0 ) {
        m_emitters = null;
        return false;
      }

      if ( SinkAll ) {
        m_templates.Clear();

        foreach ( var emitter in m_emitters ) {
          foreach ( var template in emitter.Templates )
            if ( !m_templates.Contains( template ) )
              m_templates.Add( template );
        }
      }

      if ( m_templates.Count == 0 ) {
        Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: Zero emitter templates associated to this sink.", this );
        m_emitters = null;
        return false;
      }

      var templateGroupNames = new List<string>();
      var baseGroupName = $"es_{Shape.GetInstanceID().ToString()}";
      foreach ( var template in m_templates ) {
        foreach ( var emitter in m_emitters ) {
          if ( !emitter.ContainsTemplate( template ) )
            continue;
          var templateGroupName = $"{baseGroupName}_{template.GetInstanceID()}";
          var nativeTemplate = emitter.GetNativeTemplate( template );
          if ( nativeTemplate != null ) {
            foreach ( var geometry in nativeTemplate.getGeometries() )
              geometry.addGroup( templateGroupName );
            templateGroupNames.Add( templateGroupName );
          }
        }
      }

      if ( templateGroupNames.Count == 0 ) {
        Debug.LogWarning( "AGXUnity.RigidBodyEmitterSink: Zero templates associated to this sink has an emitter.", this );
        m_emitters = null;
        return false;
      }

      m_contactListener = new SinkContactListener( Shape.NativeGeometry, templateGroupNames );
      GetSimulation().add( m_contactListener );

      Simulation.Instance.StepCallbacks.SimulationPre += OnPreStep;

      return true;
    }

    public static T[] FindObjectsIncudingDisabledOfType<T>()
      where T : Component
    {
#if UNITY_2020_1_OR_NEWER
      return FindObjectsOfType<T>( true );
#else
      var components = new List<T>();
      for ( int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; ++i ) {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt( i );
        if ( !scene.isLoaded )
          continue;
        foreach ( var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects() )
          components.AddRange( go.GetComponentsInChildren<T>( true ) );
      }

      return components.ToArray();
#endif
    }

    private void Reset()
    {
      Shape = GetComponent<Collide.Shape>();
    }

    private void OnPreStep()
    {
      if ( m_contactListener == null || !m_contactListener.isEnabled() )
        return;

      foreach ( var instanceToRemove in m_contactListener.InstancesToRemove ) {
        foreach ( var emitter in m_emitters )
          if ( emitter.TryDestroy( instanceToRemove ) )
            break;
      }

      m_contactListener.InstancesToRemove.Clear();
    }

    private class SinkContactListener : agxSDK.ContactEventListener
    {
      public List<agx.RigidBody> InstancesToRemove = new List<agx.RigidBody>();

      public SinkContactListener( agxCollide.Geometry sinkGeometry, List<string> templateGroupNames )
        : base( (int)( ActivationMask.IMPACT | ActivationMask.CONTACT ) )
      {
        m_sinkGeometry = sinkGeometry;

        var filter = new agxSDK.CollisionGroupFilter( m_sinkGeometry );
        foreach ( var groupName in templateGroupNames )
          filter.addGroup( groupName );
        setFilter( filter );
      }

      public override KeepContactPolicy impact( double time, agxCollide.GeometryContact geometryContact )
      {
        return HandleContact( geometryContact );
      }

      public override KeepContactPolicy contact( double time, agxCollide.GeometryContact geometryContact )
      {
        return HandleContact( geometryContact );
      }

      private KeepContactPolicy HandleContact( agxCollide.GeometryContact geometryContact )
      {
        var otherGeometry = geometryContact.geometry( 0u ) == m_sinkGeometry ?
                              geometryContact.geometry( 1u ) :
                              geometryContact.geometry( 0u );
        var rb = otherGeometry?.getRigidBody();
        if ( rb == null )
          return KeepContactPolicy.KEEP_CONTACT;

        InstancesToRemove.Add( rb );

        return KeepContactPolicy.REMOVE_CONTACT;
      }

      private agxCollide.Geometry m_sinkGeometry = null;
    }

    [SerializeField]
    private List<RigidBody> m_templates = new List<RigidBody>();

    [System.NonSerialized]
    private RigidBodyEmitter[] m_emitters = null;

    private SinkContactListener m_contactListener = null;
  }
}
