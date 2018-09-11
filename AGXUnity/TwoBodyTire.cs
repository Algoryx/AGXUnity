using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;

namespace AGXUnity
{
  /// <summary>
  /// This class exposes the tire model
  /// It requires an instance of a TwoBodyTireProperties class, otherwise some default values are used
  /// </summary>
  public class TwoBodyTire : ScriptComponent
  {

    /// <summary>
    /// Name of this Tire component for easier identification
    /// </summary>
    public string Name;

    private const float AGX_EQUIVALENT_EPSILON = (float)1E-5;


    [SerializeField]
    private float m_radialStiffness = 0.5f * TwoBodyTireProperties.DefaultStiffness;

    [SerializeField]
    private float m_lateralStiffness = TwoBodyTireProperties.DefaultStiffness;

    [SerializeField]
    private float m_bendingStiffness = 0.5f * TwoBodyTireProperties.DefaultStiffness;

    [SerializeField]
    private float m_torsionalStiffness = 10 * TwoBodyTireProperties.DefaultStiffness;



    [SerializeField]
    private float m_radialDamping = 2 * TwoBodyTireProperties.DefaultDamping;

    [SerializeField]
    private float m_lateralDamping = TwoBodyTireProperties.DefaultDamping;

    [SerializeField]
    private float m_bendingDamping = 2 * TwoBodyTireProperties.DefaultDamping;

    [SerializeField]
    private float m_torsionalDamping = 10 * TwoBodyTireProperties.DefaultDamping;

    [SerializeField]
    private AGXUnity.RigidBody m_tireBody = null;


    public AGXUnity.TwoBodyTireProperties TireProperties=null;


    /// <summary>
    /// Body for the tire of the wheel
    /// </summary>
    public AGXUnity.RigidBody TireBody { get { return m_tireBody; } set { m_tireBody = value; inititializeTire(); } }


    [SerializeField]
    private AGXUnity.RigidBody m_rimBody = null;

    /// <summary>
    /// Body for the rim of the tire
    /// </summary>
    public AGXUnity.RigidBody RimBody { get { return m_rimBody; } set { m_rimBody = value; inititializeTire(); } }

    private agxModel.TwoBodyTire m_tireModel = null;

    // Use this for initialization
    protected override bool Initialize()
    {
      inititializeTire();

      return true;
    }

    protected override void OnEnable()
    {
      if (m_tireModel == null)
        return;

      if (m_tireModel.getSimulation() == null)
        GetSimulation().add(m_tireModel);
    }

    protected override void OnDisable()
    {
      if (m_tireModel == null)
        return;

      var sim = GetSimulation();
      if (sim != null)
        sim.remove(m_tireModel);
    }

    // Update is called once per frame
    void Update()
    {

      // Update the parameter models in case someone has changed the values
      updateTireModelParameters();
    }


    bool equivalent(float lhs, float rhs, float epsilon = AGX_EQUIVALENT_EPSILON)
    {
      return (lhs + epsilon >= rhs) && (lhs - epsilon <= rhs);
    }

    private void clearTireModel()
    {
      if (m_tireModel == null)
        return;

      m_tireModel.getSimulation().remove(m_tireModel);
      m_tireModel = null;
    }

    private void updateTireModelParameters()
    {
      // Only update if changed
      if (TireProperties && 
        equivalent(TireProperties.RadialStiffness, m_radialStiffness) &&
        equivalent(TireProperties.LateralStiffness, m_lateralStiffness) &&
        equivalent(TireProperties.BendingStiffness, m_bendingStiffness) &&
        equivalent(TireProperties.TorsionalStiffness, m_torsionalStiffness) &&

        equivalent(TireProperties.RadialDamping, m_radialDamping) &&
        equivalent(TireProperties.LateralDamping, m_lateralDamping) &&
        equivalent(TireProperties.BendingDamping, m_bendingDamping) &&
        equivalent(TireProperties.TorsionalDamping, m_torsionalDamping))
        return;

      // If not using tire model, just skip this
      if (m_tireBody == null || m_rimBody == null)
      {
        clearTireModel();
      }

      if (TireProperties)
      { 
        m_radialStiffness = TireProperties.RadialStiffness;
        m_lateralStiffness = TireProperties.LateralStiffness;
        m_bendingStiffness = TireProperties.BendingStiffness;
        m_torsionalStiffness = TireProperties.TorsionalStiffness;

        m_radialDamping = TireProperties.RadialDamping;
        m_lateralDamping = TireProperties.LateralDamping;
        m_bendingDamping = TireProperties.BendingDamping;
        m_torsionalDamping = TireProperties.TorsionalDamping;
      }

      // This is only used if implicit contact material is used. Should NOT happen really.
      m_tireModel.setImplicitFrictionMultiplier(new agx.Vec2(1.2, 0.8));

      m_tireModel.setStiffness(m_radialStiffness, agxModel.TwoBodyTire.DeformationMode.RADIAL);
      m_tireModel.setStiffness(m_lateralStiffness, agxModel.TwoBodyTire.DeformationMode.LATERAL);
      m_tireModel.setStiffness(m_bendingStiffness, agxModel.TwoBodyTire.DeformationMode.BENDING);
      m_tireModel.setStiffness(m_torsionalStiffness, agxModel.TwoBodyTire.DeformationMode.TORSIONAL);

      // The unit for the translational damping coefficient is force * time/displacement (if using SI: Ns/m)
      // The unit for the rotational damping coefficient is torque * time/angular displacement (if using SI: Nms/rad)
      m_tireModel.setDampingCoefficient(m_radialDamping, agxModel.TwoBodyTire.DeformationMode.RADIAL);
      m_tireModel.setDampingCoefficient(m_lateralDamping, agxModel.TwoBodyTire.DeformationMode.LATERAL);
      m_tireModel.setDampingCoefficient(m_bendingDamping, agxModel.TwoBodyTire.DeformationMode.BENDING);
      m_tireModel.setDampingCoefficient(m_torsionalDamping, agxModel.TwoBodyTire.DeformationMode.TORSIONAL);
    }

    protected void inititializeTire()
    {
      if (!agx.Runtime.instance().isValid() || !agx.Runtime.instance().isModuleEnabled("AgX-Tires"))
        Debug.LogError("This Component requires a valid license for the AGX Dynamics module: AgX-Tires");


      clearTireModel();

      if (m_rimBody == null || m_tireBody == null || m_tireBody.GetInitialized<RigidBody>() == null || m_rimBody.GetInitialized<RigidBody>() == null)
        return;

      var tire = m_tireBody.GetInitialized<RigidBody>().Native;
      var rim = m_rimBody.GetInitialized<RigidBody>().Native;

      if (tire == null || rim == null)
      {
        Debug.LogWarning("Two Tire Model requires two bodies, one for the tire and one for the rim");
        return;
      }
      // Make sure orientation of the wheel is correct
      var m = new agx.AffineMatrix4x4(new agx.Quat(new agx.Vec3(0, 0, 1), new agx.Vec3(0, 1, 0)), new agx.Vec3());

      // Create a tire model that connects the Tire with the Rim
      m_tireModel = new agxModel.TwoBodyTire(tire, 1.0, rim, 0.5, m);

      GetSimulation().add(m_tireModel);
    }
  }

}
