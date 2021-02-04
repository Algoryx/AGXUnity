using UnityEngine;

using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_Interaction = Brick.Physics.Mechanics.AttachmentPairInteraction;
using B_Mechanics = Brick.Physics.Mechanics;

namespace BrickUnity.Factories
{
  public static class ConstraintFactory
  {
    public static void SetControllers(this AGXUnity.Constraint constraint, B_Connector b_connector)
    {
      foreach (var b_interaction in b_connector.Interactions)
      {
        switch (b_interaction)
        {
          case B_Interaction.MotorInteraction1D b_motor1D:
            constraint.SetTargetSpeedController(b_motor1D);
            break;
          case B_Interaction.LockInteraction1D b_lock1D:
            constraint.SetLockController(b_lock1D);
            break;
          case B_Interaction.RangeMinMaxInteraction1D b_range1D:
            constraint.SetRangeController(b_range1D);
            break;
          case B_Interaction.FrictionInteraction1D b_friction1D:
            constraint.SetFrictionController(b_friction1D);
            break;
          default:
            if (b_interaction != b_connector.MainInteraction)
            {
              Debug.LogWarning($"Could not create interaction for {constraint.GetComponent<BrickObject>().path}. Unknown interaction type.");
              var go_interaction = new GameObject();
            }
            break;
        }
      }
    }

    public static void SetTargetSpeedController(this AGXUnity.Constraint constraint, B_Interaction.MotorInteraction1D b_motor1D)
    {
      var controllerType = AGXUnity.Constraint.ControllerType.Primary;
      if (b_motor1D is B_Interaction.RotationalMotorInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Rotational;
      else if (b_motor1D is B_Interaction.TranslationalMotorInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Translational;

      var targetSpeedController = constraint.GetController<AGXUnity.TargetSpeedController>(controllerType);

      targetSpeedController.SetGeneralControllerValues(b_motor1D);
      targetSpeedController.Speed = (float)b_motor1D.Speed;
      targetSpeedController.LockAtZeroSpeed = b_motor1D.LockAtZeroSpeed;
    }

    public static void SetLockController(this AGXUnity.Constraint constraint, B_Interaction.LockInteraction1D b_lock1D)
    {
      var controllerType = AGXUnity.Constraint.ControllerType.Primary;
      if (b_lock1D is B_Interaction.TranslationalLockInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Translational;
      // No RotationalLockInteraction1D existing in brick....
      var lockController = constraint.GetController<AGXUnity.LockController>(controllerType);
      lockController.SetGeneralControllerValues(b_lock1D);
      //lockController.Position = ??
    }

    public static void SetRangeController(this AGXUnity.Constraint constraint, B_Interaction.RangeMinMaxInteraction1D b_range1D)
    {
      var controllerType = AGXUnity.Constraint.ControllerType.Primary;
      if (b_range1D is B_Interaction.RotationalRangeMinMaxInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Rotational;
      else if (b_range1D is B_Interaction.TranslationalRangeMinMaxInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Translational;

      var rangeController = constraint.GetController<AGXUnity.RangeController>(controllerType);

      rangeController.SetGeneralControllerValues(b_range1D);
      float minValue = (float)Brick.Math.Utils.ToRad(b_range1D.MinValue);
      float maxValue = (float)Brick.Math.Utils.ToRad(b_range1D.MaxValue);
      rangeController.Range = new AGXUnity.RangeReal(minValue, maxValue);
    }

    public static void SetFrictionController(this AGXUnity.Constraint constraint, B_Interaction.FrictionInteraction1D b_friction1D)
    {
      var controllerType = AGXUnity.Constraint.ControllerType.Primary;
      if (b_friction1D is B_Interaction.RotationalFrictionInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Rotational;
      else if (b_friction1D is B_Interaction.TranslationalFrictionInteraction1D)
        controllerType = AGXUnity.Constraint.ControllerType.Translational;

      var frictionController = constraint.GetController<AGXUnity.FrictionController>(controllerType);

      frictionController.SetGeneralControllerValues(b_friction1D);
      frictionController.FrictionCoefficient = (float)b_friction1D.Coefficient;
    }

    public static void SetGeneralControllerValues(this AGXUnity.ElementaryConstraintController controller, B_Interaction.Interaction1D b_interaction)
    {
      controller.Enable = b_interaction.Enabled;
      controller.Compliance = 1f / (float)b_interaction.Stiffness;
      controller.Damping = (float)b_interaction.Damping / (float)b_interaction.Stiffness;
      controller.ForceRange = new AGXUnity.RangeReal((float)b_interaction.MinForce, (float)b_interaction.MaxForce);
    }


    public static void SetBallJointComplianceAndDamping(this AGXUnity.Constraint constraint, B_Interaction b_interaction)
    {
      constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongNormal, AGXUnity.Constraint.TranslationalDof.X);
      constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongCross, AGXUnity.Constraint.TranslationalDof.Y);
      constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongTangent, AGXUnity.Constraint.TranslationalDof.Z);

      constraint.SetDamping((float)(b_interaction.Damping6D.AlongNormal / b_interaction.Stiffness6D.AlongNormal),
                            AGXUnity.Constraint.TranslationalDof.X);
      constraint.SetDamping((float)(b_interaction.Damping6D.AlongCross / b_interaction.Stiffness6D.AlongCross),
                            AGXUnity.Constraint.TranslationalDof.Y);
      constraint.SetDamping((float)(b_interaction.Damping6D.AlongTangent / b_interaction.Stiffness6D.AlongTangent),
                            AGXUnity.Constraint.TranslationalDof.Z);
    }

    public static void SetComplianceAndDamping(this AGXUnity.Constraint constraint, B_Interaction b_interaction)
    {
      if (b_interaction is B_Mechanics.LockJointInteraction ||
        b_interaction is B_Mechanics.HingeInteraction ||
        b_interaction is B_Mechanics.PrismaticInteraction ||
        b_interaction is B_Mechanics.CylindricalInteraction)
      {
        constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongNormal, AGXUnity.Constraint.TranslationalDof.X);
        constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongCross, AGXUnity.Constraint.TranslationalDof.Y);
        if (b_interaction is B_Mechanics.LockJointInteraction || b_interaction is B_Mechanics.HingeInteraction)
          constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AlongTangent, AGXUnity.Constraint.TranslationalDof.Z);

        constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AroundNormal, AGXUnity.Constraint.RotationalDof.X);
        constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AroundCross, AGXUnity.Constraint.RotationalDof.Y);
        if (b_interaction is B_Mechanics.LockJointInteraction || b_interaction is B_Mechanics.PrismaticInteraction)
          constraint.SetCompliance(1f / (float)b_interaction.Stiffness6D.AroundTangent, AGXUnity.Constraint.RotationalDof.Z);

