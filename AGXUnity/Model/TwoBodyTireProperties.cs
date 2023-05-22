using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  /// <summary>
  /// Properties of a TwoBodyTire.
  /// 
  /// Radial stiffness/damping affects translation orthogonal to tire rotation axis.
  /// Lateral stiffness/damping affects translation in axis of rotation.
  /// Bending stiffness/damping affects rotation orthogonal to axis of rotation.
  /// Torsional stiffness/damping affects rotation in axis of rotation.
  /// 
  /// The unit for translational stiffness is force/displacement (if using SI: N/m)
  /// The unit for rotational stiffness is torque/angular displacement(if using SI: Nm/rad)
  /// The unit for the translational damping coefficient is force* time/displacement(if using SI: Ns/m)
  /// The unit for the rotational damping coefficient is torque* time/angular displacement(if using SI: Nms/rad)
  /// </summary>
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#two-body-tire-properties" )]
  public class TwoBodyTireProperties : ScriptAsset
  {
    [SerializeField]
    private float m_radialStiffness = 3.5E5f;

    /// <summary>
    /// Radial stiffness. Affects translation orthogonal to tire rotation axis.
    /// Default: 3.5E5
    /// </summary>
    [ClampAboveZeroInInspector]
    public float RadialStiffness
    {
      get { return m_radialStiffness; }
      set
      {
        m_radialStiffness = value;
        Propagate( native => native.setStiffness( m_radialStiffness,
                                                  agxModel.TwoBodyTire.DeformationMode.RADIAL ) );
      }
    }

    [SerializeField]
    private float m_radialDampingCoefficient = 7.0E3f;

    /// <summary>
    /// Radial damping coefficient. Affects translation orthogonal to tire rotation axis.
    /// Default: 7.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float RadialDampingCoefficient
    {
      get { return m_radialDampingCoefficient; }
      set
      {
        m_radialDampingCoefficient = value;
        Propagate( native => native.setDampingCoefficient( m_radialDampingCoefficient,
                                                           agxModel.TwoBodyTire.DeformationMode.RADIAL ) );
      }
    }

    [SerializeField]
    private float m_lateralStiffness = 3.0E5f;

    /// <summary>
    /// Lateral stiffness. Affects translation in axis of rotation.
    /// Default: 3.0E5
    /// </summary>
    [ClampAboveZeroInInspector]
    public float LateralStiffness
    {
      get { return m_lateralStiffness; }
      set
      {
        m_lateralStiffness = value;
        Propagate( native => native.setStiffness( m_lateralStiffness,
                                                  agxModel.TwoBodyTire.DeformationMode.LATERAL ) );
      }
    }

    [SerializeField]
    private float m_lateralDampingCoefficient = 5.0E3f;

    /// <summary>
    /// Lateral damping coefficient. Affects translation in axis of rotation.
    /// Default: 5.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float LateralDampingCoefficient
    {
      get { return m_lateralDampingCoefficient; }
      set
      {
        m_lateralDampingCoefficient = value;
        Propagate( native => native.setDampingCoefficient( m_lateralDampingCoefficient,
                                                           agxModel.TwoBodyTire.DeformationMode.LATERAL ) );
      }
    }

    [SerializeField]
    private float m_bendingStiffness = 3.0E5f;

    /// <summary>
    /// Bending stiffness. Affects rotation orthogonal to axis of rotation.
    /// Default: 3.0E5
    /// </summary>
    [ClampAboveZeroInInspector]
    public float BendingStiffness
    {
      get { return m_bendingStiffness; }
      set
      {
        m_bendingStiffness = value;
        Propagate( native => native.setStiffness( m_bendingStiffness,
                                                  agxModel.TwoBodyTire.DeformationMode.BENDING ) );
      }
    }

    [SerializeField]
    private float m_bendingDampingCoefficient = 5.0E3f;

    /// <summary>
    /// Bending damping coefficient. Affects rotation orthogonal to axis of rotation.
    /// Default: 5.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float BendingDampingCoefficient
    {
      get { return m_bendingDampingCoefficient; }
      set
      {
        m_bendingDampingCoefficient = value;
        Propagate( native => native.setDampingCoefficient( m_bendingDampingCoefficient,
                                                           agxModel.TwoBodyTire.DeformationMode.BENDING ) );
      }
    }

    [SerializeField]
    private float m_torsionalStiffness = 3.0E5f;

    /// <summary>
    /// Torsional stiffness. Affects rotation in axis of rotation.
    /// Default: 3.0E5
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TorsionalStiffness
    {
      get { return m_torsionalStiffness; }
      set
      {
        m_torsionalStiffness = value;
        Propagate( native => native.setStiffness( m_torsionalStiffness,
                                                  agxModel.TwoBodyTire.DeformationMode.TORSIONAL ) );
      }
    }

    [SerializeField]
    private float m_torsionalDampingCoefficient = 5.0E3f;

    /// <summary>
    /// Torsional damping coefficient. Affects rotation in axis of rotation.
    /// Default: 5.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TorsionalDampingCoefficient
    {
      get { return m_torsionalDampingCoefficient; }
      set
      {
        m_torsionalDampingCoefficient = value;
        Propagate( native => native.setDampingCoefficient( m_torsionalDampingCoefficient,
                                                           agxModel.TwoBodyTire.DeformationMode.TORSIONAL ) );
      }
    }

    /// <summary>
    /// Explicit synchronization of all properties to the given
    /// tire instance.
    /// </summary>
    /// <remarks>
    /// This call wont have any effect unless the native instance
    /// of the tire has been created.
    /// </remarks>
    /// <param name="tire">Tire instance to synchronize.</param>
    public void Synchronize( TwoBodyTire tire )
    {
      try {
        m_singleSynchronizeInstance = tire;
        Utils.PropertySynchronizer.Synchronize( this );
      }
      finally {
        m_singleSynchronizeInstance = null;
      }
    }

    /// <summary>
    /// Internal.
    /// 
    /// Register tire instance to adopt these tire properties.
    /// </summary>
    /// <param name="tire"></param>
    public void Register( TwoBodyTire tire )
    {
      if ( !m_tires.Contains( tire ) )
        m_tires.Add( tire );

      Synchronize( tire );
    }

    /// <summary>
    /// Internal.
    /// 
    /// Unregister tire instance from these properties.
    /// </summary>
    /// <param name="tire"></param>
    public void Unregister( TwoBodyTire tire )
    {
      m_tires.Remove( tire );
    }

    private TwoBodyTireProperties()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    public override void Destroy()
    {
    }

    private void Propagate( Action<agxModel.TwoBodyTire> action )
    {
      if ( action == null )
        return;

      if ( m_singleSynchronizeInstance != null ) {
        if ( m_singleSynchronizeInstance.Native != null )
          action( m_singleSynchronizeInstance.Native );
        return;
      }

      foreach ( var tire in m_tires )
        if ( tire.Native != null )
          action( tire.Native );
    }

    [NonSerialized]
    private List<TwoBodyTire> m_tires = new List<TwoBodyTire>();

    [NonSerialized]
    private TwoBodyTire m_singleSynchronizeInstance = null;
  }
}
