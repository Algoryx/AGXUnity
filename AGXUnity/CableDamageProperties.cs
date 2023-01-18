using System;
using UnityEngine;
using System.Collections.Generic;

namespace AGXUnity
{
  public class CableDamageProperties : ScriptAsset
  {
    private const float defaultValue = 0f;

    [SerializeField]
    private float m_bendDeformation = defaultValue;
    [HideInInspector]
    public float BendDeformation
    {
      get { return m_bendDeformation; }
      set
      {
        m_bendDeformation = value;
        Propagate( damage => damage.setBendDeformationWeight( m_bendDeformation ) );
      }
    }

    [SerializeField]
    private float m_bendTension = defaultValue;
    [HideInInspector]
    public float BendTension
    {
      get { return m_bendTension; }
      set
      {
        m_bendTension = value;
        Propagate( damage => damage.setBendTensionWeight( m_bendTension ) );
      }
    }

    [SerializeField]
    private float m_bendRate = defaultValue;
    [HideInInspector]
    public float BendRate
    {
      get { return m_bendRate; }
      set
      {
        m_bendRate = value;
        Propagate( damage => damage.setBendRateWeight( m_bendRate ) );
      }
    }

    [SerializeField]
    private float m_twistDeformation = defaultValue;
    [HideInInspector]
    public float TwistDeformation
    {
      get { return m_twistDeformation; }
      set
      {
        m_twistDeformation = value;
        Propagate( damage => damage.setTwistDeformationWeight( m_twistDeformation ) );
      }
    }

    [SerializeField]
    private float m_twistTension = defaultValue;
    [HideInInspector]
    public float TwistTension
    {
      get { return m_twistTension; }
      set
      {
        m_twistTension = value;
        Propagate( damage => damage.setTwistTensionWeight( m_twistTension ) );
      }
    }

    [SerializeField]
    private float m_twistRate = defaultValue;
    [HideInInspector]
    public float TwistRate
    {
      get { return m_twistRate; }
      set
      {
        m_twistRate = value;
        Propagate( damage => damage.setTwistRateWeight( m_twistRate ) );
      }
    }

    [SerializeField]
    private float m_stretchDeformation = defaultValue;
    [HideInInspector]
    public float StretchDeformation
    {
      get { return m_stretchDeformation; }
      set
      {
        m_stretchDeformation = value;
        Propagate( damage => damage.setStretchDeformationWeight( m_stretchDeformation ) );
      }
    }

    [SerializeField]
    private float m_stretchTension = defaultValue;
    [HideInInspector]
    public float StretchTension
    {
      get { return m_stretchTension; }
      set
      {
        m_stretchTension = value;
        Propagate( damage => damage.setStretchTensionWeight( m_stretchTension ) );
      }
    }

    [SerializeField]
    private float m_stretchRate = defaultValue;
    [HideInInspector]
    public float StretchRate
    {
      get { return m_stretchRate; }
      set
      {
        m_stretchRate = value;
        Propagate( damage => damage.setStretchRateWeight( m_stretchRate ) );
      }
    }

    [SerializeField]
    private float m_normalForce = defaultValue;
    [HideInInspector]
    public float NormalForce
    {
      get { return m_normalForce; }
      set
      {
        m_normalForce = value;
        Propagate( damage => damage.setNormalForceWeight( m_normalForce ) );
      }
    }

    [SerializeField]
    private float m_frictionForce = defaultValue;
    [HideInInspector]
    public float FrictionForce
    {
      get { return m_frictionForce; }
      set
      {
        m_frictionForce = value;
        Propagate( damage => damage.setFrictionForceWeight( m_frictionForce ) );
      }
    }

    [SerializeField]
    private float m_bendThreshHold = defaultValue;
    [HideInInspector]
    public float BendThreshold
    {
      get { return m_bendThreshHold; }
      set
      {
        m_bendThreshHold = value;
        Propagate( damage => damage.setBendThreshold( m_bendThreshHold ) );
      }
    }

    [SerializeField]
    private float m_twistThreshold = defaultValue;
    [HideInInspector]
    public float TwistThreshold
    {
      get { return m_twistThreshold; }
      set
      {
        m_twistThreshold = value;
        Propagate( damage => damage.setTwistThreshold( m_twistThreshold ) );
      }
    }

    [SerializeField]
    private float m_stretchThreshold = defaultValue;
    [HideInInspector]
    public float StretchThreshold
    {
      get { return m_stretchThreshold; }
      set
      {
        m_stretchThreshold = value;
        Propagate( damage => damage.setStretchThreshold( m_stretchThreshold ) );
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
  }
}
