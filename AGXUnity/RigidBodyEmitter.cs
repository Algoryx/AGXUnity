using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnity
{
  /// <summary>
  /// Emit rigid bodies (native) given a set of prefab templates. Instances of
  /// the visual representation in the prefab is used for rendering, updating
  /// the transforms given the simulation of the emitted bodies.
  /// </summary>
  public class RigidBodyEmitter : ScriptComponent
  {
    /// <summary>
    /// Quantity in given context.
    /// </summary>
    public enum Quantity
    {
      /// <summary>
      /// The number of emitted objects.
      /// </summary>
      Count,
      /// <summary>
      /// The volume of emitted objects.
      /// </summary>
      Volume,
      /// <summary>
      /// The mass of emitted objects.
      /// </summary>
      Mass
    }

    /// <summary>
    /// Rigid body template reference with probability weight.
    /// </summary>
    [Serializable]
    public class TemplateEntry
    {
      /// <summary>
      /// Template rigid body.
      /// </summary>
      [SerializeField]
      public RigidBody RigidBody = null;

      /// <summary>
      /// Random probability weight.
      /// </summary>
      [SerializeField]
      public float ProbabilityWeight = 0.5f;
    }

    /// <summary>
    /// Convert Quantity to native agx.Emitter.Quantity.
    /// </summary>
    /// <param name="quantity">Managed Quantity.</param>
    /// <returns>Native Quantity matching managed Quantity.</returns>
    public static agx.Emitter.Quantity ToNative( Quantity quantity )
    {
      return (agx.Emitter.Quantity)quantity;
    }

    /// <summary>
    /// Native instance of this emitter, created in Initialize if
    /// the setup is valid.
    /// </summary>
    public agx.RigidBodyEmitter Native { get; private set; } = null;

    /// <summary>
    /// Native distribution table containing the distribution models/templates.
    /// </summary>
    public agx.Emitter.DistributionTable NativeDistributionTable { get; private set; }

    /// <summary>
    /// Rigid body templates added to this emitter.
    /// </summary>
    public RigidBody[] Templates { get { return m_templates.Select( entry => entry.RigidBody ).ToArray(); } }

    /// <summary>
    /// Template entries added to this emitter.
    /// </summary>
    public TemplateEntry[] TemplateEntries { get { return m_templates.ToArray(); } }

    [SerializeField]
    private Quantity m_emittingQuantity = Quantity.Count;

    /// <summary>
    /// Quantity unit Count, Volume or Mass emitted each second.
    /// </summary>
    public Quantity EmittingQuantity
    {
      get { return m_emittingQuantity; }
      set
      {
        m_emittingQuantity = value;
        if ( Native != null )
          Native.setQuantity( ToNative( m_emittingQuantity ) );
      }
    }

    [SerializeField]
    private float m_maximumQuantity = float.PositiveInfinity;

    /// <summary>
    /// Maximum quantity to emit. Default: Infinity.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float MaximumQuantity
    {
      get { return m_maximumQuantity; }
      set
      {
        m_maximumQuantity = value;
        if ( Native != null )
          Native.setMaximumEmittedQuantity( m_maximumQuantity );
      }
    }

    [SerializeField]
    private float m_emitRate = 100.0f;

    /// <summary>
    /// Emit rate of emitting quantity, i.e,:
    ///   Quantity.Count: Number of objects per second.
    ///   Quantity.Volume: Volume of objects per second.
    ///   Quantity.Mass: Mass of objects per second.
    /// Default: 100.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float EmitRate
    {
      get { return m_emitRate; }
      set
      {
        m_emitRate = value;
        if ( Native != null )
          Native.setRate( m_emitRate );
      }
    }

    [SerializeField]
    private Vector3 m_initialVelocity = Vector3.zero;

    /// <summary>
    /// Initial velocity of an emitted body, given in the frame of the
    /// emitter shape.
    /// </summary>
    public Vector3 InitialVelocity
    {
      get { return m_initialVelocity; }
      set
      {
        m_initialVelocity = value;
        if ( Native != null )
          Native.setVelocity( m_initialVelocity.ToHandedVec3() );
      }
    }

    [SerializeField]
    private int m_randomSeed = 617;

    /// <summary>
    /// Random seed used when emitting bodies which are randomly positioned
    /// within the emitter shape and receives some randomized velocity with
    /// respect to the given initial velocity.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public int RandomSeed
    {
      get { return m_randomSeed; }
      set
      {
        m_randomSeed = value;
        if ( Native != null )
          Native.setSeed( (uint)m_randomSeed );
      }
    }

    [SerializeField]
    private Collide.Shape m_emitterShape = null;

    /// <summary>
    /// Emitter shape where the template bodies are emitted inside.
    /// </summary>
    /// <remarks>
    /// This shape must be a sensor or will be explicitly changed to a sensor
    /// (with a warning) when the shape is added to the native emitter.
    /// </remarks>
    [AllowRecursiveEditing]
    [IgnoreSynchronization]
    public Collide.Shape EmitterShape
    {
      get { return m_emitterShape; }
      set { m_emitterShape = value; }
    }

    [SerializeField]
    private Quantity m_probabilityQuantity = Quantity.Count;

    /// <summary>
    /// The probability quantity of the template probability weights.
    /// </summary>
    public Quantity ProbabilityQuantity
    {
      get { return m_probabilityQuantity; }
      set
      {
        m_probabilityQuantity = value;
        if ( NativeDistributionTable != null )
          NativeDistributionTable.setProbabilityQuantity( ToNative( m_probabilityQuantity ) );
      }
    }

    /// <summary>
    /// Add template with given probability weight. <paramref name="template"/> is
    /// assumed to be a prefab and it's probably undefined to have <paramref name="template"/>
    /// as an instance.
    /// </summary>
    /// <param name="template">Template rigid body.</param>
    /// <param name="probabilityWeight">Probability weight of the template body.</param>
    /// <returns>True if added, false if null or already added.</returns>
    public bool AddTemplate( RigidBody template, float probabilityWeight )
    {
      if ( template == null || ContainsTemplate( template ) )
        return false;

      m_templates.Add( new TemplateEntry() { RigidBody = template, ProbabilityWeight = probabilityWeight } );

      return true;
    }

    /// <summary>
    /// Remove template from this emitter.
    /// </summary>
    /// <param name="template">Template rigid body to remove.</param>
    /// <returns>True if removed, false if null or not present.</returns>
    public bool RemoveTemplate( RigidBody template )
    {
      if ( template == null || !ContainsTemplate( template ) )
        return false;

      m_templates.RemoveAt( m_templates.FindIndex( t => t.RigidBody == template ) );

      return true;
    }

    /// <summary>
    /// Find if <paramref name="template"/> is part of this emitter.
    /// </summary>
    /// <param name="template">Template rigid body to check.</param>
    /// <returns>True if <paramref name="template"/> is part of this emitter, otherwise false.</returns>
    public bool ContainsTemplate( RigidBody template )
    {
      return m_templates.Find( t => t.RigidBody == template ) != null;
    }

    /// <summary>
    /// Remove deleted templates.
    /// </summary>
    public void RemoveInvalidTemplates()
    {
      m_templates.RemoveAll( template => template.RigidBody == null );
    }

    protected override bool Initialize()
    {
      RemoveInvalidTemplates();

      if ( m_templates.Count == 0 ) {
        Debug.LogWarning( "Unable to initialize RigidBodyEmitter: 0 valid templates.", this );
        return false;
      }
      if ( EmitterShape == null ) {
        Debug.LogWarning( "Unable to initialize RigidBodyEmitter: Emitter Shape is null.", this );
        return false;
      }
      if ( EmitterShape.GetInitialized<Collide.Shape>() == null ) {
        Debug.LogWarning( $"Unable to initialize RigidBodyEmitter: Emitter Shape {EmitterShape.name} is invalid.", this );
        return false;
      }
      if ( !EmitterShape.IsSensor )
        Debug.LogWarning( $"RigidBodyEmitter Emitter Shape isn't a sensor but will be set to sensor by AGX Dynamics.", this );

      Native = new agx.RigidBodyEmitter();
      Native.setEnable( isActiveAndEnabled );
      m_event = new EmitEvent( this );

      NativeDistributionTable = new agx.Emitter.DistributionTable();
      Native.setDistributionTable( NativeDistributionTable );

      var msProperties = GetComponent<MergeSplitProperties>()?.GetInitialized<MergeSplitProperties>();
      foreach ( var entry in TemplateEntries ) {
        var templateInstance = RigidBody.InstantiateTemplate( entry.RigidBody,
                                                              entry.RigidBody.GetComponentsInChildren<Collide.Shape>() );
        if ( msProperties != null )
          msProperties.RegisterNativeAndSynchronize( agxSDK.MergeSplitHandler.getOrCreateProperties( templateInstance ) );
        NativeDistributionTable.addModel( new agx.RigidBodyEmitter.DistributionModel( templateInstance,
                                                                                      entry.ProbabilityWeight ) );
        m_event.MapResource( entry.RigidBody.name,
                             entry.RigidBody.GetComponentInChildren<Rendering.ShapeVisual>()?.gameObject );
      }

      Native.setGeometry( EmitterShape.NativeGeometry );

      GetSimulation().add( Native );
      Simulation.Instance.StepCallbacks.PostStepForward += SynchronizeVisuals;

      return true;
    }

    protected override void OnEnable()
    {
      if ( Native != null )
        Native.setEnable( true );
    }

    protected override void OnDisable()
    {
      if ( Native != null )
        Native.setEnable( false );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= SynchronizeVisuals;
      }

      Native = null;
      NativeDistributionTable = null;

      m_event?.Dispose();
      m_event = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      RandomSeed = (int)UnityEngine.Random.Range( 0.0f, (float)int.MaxValue - 1 );
      EmitterShape = GetComponent<Collide.Shape>();
    }

    private void SynchronizeVisuals()
    {
      if ( m_event == null )
        return;

      for ( int i = 0; i < m_event.RigidBodies.Count; ++i ) {
        var visual = m_event.Visuals[ i ];
        if ( visual == null )
          continue;
        visual.transform.position = m_event.RigidBodies[ i ].getPosition().ToHandedVector3();
        visual.transform.rotation = m_event.RigidBodies[ i ].getRotation().ToHandedQuaternion();
      }
    }

    private agx.RigidBody CreateTemplate( float radius )
    {
      var rb = new agx.RigidBody( $"{name}_t_{radius.ToString( "0.00" )}" );
      rb.add( new agxCollide.Geometry( new agxCollide.Sphere( radius ) ) );
      rb.setHandleAsParticle( true );
      return rb;
    }

    private class EmitEvent : agx.RigidBodyEmitterEvent
    {
      public List<agx.RigidBody> RigidBodies { get; private set; } = new List<agx.RigidBody>();

      public List<GameObject> Visuals { get; private set; } = new List<GameObject>();

      public EmitEvent( RigidBodyEmitter emitter )
        : base( emitter.Native )
      {
        m_visualRoot = RuntimeObjects.GetOrCreateRoot( emitter );
      }

      public void MapResource( string name, GameObject resource )
      {
        if ( resource == null )
          return;
        m_nameResourceTable.Add( name, resource );
      }

      public override void onEmit( agx.RigidBody instance )
      {
        RigidBodies.Add( instance );
        if ( m_nameResourceTable.TryGetValue( instance.getName(), out var resource ) ) {
          var visual = Instantiate( resource );
          if ( m_visualRoot != null )
            visual.transform.parent = m_visualRoot.transform;
          visual.transform.position = instance.getPosition().ToHandedVector3();
          visual.transform.rotation = instance.getRotation().ToHandedQuaternion();
          Visuals.Add( visual );
        }
        else
          Visuals.Add( null );
      }

      protected override void Dispose( bool disposing )
      {
        RigidBodies.Clear();
        Visuals.Clear();
        m_nameResourceTable.Clear();
        if ( m_visualRoot != null )
          DestroyImmediate( m_visualRoot );
        m_visualRoot = null;
        base.Dispose( disposing );
      }

      private Dictionary<string, GameObject> m_nameResourceTable = new Dictionary<string, GameObject>();
      private GameObject m_visualRoot = null;
    }

    [SerializeField]
    private List<TemplateEntry> m_templates = new List<TemplateEntry>();

    [NonSerialized]
    private EmitEvent m_event = null;
  }
}
