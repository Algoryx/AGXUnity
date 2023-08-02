using System;
using System.Linq;
using System.Collections.Generic;
using AGXUnity.Utils;
using AGXUnity.Collide;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Rigid body object. Dynamic, kinematic or static, carrying mass and
  /// inertia. Possible to constrain and contains in general shapes.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Rigid Body" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( MassProperties ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#rigid-body" )]
  public class RigidBody : ScriptComponent
  {
    /// <summary>
    /// Finds shapes belonging to <paramref name="gameObject"/> where
    /// <paramref name="gameObject"/> could be part of an articulated
    /// system where gameObject.GetComponentsInChildren( typeof( Shape ) )
    /// could return the wrong set of shapes.
    /// </summary>
    /// <param name="gameObject">Game object to find shapes for.</param>
    /// <returns>Array of shapes associated to <paramref name="gameObject"/>.</returns>
    public static Shape[] FindShapes( GameObject gameObject )
    {
      var shapes = new Shape[] { };
      if ( gameObject == null )
        return shapes;

      if ( gameObject.GetComponent<RigidBody>() != null )
        shapes = gameObject.GetComponent<RigidBody>().Shapes;
      else {
        var parentRigidBody = gameObject.GetComponentInParent<RigidBody>();
        shapes = gameObject.GetComponentsInChildren<Shape>();
        // If the parent rigid body is part of an articulated system we match the
        // child shapes against its shapes, so we're excluding shapes that belongs
        // to other rigid bodies.
        if ( parentRigidBody != null && parentRigidBody.HasArticulatedRoot )
          shapes = shapes.Where( shape => parentRigidBody.Shapes.Contains( shape ) ).ToArray();
      }

      return shapes;
    }

    /// <summary>
    /// Native instance.
    /// </summary>
    private agx.RigidBody m_rb = null;

    /// <summary>
    /// Cached mass properties component.
    /// </summary>
    private MassProperties m_massPropertiesComponent = null;
    
    /// <summary>
    /// Cached unity-transform.
    /// </summary>
    private Transform m_transform;

    #region Public Serialized Properties
    /// <summary>
    /// Restoring this from when mass properties were ScriptAsset so that
    /// we can convert it to the component version.
    /// </summary>
    [UnityEngine.Serialization.FormerlySerializedAs( "m_massProperties" )]
    [SerializeField]
    private MassProperties m_massPropertiesAsAsset = null;

    /// <summary>
    /// Motion control of this rigid body, paired with property MotionControl.
    /// </summary>
    [SerializeField]
    private agx.RigidBody.MotionControl m_motionControl = agx.RigidBody.MotionControl.DYNAMICS;

    /// <summary>
    /// Get or set motion control of this rigid body.
    /// </summary>
    [System.ComponentModel.Description( "Change motion control:\n  - STATIC: Not moving, velocity and angular velocity ignored\n" +
                                        "  - KINEMATICS: Infinitely heavy, controlled with velocity and angular velocity\n" +
                                        "  - DYNAMICS: Moved given dynamics" )]
    public agx.RigidBody.MotionControl MotionControl
    {
      get { return m_motionControl; }
      set
      {
        m_motionControl = value;

        if ( m_rb != null )
          m_rb.setMotionControl( value );
      }
    }

    /// <summary>
    /// Toggle if the rigid body should be handled as particle or not.
    /// Paired with property HandleAsParticle.
    /// </summary>
    [SerializeField]
    private bool m_handleAsParticle = false;

    /// <summary>
    /// Toggle if the rigid body should be handled as particle or not.
    /// If particle, the rotational degrees of freedoms are ignored.
    /// </summary>
    public bool HandleAsParticle
    {
      get { return m_handleAsParticle; }
      set
      {
        m_handleAsParticle = value;
        if ( Native != null )
          Native.setHandleAsParticle( m_handleAsParticle );
      }
    }

    /// <summary>
    /// Linear velocity of this rigid body, paired with property LinearVelocity.
    /// </summary>
    [SerializeField]
    private Vector3 m_linearVelocity = new Vector3();

    /// <summary>
    /// Get or set linear velocity of this rigid body.
    /// </summary>
    public Vector3 LinearVelocity
    {
      get { return m_linearVelocity; }
      set
      {
        m_linearVelocity = value;
        if ( Native != null )
          Native.setVelocity( m_linearVelocity.ToHandedVec3() );
      }
    }

    /// <summary>
    /// Angular velocity of this rigid body, paired with property AngularVelocity.
    /// </summary>
    [SerializeField]
    private Vector3 m_angularVelocity = new Vector3();

    /// <summary>
    /// Get or set angular velocity of this rigid body.
    /// </summary>
    public Vector3 AngularVelocity
    {
      get { return m_angularVelocity; }
      set
      {
        m_angularVelocity = value;
        if ( Native != null )
          Native.setAngularVelocity( m_angularVelocity.ToHandedVec3() );
      }
    }

    /// <summary>
    /// Linear velocity damping of this rigid body, paired with property LinearVelocityDamping.
    /// </summary>
    [SerializeField]
    private Vector3 m_linearVelocityDamping = new Vector3( 0, 0, 0 );

    /// <summary>
    /// Get or set linear velocity damping of this rigid body.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector3 LinearVelocityDamping
    {
      get { return m_linearVelocityDamping; }
      set
      {
        m_linearVelocityDamping = value;
        if ( Native != null )
          Native.setLinearVelocityDamping( m_linearVelocityDamping.ToVec3f() );
      }
    }

    /// <summary>
    /// Angular velocity damping of this rigid body, paired with property AngularVelocityDamping.
    /// </summary>
    [SerializeField]
    private Vector3 m_angularVelocityDamping = new Vector3( 0, 0, 0 );

    /// <summary>
    /// Get or set angular velocity damping of this rigid body.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector3 AngularVelocityDamping
    {
      get { return m_angularVelocityDamping; }
      set
      {
        m_angularVelocityDamping = value;
        if ( Native != null )
          Native.setAngularVelocityDamping( m_angularVelocityDamping.ToVec3f() );
      }
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// Get native instance, if initialized.
    /// </summary>
    [HideInInspector]
    public agx.RigidBody Native { get { return m_rb; } }

    /// <summary>
    /// Mass properties of this rigid body.
    /// </summary>
    [HideInInspector]
    public MassProperties MassProperties
    {
      get
      {
        if ( m_massPropertiesComponent == null )
          m_massPropertiesComponent = GetComponent<MassProperties>();
        return m_massPropertiesComponent;
      }
    }

    /// <summary>
    /// Array of shapes belonging to this rigid body instance.
    /// </summary>
    [HideInInspector]
    public Shape[] Shapes
    {
      get
      {
        return State != States.INITIALIZED ?
                 GetShapes() :
                 m_shapesCache;
      }
      private set
      {
        m_shapesCache = value ?? new Shape[] { };
      }
    }

    /// <summary>
    /// True if this rigid body has an articulated root parent.
    /// </summary>
    [HideInInspector]
    public bool HasArticulatedRoot
    {
      get
      {
        return ((State == States.CONSTRUCTED || State == States.DESTROYED) && GetArticulatedRoot() != null) ||
               m_hasArticulatedRoot;
      }
      private set
      {
        m_hasArticulatedRoot = value;
      }
    }

    private Shape[] m_shapesCache = new Shape[] { };
    private bool m_hasArticulatedRoot = false;
    #endregion

    #region Public Methods
    /// <summary>
    /// Finds all shapes belonging to this rigid body instance.
    /// </summary>
    /// <returns>Array of shapes belonging to this rigid body.</returns>
    public Shape[] GetShapes()
    {
      var shapes = new List<Shape>();
      CollectShapes( transform, shapes );
      return shapes.ToArray();
    }

    /// <summary>
    /// Finds articulated root instance in parents.
    /// </summary>
    /// <returns>Articulated root instance if present, otherwise null.</returns>
    public ArticulatedRoot GetArticulatedRoot()
    {
      return GetComponentInParent<ArticulatedRoot>();
    }

    /// <summary>
    /// Updates mass properties script asset with current mass, inertia etc.
    /// </summary>
    public void UpdateMassProperties()
    {
      PeekTemporaryNativeOrGetNative( ( rb, isTemp ) =>
      {
        if ( !isTemp ) {
          rb.getMassProperties().setAutoGenerateMask( (uint)agx.MassProperties.AutoGenerateFlags.AUTO_GENERATE_ALL );
          rb.updateMassProperties();
          rb.getMassProperties().setAutoGenerateMask( 0u );
        }

        MassProperties.SetDefaultCalculated( rb );
      } );
    }

    /// <summary>
    /// Synchronizes native instance position and rotation with current
    /// game object transform position and rotation.
    /// </summary>
    public void SyncNativeTransform()
    {
      SyncNativeTransform( m_rb );
    }

    /// <summary>
    /// During runtime, the shapes that belongs to this rigid body are cached when
    /// this instance is initialized. If additional shapes are added after this
    /// rigid body has been initialized, call this method to update the cache and
    /// for the newly added shapes to be available using the property Shapes.
    /// </summary>
    public void SyncShapesCache()
    {
      m_shapesCache = GetShapes();
    }

    /// <summary>
    /// Peek at a temporary native instance or the current (if initialized).
    /// </summary>
    /// <param name="callback">Callback with temporary or already initialized native instance. Callback signature ( nativeRb, isTemporary ).</param>
    /// <remarks>
    /// Always assume the native instance to be temporary. It's never safe to cache an instance to the native object.
    /// </remarks>
    public void PeekTemporaryNativeOrGetNative( Action<agx.RigidBody, bool> callback )
    {
      if ( callback == null )
        return;

      if ( m_rb != null )
        callback( m_rb, false );
      else {
        using ( var rb = new agx.RigidBody() ) {
          foreach ( var shape in Shapes ) {
            var geometry = shape.CreateTemporaryNative();
            if ( geometry == null )
              continue;

            geometry.setEnable( shape.isActiveAndEnabled );
            if ( shape.Material != null )
              geometry.setMaterial( shape.Material.CreateTemporaryNative() );

            rb.add( geometry, shape.GetNativeRigidBodyOffset( this ) );
          }

          // For center of mass position/rotation to be correct we have to
          // synchronize the native transform given current game object transform.
          SyncNativeTransform( rb );

          callback( rb, true );

          // Hitting "Update" (mass or inertia in the Inspector) several times
          // will crash agx if we don't remove the geometries and shapes.
          while ( rb.getGeometries().Count > 0 ) {
            var geometry = rb.getGeometries()[ 0 ].get();
            if ( geometry.getShapes().Count > 0 )
              geometry.remove( geometry.getShapes()[ 0 ].get() );
            rb.remove( geometry );
          }
        }
      }
    }

    public static agx.RigidBody InstantiateTemplate( RigidBody template, Shape[] shapes )
    {
      if ( template == null )
        return null;

      var native = new agx.RigidBody( template.name );
      foreach ( var shape in shapes ) {
        var geometry = shape.CreateTemporaryNative();

        // shape.isActiveAndEnabled is always false for loaded prefabs.
        geometry.setEnable( shape.enabled );

        if ( shape.Material != null )
          geometry.setMaterial( shape.Material.GetInitialized<ShapeMaterial>().Native );
        native.add( geometry, shape.GetNativeRigidBodyOffset( template ) );
      }

      template.SyncNativeTransform( native );

      // MassProperties (synchronization below) wont write any data if UseDefault = true.
      native.getMassProperties().setAutoGenerateMask( (uint)agx.MassProperties.AutoGenerateFlags.AUTO_GENERATE_ALL );
      native.updateMassProperties();
      template.MassProperties.SetDefaultCalculated( native );
      native.getMassProperties().setAutoGenerateMask( 0u );

      var prevNative = template.m_rb;
      try {
        template.m_rb = native;
        PropertySynchronizer.Synchronize( template );
        PropertySynchronizer.Synchronize( template.MassProperties );
      }
      finally {
        template.m_rb = prevNative;
      }

      return native;
    }

    public bool PatchMassPropertiesAsComponent()
    {
      // Already have mass properties as component - this instance has been patched.
      if ( GetComponent<MassProperties>() != null )
        return false;

      MassProperties mp = gameObject.AddComponent<MassProperties>();
      if ( m_massPropertiesAsAsset != null ) {
        mp.CopyFrom( m_massPropertiesAsAsset );
        DestroyImmediate( m_massPropertiesAsAsset );
        m_massPropertiesAsAsset = null;
      }

      return true;
    }

    public void RestoreLocalDataFrom( agx.RigidBody native )
    {
      if ( native == null )
        throw new ArgumentNullException( "native", "Native object is null." );

      MassProperties.RestoreLocalDataFrom( native );

      enabled = native.getEnable();
      gameObject.SetActive( enabled );

      MotionControl          = native.getMotionControl();
      HandleAsParticle       = native.getHandleAsParticle();
      LinearVelocity         = native.getVelocity().ToHandedVector3();
      LinearVelocityDamping  = native.getLinearVelocityDamping().ToHandedVector3();
      AngularVelocity        = native.getAngularVelocity().ToHandedVector3();
      AngularVelocityDamping = native.getAngularVelocityDamping().ToHandedVector3();
    }
    #endregion

    #region Protected Virtual Methods
    protected override bool Initialize()
    {
      m_transform        = transform;
      Shapes             = GetShapes();
      HasArticulatedRoot = GetArticulatedRoot() != null &&
                           GetArticulatedRoot().enabled;
      
      VerifyConfiguration();

      m_rb = new agx.RigidBody();
      m_rb.setName( name );
      m_rb.setEnable( isActiveAndEnabled );
      m_rb.getMassProperties().setAutoGenerateMask( 0u );

      SyncNativeTransform( m_rb );

      SyncShapes();

      GetSimulation().add( m_rb );

      UpdateMassProperties();

      HandleUpdateCallbacks( isActiveAndEnabled );

      return true;
    }

    protected override void OnEnable()
    {
      HandleEnableDisable( true );
    }

    protected override void OnDisable()
    {
      HandleEnableDisable( false );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().remove( m_rb );

      if ( m_rb != null )
        m_rb.ReturnToPool();

      m_rb               = null;
      m_transform        = null;
      Shapes             = null;
      HasArticulatedRoot = false;

      base.OnDestroy();
    }
    #endregion

    private void HandleEnableDisable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.getEnable() == enable )
        return;

      Native.setEnable( enable );

      if ( !Simulation.HasInstance )
        return;

      HandleUpdateCallbacks( enable );
    }

    private void HandleUpdateCallbacks( bool enable )
    {
      // The articulated root component is in charge of the
      // transform synchronization.
      if ( HasArticulatedRoot )
        return;

      if ( enable )
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransformsCallback;
      else
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransformsCallback;
    }

    internal void OnPostSynchronizeTransformsCallback()
    {
      SyncUnityTransform();
      SyncProperties();

      bool debugRenderingEnabled = Rendering.DebugRenderManager.IsActiveForSynchronize &&
                                   Rendering.DebugRenderManager.Instance.isActiveAndEnabled;
      if ( debugRenderingEnabled )
        Rendering.DebugRenderManager.OnPostSynchronizeTransforms( this );
    }

    private void SyncShapes()
    {
      foreach ( var shape in Shapes ) {
        try {
          shape.GetInitialized<Shape>().SetRigidBody( this );
        }
        catch ( System.Exception e ) {
          Debug.LogWarning( "Shape with name: " + shape.name + " failed to initialize. Ignored.", this );
          Debug.LogException( e, shape );
        }
      }
    }

    /// <summary>
    /// Depth first search for shapes. If another rigid body instance is found,
    /// we assume this rigid body is part of an Articulated Root, and stop the
    /// search for that child.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="shapes">Collected shapes along the way.</param>
    private void CollectShapes( Transform parent, List<Shape> shapes )
    {
      if ( parent == null )
        return;
      var rb = parent.GetComponent<RigidBody>();
      if ( rb != null && rb != this )
        return;

      var shape = parent.GetComponent<Shape>();
      if ( shape != null )
        shapes.Add( shape );

      foreach ( Transform child in parent )
        CollectShapes( child, shapes );
    }

    private void SyncUnityTransform()
    {
      if ( m_rb == null )
        return;

      // Local or global here? If we have a parent that moves?
      // If the parent moves, its transform has to be synced
      // down, and that is hard.
      m_transform.SetPositionAndRotation( m_rb.getPosition().ToHandedVector3(),
                                          m_rb.getRotation().ToHandedQuaternion() );
    }

    private void SyncProperties()
    {
      // TODO: If "get" has native we can return the current velocity? Still possible to set.
      if ( m_rb == null )
        return;

      m_linearVelocity = m_rb.getVelocity().ToHandedVector3();
      m_angularVelocity = m_rb.getAngularVelocity().ToHandedVector3();
    }

    private void SyncNativeTransform( agx.RigidBody nativeRb )
    {
      if ( nativeRb == null )
        return;

      nativeRb.setPosition( transform.position.ToHandedVec3() );
      nativeRb.setRotation( transform.rotation.ToHandedQuat() );
    }

    private void VerifyConfiguration()
    {
      // If we have an articulated root that controls the
      // transform synchronization we support other rigid
      // bodies in the hierarchy.
      if ( HasArticulatedRoot )
        return;

      // If there's no articulated root we have to verify
      // so that this rigid body instance doesn't have another
      // rigid body as parent - because it's UB when we don't
      // know the execution order of transform synchronization.
      var parent = transform.parent;
      while ( parent != null ) {
        bool hasBody = parent.GetComponent<RigidBody>() != null;
        if ( hasBody )
          throw new Exception( "An AGXUnity.RigidBody may not have an other AGXUnity.RigidBody as parent." );
        parent = parent.parent;
      }
    }
  }
}
