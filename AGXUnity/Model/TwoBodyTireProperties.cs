﻿using System;
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
  public class TwoBodyTireProperties : ScriptAsset
  {
    [SerializeField]
    private float m_radialStiffness = 5.0E3f;

    /// <summary>
    /// Radial stiffness. Affects translation orthogonal to tire rotation axis.
    /// Default: 5.0E3
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
    private float m_radialDampingCoefficient = 1.67E4f;

    /// <summary>
    /// Radial damping coefficient. Affects translation orthogonal to tire rotation axis.
    /// Default: 1.67E4
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
    private float m_lateralStiffness = 5.0E3f;

    /// <summary>
    /// Lateral stiffness. Affects translation in axis of rotation.
    /// Default: 5.0E3
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
    private float m_lateralDampingCoefficient = 1.67E6f;

    /// <summary>
    /// Lateral damping coefficient. Affects translation in axis of rotation.
    /// Default: 1.67E6
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
    private float m_bendingStiffness = 2.5E3f;

    /// <summary>
    /// Bending stiffness. Affects rotation orthogonal to axis of rotation.
    /// Default: 2.5E3
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
    private float m_bendingDampingCoefficient = 8.33E3f;

    /// <summary>
    /// Bending damping coefficient. Affects rotation orthogonal to axis of rotation.
    /// Default: 8.33E3
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
    private float m_torsionalStiffness = 2.5E3f;

    /// <summary>
    /// Torsional stiffness. Affects rotation in axis of rotation.
    /// Default: 2.5E3
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
    private float m_torsionalDampingCoefficient = 8.33E3f;

    /// <summary>
    /// Torsional damping coefficient. Affects rotation in axis of rotation.
    /// Default: 8.33E3
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
    /// Internal.
    /// 
    /// Register tire instance to adopt these tire properties.
    /// </summary>
    /// <param name="tire"></param>
    public void Register( TwoBodyTire tire )
    {
      if ( !m_tires.Contains( tire ) ) {
        m_tires.Add( tire );

        // Synchronizing properties for all shovels. Could be
        // avoided by adding a state so that Propagate only
        // shows current added terrain.
        Utils.PropertySynchronizer.Synchronize( this );
      }
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

      foreach ( var tire in m_tires )
        if ( tire.Native != null )
          action( tire.Native );
    }

    private List<TwoBodyTire> m_tires = new List<TwoBodyTire>();
  }
}
