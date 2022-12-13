using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

      /// <summary>
      /// Find render resource coupled to the RigidBody template prefab.
      /// If RigidBody has AGXUnity.Rendering.ShapeVisual, the shape visual
      /// game object will be used. If RigidBod has a MeshRenderer the top
      /// parent without AGXUnity.Collide.Shape and/or AGXUnity.RigidBody in
      /// its hierarchy will be used.
      /// </summary>
      /// <returns>Render resource game object if found - otherwise null.</returns>
      public GameObject FindRenderResource()
      {
        if ( RigidBody == null )
          return null;

        // Prefer ShapeVisual, additional objects can be placed as
        // children to the ShapeVisual game object.
        var shapeVisual = RigidBody.GetComponentInChildren<Rendering.ShapeVisual>();
        if ( shapeVisual != null && !HasPhysics( shapeVisual.transform ) )
          return shapeVisual.gameObject;

        var meshRenderer = RigidBody.GetComponentInChildren<MeshRenderer>();
        if ( meshRenderer != null )
          return FindParentWithoutPhysics( meshRenderer.transform );

        return null;
      }

      /// <summary>
      /// Finds top parent without physics components as children.
      /// </summary>
      /// <param name="child">Child transform.</param>
      /// <returns>Top parent game object without physics components - null if not found.</returns>
      private static GameObject FindParentWithoutPhysics( Transform child )
      {
        if ( child == null )
          return null;

        if ( HasPhysics( child ) )
          return null;

        while ( child.parent != null && !HasPhysics( child.parent ) )
          child = child.parent;

        return child?.gameObject;
      }

      private static bool HasPhysics( Transform transform )
      {
        return transform.GetComponentInChildren<Collide.Shape>() != null ||
               transform.GetComponentInChildren<RigidBody>() != null;
      }
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

    /// <summary>
    /// Destroys visual resources and removes the instance from
    /// the simulation if the instance has been emitted from this
    /// emitter (check return value).
    /// </summary>
    /// <param name="instance">Instance to destroy and remove from the simulation.</param>
    /// <returns>True if destroyed and removed, otherwise false.</returns>
    public bool TryDestroy( agx.RigidBody instance )
    {
      if ( m_event == null )
        return false;

      return m_event.TryDestroy( instance );
    }

    /// <summary>
    /// Find native template given prefab template. This is only
    /// valid to access during runtime.
    /// </summary>
    /// <param name="template">Template prefab.</param>
    /// <returns>Native template if found, otherwise null.</returns>
    public agx.RigidBody GetNativeTemplate( RigidBody template )
    {
      if ( !Application.isPlaying )
        return null;

      var index = m_templates.FindIndex( entry => entry.RigidBody == template );
      // Template found, check if we have the instance in the distribution
      // model or we have to create an before we're initialized.
      if ( index >= 0 ) {
        if ( Native != null ) {
          if ( index < m_distributionModels.Count )
            return m_distributionModels[ index ].getBodyTemplate();
        }
        else if ( m_preInitializedTemplates.TryGetValue( template, out var cachedPreInitialized ) )
          return cachedPreInitialized;
        else {
          var templateInstance = RigidBody.InstantiateTemplate( template,
                                                                template.GetComponentsInChildren<Collide.Shape>() );
          if ( templateInstance != null )
            m_preInitializedTemplates.Add( template, templateInstance );
          return templateInstance;
        }
      }

      return null;
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

      m_distributionModels = new List<agx.RigidBodyEmitter.DistributionModel>();
      var msProperties = GetComponent<MergeSplitProperties>()?.GetInitialized<MergeSplitProperties>();
      foreach ( var entry in TemplateEntries ) {
        agx.RigidBody templateInstance = null;
        if ( !m_preInitializedTemplates.TryGetValue( entry.RigidBody, out templateInstance ) )
          templateInstance = RigidBody.InstantiateTemplate( entry.RigidBody,
                                                            entry.RigidBody.GetComponentsInChildren<Collide.Shape>() );
        if ( msProperties != null )
          msProperties.RegisterNativeAndSynchronize( agxSDK.MergeSplitHandler.getOrCreateProperties( templateInstance ) );
        var distributionModel = new agx.RigidBodyEmitter.DistributionModel( templateInstance,
                                                                            entry.ProbabilityWeight );

        NativeDistributionTable.addModel( distributionModel );
        m_distributionModels.Add( distributionModel );

        m_event.MapResource( entry.RigidBody.name,
                             entry.FindRenderResource() );

        // Handling collision group in the template hierarchy.
        // These components aren't instantiated during emit.
        // This enables collision filtering via the CollisionGroupsManager.
        var collisionGroupComponents = entry.RigidBody.GetComponentsInChildren<CollisionGroups>();
        foreach ( var collisionGroupComponent in collisionGroupComponents )
          foreach ( var collisionGroupEntry in collisionGroupComponent.Groups )
            Native.addCollisionGroup( collisionGroupEntry.Tag.To32BitFnv1aHash() );
      }

      m_preInitializedTemplates.Clear();

      Native.setGeometry( EmitterShape.NativeGeometry );

      GetSimulation().add( Native );
      Simulation.Instance.StepCallbacks.PostStepForward += SynchronizeVisuals;
      Simulation.Instance.StepCallbacks.SimulationPreCollide += OnSimulationPreCollide;

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
        Simulation.Instance.StepCallbacks.SimulationPreCollide -= OnSimulationPreCollide;
      }

      Native = null;
      NativeDistributionTable = null;
      m_distributionModels = null;

      m_event?.Dispose();
      m_event = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      RandomSeed = (int)UnityEngine.Random.Range( 0.0f, (float)int.MaxValue - 1 );
      EmitterShape = GetComponent<Collide.Shape>();
    }

    private void OnSimulationPreCollide()
    {
      // The emitter is emitting bodies before simulation pre-collide
      // events are fired. Simulation pre-collide is called from the
      // main thread so it's safe to instantiate visuals from here.
      m_event?.CreateEmittedVisuals();
    }

    private void SynchronizeVisuals()
    {
      m_event?.SynchronizeVisuals();
    }

    private class EmitEvent : agx.RigidBodyEmitterEmitCache
    {
      public EmitEvent( RigidBodyEmitter emitter )
        : base( emitter.Native )
      {
        m_visualRoot = RuntimeObjects.GetOrCreateRoot( emitter );
        var keepAliveGo = new GameObject( $"{m_visualRoot.name}_keepAlive" );
        keepAliveGo.AddComponent<OnSelectionProxy>().Component = emitter;
        m_visualRoot.AddChild( keepAliveGo );
      }

      public void MapResource( string name, GameObject resource )
      {
        if ( resource == null )
          return;
        m_nameResourceTable.Add( name, resource );
      }

      public bool TryDestroy( agx.RigidBody instance )
      {
        if ( !m_instanceDataTable.TryGetValue( instance, out var data ) )
          return false;

        Destroy( data.Visual );
        if ( Simulation.HasInstance && Simulation.Instance.Native != null )
          Simulation.Instance.Native.remove( instance );

        m_instanceDataTable.Remove( instance );

        return true;
      }

      public void CreateEmittedVisuals()
      {
        var emittedBodies = base.getEmittedBodies();
        foreach ( var instance in emittedBodies ) {
          var resourceName = PatchEmittedName( instance.get() );
          if ( m_nameResourceTable.TryGetValue( resourceName, out var resource ) ) {
            var visual = Instantiate( resource );
            if ( m_visualRoot != null )
              visual.transform.SetParent( m_visualRoot.transform );
            visual.transform.position = instance.getPosition().ToHandedVector3();
            visual.transform.rotation = instance.getRotation().ToHandedQuaternion();
            visual.transform.localScale = resource.transform.lossyScale;

            m_instanceDataTable.Add( instance.get(), new EmitData()
            {
              RigidBody = instance.get(),
              Visual = visual
            } );
          }
          else {
            Debug.LogWarning( $"AGXUnity.RigidBodyEmitter: No visual resource matched emitted named \"{resourceName}\"." );

            Simulation.Instance.Native.remove( instance.get() );
          }
        }
        base.clear();
      }

      public void SynchronizeVisuals()
      {
        List<agx.RigidBody> keysToRemove = null;
        foreach ( var data in m_instanceDataTable.Values ) {
          var rb = data.RigidBody;
          var visual = data.Visual;
          if ( visual == null ) {
            if ( keysToRemove == null )
              keysToRemove = new List<agx.RigidBody>();
            keysToRemove.Add( rb );
            continue;
          }

          visual.transform.position = rb.getPosition().ToHandedVector3();
          visual.transform.rotation = rb.getRotation().ToHandedQuaternion();
        }

        var simulation = Simulation.HasInstance ?
                           Simulation.Instance.Native :
                           null;
        for ( int i = 0; keysToRemove != null && i < keysToRemove.Count; ++i ) {
          if ( simulation != null )
            simulation.remove( keysToRemove[ i ] );
          m_instanceDataTable.Remove( keysToRemove[ i ] );
        }
      }

      protected override void Dispose( bool disposing )
      {
        m_newInstances.Clear();
        m_instanceDataTable.Clear();
        m_nameResourceTable.Clear();
        if ( m_visualRoot != null )
          DestroyImmediate( m_visualRoot );
        m_visualRoot = null;
        base.Dispose( disposing );
      }

      private struct EmitData
      {
        public agx.RigidBody RigidBody;
        public GameObject Visual;
      }

      private string PatchEmittedName( agx.RigidBody rb )
      {
        var name = rb.getName();
        var match = s_emittedNamePatchRegex.Match( name );
        if ( match.Success ) {
          // Index 0 is the original name, group index 1 is the name without _emittedNNNN.
          name = match.Groups[ 1 ].Value;
          rb.setName( name );
        }

        return name;
      }

      private static Regex s_emittedNamePatchRegex = new Regex( @"(.*?)(_emitted\d+)$", RegexOptions.Compiled );

      private Dictionary<string, GameObject> m_nameResourceTable = new Dictionary<string, GameObject>();
      private Dictionary<agx.RigidBody, EmitData> m_instanceDataTable = new Dictionary<agx.RigidBody, EmitData>();
      private List<agx.RigidBody> m_newInstances = new List<agx.RigidBody>();
      private GameObject m_visualRoot = null;
    }

    [SerializeField]
    private List<TemplateEntry> m_templates = new List<TemplateEntry>();

    /// <summary>
    /// If this emitter is disabled in some way we must be able to provide
    /// native templates to sinks.
    /// </summary>
    [NonSerialized]
    private Dictionary<RigidBody, agx.RigidBody> m_preInitializedTemplates = new Dictionary<RigidBody, agx.RigidBody>();

    [NonSerialized]
    private EmitEvent m_event = null;

    [NonSerialized]
    private List<agx.RigidBodyEmitter.DistributionModel> m_distributionModels = null;
  }
}
