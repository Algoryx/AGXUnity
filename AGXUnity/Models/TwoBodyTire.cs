using System.Collections;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Models
{
  public class TwoBodyTire : ScriptComponent
  {
    /// <summary>
    /// Native instance agxModel.TwoBodyTire, created in Start/Initialize.
    /// </summary>
    public agxModel.TwoBodyTire Native { get; private set; } = null;

    [SerializeField]
    private RigidBody m_tireRigidBody = null;

    /// <summary>
    /// Tire rigid body - the rubber part of a wheel.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public RigidBody TireRigidBody
    {
      get { return m_tireRigidBody; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "TwoBodyTire assign TireRigidBody during runtime isn't supported.", this );
          return;
        }
        m_tireRigidBody = value;
      }
    }

    [SerializeField]
    private float m_tireRadius = 1.0f;

    /// <summary>
    /// Tire (outer) radius.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float TireRadius
    {
      get { return m_tireRadius; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "TwoBodyTire assign TireRadius during runtime isn't supported.", this );
          return;
        }
        m_tireRadius = value;
      }
    }

    [SerializeField]
    private RigidBody m_rimRigidBody = null;

    /// <summary>
    /// Rim rigid body - the rigid inner part of a wheel.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public RigidBody RimRigidBody
    {
      get { return m_rimRigidBody; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "TwoBodyTire assign RimRigidBody during runtime isn't supported.", this );
          return;
        }
        m_rimRigidBody = value;
      }
    }

    [SerializeField]
    private float m_rimRadius = 0.5f;

    /// <summary>
    /// Rim (inner) radius.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float RimRadius
    {
      get { return m_rimRadius; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "TwoBodyTire assign RimRadius during runtime isn't supported.", this );
          return;
        }
        m_rimRadius = value;
      }
    }

    [SerializeField]
    private Constraint m_tireRimConstraint = null;

    /// <summary>
    /// Constraint defined between tire and rim - if present. This constraint
    /// will be disabled when an agxModel.TwoBodyTire instance is created.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public Constraint TireRimConstraint
    {
      get { return m_tireRimConstraint; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "TwoBodyTire assign TireRimConstraint during runtime isn't supported.", this );
          return;
        }
        m_tireRimConstraint = value;
      }
    }

    /// <summary>
    /// Configure this two body tire given constraint between the
    /// tire and rim rigid bodies.
    /// </summary>
    /// <param name="tireRimConstraint">Constraint between tire and rim bodies.</param>
    /// <param name="tireRigidBody">The tire rigid body.</param>
    /// <returns>True if configured successfully, otherwise false.</returns>
    public bool Configure( Constraint tireRimConstraint, RigidBody tireRigidBody )
    {
      if ( tireRimConstraint == null || tireRigidBody == null )
        return false;

      // Will throw if tireRigidBody isn't part of tireRimConstraint.
      try {
        RimRigidBody = tireRimConstraint.AttachmentPair.Other( tireRigidBody );
        if ( RimRigidBody == null )
          throw new Exception( "TwoBodyTire rim rigid body not found in tire <-> rim constraint." );

        TireRimConstraint = tireRimConstraint;
        TireRigidBody     = tireRigidBody;
      }
      catch ( Exception ) {
        return false;
      }

      TireRadius = FindRadius( TireRigidBody );
      RimRadius  = FindRadius( RimRigidBody );

      return true;
    }

    /// <summary>
    /// Assign Tire rigid body and estimate the radius.
    /// </summary>
    /// <param name="tireRigidBody">Tire rigid body.</param>
    /// <returns>True if assigned, otherwise false.</returns>
    public bool SetTire( RigidBody tireRigidBody )
    {
      if ( tireRigidBody == null ) {
        Debug.LogWarning( "TwoBodyTire.SetTire: Tire rigid body is null, use property TireRigidBody instead.", this );
        return false;
      }

      TireRigidBody = tireRigidBody;
      TireRadius    = FindRadius( tireRigidBody );

      return TireRigidBody == tireRigidBody;
    }

    /// <summary>
    /// Assign Rim rigid body and estimate the radius.
    /// </summary>
    /// <param name="rimRigidBody">Rim rigid body.</param>
    /// <returns>True if assigned, otherwise false.</returns>
    public bool SetRim( RigidBody rimRigidBody )
    {
      if ( rimRigidBody == null ) {
        Debug.LogWarning( "TwoBodyTire.SetRim: Rim rigid body is null, use property TireRigidBody instead.", this );
        return false;
      }

      RimRigidBody = rimRigidBody;
      RimRadius    = FindRadius( rimRigidBody );

      return RimRigidBody == rimRigidBody;
    }

    protected override bool Initialize()
    {
      if ( TireRigidBody == null || RimRigidBody == null ) {
        Debug.LogError( "TwoBodyTire failed to initialize: Tire or Rim rigid body is null.", this );
        return false;
      }

      var nativeTireRigidBody = TireRigidBody.GetInitialized<RigidBody>().Native;
      var nativeRimRigidBody  = RimRigidBody.GetInitialized<RigidBody>().Native;
      if ( nativeTireRigidBody == null || nativeRimRigidBody == null ) {
        Debug.LogError( "TwoBodyTire failed to initialize: Tire or Rim rigid body failed to initialize.", this );
        return false;
      }

      var worldRotationAxis = FindRotationAxisWorld();
      var rotationAxisTransform = agx.AffineMatrix4x4.identity();
      if ( worldRotationAxis == Vector3.zero ) {
        Debug.LogWarning( "TwoBodyTire failed to identify rotation axis - assuming Tire local z axis." );
        rotationAxisTransform.setRotate( agx.Quat.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() ) );
      }
      else {
        rotationAxisTransform.setRotate( agx.Quat.rotate( nativeTireRigidBody.getFrame().transformVectorToLocal( worldRotationAxis.ToHandedVec3() ),
                                                          agx.Vec3.Y_AXIS() ) );
      }

      Native = new agxModel.TwoBodyTire( nativeTireRigidBody,
                                         TireRadius,
                                         nativeRimRigidBody,
                                         RimRadius,
                                         rotationAxisTransform );
      GetSimulation().add( Native );

      if ( TireRimConstraint != null && TireRimConstraint.GetInitialized<Constraint>().IsEnabled ) {
        m_tireRimConstraintInitialState = TireRimConstraint.enabled;
        TireRimConstraint.enabled = false;
      }

      return true;
    }

    protected override void OnDestroy()
    {
      if ( GetSimulation() != null && Native != null )
        GetSimulation().remove( Native );

      if ( TireRimConstraint != null )
        TireRimConstraint.enabled = m_tireRimConstraintInitialState;

      Native = null;
    }

    /// <summary>
    /// Estimates radius given shapes and rendering meshes in the rigid
    /// body. The maximum radius found is returned - 0 if nothing was found.
    /// </summary>
    /// <param name="rb">Tire/rim rigid body.</param>
    /// <returns>Radius > 0 if radius was found, otherwise 0.</returns>
    private float FindRadius( RigidBody rb )
    {
      var radiusShapes = FindRadius( rb.GetComponentsInChildren<Collide.Shape>() );
      var radiusMeshes = FindRadius( rb.GetComponentsInChildren<MeshFilter>() );
      // Unsure how reliable shape radius is and it seems that
      // the maximum of the two gives most accurate result. E.g.,
      // the tire has a primitive cylinder encapsulating the whole
      // wheel while the rim probably don't have one encapsulating
      // the rim.
      return Mathf.Max( radiusShapes, radiusMeshes );
    }

    private float FindRadius( Collide.Shape[] shapes )
    {
      float maxRadius = 0.0f;
      foreach ( var shape in shapes ) {
        var radiusProperty = shape.GetType().GetProperty( "Radius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public );
        if ( radiusProperty == null )
          continue;
        maxRadius = Mathf.Max( maxRadius, (float)radiusProperty.GetGetMethod().Invoke( shape, new object[] { } ) );
      }
      return maxRadius;
    }

    private float FindRadius( MeshFilter[] filters )
    {
      float maxRadius = 0.0f;
      foreach ( var filter in filters ) {
        var localBound = filter.sharedMesh.bounds;
        maxRadius = Mathf.Max( maxRadius, localBound.extents.x, localBound.extents.y, localBound.extents.z );
      }
      return maxRadius;
    }

    private Vector3 FindRotationAxisWorld()
    {
      var result = Vector3.zero;
      foreach ( var shape in TireRigidBody.GetComponentsInChildren<Collide.Shape>() ) {
        if ( shape is Collide.Cylinder || shape is Collide.Capsule )
          result = shape.transform.TransformDirection( Vector3.up );
      }
      if ( result != Vector3.zero )
        return result;

      MeshFilter bestFilter = null;
      float maxExtent = 0.0f;
      foreach ( var filter in TireRigidBody.GetComponentsInChildren<MeshFilter>() ) {
        var boundsExtents = filter.sharedMesh.bounds.extents;
        // 1. Max and middle value should be approximately the same.
        // 2. Min value is "much" less than the middle value.
        if ( boundsExtents.MaxValue() < 0.95f * boundsExtents.MiddleValue() &&
             boundsExtents.MinValue() < 0.85f * boundsExtents.MiddleValue() ) {
          if ( boundsExtents.MaxValue() > maxExtent ) {
            maxExtent = boundsExtents.MaxValue();
            bestFilter = filter;
          }
        }
      }

      if ( bestFilter != null ) {
        var localAxis = Vector3.zero;
        localAxis[ bestFilter.sharedMesh.bounds.extents.MinIndex() ] = 1.0f;
        result = bestFilter.transform.TransformDirection( localAxis );
      }

      return result;
    }

    private void DrawGizmos( Color color )
    {
      Gizmos.color = color;
      Native.getHinge().getAttachmentPair().transform();
      var position = Native.getHinge().getAttachment( 0 ).get( agx.Attachment.Transformed.ANCHOR_POS ).ToHandedVector3();
      Gizmos.DrawMesh( Constraint.GetOrCreateGizmosMesh(),
                       position,
                       Quaternion.FromToRotation( Vector3.up,
                                                  Native.getHinge().getAttachment( 0 ).get( agx.Attachment.Transformed.N ).ToHandedVector3() ),
                       0.85f * Rendering.Spawner.Utils.FindConstantScreenSizeScale( position, Camera.current ) * Vector3.one );
    }

    private void OnDrawGizmosSelected()
    {
      if ( Native != null )
        DrawGizmos( Color.Lerp( Color.yellow, Color.green, 0.25f ) );
    }

    private bool m_tireRimConstraintInitialState = true;
  }

  ///// <summary>
  ///// This class exposes the tire model
  ///// It requires an instance of a TwoBodyTireProperties class, otherwise some default values are used
  ///// </summary>
  //public class TwoBodyTire : ScriptComponent
  //{

  //  /// <summary>
  //  /// Name of this Tire component for easier identification
  //  /// </summary>
  //  public string Name;

  //  private const float AGX_EQUIVALENT_EPSILON = (float)1E-5;


  //  [SerializeField]
  //  private float m_radialStiffness = 0.5f * TwoBodyTireProperties.DefaultStiffness;

  //  [SerializeField]
  //  private float m_lateralStiffness = TwoBodyTireProperties.DefaultStiffness;

  //  [SerializeField]
  //  private float m_bendingStiffness = 0.5f * TwoBodyTireProperties.DefaultStiffness;

  //  [SerializeField]
  //  private float m_torsionalStiffness = 10 * TwoBodyTireProperties.DefaultStiffness;



  //  [SerializeField]
  //  private float m_radialDamping = 2 * TwoBodyTireProperties.DefaultDamping;

  //  [SerializeField]
  //  private float m_lateralDamping = TwoBodyTireProperties.DefaultDamping;

  //  [SerializeField]
  //  private float m_bendingDamping = 2 * TwoBodyTireProperties.DefaultDamping;

  //  [SerializeField]
  //  private float m_torsionalDamping = 10 * TwoBodyTireProperties.DefaultDamping;

  //  [SerializeField]
  //  private AGXUnity.RigidBody m_tireBody = null;


  //  public AGXUnity.TwoBodyTireProperties TireProperties=null;


  //  /// <summary>
  //  /// Body for the tire of the wheel
  //  /// </summary>
  //  public AGXUnity.RigidBody TireBody { get { return m_tireBody; } set { m_tireBody = value; inititializeTire(); } }


  //  [SerializeField]
  //  private AGXUnity.RigidBody m_rimBody = null;

  //  /// <summary>
  //  /// Body for the rim of the tire
  //  /// </summary>
  //  public AGXUnity.RigidBody RimBody { get { return m_rimBody; } set { m_rimBody = value; inititializeTire(); } }

  //  private agxModel.TwoBodyTire m_tireModel = null;

  //  // Use this for initialization
  //  protected override bool Initialize()
  //  {
  //    inititializeTire();

  //    return true;
  //  }

  //  protected override void OnEnable()
  //  {
  //    if (m_tireModel == null)
  //      return;

  //    if (m_tireModel.getSimulation() == null)
  //      GetSimulation().add(m_tireModel);
  //  }

  //  protected override void OnDisable()
  //  {
  //    if (m_tireModel == null)
  //      return;

  //    var sim = GetSimulation();
  //    if (sim != null)
  //      sim.remove(m_tireModel);
  //  }

  //  // Update is called once per frame
  //  void Update()
  //  {

  //    // Update the parameter models in case someone has changed the values
  //    updateTireModelParameters();
  //  }


  //  bool equivalent(float lhs, float rhs, float epsilon = AGX_EQUIVALENT_EPSILON)
  //  {
  //    return (lhs + epsilon >= rhs) && (lhs - epsilon <= rhs);
  //  }

  //  private void clearTireModel()
  //  {
  //    if (m_tireModel == null)
  //      return;

  //    m_tireModel.getSimulation().remove(m_tireModel);
  //    m_tireModel = null;
  //  }

  //  private void updateTireModelParameters()
  //  {
  //    // Only update if changed
  //    if (TireProperties && 
  //      equivalent(TireProperties.RadialStiffness, m_radialStiffness) &&
  //      equivalent(TireProperties.LateralStiffness, m_lateralStiffness) &&
  //      equivalent(TireProperties.BendingStiffness, m_bendingStiffness) &&
  //      equivalent(TireProperties.TorsionalStiffness, m_torsionalStiffness) &&

  //      equivalent(TireProperties.RadialDamping, m_radialDamping) &&
  //      equivalent(TireProperties.LateralDamping, m_lateralDamping) &&
  //      equivalent(TireProperties.BendingDamping, m_bendingDamping) &&
  //      equivalent(TireProperties.TorsionalDamping, m_torsionalDamping))
  //      return;

  //    // If not using tire model, just skip this
  //    if (m_tireBody == null || m_rimBody == null)
  //    {
  //      clearTireModel();
  //    }

  //    if (TireProperties)
  //    { 
  //      m_radialStiffness = TireProperties.RadialStiffness;
  //      m_lateralStiffness = TireProperties.LateralStiffness;
  //      m_bendingStiffness = TireProperties.BendingStiffness;
  //      m_torsionalStiffness = TireProperties.TorsionalStiffness;

  //      m_radialDamping = TireProperties.RadialDamping;
  //      m_lateralDamping = TireProperties.LateralDamping;
  //      m_bendingDamping = TireProperties.BendingDamping;
  //      m_torsionalDamping = TireProperties.TorsionalDamping;
  //    }

  //    // This is only used if implicit contact material is used. Should NOT happen really.
  //    m_tireModel.setImplicitFrictionMultiplier(new agx.Vec2(1.2, 0.8));

  //    m_tireModel.setStiffness(m_radialStiffness, agxModel.TwoBodyTire.DeformationMode.RADIAL);
  //    m_tireModel.setStiffness(m_lateralStiffness, agxModel.TwoBodyTire.DeformationMode.LATERAL);
  //    m_tireModel.setStiffness(m_bendingStiffness, agxModel.TwoBodyTire.DeformationMode.BENDING);
  //    m_tireModel.setStiffness(m_torsionalStiffness, agxModel.TwoBodyTire.DeformationMode.TORSIONAL);

  //    // The unit for the translational damping coefficient is force * time/displacement (if using SI: Ns/m)
  //    // The unit for the rotational damping coefficient is torque * time/angular displacement (if using SI: Nms/rad)
  //    m_tireModel.setDampingCoefficient(m_radialDamping, agxModel.TwoBodyTire.DeformationMode.RADIAL);
  //    m_tireModel.setDampingCoefficient(m_lateralDamping, agxModel.TwoBodyTire.DeformationMode.LATERAL);
  //    m_tireModel.setDampingCoefficient(m_bendingDamping, agxModel.TwoBodyTire.DeformationMode.BENDING);
  //    m_tireModel.setDampingCoefficient(m_torsionalDamping, agxModel.TwoBodyTire.DeformationMode.TORSIONAL);
  //  }

  //  protected void inititializeTire()
  //  {
  //    if (!agx.Runtime.instance().isValid() || !agx.Runtime.instance().isModuleEnabled("AgX-Tires"))
  //      Debug.LogError("This Component requires a valid license for the AGX Dynamics module: AgX-Tires");


  //    clearTireModel();

  //    if (m_rimBody == null || m_tireBody == null || m_tireBody.GetInitialized<RigidBody>() == null || m_rimBody.GetInitialized<RigidBody>() == null)
  //      return;

  //    var tire = m_tireBody.GetInitialized<RigidBody>().Native;
  //    var rim = m_rimBody.GetInitialized<RigidBody>().Native;

  //    if (tire == null || rim == null)
  //    {
  //      Debug.LogWarning("Two Tire Model requires two bodies, one for the tire and one for the rim");
  //      return;
  //    }
  //    // Make sure orientation of the wheel is correct
  //    var m = new agx.AffineMatrix4x4(new agx.Quat(new agx.Vec3(0, 0, 1), new agx.Vec3(0, 1, 0)), new agx.Vec3());

  //    // Create a tire model that connects the Tire with the Rim
  //    m_tireModel = new agxModel.TwoBodyTire(tire, 1.0, rim, 0.5, m);

  //    GetSimulation().add(m_tireModel);
  //  }
  //}
}
