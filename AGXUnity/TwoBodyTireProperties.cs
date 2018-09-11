using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;

namespace AGXUnity
{

  
  /// <summary>
  /// Store the properties of a TwoBodyTire. Connect this object to one or more TwoBodyTire instances.
  /// </summary>
  public class TwoBodyTireProperties : ScriptAsset
  {

    /// <summary>
    /// Name of this Tire component for easier identification
    /// </summary>
    [SerializeField]
    public string Name;

    [HideInInspector]
    static public float DefaultStiffness = 9000000;

    [HideInInspector]
    public const float DefaultDamping = 700000;

    /// <summary>
    /// Radial stiffness of the Tire
    /// </summary>
    [SerializeField]
    public float RadialStiffness = 0.01f * DefaultStiffness;



    /// <summary>
    /// Lateral stiffness of the Tire
    /// </summary>
    [SerializeField]
    public float LateralStiffness = DefaultStiffness;

    /// <summary>
    /// Bending stiffness of the Tire
    /// </summary>
    [SerializeField]
    public float BendingStiffness = 0.5f * DefaultStiffness;

    /// <summary>
    /// Torsional stiffness of the Tire
    /// </summary>
    [SerializeField]
    public float TorsionalStiffness = 10 * DefaultStiffness;

    /// <summary>
    /// Radial damping coefficient of the tires
    /// </summary>
    [SerializeField]
    public float RadialDamping = 0.002f * DefaultDamping;

    /// <summary>
    /// Lateral damping coefficient of the tires
    /// </summary>
    [SerializeField]
    public float LateralDamping = DefaultDamping;

    /// <summary>
    /// Lateral damping coefficient of the tires
    /// </summary>
    [SerializeField]
    public float BendingDamping = 2 * DefaultDamping;

    /// <summary>
    /// Lateral damping coefficient of the tires
    /// </summary>
    [SerializeField]
    public float TorsionalDamping = 10 * DefaultDamping;


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

  }
}
