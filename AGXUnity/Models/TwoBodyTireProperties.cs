using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Models
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
    private float[] m_stiffness = new float[ 4 ] { 5.0E3f, 5.0E3f, 2.5E3f, 2.5E3f };

    [SerializeField]
    private float[] m_dampingCoefficients = new float[ 4 ] { 1.67E4f, 1.67E6f, 8.33E3f, 8.33E3f };

    /// <summary>
    /// Radial stiffness. Affects translation orthogonal to tire rotation axis.
    /// Default: 5.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float RadialStiffness
    {
      get { return m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ]; }
      set
      {
        m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ] = value;
        Propagate( native => native.setStiffness( m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ],
                                                  agxModel.TwoBodyTire.DeformationMode.RADIAL ) );
      }
    }

    /// <summary>
    /// Radial damping coefficient. Affects translation orthogonal to tire rotation axis.
    /// Default: 1.67E4
    /// </summary>
    [ClampAboveZeroInInspector]
    public float RadialDampingCoefficient
    {
      get { return m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ]; }
      set
      {
        m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ] = value;
        Propagate( native => native.setDampingCoefficient( m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.RADIAL ],
                                                           agxModel.TwoBodyTire.DeformationMode.RADIAL ) );
      }
    }

    /// <summary>
    /// Lateral stiffness. Affects translation in axis of rotation.
    /// Default: 5.0E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float LateralStiffness
    {
      get { return m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ]; }
      set
      {
        m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ] = value;
        Propagate( native => native.setStiffness( m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ],
                                                  agxModel.TwoBodyTire.DeformationMode.LATERAL ) );
      }
    }

    /// <summary>
    /// Lateral damping coefficient. Affects translation in axis of rotation.
    /// Default: 1.67E6
    /// </summary>
    [ClampAboveZeroInInspector]
    public float LateralDampingCoefficient
    {
      get { return m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ]; }
      set
      {
        m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ] = value;
        Propagate( native => native.setDampingCoefficient( m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.LATERAL ],
                                                           agxModel.TwoBodyTire.DeformationMode.LATERAL ) );
      }
    }

    /// <summary>
    /// Bending stiffness. Affects rotation orthogonal to axis of rotation.
    /// Default: 2.5E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float BendingStiffness
    {
      get { return m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ]; }
      set
      {
        m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ] = value;
        Propagate( native => native.setStiffness( m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ],
                                                  agxModel.TwoBodyTire.DeformationMode.BENDING ) );
      }
    }

    /// <summary>
    /// Bending damping coefficient. Affects rotation orthogonal to axis of rotation.
    /// Default: 8.33E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float BendingDampingCoefficient
    {
      get { return m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ]; }
      set
      {
        m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ] = value;
        Propagate( native => native.setDampingCoefficient( m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.BENDING ],
                                                           agxModel.TwoBodyTire.DeformationMode.BENDING ) );
      }
    }

    /// <summary>
    /// Torsional stiffness. Affects rotation in axis of rotation.
    /// Default: 2.5E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TorsionalStiffness
    {
      get { return m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ]; }
      set
      {
        m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ] = value;
        Propagate( native => native.setStiffness( m_stiffness[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ],
                                                  agxModel.TwoBodyTire.DeformationMode.TORSIONAL ) );
      }
    }

    /// <summary>
    /// Torsional damping coefficient. Affects rotation in axis of rotation.
    /// Default: 8.33E3
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TorsionalDampingCoefficient
    {
      get { return m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ]; }
      set
      {
        m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ] = value;
        Propagate( native => native.setDampingCoefficient( m_dampingCoefficients[ (int)agxModel.TwoBodyTire.DeformationMode.TORSIONAL ],
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
