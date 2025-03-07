using AGXUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    private static bool Patching { get; set; } = false;

    #region 5.2.0
#pragma warning disable CS0618
#pragma warning disable CS0612 // Type or member is obsolete

    public static bool SceneNeedsPatch()
    {
      if ( Patching )
        return false;

      var roots = SceneManager.GetActiveScene().GetRootGameObjects();

      var constraints = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.ElementaryConstraint>(true) );
      var attachments = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.AttachmentPair>(true) );
      var mps         = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.MassProperties>(true) );
      var cables      = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.CableRoute>(true) );
      var wires       = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.WireRoute>(true) );
      var winches     = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.WireWinch>(true) );

      if ( constraints.Count() > 0 )
        return true;
      if ( attachments.Count() > 0 )
        return true;
      if ( mps.Count() > 0 )
        return true;
      if ( cables.Count() > 0 )
        return true;
      if ( wires.Count() > 0 )
        return true;
      if ( winches.Count() > 0 )
        return true;
      return false;
    }

    [MenuItem( "AGXUnity/Utils/Apply 5.2.0 patch" )]
    public static void ApplyPatches()
    {
      if ( Patching )
        return;

      if ( !ShouldPatch ) {
        PatchWarningIssued = false;
        return;
      }

      Patching = true;

      try {
        s_processed.Clear();

        var prefabs = AssetDatabase.FindAssets( "t:prefab" ).Select(guid => AssetDatabase.GUIDToAssetPath( guid ) ).ToArray();
        for ( int i = 0; i < prefabs.Length; i++ ) {
          EditorUtility.DisplayProgressBar( "Patching objects to 5.2.0", "Patching prefabs...", (float)i / prefabs.Length );
          HandlePrefabPath( prefabs[ i ] );
        }

        var scenes = AssetDatabase.FindAssets( "t:scene" ).Select(guid => AssetDatabase.GUIDToAssetPath( guid ) ).Where(p => !p.StartsWith("Packages")).ToArray();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for ( int i = 0; i < scenes.Length; i++ ) {
          EditorUtility.DisplayProgressBar( "Patching objects to 5.2.0", "Patching Scenes...", (float)i / scenes.Length );
          EditorSceneManager.OpenScene( scenes[ i ], OpenSceneMode.Single );
          PatchScene();
        }

        for ( int i = 0; i < prefabs.Length; i++ ) {
          EditorUtility.DisplayProgressBar( "Patching objects to 5.2.0", "Removing old prefab components...", (float)i / prefabs.Length );
          var path = prefabs[i];
          if ( String.IsNullOrEmpty( path ) || path.Replace( "\\", "/" ).StartsWith( "Packages/" ) )
            continue;

          using ( var loaded = new PrefabUtility.EditPrefabContentsScope( path ) ) {
            var ecs         = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.ElementaryConstraint>(true);
            var attachments = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.AttachmentPair>(true);
            var mps         = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.MassProperties>(true);
            var cables      = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.CableRoute>(true);
            var wires       = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.WireRoute>(true);
            var winches     = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.WireWinch>(true);

            foreach ( var ec in ecs )
              Object.DestroyImmediate( ec );
            foreach ( var ap in attachments )
              Object.DestroyImmediate( ap );
            foreach ( var mp in mps )
              Object.DestroyImmediate( mp );
            foreach ( var cable in cables )
              Object.DestroyImmediate( cable );
            foreach ( var wire in wires )
              Object.DestroyImmediate( wire );
            foreach ( var winch in winches )
              Object.DestroyImmediate( winch );
          }
        }

        EditorSceneManager.RestoreSceneManagerSetup( setup );
      }
      catch ( System.Exception e ) {
        Debug.LogError( "Failed to patch with error" + e.ToString() );
      }
      EditorUtility.ClearProgressBar();
      PatchWarningIssued = false;
      Patching = false;
    }

    private static HashSet<string> s_processed = new HashSet<string>();

    private static void HandlePrefabPath( string path )
    {
      if ( String.IsNullOrEmpty( path ) || s_processed.Contains( path ) )
        return;

      if ( path.Replace( "\\", "/" ).StartsWith( "Packages/" ) ) {
        Debug.LogWarning( $"Patching  process found immutable dependent asset '{path}'. This asset will not be patched" );
        return;
      }

      s_processed.Add( path );
      using ( var loaded = new PrefabUtility.EditPrefabContentsScope( path ) ) {
        var constraints = loaded.prefabContentsRoot.GetComponentsInChildren<Constraint>(true);
        var mps = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.MassProperties>(true);
        var cables = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.CableRoute>(true);
        var wires = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.WireRoute>(true);
        var winches = loaded.prefabContentsRoot.GetComponentsInChildren<AGXUnity.Deprecated.WireWinch>(true);

        if ( mps.Length == 0 && cables.Length == 0 && wires.Length == 0 && !constraints.Any( c => c.GetComponents<AGXUnity.Deprecated.ElementaryConstraint>().Length != 0 ) )
          return;

        foreach ( var s in loaded.prefabContentsRoot.GetComponentsInChildren<ScriptComponent>( true ) ) {
          if ( s != null ) {
            var subPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( s );
            HandlePrefabPath( subPath );
          }
        }

        foreach ( var constraint in constraints )
          MigrateConstraint( constraint );
        foreach ( var mp in mps )
          MigrateMassProperties( mp );
        foreach ( var cable in cables )
          MigrateCableRoute( cable.GetComponent<Cable>(), cable );
        foreach ( var wire in wires )
          MigrateWireRoute( wire.GetComponent<Wire>(), wire );
      }
    }

    private static void PatchScene()
    {
      var roots = SceneManager.GetActiveScene().GetRootGameObjects();

      var constraints = roots.SelectMany( r => r.GetComponentsInChildren<Constraint>(true) );
      var ecs         = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.ElementaryConstraint>(true) );
      var attachments = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.AttachmentPair>(true) );
      var mps         = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.MassProperties>(true) );
      var cables      = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.CableRoute>(true) );
      var wires       = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.WireRoute>(true) );
      var winches     = roots.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Deprecated.WireWinch>(true) );

      foreach ( var constraint in constraints )
        MigrateConstraint( constraint );
      foreach ( var mp in mps )
        MigrateMassProperties( mp );
      foreach ( var cable in cables )
        MigrateCableRoute( cable.GetComponent<Cable>(), cable );
      foreach ( var wire in wires )
        MigrateWireRoute( wire.GetComponent<Wire>(), wire );

      foreach ( var ec in ecs )
        Object.DestroyImmediate( ec );
      foreach ( var ap in attachments )
        Object.DestroyImmediate( ap );
      foreach ( var mp in mps )
        Object.DestroyImmediate( mp );
      foreach ( var cable in cables )
        Object.DestroyImmediate( cable );
      foreach ( var wire in wires )
        Object.DestroyImmediate( wire );
      foreach ( var winch in winches )
        Object.DestroyImmediate( winch );

      EditorSceneManager.SaveOpenScenes();
    }

    private static void CopyDefaultAndUserValue<T>( DefaultAndUserValue<T> source, DefaultAndUserValue<T> target )
      where T : struct
    {
      target.UserValue = source.UserValue;
      target.DefaultValue = source.DefaultValue;
      target.UseDefault = source.UseDefault;
    }

    private static void MigrateMassProperties( AGXUnity.Deprecated.MassProperties oldMP )
    {
      if ( oldMP != null ) {
        var rb = oldMP.RigidBody;
        if ( rb == null || !ShouldPatch ) return;
        CopyDefaultAndUserValue( oldMP.Mass, rb.MassProperties.Mass );
        CopyDefaultAndUserValue( oldMP.InertiaDiagonal, rb.MassProperties.InertiaDiagonal );
        CopyDefaultAndUserValue( oldMP.InertiaOffDiagonal, rb.MassProperties.InertiaOffDiagonal );
        CopyDefaultAndUserValue( oldMP.CenterOfMassOffset, rb.MassProperties.CenterOfMassOffset );
        rb.MassProperties.MassCoefficients = oldMP.MassCoefficients;
        rb.MassProperties.InertiaCoefficients = oldMP.InertiaCoefficients;
        PrefabUtility.RecordPrefabInstancePropertyModifications( rb );
        EditorUtility.SetDirty( rb );
        EditorUtility.SetDirty( rb.gameObject );
      }
    }

    static FieldInfo s_ecField = null;
    private static List<ElementaryConstraint> GetConstraintECs( Constraint constraint )
    {
      if ( s_ecField == null )
        s_ecField = typeof( Constraint ).GetField( "m_elementaryConstraintsNew", BindingFlags.NonPublic | BindingFlags.Instance );
      return (List<ElementaryConstraint>)s_ecField.GetValue( constraint );
    }

    private static void MigrateConstraint( Constraint constraint )
    {
      bool patched = false;
      List<ElementaryConstraint> newEcs = GetConstraintECs(constraint);
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
          patched = true;
        }
      }

      if ( constraint.TryGetComponent<AGXUnity.Deprecated.AttachmentPair>( out var ap ) ) {
        if ( !ShouldPatch ) return;
        constraint.AttachmentPair.Synchronized = ap.Synchronized;
        constraint.AttachmentPair.ReferenceFrame = ap.ReferenceFrame;
        constraint.AttachmentPair.ConnectedFrame = ap.ConnectedFrame;
        patched = true;
      }

      if ( patched ) {
        PrefabUtility.RecordPrefabInstancePropertyModifications( constraint );
        EditorUtility.SetDirty( constraint );
        EditorUtility.SetDirty( constraint.gameObject );
      }
    }

    private static void MigrateCableRoute( Cable cable, AGXUnity.Deprecated.CableRoute route )
    {
      if ( !ShouldPatch ) return;

      if ( cable != null ) {
        var newRoute = cable.Route;
        newRoute.Clear();
        foreach ( var node in route )
          newRoute.Add( node );
        EditorUtility.SetDirty( cable.gameObject );
        EditorUtility.SetDirty( cable );
        PrefabUtility.RecordPrefabInstancePropertyModifications( cable );
      }
    }

    private static void MigrateWireRoute( Wire wire, AGXUnity.Deprecated.WireRoute route )
    {

      if ( !ShouldPatch ) return;
      if ( wire != null ) {
        var go = wire.gameObject;
        var newRoute = wire.Route;
        newRoute.Clear();
        foreach ( var node in route ) {
          newRoute.Add( node );
          if ( node.Type == Wire.NodeType.WinchNode && node.DeprecatedWinchComponent != null ) {
            if ( !ShouldPatch ) return;
            // Force node to create winch object
            node.Type = node.Type;

            var old = node.DeprecatedWinchComponent;
            node.Winch.Speed = old.Speed;
            node.Winch.PulledInLength = old.PulledInLength;
            node.Winch.ForceRange = old.ForceRange;
            node.Winch.BrakeForceRange = old.BrakeForceRange;
          }
        }
        EditorUtility.SetDirty( go );
        EditorUtility.SetDirty( wire );
        PrefabUtility.RecordPrefabInstancePropertyModifications( wire );
      }
    }
#pragma warning restore CS0618
#pragma warning restore CS0612 // Type or member is obsolete
    #endregion
  }
}
