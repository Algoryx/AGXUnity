using System.Collections;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Model
{
  /// <summary>
  /// Two body tire model where the rubber tire and solid rim are separate
  /// rigid bodies. The elastic behaviors are controlled by radial, lateral,
  /// bend and torsional stiffness and damping coefficients.
  ///
  ///  - Radial stiffness/damping affects translation orthogonal to tire rotation axis.
  ///  - Lateral stiffness/damping affects translation in axis of rotation.
  ///  - Bending stiffness/damping affects rotation orthogonal to axis of rotation.
  ///  - Torsional stiffness/damping affects rotation in axis of rotation.
  ///
  /// The unit for translational stiffness is force/displacement (if using SI: N/m)
  /// The unit for rotational stiffness is torque/angular displacement(if using SI: Nm/rad)
  /// The unit for the translational damping coefficient is force* time/displacement(if using SI: Ns/m)
  /// The unit for the rotational damping coefficient is torque* time/angular displacement(if using SI: Nms/rad)
  ///
  /// Note: This feature requires specific license in AGX Dynamics.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Model/Two Body Tire" )]
  public class TwoBodyTire : Tire
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

    [SerializeField]
    private TwoBodyTireProperties m_properties = null;

    /// <summary>
    /// Radial, lateral, bend and torsional properties of the tire.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public TwoBodyTireProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( m_properties != null )
          m_properties.Unregister( this );

        m_properties = value;

        if ( m_properties != null )
          m_properties.Register( this );
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
        TireRigidBody = tireRigidBody;
      }
      catch ( Exception ) {
        return false;
      }

      TireRadius = FindRadius( TireRigidBody );
      RimRadius = FindRadius( RimRigidBody );

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
      TireRadius = FindRadius( tireRigidBody );

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
      RimRadius = FindRadius( rimRigidBody );

      return RimRigidBody == rimRigidBody;
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTires, this ) )
        return false;

      if ( TireRigidBody == null || RimRigidBody == null ) {
        Debug.LogError( "TwoBodyTire failed to initialize: Tire or Rim rigid body is null.", this );
        return false;
      }

      var nativeTireRigidBody = TireRigidBody.GetInitialized<RigidBody>()?.Native;
      var nativeRimRigidBody = RimRigidBody.GetInitialized<RigidBody>()?.Native;
      if ( nativeTireRigidBody == null || nativeRimRigidBody == null ) {
        Debug.LogError( "TwoBodyTire failed to initialize: Tire or Rim rigid body failed to initialize.", this );
        return false;
      }

      Native = new agxModel.TwoBodyTire( nativeTireRigidBody,
                                         TireRadius,
                                         nativeRimRigidBody,
                                         RimRadius,
                                         FindNativeTransform( TireRigidBody ) );
      GetSimulation().add( Native );

      if ( TireRimConstraint != null ) {
        // Disable the tire rim constraint since agx.TwoBodyTire will
        // create an additional constraint between the tire and the rim
        // with tire properties applied.
        if ( TireRimConstraint.GetInitialized<Constraint>().IsEnabled ) {
          m_tireRimConstraintInitialState = TireRimConstraint.enabled;
          TireRimConstraint.enabled = false;
        }

        // The "hinge" is replacing the TireRimConstraint, take the
        // solve type from disabled rim constraint.
        Native.getHinge().setSolveType( Constraint.Convert( TireRimConstraint.SolveType ) );
      }

      if ( Properties != null )
        Properties.Register( this );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance && Native != null )
        GetSimulation().remove( Native );

      if ( TireRimConstraint != null )
        TireRimConstraint.enabled = m_tireRimConstraintInitialState;

      if ( Properties != null )
        Properties.Unregister( this );

      Native = null;

      base.OnDestroy();
    }

    private bool m_tireRimConstraintInitialState = true;
  }
}