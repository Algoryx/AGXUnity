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

        agx.RigidBody native = GetNative();
        if ( native != null ) {
          native.getMassProperties().setMass( m_mass.Value );
          native.getMassProperties().setInertiaTensor( m_inertiaDiagonal.Value.ToVec3() );
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
        agx.RigidBody native = GetNative();
        if ( native != null )
          native.getMassProperties().setInertiaTensor( m_inertiaDiagonal.Value.ToVec3() );
      }
    }

    [SerializeField]
    private Vector3 m_massCoefficients = new Vector3( 0.0f, 0.0f, 0.0f );
    [ClampAboveZeroInInspector(true)]
    public Vector3 MassCoefficients
    {
      get { return m_massCoefficients; }
      set
      {
        m_massCoefficients = value;
        agx.RigidBody native = GetNative();
        if ( native != null )
          native.getMassProperties().setMassCoefficients( m_massCoefficients.ToVec3() );
      }
    }

    [SerializeField]
    private Vector3 m_inertiaCoefficients = new Vector3( 0.0f, 0.0f, 0.0f );
    [ClampAboveZeroInInspector]
    public Vector3 InertiaCoefficients
    {
      get { return m_inertiaCoefficients; }
      set
      {
        m_inertiaCoefficients = value;
        agx.RigidBody native = GetNative();
        if ( native != null )
          native.getMassProperties().setInertiaTensorCoefficients( m_inertiaCoefficients.ToVec3() );
      }
    }

    public MassProperties()
    {
      // When the user clicks "Update" in the editor we receive
      // a callback to update mass of the body.
      Mass.OnForcedUpdate            += OnForcedMassInertiaUpdate;
      InertiaDiagonal.OnForcedUpdate += OnForcedMassInertiaUpdate;

      Mass.OnNewUserValue     += OnUserMassUpdated;
      Mass.OnUseDefaultToggle += OnUseDefaultMassUpdated;

      InertiaDiagonal.OnNewUserValue     += OnUserInertiaUpdated;
      InertiaDiagonal.OnUseDefaultToggle += OnUseDefaultInertiaUpdated;
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

      float inertiaScale = 1.0f;
      if ( !Mass.UseDefault )
        inertiaScale = Mass.UserValue / Mass.DefaultValue;

      InertiaDiagonal.DefaultValue = inertiaScale * nativeRb.getMassProperties().getPrincipalInertiae().ToVector3();
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

      m_massCoefficients    = source.m_massCoefficients;
      m_inertiaCoefficients = source.m_inertiaCoefficients;
    }

    /// <summary>
    /// Reads values from native instance.
    /// </summary>
    /// <param name="native">Source native instance.</param>
    public void RestoreLocalDataFrom( agx.MassProperties native )
    {
      Mass.UserValue = Convert.ToSingle( native.getMass() );
      InertiaDiagonal.UserValue = native.getPrincipalInertiae().ToVector3();

      Mass.UseDefault = false;
      InertiaDiagonal.UseDefault = false;
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

    /// <summary>
    /// Finds the native rigid body instance this mass properties belongs to.
    /// </summary>
    /// <returns>Native rigid body instance where native.getMassproperties() == this (native).</returns>
    private agx.RigidBody GetNative()
    {
      return m_rb != null ? m_rb.Native : null;
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
        GetNative().getMassProperties().setInertiaTensor( newValue.ToVec3() );
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
  }
}
