using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Shape = AGXUnity.Collide.Shape;

namespace AGXUnityEditor.Utils
{
  public static partial class PhysXConversion
  {
    public static Result ConvertScene( IEnumerable<Component> selected )
    {
      var result = new Result();
      if ( EditorApplication.isPlayingOrWillChangePlaymode ) {
        result.Errors.Add( "Conversion is only available in Edit mode." );
        return result;
      }
      var plan = BuildPlan( Discover( SceneRoots() ), selected );
      if ( !plan.CanExecute ) {
        result.Errors.AddRange( plan.Errors );
        return result;
      }
      Undo.IncrementCurrentGroup();
      int group = Undo.GetCurrentGroup();
      Undo.SetCurrentGroupName( "Convert PhysX components to AGX" );
      try {
        Execute( plan, true );
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations( group );
        result.Converted = plan.ComponentCount;
      }
      catch ( Exception exception ) {
        Undo.RevertAllDownToGroup( group );
        result.Errors.Add( "Scene conversion rolled back: " + exception.Message );
      }
      return result;
    }

    public static Result ConvertPrefab( string path )
    {
      var result = new Result();
      if ( EditorApplication.isPlayingOrWillChangePlaymode ) {
        result.Errors.Add( "Conversion is only available in Edit mode." );
        return result;
      }
      GameObject root = null;
      try {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( asset == null || PrefabUtility.IsPartOfImmutablePrefab( asset ) || !path.StartsWith( "Assets/", StringComparison.Ordinal ) )
          throw new InvalidOperationException( "Prefab is missing, immutable, or outside Assets." );
        root = PrefabUtility.LoadPrefabContents( path );
        var candidates = Discover( new[] { root } );
        var plan = BuildPlan( candidates, candidates.Where( c => c.Supported ).Select( c => c.Source ) );
        result.Skipped = candidates.Where( c => !c.Supported ).Sum( c => c.ComponentCount );
        if ( !plan.CanExecute ) {
          result.Errors.AddRange( plan.Errors );
          return result;
        }
        Execute( plan, false );
        PrefabUtility.SaveAsPrefabAsset( root, path, out bool success );
        if ( !success )
          throw new InvalidOperationException( "Unity could not save the converted prefab." );
        result.Converted = plan.ComponentCount;
      }
      catch ( Exception exception ) {
        result.Errors.Add( path + ": " + exception.Message );
      }
      finally {
        if ( root != null )
          PrefabUtility.UnloadPrefabContents( root );
      }
      return result;
    }

    private static void Execute( Plan plan, bool undo )
    {
      var sources = new List<Component>();
      var bodies = plan.Candidates.Select( c => c.Source ).OfType<Rigidbody>().ToArray();
      foreach ( var body in bodies ) {
        ConvertBody( body, undo );
        sources.Add( body );
      }
      var articulatedRoots = new HashSet<GameObject>();
      foreach ( var body in bodies ) {
        var top = body.gameObject;
        for ( var parent = body.transform.parent; parent != null; parent = parent.parent )
          if ( parent.GetComponent<AGXUnity.RigidBody>() != null )
            top = parent.gameObject;
        if ( top != body.gameObject )
          articulatedRoots.Add( top );
      }
      foreach ( var root in articulatedRoots )
        if ( root.GetComponentInParent<AGXUnity.ArticulatedRoot>( true ) == null )
          Add<AGXUnity.ArticulatedRoot>( root, undo );
      foreach ( var candidate in plan.Candidates ) {
        var colliders = candidate.Source is Collider collider ? new[] { collider } : candidate.Colliders;
        foreach ( var source in colliders ) {
          ConvertCollider( source, undo );
          sources.Add( source );
        }
      }
      // PhysX colliders must be removed before their owning rigidbodies.
      foreach ( var source in sources.OrderBy( s => s is Rigidbody ? 1 : 0 ) ) {
        var scene = source.gameObject.scene;
        if ( undo )
          Undo.DestroyObjectImmediate( source );
        else
          UnityEngine.Object.DestroyImmediate( source );
        if ( undo )
          EditorSceneManager.MarkSceneDirty( scene );
      }
    }

    private static T Add<T>( GameObject gameObject, bool undo ) where T : Component =>
      undo ? Undo.AddComponent<T>( gameObject ) : gameObject.AddComponent<T>();

