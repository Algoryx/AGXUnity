using System;
using System.Collections.Generic;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Base class for shapes. This object represents agxCollide.Geometry
  /// and agxCollide.Shape. I.e., this object contains both an instance
  /// to a native agxCollide::Geometry and an agxCollide::Shape.
  /// </summary>
  [DisallowMultipleComponent]
  public abstract class Shape : ScriptComponent
  {
    /// <summary>
    /// Utils (resize etc.) utils for this shape if supported.
    /// </summary>
    private ShapeUtils m_utils = null;

    /// <summary>
    /// Native geometry instance.
    /// </summary>
    protected agxCollide.Geometry m_geometry = null;

    /// <summary>
    /// Native shape instance.
    /// </summary>
    protected agxCollide.Shape m_shape = null;

    /// <summary>
    /// Some value of minimum size of a shape.
    /// </summary>
    [HideInInspector]
    public float MinimumLength { get { return 1.0E-5f; } }

    /// <summary>
    /// Collisions of shape enabled/disabled. Default enabled.
    /// </summary>
    [SerializeField]
    private bool m_collisionsEnabled = true;

    /// <summary>
    /// Enable/disable collisions for this shape.
    /// </summary>
    public bool CollisionsEnabled
    {
      get { return m_collisionsEnabled; }
      set
      {
        m_collisionsEnabled = value;
        if ( NativeGeometry != null )
          NativeGeometry.setEnableCollisions( m_collisionsEnabled );
      }
    }

    /// <summary>
    /// Shape material instance paired with property Material.
    /// </summary>
    [SerializeField]
    private ShapeMaterial m_material = null;
    /// <summary>
    /// Get or set shape material instance.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( m_material != null && m_geometry != null )
          m_geometry.setMaterial( m_material.GetInitialized<ShapeMaterial>().Native );
      }
    }

    /// <summary>
    /// Native geometry object, if initialized.
    /// </summary>
    public agxCollide.Geometry NativeGeometry { get { return m_geometry; } }

    /// <summary>
    /// Native shape objects, if initialized.
    /// </summary>
    public agxCollide.Shape NativeShape { get { return m_shape; } }

    /// <summary>
    /// True if this shape component is enabled, active in hierarchy and if part of a rigid body,
    /// the rigid body is enabled.
    /// </summary>
    [HideInInspector]
    public bool IsEnabledInHierarchy
    {
      get
      {
        RigidBody rb = RigidBody;
        return enabled && gameObject.activeInHierarchy && ( rb == null || rb.enabled );
      }
    }

    /// <summary>
    /// True if the game object this active and this component is enabled.
    /// </summary>
    [HideInInspector]
    public bool IsEnabled
    {
      get
      {
        return gameObject.activeSelf && enabled;
      }
    }

    /// <summary>
    /// Rigid body parent to this shape. Null if 'free' shape.
    /// </summary>
    [HideInInspector]
    public RigidBody RigidBody { get { return GetComponentInParent<RigidBody>(); } }

    private Rendering.ShapeVisual m_visual = null;

    [HideInInspector]
    public Rendering.ShapeVisual Visual
    {
      get
      {
        if ( m_visual == null )
          m_visual = Rendering.ShapeVisual.Find( this );
        return m_visual;
      }
    }

    /// <summary>
    /// Abstract scale. Mainly used in debug rendering which uses unit size
    /// and scale. E.g., a sphere with radius 0.3 m should return (0.6, 0.6, 0.6).
    /// </summary>
    /// <returns>Scale of the shape.</returns>
    public abstract Vector3 GetScale();

    /// <summary>
    /// Creates an instance of the native shape and returns it. This method
    /// shouldn't store an instance to this object, simply create a new instance.
    /// E.g., sphere "return new agxCollide.Sphere( Radius );".
    /// </summary>
    /// <returns>An instance to the native shape.</returns>
    protected abstract agxCollide.Shape CreateNative();

    /// <summary>
    /// Used to calculate things related to our shapes, e.g., CM-offset, mass and inertia.
    /// </summary>
    /// <returns>Native shape to be considered temporary (i.e., probably not defined to keep reference to this shape).</returns>
    public virtual agxCollide.Shape CreateTemporaryNative()
    {
      return CreateNative();
    }

    /// <summary>
    /// The relative transform between the shape and the geometry. E.g., height-field may
    /// want to use this transform to map to unity terrain.
    /// </summary>
    /// <returns>Relative transform geometry -> shape.</returns>
    public virtual agx.AffineMatrix4x4 GetNativeGeometryOffset()
    {
      return new agx.AffineMatrix4x4();
    }

    /// <summary>
    /// The relative transform used between a rigid body and this shape.
    /// </summary>
    /// <returns>Relative transform between rigid body (parent) and this shape, in native format.</returns>
    public agx.AffineMatrix4x4 GetNativeRigidBodyOffset( RigidBody rb )
    {
      // If we're on the same level as the rigid body we have by
      // definition no offset to the body.
      if ( rb == null || rb.gameObject == gameObject )
        return new agx.AffineMatrix4x4();

      // Using the world position of the shape - which includes scaling etc.
      agx.AffineMatrix4x4 shapeInWorld = new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(), transform.position.ToHandedVec3() );
      agx.AffineMatrix4x4 rbInWorld    = new agx.AffineMatrix4x4( rb.transform.rotation.ToHandedQuat(), rb.transform.position.ToHandedVec3() );
      return shapeInWorld * rbInWorld.inverse();
    }

    /// <summary>
    /// Add shape to a rigid body instance.
    /// NOTE: This method is used by the RigidBody object.
    /// </summary>
    /// <param name="rb"></param>
    public void SetRigidBody( RigidBody rb )
    {
      if ( m_geometry == null || m_geometry.getShapes().Count == 0 || m_geometry.getRigidBody() != null )
        return;

      // Search in our game object for rigid body and remove this?
      if ( !rb.gameObject.HasChild( gameObject ) )
        throw new Exception( "RigidBody not parent to Shape." );

      m_geometry.setEnable( IsEnabled );

      rb.Native.add( m_geometry, GetNativeRigidBodyOffset( rb ) );
    }

    /// <summary>
    /// Call this method when the size of the shape has been changed.
    /// </summary>
    public void SizeUpdated()
    {
      // Avoids calling sync of debug rendering when the properties
      // are being synchronized during initialize.
      if ( !IsSynchronizingProperties ) {
        Rendering.DebugRenderManager.SynchronizeScale( this );

        if ( Visual != null )
          Visual.OnSizeUpdated();
      }
    }

    /// <summary>
    /// Returns already created or creates a new instance of the specific
    /// shape utils given type of this shape.
    /// </summary>
    public ShapeUtils GetUtils()
    {
      if ( m_utils == null )
        m_utils = ShapeUtils.Create( this );
      return m_utils;
    }

    /// <summary>
    /// Finds all objects that may be affected when changing a shape. E.g., shape visual.
    /// </summary>
    /// <returns>Array of objects that may be affected when changing this instance.</returns>
    public UnityEngine.Object[] GetUndoCollection()
    {
      var collection = new List<UnityEngine.Object>();
      collection.Add( this );
      collection.AddRange( GetComponentsInChildren<Rendering.ShapeVisual>() );
      collection.AddRange( GetComponentsInChildren<MeshRenderer>() );
      collection.AddRange( GetComponentsInChildren<MeshFilter>() );
      return collection.ToArray();
    }

    /// <summary>
    /// Creates native shape and geometry. Assigns material to the
    /// native geometry if material is present.
    /// </summary>
    /// <returns></returns>
    protected override bool Initialize()
    {
      m_shape = CreateNative();

      if ( m_shape == null )
        return false;

      m_geometry = new agxCollide.Geometry( m_shape, GetNativeGeometryOffset() );
      m_geometry.setName( name );
      m_geometry.setEnable( IsEnabled );

      if ( Material != null )
        m_geometry.setMaterial( m_material.GetInitialized<ShapeMaterial>().Native );

      SyncNativeTransform();

      GetSimulation().add( m_geometry );

      // Temp hack to get "pulley property" of a RigidBody which name
      // contains the name "sheave".
      //RigidBody rbTmp = Find.FirstParentWithComponent<RigidBody>( gameObject );
      //if ( rbTmp != null && rbTmp.gameObject.name.ToLower().Contains( "sheave" ) ) {
      //  Debug.Log( "Adding pulley property to: " + gameObject.name + " from rb.name = " + rbTmp.gameObject.name );
      //  m_geometry.getPropertyContainer().addPropertyBool( "Pulley", true );
      //}

      // TODO: Add pre-synch to be able to move geometries during play?
      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransformsCallback;

      return base.Initialize();
    }

    /// <summary>
    /// When enabled and native instance isn't enabled, enable shape/geometry
    /// and update mass properties (if rigid body is present).
    /// </summary>
    /// <remarks>
    /// This callback is executed when pressing 'Play' or starting an application.
    /// </remarks>
    protected override void OnEnable()
    {
      if ( m_geometry != null && !m_geometry.getEnable() ) {
        m_geometry.setEnable( true );

        var rb = RigidBody;
        if ( rb != null && rb.Native != null )
          rb.UpdateMassProperties();
      }
    }

    /// <summary>
    /// When disabled and native instance is enabled, disable shape/geometry
    /// and update mass properties (if rigid body is present).
    /// </summary>
    /// <remarks>
    /// This callback is executed when pressing 'Stop' or when exiting an application.
    /// </remarks>
    protected override void OnDisable()
    {
      if ( m_geometry != null && m_geometry.getEnable() ) {
        m_geometry.setEnable( false );

        var rb = RigidBody;
        if ( rb != null && rb.Native != null )
          rb.UpdateMassProperties();

        Rendering.DebugRenderManager.OnShapeDisable( this );
      }
    }

    /// <summary>
    /// Removes the native geometry from the simulation.
    /// </summary>
    protected override void OnDestroy()
    {
      if ( m_geometry != null && GetSimulation() != null )
        GetSimulation().remove( m_geometry );

      if ( Simulation.Instance != null )
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransformsCallback;

      if ( m_shape != null )
        m_shape.Dispose();
      m_shape = null;

      if ( m_geometry != null )
        m_geometry.Dispose();
      m_geometry = null;

      base.OnDestroy();
    }

    /// <summary>
    /// Late update call from Unity where stepForward can
    /// be assumed to be done.
    /// </summary>
    private void OnPostSynchronizeTransformsCallback()
    {
      SyncUnityTransform();

      // If we have a body the debug rendering synchronization is made from that body.
      if ( m_geometry != null && m_geometry.getRigidBody() == null )
        Rendering.DebugRenderManager.OnPostSynchronizeTransforms( this );
    }

    /// <summary>
    /// "Forward" synchronize the transform when e.g., the game object
    /// has been moved in the editor.
    /// </summary>
    protected virtual void SyncNativeTransform()
    {
      // Automatic synchronization if we have a parent.
      if ( m_geometry != null && m_geometry.getRigidBody() == null )
        m_geometry.setLocalTransform( new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(), transform.position.ToHandedVec3() ) );
    }

    /// <summary>
    /// "Back" synchronize of transforms given the simulation has
    /// updated the transforms.
    /// </summary>
    protected virtual void SyncUnityTransform()
    {
      if ( transform.parent == null && m_geometry != null ) {
        agx.AffineMatrix4x4 t = m_geometry.getTransform();
        transform.position = t.getTranslate().ToHandedVector3();
        transform.rotation = t.getRotate().ToHandedQuaternion();
      }
    }
  }
}
