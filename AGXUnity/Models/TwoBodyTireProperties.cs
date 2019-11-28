using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Models
{  
  /// <summary>
  /// Properties of a TwoBodyTire.
  /// </summary>
  public class TwoBodyTireProperties : ScriptAsset
  {
    [SerializeField]
    private float[] m_stiffness = new float[ 4 ] { 5.0E3f, 5.0E3f, 2.5E3f, 2.5E3f };

    [SerializeField]
    private float[] m_dampingCoefficients = new float[ 4 ] { 1.67E4f, 1.67E6f, 8.33E3f, 8.33E3f };

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
