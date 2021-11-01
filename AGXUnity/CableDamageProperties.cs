using System;
using UnityEngine;
using System.Collections.Generic;

namespace AGXUnity
{
  public class CableDamageProperties : ScriptAsset
  {
    // CableDamageTypes.h
    //     enum DamageType
    // {
    //   BEND_DEFORMATION,
    //   TWIST_DEFORMATION,
    //   STRETCH_DEFORMATION,

    //   BEND_TENSION,
    //   TWIST_TENSION,
    //   STRETCH_TENSION,

    //   BEND_RATE,
    //   TWIST_RATE,
    //   STRETCH_RATE,

    //   NORMAL_FORCE,
    //   FRICTION_FORCE,

    //   NUM_CABLE_DAMAGE_TYPES
    // };

    private const float defaultValue = 1f;

    [SerializeField]
    private float m_bendDeformation = defaultValue;
    public float BendDeformation
    {
      get { return m_bendDeformation; }
      set
      {
        m_bendDeformation = value;
        Propagate( damage => damage.setBendDeformationWeight( m_bendDeformation ) );
      }
    }

    /// <summary>
    /// Explicit synchronization of all properties to the given
    /// cable damage instance.
    /// </summary>
    /// <remarks>
    /// This call wont have any effect unless the native instance
    /// of the cable damage has been created.
    /// </remarks>
    /// <param name="damage">Cable damage object instance to synchronize.</param>
    public void Synchronize( CableDamage damage )
    {
      try {
        m_singleSynchronizeInstance = damage;
        Utils.PropertySynchronizer.Synchronize( this );
      }
      finally {
        m_singleSynchronizeInstance = null;
      }
    }

    /// <summary>
    /// Register as listener of these settings. Current settings will
    /// be applied to the cable damage instance directly when added.
    /// </summary>
    /// <param name="damage">Cable damage object instance to which these settings should apply.</param>
    public void Register( CableDamage damage )
    {
      if ( !m_cableDamages.Contains( damage ) )
        m_cableDamages.Add( damage );

      Synchronize( damage );
    }

    /// <summary>
    /// Unregister as listener of these settings.
    /// </summary>
    /// <param name="damage"></param>
    public void Unregister( CableDamage damage )
    {
      m_cableDamages.Remove( damage );
    }

    public override void Destroy()
    {
      m_cableDamages.Clear();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    private void Propagate( Action<agxCable.CableDamage> action )
    {
      if ( action == null )
        return;

      if ( m_singleSynchronizeInstance != null ) {
        if ( m_singleSynchronizeInstance.Native != null )
          action( m_singleSynchronizeInstance.Native );
        return;
      }

      foreach ( var cableDamage in m_cableDamages )
        if ( cableDamage.Native != null )
          action( cableDamage.Native );
    }

    [NonSerialized]
    private List<CableDamage> m_cableDamages = new List<CableDamage>();

    [NonSerialized]
    private CableDamage m_singleSynchronizeInstance = null;

    public Action<CableProperties.Direction> OnPropertyUpdated = delegate { };

  // TODO there is no cableDamageProperties in native, this is just some bundling 
    // public CableDamageProperties RestoreLocalDataFrom( agxCable.CableDamageProperties native, agxCable.CablePlasticity plasticity )
    // {
    //   if ( native == null )
    //     return this;

    //   foreach ( CableProperties.Direction dir in Directions ) {
    //     this[ dir ].Deformation = Convert.ToSingle( native.getYoungsModulus( ToNative( dir ) ) );
    //     this[ dir ].Rate = Convert.ToSingle( native.getPoissonsRatio( ToNative( dir ) ) );
    //     this[ dir ].Tension    = plasticity != null ?
    //                                   Convert.ToSingle( plasticity.getYieldPoint( ToNative( dir ) ) ) :
    //                                   float.PositiveInfinity;
    //   }

    //   return this;
    // }
  }
}