        constraint.SetDamping((float)(b_interaction.Damping6D.AlongNormal / b_interaction.Stiffness6D.AlongNormal),
                              AGXUnity.Constraint.TranslationalDof.X);
        constraint.SetDamping((float)(b_interaction.Damping6D.AlongCross / b_interaction.Stiffness6D.AlongCross),
                              AGXUnity.Constraint.TranslationalDof.Y);
        if (b_interaction is B_Mechanics.LockJointInteraction || b_interaction is B_Mechanics.HingeInteraction)
          constraint.SetDamping((float)(b_interaction.Damping6D.AlongTangent / b_interaction.Stiffness6D.AlongTangent),
                                AGXUnity.Constraint.TranslationalDof.Z);

        constraint.SetDamping((float)(b_interaction.Damping6D.AroundNormal / b_interaction.Stiffness6D.AroundNormal),
                              AGXUnity.Constraint.RotationalDof.X);
        constraint.SetDamping((float)(b_interaction.Damping6D.AroundCross / b_interaction.Stiffness6D.AroundCross),
                              AGXUnity.Constraint.RotationalDof.Y);
        if (b_interaction is B_Mechanics.LockJointInteraction || b_interaction is B_Mechanics.PrismaticInteraction)
          constraint.SetDamping((float)(b_interaction.Damping6D.AroundTangent / b_interaction.Stiffness6D.AroundTangent),
                                AGXUnity.Constraint.RotationalDof.Z);
      }
      else if (b_interaction is B_Mechanics.BallJointInteraction)
        constraint.SetBallJointComplianceAndDamping(b_interaction);
      else if (b_interaction is B_Mechanics.SpringJointInteraction)
      {
        var lockController = constraint.GetController<AGXUnity.LockController>();
        lockController.Compliance = 1f / (float)b_interaction.Stiffness6D.AlongTangent;
        lockController.Damping = (float)(b_interaction.Damping6D.AlongTangent / b_interaction.Stiffness6D.AlongTangent);
      }
    }
  }
}
