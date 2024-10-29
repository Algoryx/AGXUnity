using AGXUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
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
      AssetDatabase.DisallowAutoRefresh();

      // 5.2.0
      ApplyRemoveElementaryConstraintComponents();
      ApplyRemoveMassPropertiesComponents();
      ApplyRemoveRouteComponents();
      ApplyRemoveWinchNodeComponents();

      AssetDatabase.AllowAutoRefresh();

      // Save everything if any patch was applied
      if ( PatchWarningIssued && ShouldPatchUserResponse ) {
        // Reset user confirmation
        PatchWarningIssued = false;

        AssetDatabase.SaveAssets();
        // Save the current scenes
        EditorSceneManager.SaveOpenScenes();
      }
    }

    private static void CopyDefaultAndUserValue<T>( DefaultAndUserValue<T> source, DefaultAndUserValue<T> target )
      where T : struct
    {
      target.UserValue = source.UserValue;
      target.DefaultValue = source.DefaultValue;
      target.UseDefault = source.UseDefault;
    }

    private static T[] FindPrefabDepthOrderedObjects<T>()
      where T : Object
    {
      return Resources.FindObjectsOfTypeAll<T>().OrderBy( o => FindPrefabDepth( o ) ).ToArray();
    }

    #region 5.2.0
#pragma warning disable CS0618
#pragma warning disable CS0612 // Type or member is obsolete
    private static void ApplyRemoveMassPropertiesComponents()
    {
      var mps = FindPrefabDepthOrderedObjects<AGXUnity.Deprecated.MassProperties>();

      foreach ( var oldMP in mps ) {
        var rb = oldMP.RigidBody;
        if ( oldMP != null ) {
          if ( !ShouldPatch ) return;
          CopyDefaultAndUserValue( oldMP.Mass, rb.MassProperties.Mass );
          CopyDefaultAndUserValue( oldMP.InertiaDiagonal, rb.MassProperties.InertiaDiagonal );
          CopyDefaultAndUserValue( oldMP.InertiaOffDiagonal, rb.MassProperties.InertiaOffDiagonal );
          CopyDefaultAndUserValue( oldMP.CenterOfMassOffset, rb.MassProperties.CenterOfMassOffset );
          rb.MassProperties.MassCoefficients = oldMP.MassCoefficients;
          rb.MassProperties.InertiaCoefficients = oldMP.InertiaCoefficients;
          Object.DestroyImmediate( oldMP, true );
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications( rb );
        EditorUtility.SetDirty( rb );
        EditorUtility.SetDirty( rb.gameObject );
      }
    }

    private static int FindPrefabDepth( Object obj )
    {
      int depth = 0;
      obj = PrefabUtility.GetNearestPrefabInstanceRoot( obj );
      while ( obj != null && depth < 50 ) {
        obj = PrefabUtility.GetCorrespondingObjectFromSource( obj );
        obj = PrefabUtility.GetNearestPrefabInstanceRoot( obj );
        depth++;
      }
      return depth;
    }

    private static void ApplyRemoveElementaryConstraintComponents()
    {
      var constraints = FindPrefabDepthOrderedObjects<Constraint>();

      var ecGetter = typeof( Constraint ).GetField( "m_elementaryConstraintsNew", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );

      foreach ( var constraint in constraints ) {
        bool patched = false;
        if ( PrefabUtility.IsPartOfPrefabInstance( constraint ) )
          PrefabUtility.MergePrefabInstance( PrefabUtility.GetNearestPrefabInstanceRoot( constraint ) );
        List<ElementaryConstraint> newEcs = (List<ElementaryConstraint>)ecGetter.GetValue(constraint);
        var oldEcs = constraint.GetComponents<AGXUnity.Deprecated.ElementaryConstraint>();
        if ( oldEcs.Length > 0 ) {
          foreach ( var ec in oldEcs ) {
            if ( !ShouldPatch ) return;

            ElementaryConstraint newEc = newEcs.FirstOrDefault( existing => existing?.NativeName == ec.NativeName );
            if ( newEc ==  null ) {
              newEc = ec switch
              {
                AGXUnity.Deprecated.TargetSpeedController ts => new TargetSpeedController(),
                AGXUnity.Deprecated.RangeController rc => new RangeController(),
                AGXUnity.Deprecated.LockController lc => new LockController(),
                AGXUnity.Deprecated.ScrewController sc => new ScrewController(),
                AGXUnity.Deprecated.FrictionController fc => new FrictionController(),
                AGXUnity.Deprecated.ElectricMotorController emc => new ElectricMotorController(),
                _ => new ElementaryConstraint()
              };
              newEcs.Add( newEc );
            }

            switch ( ec ) {
              case AGXUnity.Deprecated.TargetSpeedController ts:
                ( newEc as TargetSpeedController ).LockAtZeroSpeed = ts.LockAtZeroSpeed;
                ( newEc as TargetSpeedController ).Speed = ts.Speed;
                break;
              case AGXUnity.Deprecated.RangeController rc: ( newEc as RangeController ).Range = rc.Range; break;
              case AGXUnity.Deprecated.LockController lc: ( newEc as LockController ).Position = lc.Position; break;
              case AGXUnity.Deprecated.ScrewController sc: ( newEc as ScrewController ).Lead = sc.Lead; break;
              case AGXUnity.Deprecated.FrictionController fc:
                ( newEc as FrictionController ).FrictionCoefficient = fc.FrictionCoefficient;
                ( newEc as FrictionController ).MinimumStaticFrictionForceRange= fc.MinimumStaticFrictionForceRange;
                break;
              case AGXUnity.Deprecated.ElectricMotorController emc:
                ( newEc as ElectricMotorController ).Voltage = emc.Voltage;
                ( newEc as ElectricMotorController ).ArmatureResistance = emc.ArmatureResistance;
                ( newEc as ElectricMotorController ).TorqueConstant = emc.TorqueConstant;
                break;
              default: break;
            };

            newEc.Enable = ec.Enable;

            newEc.MigrateInternalData( ec );
            Object.DestroyImmediate( ec, true );
            patched = true;
          }
        }

        if ( constraint.TryGetComponent<AGXUnity.Deprecated.AttachmentPair>( out var ap ) ) {
          if ( !ShouldPatch ) return;
          constraint.AttachmentPair.Synchronized = ap.Synchronized;
          constraint.AttachmentPair.ReferenceFrame = ap.ReferenceFrame;
          constraint.AttachmentPair.ConnectedFrame = ap.ConnectedFrame;

          Object.DestroyImmediate( ap, true );
          patched = true;
        }

        if ( patched ) {
          PrefabUtility.RecordPrefabInstancePropertyModifications( constraint );
          EditorUtility.SetDirty( constraint );
          EditorUtility.SetDirty( constraint.gameObject );
        }
      }
    }

    private static void ApplyRemoveRouteComponents()
    {
      var wireRoutes = FindPrefabDepthOrderedObjects<AGXUnity.Deprecated.WireRoute>().Select(route => Tuple.Create(route, route.Wire));
      var cableRoutes = FindPrefabDepthOrderedObjects<AGXUnity.Deprecated.CableRoute>().Select(route => Tuple.Create(route, route.GetComponent<Cable>()));

      var found = AssetDatabase.FindAssets("t:CableRoute");

      foreach ( var (route, wire) in wireRoutes ) {
        if ( !ShouldPatch ) return;
        //if ( PrefabUtility.IsPartOfPrefabInstance( wire ) )
        //  PrefabUtility.MergePrefabInstance( PrefabUtility.GetNearestPrefabInstanceRoot( wire ) );
        if ( wire != null ) {
          var go = wire.gameObject;
          var newRoute = wire.Route;
          newRoute.Clear();
          foreach ( var node in route )
            newRoute.Add( node );
          Object.DestroyImmediate( route, true );
          EditorUtility.SetDirty( go );
          EditorUtility.SetDirty( wire );
          PrefabUtility.RecordPrefabInstancePropertyModifications( wire );
        }
        else // The FindObjectOfTypeAll seems to pick up some stray objects that are not linked to any wire. We simply destroy these
          Object.DestroyImmediate( route, true );
      }

      foreach ( var (route, cable) in cableRoutes ) {
        if ( !ShouldPatch ) return;
        if ( PrefabUtility.IsPartOfPrefabInstance( cable ) ) {
          var root = PrefabUtility.GetNearestPrefabInstanceRoot( cable );
          if ( !( PrefabUtility.IsPartOfVariantPrefab( root ) && PrefabUtility.IsPartOfPrefabAsset( root ) ) )
            PrefabUtility.MergePrefabInstance( root );
        }
        if ( cable != null ) {
          var newRoute = cable.Route;
          newRoute.Clear();
          foreach ( var node in route )
            newRoute.Add( node );
          Object.DestroyImmediate( route, true );
          EditorUtility.SetDirty( cable.gameObject );
          EditorUtility.SetDirty( cable );
          PrefabUtility.RecordPrefabInstancePropertyModifications( cable );
        }
        else
          Object.DestroyImmediate( route, true );
      }
    }

    private static void ApplyRemoveWinchNodeComponents()
    {
      var wires = FindPrefabDepthOrderedObjects<Wire>();

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
    }
#pragma warning restore CS0618
#pragma warning restore CS0612 // Type or member is obsolete
    #endregion
  }
}
