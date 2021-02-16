using UnityEngine;

using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_Interaction = Brick.Physics.Mechanics.AttachmentPairInteraction;
using B_Mechanics = Brick.Physics.Mechanics;
using B_Stiffness = Brick.Physics.Mechanics.InteractionData6D.Stiffness6D;
using B_Damping = Brick.Physics.Mechanics.InteractionData6D.Damping6D;

using AU_Constraint = AGXUnity.Constraint;
using AU_Controller = AGXUnity.ElementaryConstraintController;
using AU_LockController = AGXUnity.LockController;
using AU_MotorController = AGXUnity.TargetSpeedController;
using AU_RangeController = AGXUnity.RangeController;
using AU_FrictionController = AGXUnity.FrictionController;
using AU_ControllerType = AGXUnity.Constraint.ControllerType;

namespace AGXUnity.BrickUnity.Factories
{
  public static class ConstraintFactory
  {
    public static void SetControllers(this AU_Constraint constraint, B_Connector b_connector, bool overwriteIfDefault)
    {
      foreach (var b_interaction in b_connector.Interactions)
      {
        switch (b_interaction)
        {
          case B_Interaction.MotorInteraction1D b_motor1D:
            constraint.SetTargetSpeedController(b_motor1D, overwriteIfDefault);
            break;
          case B_Interaction.LockInteraction1D b_lock1D:
            constraint.SetLockController(b_lock1D, overwriteIfDefault);
            break;
          case B_Interaction.RangeMinMaxInteraction1D b_range1D:
            constraint.SetRangeController(b_range1D, overwriteIfDefault);
            break;
          case B_Interaction.FrictionInteraction1D b_friction1D:
            constraint.SetFrictionController(b_friction1D, overwriteIfDefault);
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

    /// <summary>
    /// Set the properties of the target speed controller of a constraint from a Brick object
    /// </summary>
    /// <param name="constraint">An AGXUnity.Constraint with a target speed controller</param>
    /// <param name="b_motor1D">A Brick motor 1D interaction</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetTargetSpeedController(this AU_Constraint constraint,
                                                B_Interaction.MotorInteraction1D b_motor1D,
                                                bool overwriteIfDefault)
    {
      var controllerType = AU_ControllerType.Primary;
      if (b_motor1D is B_Interaction.RotationalMotorInteraction1D)
        controllerType = AU_ControllerType.Rotational;
      else if (b_motor1D is B_Interaction.TranslationalMotorInteraction1D)
        controllerType = AU_ControllerType.Translational;

      var targetSpeedController = constraint.GetController<AU_MotorController>(controllerType);

      targetSpeedController.SetGeneralControllerValues(b_motor1D, overwriteIfDefault);
      if (overwriteIfDefault || !b_motor1D._speedIsDefault)
        targetSpeedController.Speed = (float)b_motor1D.Speed;
      if (overwriteIfDefault || !b_motor1D._lockAtZeroSpeedIsDefault)
        targetSpeedController.LockAtZeroSpeed = b_motor1D.LockAtZeroSpeed;
    }

    /// <summary>
    /// Set the properties of the lock controller of a constraint from a Brick object
    /// </summary>
    /// <param name="constraint">An AGXUnity.Constraint with a lock controller</param>
    /// <param name="b_lock1D">A Brick lock 1D interaction</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetLockController(this AU_Constraint constraint,
                                         B_Interaction.LockInteraction1D b_lock1D,
                                         bool overwriteIfDefault)
    {
      var controllerType = AU_ControllerType.Primary;
      if (b_lock1D is B_Interaction.TranslationalLockInteraction1D)
        controllerType = AU_ControllerType.Translational;
      // No RotationalLockInteraction1D existing in brick....
      var lockController = constraint.GetController<AU_LockController>(controllerType);
      lockController.SetGeneralControllerValues(b_lock1D, overwriteIfDefault);
      //lockController.Position = ??
    }

    /// <summary>
    /// Set the properties of the range controller of a constraint from a Brick object
    /// </summary>
    /// <param name="constraint">An AGXUnity.Constraint with a range controller</param>
    /// <param name="b_range1D">A Brick range 1D interaction</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetRangeController(this AU_Constraint constraint,
                                          B_Interaction.RangeMinMaxInteraction1D b_range1D,
                                          bool overwriteIfDefault)
    {
      var controllerType = AU_ControllerType.Primary;
      if (b_range1D is B_Interaction.RotationalRangeMinMaxInteraction1D)
        controllerType = AU_ControllerType.Rotational;
      else if (b_range1D is B_Interaction.TranslationalRangeMinMaxInteraction1D)
        controllerType = AU_ControllerType.Translational;

      var rangeController = constraint.GetController<AU_RangeController>(controllerType);

      rangeController.SetGeneralControllerValues(b_range1D, overwriteIfDefault);
      var range = rangeController.Range;
      if (overwriteIfDefault || !b_range1D._minValueIsDefault)
        range.Min = (float)Brick.Math.Utils.ToRad(b_range1D.MinValue);
      if (overwriteIfDefault || !b_range1D._maxValueIsDefault)
        range.Max = (float)Brick.Math.Utils.ToRad(b_range1D.MaxValue);
      rangeController.Range = range;
    }

