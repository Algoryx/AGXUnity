using AGXUnity;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  public static class PatchHelper
  {
    private static bool PatchWarningIssued { get; set; } = false;
    private static bool ShouldPatchUserResponse { get; set; } = false;

    private static bool ShouldPatch
    {
      get
      {
        if ( !PatchWarningIssued ) {
          ShouldPatchUserResponse = EditorUtility.DisplayDialog(
            "Apply patches",
            "Some of the components in this project has been updated and has to be patched. " +
            "Please ensure that the project has been properly backed up before proceeding.",
            "Proceed",
            "Cancel" );
          PatchWarningIssued = true;
        }

        return ShouldPatchUserResponse;
      }
    }


    public static void ApplyPatches()
    {
      // 5.2.0
      ApplyRemoveMassPropertiesComponent();
      ApplyRemoveElementaryConstraintComponents();
    }

    #region 5.2.0
    private static void ApplyRemoveMassPropertiesComponent()
    {
#if UNITY_6000_0_OR_NEWER
      var rbs = Object.FindObjectsByType<RigidBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
      var rbs = Object.FindObjectsOfType<RigidBody>(true);
#endif

      foreach ( var rb in rbs ) {
#pragma warning disable CS0618 // Type or member is obsolete
        var oldMP = rb.GetComponent<AGXUnity.Deprecated.MassProperties>();
#pragma warning restore CS0618 // Type or member is obsolete
        if ( oldMP != null ) {
          if ( !ShouldPatch )
            return;
          rb.MassProperties.Mass = oldMP.Mass;
          rb.MassProperties.InertiaDiagonal = oldMP.InertiaDiagonal;
          rb.MassProperties.InertiaOffDiagonal.UserValue = oldMP.InertiaOffDiagonal.UserValue;
          rb.MassProperties.InertiaOffDiagonal.DefaultValue = oldMP.InertiaOffDiagonal.DefaultValue;
          rb.MassProperties.InertiaOffDiagonal.UseDefault = oldMP.InertiaOffDiagonal.UseDefault;
          rb.MassProperties.CenterOfMassOffset = oldMP.CenterOfMassOffset;
          rb.MassProperties.MassCoefficients = oldMP.MassCoefficients;
          rb.MassProperties.InertiaCoefficients = oldMP.InertiaCoefficients;
          Object.DestroyImmediate( oldMP );
        }
        EditorUtility.SetDirty( rb );
        EditorUtility.SetDirty( rb.gameObject );
      }
      AssetDatabase.SaveAssets();
    }

    private static void ApplyRemoveElementaryConstraintComponents()
    {
#if UNITY_6000_0_OR_NEWER
      var constraints = Object.FindObjectsByType<Constraint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
      var constraints = Object.FindObjectsOfType<Constraint>(true);
#endif
#pragma warning disable CS0612 // Type or member is obsolete
      foreach ( var constraint in constraints ) {
        List<ElementaryConstraint> newEcs = new List<ElementaryConstraint>();
        foreach ( var ec in constraint.GetComponents<AGXUnity.Deprecated.ElementaryConstraint>() ) {
          if ( !ShouldPatch )
            return;
          ElementaryConstraint newEc;
          if ( ec is AGXUnity.Deprecated.TargetSpeedController ts )
            newEc = new TargetSpeedController()
            {
              LockAtZeroSpeed = ts.LockAtZeroSpeed,
              Speed = ts.Speed
            };
          else if ( ec is AGXUnity.Deprecated.RangeController rc )
            newEc = new RangeController() { Range = rc.Range };
          else if ( ec is AGXUnity.Deprecated.LockController lc )
            newEc = new LockController() { Position = lc.Position };
          else if ( ec is AGXUnity.Deprecated.ScrewController sc )
            newEc = new ScrewController() { Lead = sc.Lead };
          else if ( ec is AGXUnity.Deprecated.FrictionController fc )
            newEc = new FrictionController()
            {
              FrictionCoefficient = fc.FrictionCoefficient,
              NonLinearDirectSolveEnabled = fc.NonLinearDirectSolveEnabled,
            };
          else if ( ec is AGXUnity.Deprecated.ElectricMotorController emc )
            newEc = new ElectricMotorController()
            {
              Voltage = emc.Voltage,
              ArmatureResistance = emc.ArmatureResistance,
              TorqueConstant = emc.TorqueConstant
            };
          else
            newEc = new ElementaryConstraint();

          newEc.Enable = ec.Enable;

          newEc.MigrateInternalData( ec );
          newEcs.Add( newEc );
          Object.DestroyImmediate( ec );
        }
        constraint.MigrateElementaryConstraints( newEcs );

        if ( constraint.TryGetComponent<AGXUnity.Deprecated.AttachmentPair>( out var ap ) ) {
          if ( !ShouldPatch )
            return;
          constraint.AttachmentPair.Synchronized = ap.Synchronized;
          constraint.AttachmentPair.ReferenceFrame = ap.ReferenceFrame;
          constraint.AttachmentPair.ConnectedFrame = ap.ConnectedFrame;

          Object.DestroyImmediate( ap );
        }

        EditorUtility.SetDirty( constraint );
        EditorUtility.SetDirty( constraint.gameObject );
      }
      AssetDatabase.SaveAssets();
#pragma warning restore CS0612 // Type or member is obsolete
      #endregion
    }
  }
}