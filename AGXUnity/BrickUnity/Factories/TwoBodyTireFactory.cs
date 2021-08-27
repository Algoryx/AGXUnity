using System.Collections.Generic;
using UnityEngine;

using B_TwoBodyTire = Brick.AGXBrick.TwoBodyTire;
using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;

namespace AGXUnity.BrickUnity.Factories
{
  public static class TwoBodyTireFactory
  {
    public static Model.TwoBodyTire AddTwoBodyTire(this GameObject go, B_TwoBodyTire b_tire,
                                                   Dictionary<B_RigidBody, GameObject> bodyDict,
                                                   Dictionary<string, Object> tireProperties)
    {
      var au_tire = go.AddComponent<Model.TwoBodyTire>();

      var goRim = bodyDict[b_tire.AttachmentOnRim.Body as B_RigidBody];
      var goTire = bodyDict[b_tire.AttachmentOnTire.Body as B_RigidBody];

      au_tire.RimRigidBody = goRim.GetComponent<RigidBody>();
      au_tire.TireRigidBody = goTire.GetComponent<RigidBody>();

      au_tire.RimRadius = (float)b_tire.RimRadius;
      au_tire.TireRadius = (float)b_tire.TireRadius;

      var tireRimLock = go.GetComponentInChildren<Constraint>();
      if (tireRimLock.GetComponent<BrickObject>().type is "Brick.AGXBrick.TwoBodyTire.TireRimConnector")
      {
        au_tire.TireRimConstraint = tireRimLock;
      }

      var au_tireProperties = CreateTireProperties(b_tire);
      tireProperties.Add(b_tire.Name, au_tireProperties);

      au_tire.Properties = au_tireProperties;

      tireRimLock.gameObject.GetComponent<BrickObject>().synchronize = false;


      return au_tire;
    }

    private static Model.TwoBodyTireProperties CreateTireProperties(B_TwoBodyTire b_tire)
    {
      var au_tireProperties = ScriptableObject.CreateInstance<Model.TwoBodyTireProperties>();

      au_tireProperties.RadialStiffness = (float)b_tire.RadialStiffness;
      au_tireProperties.LateralStiffness = (float)b_tire.LateralStiffness;
      au_tireProperties.BendingStiffness = (float)b_tire.BendingStiffness;
      au_tireProperties.TorsionalStiffness = (float)b_tire.TorsionalStiffness;

      au_tireProperties.RadialDampingCoefficient = (float)b_tire.RadialDamping;
      au_tireProperties.LateralDampingCoefficient = (float)b_tire.LateralDamping;
      au_tireProperties.BendingDampingCoefficient = (float)b_tire.BendingDamping;
      au_tireProperties.TorsionalDampingCoefficient = (float)b_tire.TorsionalDamping;

      return au_tireProperties;
    }

  }
}