    /// <summary>
    /// Set the properties of the friction controller of a constraint from a Brick object
    /// </summary>
    /// <param name="constraint">An AGXUnity.Constraint with a friction controller</param>
    /// <param name="b_friction1D">A Brick friction 1D interaction</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetFrictionController(this AU_Constraint constraint,
                                             B_Interaction.FrictionInteraction1D b_friction1D,
                                             bool overwriteIfDefault)
    {
      var controllerType = AU_ControllerType.Primary;
      if (b_friction1D is B_Interaction.RotationalFrictionInteraction1D)
        controllerType = AU_ControllerType.Rotational;
      else if (b_friction1D is B_Interaction.TranslationalFrictionInteraction1D)
        controllerType = AU_ControllerType.Translational;

      var frictionController = constraint.GetController<AU_FrictionController>(controllerType);

      frictionController.SetGeneralControllerValues(b_friction1D, overwriteIfDefault);
      if (overwriteIfDefault || !b_friction1D._coefficientIsDefault)
        frictionController.FrictionCoefficient = (float)b_friction1D.Coefficient;
    }

    /// <summary>
    /// Set compliance, damping and force range of a controller.
    /// </summary>
    /// <param name="controller">An AGXUnity 1D controller on which to set the properties</param>
    /// <param name="b_interaction">A Brick 1D interaction from which the properties will be set</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity controller's values even if the Brick values are default</param>
    public static void SetGeneralControllerValues(this AU_Controller controller,
                                                  B_Interaction.Interaction1D b_interaction,
                                                  bool overwriteIfDefault)
    {
      if (overwriteIfDefault || !b_interaction._enabledIsDefault)
        controller.Enable = b_interaction.Enabled;
      if (overwriteIfDefault || !b_interaction._stiffnessIsDefault)
        controller.Compliance = 1f / (float)b_interaction.Stiffness;
      if (overwriteIfDefault || !b_interaction._dampingIsDefault)
        controller.Damping = (float)b_interaction.Damping * controller.Compliance;
      var forceRange = controller.ForceRange;
      if (overwriteIfDefault || !b_interaction._minForceIsDefault)
        forceRange.Min = (float)b_interaction.MinForce;
      if (overwriteIfDefault || !b_interaction._maxForceIsDefault)
        forceRange.Max = (float)b_interaction.MaxForce;
      controller.ForceRange = forceRange;
    }

    /// <summary>
    /// Set compliance and damping on all degrees of freedom of a ball joint
    /// </summary>
    /// <param name="constraint">The AGXUnity ball joint constraint on which to set the values</param>
    /// <param name="b_interaction">The Brick interaction from which the values will be set</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity controller's values even if the Brick values are default</param>
    public static void SetBallJointComplianceAndDamping(this AU_Constraint constraint,
                                                        B_Interaction b_interaction,
                                                        bool overwriteIfDefault)
    {
      constraint.SetBrickCompliance(b_interaction.Stiffness6D, AU_Constraint.TranslationalDof.X, overwriteIfDefault);
      constraint.SetBrickCompliance(b_interaction.Stiffness6D, AU_Constraint.TranslationalDof.Y, overwriteIfDefault);
      constraint.SetBrickCompliance(b_interaction.Stiffness6D, AU_Constraint.TranslationalDof.Z, overwriteIfDefault);
      constraint.SetBrickDamping(b_interaction.Damping6D, AU_Constraint.TranslationalDof.X, overwriteIfDefault);
      constraint.SetBrickDamping(b_interaction.Damping6D, AU_Constraint.TranslationalDof.Y, overwriteIfDefault);
      constraint.SetBrickDamping(b_interaction.Damping6D, AU_Constraint.TranslationalDof.Z, overwriteIfDefault);
    }

    /// <summary>
    ///Get a value from a Brick interaction data (compliance or damping) given a rotational degree of freedom
    /// </summary>
    /// <param name="data">The Brick interaction data</param>
    /// <param name="dof">The degree of freedom for which to get the value</param>
    /// <returns>The interaction value (compliance or damping)</returns>
    public static float GetValue(this B_Mechanics.InteractionData6D data, AU_Constraint.RotationalDof dof)
    {
      switch (dof)
      {
        case AU_Constraint.RotationalDof.X:
          return (float)data.AroundNormal;
        case AU_Constraint.RotationalDof.Y:
          return (float)data.AroundCross;
        case AU_Constraint.RotationalDof.Z:
          return (float)data.AroundTangent;
        default:
          throw new System.ArgumentException($"Cannot get interaction data value for degree of freedom \"{dof}\". Select either X, Y or Z");
      }
    }

