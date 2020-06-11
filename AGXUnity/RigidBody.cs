using System;
using AGXUnity.Utils;
using AGXUnity.Collide;
using UnityEngine;
using System.Linq;

namespace AGXUnity
{
  /// <summary>
  /// Rigid body object. Dynamic, kinematic or static, carrying mass and
  /// inertia. Possible to constrain and contains in general shapes.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Rigid Body" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( MassProperties ) )]
  public class RigidBody : ScriptComponent
  {
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
    /// Toggle if the rigid body is part of an articulated structure.
    /// By default, a RigidBody cannot exist as a sub-GameObject to another RigidBody
    /// If this flag is set to true, the transformation of the RigidBody will be relative to the parent GameObject.
    /// </summary>
    [SerializeField]
    public bool RelativeTransform = false;

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
    /// True if the game object is active in hierarchy and this component is enabled.
    /// </summary>
    [HideInInspector]
    public bool IsEnabled { get { return gameObject.activeInHierarchy && enabled; } }
    #endregion

    #region Public Methods
    /// <summary>
    /// Updates mass properties script asset with current mass, inertia etc.
    /// </summary>
    public void UpdateMassProperties()
    {
      PeekTemporaryNativeOrGetNative( ( rb, isTemp ) =>
      {
        if ( !isTemp )
          rb.updateMassProperties();

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
        Shape[] shapes = GetShapesInChild();

        using ( agx.RigidBody rb = new agx.RigidBody() ) {
          foreach ( Shape shape in shapes ) {
            agxCollide.Shape nativeShape = shape.CreateTemporaryNative();
            if ( nativeShape != null ) {
              agxCollide.Geometry geometry = new agxCollide.Geometry( nativeShape );

              geometry.setEnable( shape.IsEnabled );

              if ( shape.Material != null )
                geometry.setMaterial( shape.Material.CreateTemporaryNative() );
              rb.add( geometry, shape.GetNativeRigidBodyOffset( this ) );
            }
          }

          // For center of mass position/rotation to be correct we have to
          // synchronize the native transform given current game object transform.
          SyncNativeTransform( rb );

          callback( rb, true );

          // Hitting "Update" (mass or inertia in the Inspector) several times
          // will crash agx if we don't remove the geometries and shapes.
          while ( rb.getGeometries().Count > 0 ) {
            agxCollide.Geometry geometry = rb.getGeometries()[ 0 ].get();
            if ( geometry.getShapes().Count > 0 )
              geometry.remove( geometry.getShapes()[ 0 ].get() );
            rb.remove( geometry );
          }
        }
      }
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

      MassProperties.RestoreLocalDataFrom( native.getMassProperties() );

      enabled                = native.getEnable();
      // Should the body be enabled?
      gameObject.SetActive(native.isEnabled());

      MotionControl = native.getMotionControl();
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
      m_transform = transform;
      
      VerifyConfiguration();

      m_rb = new agx.RigidBody();
      m_rb.setName( name );
      m_rb.setEnable( IsEnabled );

      SyncNativeTransform( m_rb );

      SyncShapes();

      GetSimulation().add( m_rb );

      UpdateMassProperties();

      if ( IsEnabled )
        HandleUpdateCallbacks( true );

      return base.Initialize();
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

      m_rb = null;
      m_transform = null;

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
      if ( enable )
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransformsCallback;
      else
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransformsCallback;
    }

    private void OnPostSynchronizeTransformsCallback()
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
      Shape[] shapes = GetShapesInChild();
      foreach (Shape shape in shapes)
      {
        try
        {
          shape.GetInitialized<Shape>().SetRigidBody(this);
        }
        catch (System.Exception e)
        {
          Debug.LogWarning("Shape with name: " + shape.name + " failed to initialize. Ignored.", this);
          Debug.LogException(e, shape);
        }
      }
    }

    /// <summary>
    /// Return true if the shape should be included in this RigidBody or not
    /// </summary>
    /// <param name="shape"></param>
    /// <returns></returns>
    private bool ShapeIncludedInBody(Shape shape)
    {
      bool flag = true;

      flag = (shape.RigidBody != null && !shape.RigidBody.RelativeTransform) || shape.RigidBody == this;

      return flag;
    }

    /// <summary>
    /// Return the shapes of this RigidBody
    /// </summary>
    /// <returns></returns>
    private Shape[] GetShapesInChild()
    {
      var shapes = GetComponentsInChildren<Shape>();

      // Only return shapes that is direct child of this rigidbody OR (if deeper down, does not belong to another rigidbody which is marked for RelativeTransform)
      Shape[] b = shapes.Where(x => ShapeIncludedInBody(x)).ToArray();
      return b;
    }

    public void SyncUnityTransform(Vector3 pos, Quaternion rot)
    {
      if (m_rb == null)
        return;

      m_transform.localPosition = pos;
      m_transform.localRotation = rot;
    }

    private void SyncUnityTransform()
    {
      if ( m_rb == null )
        return;

      // 
      // If the body is not being child of another RigidBody, we will update the transform here.
      // If on the other hand the body IS a child of another rigidBody, then the update will be 
      // Handled by another class (ArticulatedRigidBodyBase). This is to ensure consistent order of update
      // in the RigidBodt hierarchy 
      if (!RelativeTransform)
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
      // Verification:
      // If this body has RelativeTransform flag set, it can be a child of other RigidBodies
      // But only if there is an ArticulatedRigidBodyBase at the root.
      bool hasArBase = false;

      var parent = transform.parent;
      while ( parent != null ) {
        bool hasBody = parent.GetComponent<RigidBody>() != null;
        var arBase = parent.GetComponent<AGXUnity.ArticulatedRigidBodyBase>();

        hasArBase = hasArBase || (arBase != null && arBase.enabled);

        if ( !RelativeTransform && hasBody)
          throw new Exception( "An AGXUnity.RigidBody may not have an other AGXUnity.RigidBody as parent." );
        parent = parent.parent;
      }

      if (RelativeTransform && !hasArBase)
        throw new Exception(string.Format("An AGXUnity.RigidBody with RelativeTransform == true must have a parent with ArticulatedRigidBodyBase component: {0}", gameObject.name));
    }
  }
}
