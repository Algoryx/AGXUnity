using System;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  public class FrictionController : ElementaryConstraintController
  {
    [SerializeField]
    private float m_frictionCoefficient = 0.4167f;

    /// <summary>
    /// Get or set friction coefficient. Default: 0.4167.
    /// </summary>
    /// <remarks>
    /// If this controller is rotational( Hinge or CylindriclJoint) the radius
    /// of the axle should be included in the friction coefficient for the
    /// comparisons with the normal force to be dimensionally correct.I.e.,
    ///     friction_torque less or equal friction_coefficient* axle_radius * normal_force
    /// </remarks>
    [ClampAboveZeroInInspector( true )]
    public float FrictionCoefficient
    {
      get { return m_frictionCoefficient; }
      set
      {
        m_frictionCoefficient = value;
        if ( Native != null )
          agx.FrictionController.safeCast( Native ).setFrictionCoefficient( m_frictionCoefficient );
      }
    }

    [SerializeField]
    private bool m_nonLinearDirectSolveEnabled = false;

    /// <summary>
    /// Enable/disable non-linear update of the friction conditions given
    /// current normal force from the direct solver.When enabled - this
    /// feature is similar to scale box friction models with solve type DIRECT.
    /// Default: disabled.
    /// </summary>
    /// <remarks>
    /// This feature only supports constraint solve types DIRECT and
    /// DIRECT_AND_ITERATIVE - meaning, if the constraint has solve
    /// type ITERATIVE, this feature is ignored.
    /// </remarks>
    public bool NonLinearDirectSolveEnabled
    {
      get { return m_nonLinearDirectSolveEnabled; }
      set
      {
        m_nonLinearDirectSolveEnabled = value;
        if ( Native != null )
          agx.FrictionController.safeCast( Native ).setEnableNonLinearDirectSolveUpdate( m_nonLinearDirectSolveEnabled );
      }
    }

    protected override void Construct( agx.ElementaryConstraint tmpEc )
    {
      base.Construct( tmpEc );

      m_frictionCoefficient = Convert.ToSingle( agx.FrictionController.safeCast( tmpEc ).getFrictionCoefficient() );
      m_nonLinearDirectSolveEnabled = agx.FrictionController.safeCast( tmpEc ).getEnableNonLinearDirectSolveUpdate();
    }

    protected override void Construct( ElementaryConstraint source )
    {
      base.Construct( source );

      m_frictionCoefficient = ( source as FrictionController ).m_frictionCoefficient;
      m_nonLinearDirectSolveEnabled = ( source as FrictionController ).m_nonLinearDirectSolveEnabled;
    }
  }
}