    /// <summary>
    ///Get a value from a Brick interaction data (compliance or damping) given a translational degree of freedom
    /// </summary>
    /// <param name="data">The Brick interaction data</param>
    /// <param name="dof">The degree of freedom for which to get the value</param>
    /// <returns>The interaction value (compliance or damping)</returns>
    public static float GetValue(this B_Mechanics.InteractionData6D data, AU_Constraint.TranslationalDof dof)
    {
      switch (dof)
      {
        case AU_Constraint.TranslationalDof.X:
          return (float)data.AlongNormal;
        case AU_Constraint.TranslationalDof.Y:
          return (float)data.AlongCross;
        case AU_Constraint.TranslationalDof.Z:
          return (float)data.AlongTangent;
        default:
          throw new System.ArgumentException($"Cannot get interaction data value for degree of freedom \"{dof}\". Select either X, Y or Z");
      }
    }

    /// <summary>
    /// Check if a Brick interaction data value (compliance or damping) is default (not manually set by the user)
    /// </summary>
    /// <param name="data">The Brick interaction data</param>
    /// <param name="dof">The degree of freedom for which to check</param>
    /// <returns>True if the interaction data value is default</returns>
    public static bool GetValueIsDefault(this B_Mechanics.InteractionData6D data, AU_Constraint.RotationalDof dof)
    {
      switch (dof)
      {
        case AU_Constraint.RotationalDof.X:
          return data._aroundNormalIsDefault;
        case AU_Constraint.RotationalDof.Y:
          return data._aroundCrossIsDefault;
        case AU_Constraint.RotationalDof.Z:
          return data._aroundTangentIsDefault;
        default:
          return data._aroundNormalIsDefault && data._aroundCrossIsDefault && data._aroundTangentIsDefault;
      }
    }

    /// <summary>
    /// Check if a Brick interaction data value (compliance or damping) is default (not manually set by the user)
    /// </summary>
    /// <param name="data">The Brick interaction data</param>
    /// <param name="dof">The degree of freedom for which to check</param>
    /// <returns>True if the interaction data value is default</returns>
    public static bool GetValueIsDefault(this B_Mechanics.InteractionData6D data, AU_Constraint.TranslationalDof dof)
    {
      switch (dof)
      {
        case AU_Constraint.TranslationalDof.X:
          return data._alongNormalIsDefault;
        case AU_Constraint.TranslationalDof.Y:
          return data._alongCrossIsDefault;
        case AU_Constraint.TranslationalDof.Z:
          return data._alongTangentIsDefault;
        default:
          return data._alongNormalIsDefault && data._alongCrossIsDefault && data._alongTangentIsDefault;
      }
    }