    private static void ConvertBody( Rigidbody source, bool undo )
    {
      var target = Add<AGXUnity.RigidBody>( source.gameObject, undo );
      target.MassProperties.Mass.UseDefault = false;
      target.MassProperties.Mass.Value = source.mass;
#if UNITY_6000_0_OR_NEWER
      target.LinearVelocityDamping = Vector3.one * source.linearDamping;
      target.AngularVelocityDamping = Vector3.one * source.angularDamping;
      target.LinearVelocity = source.linearVelocity;
#else
      target.LinearVelocityDamping = Vector3.one * source.drag;
      target.AngularVelocityDamping = Vector3.one * source.angularDrag;
      target.LinearVelocity = source.velocity;
#endif
      target.AngularVelocity = source.angularVelocity;
      if ( !source.automaticCenterOfMass ) {
        target.MassProperties.CenterOfMassOffset.UseDefault = false;
        // Unity's centerOfMass is relative to position/rotation, independent of scale.
        target.MassProperties.CenterOfMassOffset.Value = source.centerOfMass;
      }
      if ( !source.automaticInertiaTensor ) {
        InertiaTensor( source.inertiaTensor, source.inertiaTensorRotation, out var diagonal, out var offDiagonal );
        target.MassProperties.InertiaDiagonal.UseDefault = false;
        target.MassProperties.InertiaDiagonal.Value = diagonal;
        target.MassProperties.InertiaOffDiagonal.UseDefault = false;
        target.MassProperties.InertiaOffDiagonal.Value = offDiagonal;
      }
      if ( source.isKinematic )
        target.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
      Finish( target, undo );
    }

    private static void ConvertCollider( Collider source, bool undo )
    {
      if ( source is TerrainCollider ) {
        var terrain = Add<AGXUnity.Model.DeformableTerrain>( source.gameObject, undo );
        terrain.enabled = source.enabled;
        Finish( terrain, undo );
        return;
      }
      Shape target;
      {
        var child = new GameObject( "AGX " + source.GetType().Name );
        if ( undo ) {
          Undo.RegisterCreatedObjectUndo( child, "Create AGX shape" );
          Undo.SetTransformParent( child.transform, source.transform, "Parent AGX shape" );
        }
        else
          child.transform.SetParent( source.transform, false );
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        child.layer = source.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags( child, GameObjectUtility.GetStaticEditorFlags( source.gameObject ) );
        var scale = Abs( source.transform.lossyScale );
        switch ( source ) {
          case BoxCollider box:
            var agxBox = Add<AGXUnity.Collide.Box>( child, undo );
            child.transform.localPosition = box.center;
            agxBox.HalfExtents = Vector3.Scale( box.size * 0.5f, scale );
            target = agxBox;
            break;
          case SphereCollider sphere:
            var agxSphere = Add<AGXUnity.Collide.Sphere>( child, undo );
            child.transform.localPosition = sphere.center;
            agxSphere.Radius = sphere.radius * Mathf.Max( scale.x, Mathf.Max( scale.y, scale.z ) );
            target = agxSphere;
            break;
          case CapsuleCollider capsule:
            CapsuleDimensions( capsule, out var radius, out var height, out var rotation );
            child.transform.localPosition = capsule.center;
            child.transform.localRotation = rotation;
            if ( height < Shape.MinimumSize ) {
              var capsSphere = Add<AGXUnity.Collide.Sphere>( child, undo );
              capsSphere.Radius = radius;
              target = capsSphere;
            }
            else {
              var agxCapsule = Add<AGXUnity.Collide.Capsule>( child, undo );
              agxCapsule.Radius = radius;
              agxCapsule.Height = height;
              target = agxCapsule;
            }
            break;
          case MeshCollider mesh:
            var agxMesh = Add<AGXUnity.Collide.Mesh>( child, undo );
            if ( !agxMesh.AddSourceObject( mesh.sharedMesh ) )
              throw new InvalidOperationException( "AGX rejected source mesh " + mesh.name );
            target = agxMesh;
            break;
          default:
            throw new InvalidOperationException( "Unsupported collider " + source.GetType().Name );
        }
      }
      target.IsSensor = source.isTrigger;
      target.enabled = source.enabled;
      var owner = ColliderOwner( source );
      target.CollisionsEnabled = source.enabled && ( owner == null || owner.detectCollisions );
      Finish( target, undo );
    }

    private static void Finish( Component target, bool undo )
    {
      EditorUtility.SetDirty( target );
      if ( undo && PrefabUtility.IsPartOfPrefabInstance( target ) )
        PrefabUtility.RecordPrefabInstancePropertyModifications( target );
    }
  }
}
