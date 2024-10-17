using AGXUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
      ApplyRemoveMassPropertiesComponents();
      ApplyRemoveElementaryConstraintComponents();
      ApplyRemoveRouteComponents();
      ApplyRemoveWinchNodeComponents();
    }

    #region 5.2.0
#pragma warning disable CS0618
#pragma warning disable CS0612 // Type or member is obsolete
    private static void ApplyRemoveMassPropertiesComponents()
    {
      var mps = Resources.FindObjectsOfTypeAll<AGXUnity.Deprecated.MassProperties>();

      foreach ( var oldMP in mps ) {
        var rb = oldMP.RigidBody;
        if ( oldMP != null ) {
          if ( !ShouldPatch ) return;
          rb.MassProperties.Mass = oldMP.Mass;
          rb.MassProperties.InertiaDiagonal = oldMP.InertiaDiagonal;
          rb.MassProperties.InertiaOffDiagonal.UserValue = oldMP.InertiaOffDiagonal.UserValue;
          rb.MassProperties.InertiaOffDiagonal.DefaultValue = oldMP.InertiaOffDiagonal.DefaultValue;
          rb.MassProperties.InertiaOffDiagonal.UseDefault = oldMP.InertiaOffDiagonal.UseDefault;
          rb.MassProperties.CenterOfMassOffset = oldMP.CenterOfMassOffset;
          rb.MassProperties.MassCoefficients = oldMP.MassCoefficients;
          rb.MassProperties.InertiaCoefficients = oldMP.InertiaCoefficients;
          Object.DestroyImmediate( oldMP, true );
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications( rb );
        EditorUtility.SetDirty( rb );
        EditorUtility.SetDirty( rb.gameObject );
      }
      AssetDatabase.SaveAssets();
    }

    private static void ApplyRemoveElementaryConstraintComponents()
    {
      var constraints = Resources.FindObjectsOfTypeAll<Constraint>();
      foreach ( var constraint in constraints ) {
        List<ElementaryConstraint> newEcs = new List<ElementaryConstraint>();
        var oldEcs = constraint.GetComponents<AGXUnity.Deprecated.ElementaryConstraint>();
        if ( oldEcs.Length > 0 ) {

          foreach ( var ec in oldEcs ) {
            if ( !ShouldPatch ) return;
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
            Object.DestroyImmediate( ec, true );
          }
          constraint.MigrateElementaryConstraints( newEcs );
        }

        if ( constraint.TryGetComponent<AGXUnity.Deprecated.AttachmentPair>( out var ap ) ) {
          if ( !ShouldPatch ) return;
          constraint.AttachmentPair.Synchronized = ap.Synchronized;
          constraint.AttachmentPair.ReferenceFrame = ap.ReferenceFrame;
          constraint.AttachmentPair.ConnectedFrame = ap.ConnectedFrame;

          Object.DestroyImmediate( ap, true );
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications( constraint );
        EditorUtility.SetDirty( constraint );
        EditorUtility.SetDirty( constraint.gameObject );
      }
      AssetDatabase.SaveAssets();
    }

    private static void ApplyRemoveRouteComponents()
    {
      var wireRoutes = Resources.FindObjectsOfTypeAll<AGXUnity.Deprecated.WireRoute>().Select(route => Tuple.Create(route, route.Wire));
      var cableRoutes = Resources.FindObjectsOfTypeAll<AGXUnity.Deprecated.CableRoute>().Select(route => Tuple.Create(route, route.GetComponent<Cable>()));

      foreach ( var (route, wire) in wireRoutes ) {
        if ( !ShouldPatch ) return;
        if ( wire != null ) {
          var go = wire.gameObject;
          var newRoute = wire.Route;
          foreach ( var node in route )
            newRoute.Add( node );
          Object.DestroyImmediate( route, true );
          EditorUtility.SetDirty( go );
          PrefabUtility.RecordPrefabInstancePropertyModifications( wire );
        }
        else // The FindObjectOfTypeAll seems to pick up some stray objects that are not linked to any wire. We simply destroy these
          Object.DestroyImmediate( route, true );
      }

      foreach ( var (route, cable) in cableRoutes ) {
        if ( !ShouldPatch ) return;
        if ( cable != null ) {
          var go = route.gameObject;
          var newRoute = cable.Route;
          foreach ( var node in route )
            newRoute.Add( node );
          Object.DestroyImmediate( route, true );
          EditorUtility.SetDirty( go );
          PrefabUtility.RecordPrefabInstancePropertyModifications( cable );
        }
        else
          Object.DestroyImmediate( route, true );
      }

      AssetDatabase.SaveAssets();
    }

    private static void ApplyRemoveWinchNodeComponents()
    {
      var wires = Resources.FindObjectsOfTypeAll<Wire>();

      foreach ( var wire in wires ) {
        foreach ( var node in wire.Route ) {
          if ( node.Type == Wire.NodeType.WinchNode && node.DeprecatedWinchComponent != null ) {
            if ( !ShouldPatch ) return;
            // Force node to create winch object
            node.Type = node.Type;

            var old = node.DeprecatedWinchComponent;
            node.Winch.Speed = old.Speed;
            node.Winch.PulledInLength = old.PulledInLength;
            node.Winch.ForceRange = old.ForceRange;
            node.Winch.BrakeForceRange = old.BrakeForceRange;

            Object.DestroyImmediate( old, true );
            PrefabUtility.RecordPrefabInstancePropertyModifications( wire );
            EditorUtility.SetDirty( wire );
          }
        }
      }
      AssetDatabase.SaveAssets();
    }
#pragma warning restore CS0618
#pragma warning restore CS0612 // Type or member is obsolete
    #endregion
  }
}