    /// <summary>
    /// Set damping of an AGXUnity constraint from Brick, for a specific degree of freedom
    /// </summary>
    /// <param name="constraint">The AGXUnity constraint on which to set the damping</param>
    /// <param name="damping">The Brick damping to set</param>
    /// <param name="dof">The degree of freedom for which to set the damping</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetBrickDamping(this AU_Constraint constraint,
                                       B_Damping damping,
                                       AU_Constraint.RotationalDof dof,
                                       bool overwriteIfDefault)
    {
      if (overwriteIfDefault || !damping.GetValueIsDefault(dof))
        constraint.SetDamping(damping.GetValue(dof) * constraint.GetCompliance(dof), dof);
    }

    /// <summary>
    /// Set damping of an AGXUnity constraint from Brick, for a specific degree of freedom
    /// </summary>
    /// <param name="constraint">The AGXUnity constraint on which to set the damping</param>
    /// <param name="damping">The Brick damping to set</param>
    /// <param name="dof">The degree of freedom for which to set the damping</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetBrickDamping(this AU_Constraint constraint,
                                       B_Damping damping,
                                       AU_Constraint.TranslationalDof dof,
                                       bool overwriteIfDefault)
    {
      if (overwriteIfDefault || !damping.GetValueIsDefault(dof))
        constraint.SetDamping(damping.GetValue(dof) * constraint.GetCompliance(dof), dof);
    }

    /// <summary>
    /// Set compliance of an AGXUnity constraint from Brick stiffness, for a specific degree of freedom
    /// </summary>
    /// <param name="constraint">The AGXUnity constraint on which to set the compliance</param>
    /// <param name="stiffness">The Brick stiffness to set</param>
    /// <param name="dof">The degree of freedom for which to set the compliance</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetBrickCompliance(this AU_Constraint constraint,
                                          B_Stiffness stiffness,
                                          AU_Constraint.RotationalDof dof,
                                          bool overwriteIfDefault)
    {
      if (overwriteIfDefault || !stiffness.GetValueIsDefault(dof))
      {
        constraint.SetCompliance(1f / stiffness.GetValue(dof), dof);
        constraint.SetDamping(constraint.GetDamping(dof) / stiffness.GetValue(dof), dof);
      }
    }

    /// <summary>
    /// Set compliance of an AGXUnity constraint from Brick stiffness, for a specific degree of freedom
    /// </summary>
    /// <param name="constraint">The AGXUnity constraint on which to set the compliance</param>
    /// <param name="stiffness">The Brick stiffness to set</param>
    /// <param name="dof">The degree of freedom for which to set the compliance</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetBrickCompliance(this AU_Constraint constraint,
                                          B_Stiffness stiffness,
                                          AU_Constraint.TranslationalDof dof,
                                          bool overwriteIfDefault)
    {
      if (overwriteIfDefault || !stiffness.GetValueIsDefault(dof))
      {
        constraint.SetCompliance(1f / stiffness.GetValue(dof), dof);
        constraint.SetDamping(constraint.GetDamping(dof) / stiffness.GetValue(dof), dof);
      }
    }

    /// <summary>
    /// Set compliance and damping of an AGXUnity constraint given a Brick interaction
    /// </summary>
    /// <param name="constraint">The AGXUnity constraint on which to set the compliance and damping</param>
    /// <param name="b_interaction">The Brick interaction from which the values will be set</param>
    /// <param name="overwriteIfDefault">Set to true to overwrite the AGXUnity constraint's values even if the Brick values are default</param>
    public static void SetComplianceAndDamping(this AU_Constraint constraint,
                                               B_Interaction b_interaction,
                                               bool overwriteIfDefault)
    {
      var damping6D = b_interaction.Damping6D;
      var stiffness6D = b_interaction.Stiffness6D;
      if (b_interaction is B_Mechanics.LockJointInteraction ||
          b_interaction is B_Mechanics.HingeInteraction ||
          b_interaction is B_Mechanics.PrismaticInteraction ||
          b_interaction is B_Mechanics.CylindricalInteraction)
      {
        bool isLockOrHinge = b_interaction is B_Mechanics.LockJointInteraction ||
                             b_interaction is B_Mechanics.HingeInteraction;
        bool isLockOrPrismatic = b_interaction is B_Mechanics.LockJointInteraction ||
                                 b_interaction is B_Mechanics.PrismaticInteraction;

        constraint.SetBrickCompliance(stiffness6D, AU_Constraint.TranslationalDof.X, overwriteIfDefault);
        constraint.SetBrickCompliance(stiffness6D, AU_Constraint.TranslationalDof.Y, overwriteIfDefault);
        if (isLockOrHinge)
          constraint.SetBrickCompliance(stiffness6D, AU_Constraint.TranslationalDof.Z, overwriteIfDefault);

        constraint.SetBrickCompliance(stiffness6D, AU_Constraint.RotationalDof.X, overwriteIfDefault);
        constraint.SetBrickCompliance(stiffness6D, AU_Constraint.RotationalDof.Y, overwriteIfDefault);
        if (isLockOrPrismatic)
          constraint.SetBrickCompliance(stiffness6D, AU_Constraint.RotationalDof.Z, overwriteIfDefault);

        constraint.SetBrickDamping(damping6D, AU_Constraint.TranslationalDof.X, overwriteIfDefault);
        constraint.SetBrickDamping(damping6D, AU_Constraint.TranslationalDof.Y, overwriteIfDefault);
        if (isLockOrHinge)
          constraint.SetBrickDamping(damping6D, AU_Constraint.TranslationalDof.Z, overwriteIfDefault);

        constraint.SetBrickDamping(damping6D, AU_Constraint.RotationalDof.X, overwriteIfDefault);
        constraint.SetBrickDamping(damping6D, AU_Constraint.RotationalDof.Y, overwriteIfDefault);
        if (isLockOrPrismatic)
          constraint.SetBrickDamping(damping6D, AU_Constraint.RotationalDof.Z, overwriteIfDefault);
      }
      else if (b_interaction is B_Mechanics.BallJointInteraction)
        constraint.SetBallJointComplianceAndDamping(b_interaction, overwriteIfDefault);
      else if (b_interaction is B_Mechanics.SpringJointInteraction)
      {
        var lockController = constraint.GetController<AU_LockController>();
        lockController.Compliance = 1f / (float)stiffness6D.AlongTangent;
        lockController.Damping = (float)(damping6D.AlongTangent / stiffness6D.AlongTangent);
      }
    }
  }
}
