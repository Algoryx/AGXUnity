using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Mass properties of a RigidBody.
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
  [DisallowMultipleComponent]
  public class MassProperties : ScriptComponent
  {
    /// <summary>
    /// Only caching reference to our body.
    /// </summary>
    private RigidBody m_rb = null;

    [HideInInspector]
    public RigidBody RigidBody
    {
      get
      {
        if ( m_rb == null )
          m_rb = GetComponent<RigidBody>();
        return m_rb;
      }
    }

    /// <summary>
    /// Mass of the rigid body, holding both calculated and user specified,
    /// paired with property Mass.
    /// </summary>
    [SerializeField]
    private DefaultAndUserValueFloat m_mass = new DefaultAndUserValueFloat();

    /// <summary>
    /// Get or set mass.
    /// </summary>
    [ClampAboveZeroInInspector]
    public DefaultAndUserValueFloat Mass
    {
      get { return m_mass; }
      set
      {
        m_mass = value;

        var native = GetNative();
        if ( native != null ) {
          native.getMassProperties().setMass( m_mass.Value );
          // Explicit inertia tensor and setMass above will rescale
          // the inertia given new mass - assign "back" the user value.
          if ( !m_inertiaDiagonal.UseDefault )
            native.getMassProperties().setInertiaTensor( GetInertiaTensor( m_inertiaDiagonal, m_inertiaOffDiagonal ) );
        }
      }
    }

    /// <summary>
    /// Inertia diagonal of the rigid body, holding both calculated and user specified,
    /// paired with property InertiaDiagonal.
    /// </summary>
    [SerializeField]
    private DefaultAndUserValueVector3 m_inertiaDiagonal = new DefaultAndUserValueVector3();

    /// <summary>
    /// Get or set inertia diagonal.
    /// </summary>
    [ClampAboveZeroInInspector]
    public DefaultAndUserValueVector3 InertiaDiagonal
    {
      get { return m_inertiaDiagonal; }
      set
      {
        m_inertiaDiagonal = value;

        // If we have UseDefault, the inertia tensor has been
        // calculated for the native instance during native.updateMassProperties.
        // To not overwrite the off-diagonal elements we're not
        // writing anything back.
        // NOTE: This has to be revised when we use "update mask" 0.
        if ( m_inertiaDiagonal.UseDefault )
          return;

        var native = GetNative();
        if ( native != null )
          native.getMassProperties().setInertiaTensor( GetInertiaTensor( m_inertiaDiagonal, m_inertiaOffDiagonal ) );
      }
    }

    [SerializeField]
    private DefaultAndUserValueVector3 m_inertiaOffDiagonal = new DefaultAndUserValueVector3();

    /// <summary>
    /// Off-diagonal elements of the inertia. This is currently paired
    /// and used with the diagonal, e.g., when all elements of the
    /// inertia has been given (m_inertiaDiagonal.UseDefault == false and
    /// m_inertiaOffDiagonal.UseDefault == false).
    /// </summary>
    [IgnoreSynchronization]
    [HideInInspector]
    public DefaultAndUserValueVector3 InertiaOffDiagonal
    {
      get { return m_inertiaOffDiagonal; }
      private set { m_inertiaOffDiagonal = value; }
    }

    [SerializeField]
    private DefaultAndUserValueVector3 m_centerOfMassOffset = new DefaultAndUserValueVector3( Vector3.zero, Vector3.zero );

    public DefaultAndUserValueVector3 CenterOfMassOffset
    {
      get { return m_centerOfMassOffset; }
      set
      {
        m_centerOfMassOffset = value;
        var native = GetNative();
        if ( native != null )
          native.getCmFrame().setLocalTranslate( m_centerOfMassOffset.Value.ToHandedVec3() );
      }
    }

    [SerializeField]
    private Vector3 m_massCoefficients = new Vector3( 0.0f, 0.0f, 0.0f );

    [HideInInspector]
    [ClampAboveZeroInInspector(true)]
    public Vector3 MassCoefficients
    {
      get { return m_massCoefficients; }
      set
      {
        m_massCoefficients = value;
        var native = GetNative();
        if ( native != null )
          native.getMassProperties().setMassCoefficients( m_massCoefficients.ToVec3() );
      }
    }

    [SerializeField]
    private Vector3 m_inertiaCoefficients = new Vector3( 0.0f, 0.0f, 0.0f );

    [HideInInspector]
    [ClampAboveZeroInInspector]
    public Vector3 InertiaCoefficients
    {
      get { return m_inertiaCoefficients; }
      set
      {
        m_inertiaCoefficients = value;
        var native = GetNative();
        if ( native != null )
          native.getMassProperties().setInertiaTensorCoefficients( m_inertiaCoefficients.ToVec3() );
      }
    }

    public MassProperties()
    {
      // When the user clicks "Update" in the editor we receive
      // a callback to update mass of the body.
      Mass.OnForcedUpdate               += OnForcedMassInertiaUpdate;
      InertiaDiagonal.OnForcedUpdate    += OnForcedMassInertiaUpdate;
      CenterOfMassOffset.OnForcedUpdate += OnForcedMassInertiaUpdate;

      Mass.OnNewUserValue     += OnUserMassUpdated;
      Mass.OnUseDefaultToggle += OnUseDefaultMassUpdated;

      InertiaDiagonal.OnNewUserValue     += OnUserInertiaUpdated;
      InertiaDiagonal.OnUseDefaultToggle += OnUseDefaultInertiaUpdated;

      CenterOfMassOffset.OnNewUserValue     += OnUserCenterOfMassUpdated;
      CenterOfMassOffset.OnUseDefaultToggle += OnUseDefaultCenterOfMassUpdated;
    }

    /// <summary>
    /// Callback from RigidBody when mass properties has been calculated for a native instance.
    /// </summary>
    /// <param name="nativeRb">Native rigid body instance.</param>
    public void SetDefaultCalculated( agx.RigidBody nativeRb )
    {
      if ( nativeRb == null )
        return;

      Mass.DefaultValue = Convert.ToSingle( nativeRb.getMassProperties().getMass() );
      CenterOfMassOffset.DefaultValue = nativeRb.getCmFrame().getLocalTranslate().ToHandedVector3();

      float inertiaScale = 1.0f;
      if ( !Mass.UseDefault )
        inertiaScale = Mass.UserValue / Mass.DefaultValue;

      InertiaDiagonal.DefaultValue = inertiaScale * nativeRb.getMassProperties().getPrincipalInertiae().ToVector3();
      InertiaOffDiagonal.DefaultValue = inertiaScale * GetNativeOffDiagonal( nativeRb.getMassProperties().getInertiaTensor() ).ToVector3();
    }

    /// <summary>
    /// Callback when the user hits "Update" in the mass/inertia GUI or
    /// to verify the default values are up to date.
    /// </summary>
    public void OnForcedMassInertiaUpdate()
    {
      // Assuming we've an updated default value when the native rigid body is present.
      if ( GetNative() != null )
        return;

      if ( RigidBody != null )
        RigidBody.UpdateMassProperties();
    }

    /// <summary>
    /// Copies values from source instance.
    /// </summary>
    /// <param name="source">Source instance to copy values from.</param>
    public void CopyFrom( MassProperties source )
    {
      m_mass.CopyFrom( source.m_mass );
      m_inertiaDiagonal.CopyFrom( source.m_inertiaDiagonal );
      m_inertiaOffDiagonal.CopyFrom( source.m_inertiaOffDiagonal );
      m_centerOfMassOffset.CopyFrom( source.m_centerOfMassOffset );

      m_massCoefficients    = source.m_massCoefficients;
      m_inertiaCoefficients = source.m_inertiaCoefficients;
    }

    /// <summary>
    /// Reads values from native instance.
    /// </summary>
    /// <param name="native">Source native instance.</param>
    public void RestoreLocalDataFrom( agx.RigidBody native )
    {
      Mass.UserValue = Convert.ToSingle( native.getMassProperties().getMass() );

      var nativeInertia = native.getMassProperties().getInertiaTensor();
      InertiaDiagonal.UserValue = nativeInertia.getDiagonal().ToVector3();
      InertiaOffDiagonal.UserValue = GetNativeOffDiagonal( nativeInertia ).ToVector3();

      CenterOfMassOffset.UserValue = native.getCmFrame().getLocalTranslate().ToHandedVector3();

      Mass.UseDefault = false;
      InertiaDiagonal.UseDefault = false;
      InertiaOffDiagonal.UseDefault = false;
      CenterOfMassOffset.UseDefault = false;
    }

    protected override bool Initialize()
    {
      if ( RigidBody == null ) {
        Debug.LogError( "Unable to find RigidBody component.", this );
        return false;
      }

      RigidBody.GetInitialized<RigidBody>();

      return true;
    }

    protected virtual void Reset()
    {
      hideFlags |= HideFlags.HideInInspector;
    }

    /// <summary>
    /// Finds the native rigid body instance this mass properties belongs to.
    /// </summary>
    /// <returns>Native rigid body instance where native.getMassproperties() == this (native).</returns>
    private agx.RigidBody GetNative()
    {
      return RigidBody != null ? RigidBody.Native : null;
    }

    /// <summary>
    /// Callback when the mass is about the receive a new value. We scale
    /// the default inertia given this new value.
    /// </summary>
    /// <param name="newValue">New mass value.</param>
    private void OnUserMassUpdated( float newValue )
    {
      if ( !Mass.UseDefault && GetNative() != null )
        GetNative().getMassProperties().setMass( newValue );

      float scale = newValue / Mass.Value;
      m_inertiaDiagonal.DefaultValue = scale * m_inertiaDiagonal.DefaultValue;
    }

    /// <summary>
    /// Callback when the inertia is about the receive a new value.
    /// </summary>
    /// <param name="newValue">New inertia diagonal.</param>
    private void OnUserInertiaUpdated( Vector3 newValue )
    {
      if ( !InertiaDiagonal.UseDefault && GetNative() != null )
        GetNative().getMassProperties().setInertiaTensor( GetInertiaTensor( newValue, m_inertiaOffDiagonal ) );
    }

    private void OnUserCenterOfMassUpdated( Vector3 newCenterOfMass )
    {
      if ( !CenterOfMassOffset.UseDefault && GetNative() != null )
        GetNative().getCmFrame().setLocalTranslate( newCenterOfMass.ToHandedVec3() );
    }

    /// <summary>
    /// Called when the user toggles "UseDefault".
    /// </summary>
    /// <param name="newUseDefault">New value of UseDefault (before assigned).</param>
    private void OnUseDefaultMassUpdated( bool newUseDefault )
    {
      if ( newUseDefault == Mass.UseDefault )
        return;

      if ( newUseDefault )
        OnUserMassUpdated( Mass.DefaultValue );
      else
        OnUserMassUpdated( Mass.UserValue );
    }

    private void OnUseDefaultInertiaUpdated( bool newUseDefault )
    {
      if ( newUseDefault == InertiaDiagonal.UseDefault )
        return;

      if ( newUseDefault )
        OnUserInertiaUpdated( InertiaDiagonal.DefaultValue );
      else
        OnUserInertiaUpdated( InertiaDiagonal.UserValue );
    }

    private void OnUseDefaultCenterOfMassUpdated( bool newUseDefault )
    {
      if ( newUseDefault == CenterOfMassOffset.UseDefault )
        return;

      if ( newUseDefault )
        OnUserCenterOfMassUpdated( CenterOfMassOffset.DefaultValue );
      else
        OnUserCenterOfMassUpdated( CenterOfMassOffset.UserValue );
    }

    private static agx.SPDMatrix3x3 GetInertiaTensor( DefaultAndUserValueVector3 diagonal,
                                                      DefaultAndUserValueVector3 offDiagonal )
    {
      if ( diagonal == null || offDiagonal == null )
        throw new ArgumentNullException();
      if ( diagonal.UseDefault )
        throw new Exception( "Don't use GetInertiatensor with non-user defined diagonal." );
      return GetInertiaTensor( diagonal.UserValue, offDiagonal );
    }

    private static agx.SPDMatrix3x3 GetInertiaTensor( Vector3 diagonal,
                                                      DefaultAndUserValueVector3 offDiagonal )
    {
      var inertia = new agx.SPDMatrix3x3( diagonal.ToVec3() );
      // Off-diagonal elements are by default 0 when the user
      // has specified the diagonal.
      if ( !offDiagonal.UseDefault ) {
        inertia.set( offDiagonal.UserValue[ 0 ], 0, 1 );
        inertia.set( offDiagonal.UserValue[ 1 ], 0, 2 );
        inertia.set( offDiagonal.UserValue[ 2 ], 1, 2 );
      }
      return inertia;
    }

    private static agx.Vec3 GetNativeOffDiagonal( agx.SPDMatrix3x3 nativeInertia )
    {
      return new agx.Vec3( nativeInertia.at( 0, 1 ),
                           nativeInertia.at( 0, 2 ),
                           nativeInertia.at( 1, 2 ) );
    }
  }
}
