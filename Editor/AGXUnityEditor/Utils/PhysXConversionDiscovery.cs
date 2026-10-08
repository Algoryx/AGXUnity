using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Shape = AGXUnity.Collide.Shape;

namespace AGXUnityEditor.Utils
{
  public static partial class PhysXConversion
  {
    public static GameObject[] SceneRoots()
    {
      var roots = new List<GameObject>();
      for ( int i = 0; i < SceneManager.sceneCount; ++i ) {
        var scene = SceneManager.GetSceneAt( i );
        if ( scene.isLoaded && !EditorSceneManager.IsPreviewScene( scene ) )
          roots.AddRange( scene.GetRootGameObjects() );
      }
      return roots.ToArray();
    }

    public static List<Candidate> Discover( IEnumerable<GameObject> roots )
    {
      var objects = roots.Where( r => r != null ).ToArray();
      var colliders = objects.SelectMany( r => r.GetComponentsInChildren<Collider>( true ) ).Distinct().ToArray();
      var collidersByBody = colliders.ToLookup( ColliderOwner );
      var bodies = objects.SelectMany( r => r.GetComponentsInChildren<Rigidbody>( true ) ).Distinct().ToArray();
      var joints = objects.SelectMany( r => r.GetComponentsInChildren<Joint>( true ) ).Distinct().ToArray();
      var jointBodies = new HashSet<Rigidbody>( joints.SelectMany( j => new[] { j.GetComponent<Rigidbody>(), j.connectedBody } )
                                                     .Where( b => b != null ) );
      var pagedTerrains = new HashSet<Terrain>( objects.SelectMany( r => r.GetComponentsInChildren<AGXUnity.Model.DeformableTerrainPager>( true ) )
                                                      .SelectMany( p => AGXUnity.Utils.TerrainUtils.CollectTerrains( p.Terrain ) ) );
      var candidates = new List<Candidate>();
      foreach ( var body in bodies ) {
        var candidate = new Candidate {
          Source = body,
          Colliders = collidersByBody[ body ].ToArray()
        };
        ValidateBody( body, jointBodies, candidate );
        foreach ( var collider in candidate.Colliders )
          ValidateCollider( collider, candidate, pagedTerrains );
        candidates.Add( candidate );
      }
      foreach ( var collider in collidersByBody[ null ] ) {
        var candidate = new Candidate { Source = collider };
        ValidateCollider( collider, candidate, pagedTerrains );
        candidates.Add( candidate );
      }
      foreach ( var joint in joints ) {
        var candidate = new Candidate { Source = joint };
        candidate.Errors.Add( "Joint conversion is not supported. Both connected rigidbodies must remain PhysX." );
        candidates.Add( candidate );
      }
      // Propagate blockers across nested body groups before advertising eligibility.
      foreach ( var group in candidates.Where( c => c.Source is Rigidbody ).GroupBy( c => BodyRoot( (Rigidbody)c.Source ) ) )
        if ( group.Any( c => !c.Supported ) )
          foreach ( var candidate in group.Where( c => c.Supported ) )
            candidate.Errors.Add( "Another rigidbody in this nested hierarchy has unsupported dependencies." );
      return candidates.OrderBy( c => c.Source.gameObject.name ).ThenBy( c => c.Description ).ToList();
    }

    public static List<PrefabInfo> DiscoverPrefabs()
    {
      var result = new List<PrefabInfo>();
      foreach ( var path in AssetDatabase.FindAssets( "t:Prefab", new[] { "Assets" } ).Select( AssetDatabase.GUIDToAssetPath ) ) {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( root == null )
          continue;
        var candidates = Discover( new[] { root } );
        if ( candidates.Count == 0 )
          continue;
        result.Add( new PrefabInfo {
          Path = path,
          Name = root.name,
          SupportedCount = candidates.Where( c => c.Supported ).Sum( c => c.ComponentCount ),
          UnsupportedCount = candidates.Where( c => !c.Supported ).Sum( c => c.ComponentCount ),
          Error = PrefabUtility.IsPartOfImmutablePrefab( root ) ? "Imported or immutable prefab." : null
        } );
      }
      return result.OrderBy( p => p.Name ).ToList();
    }

    public static Plan BuildPlan( IEnumerable<Candidate> available, IEnumerable<Component> selected )
    {
      var plan = new Plan();
      var candidates = available.ToArray();
      var sources = new HashSet<Component>( selected );
      // Nested bodies share an articulated root. Include the complete body hierarchy,
      // so a partially converted parent cannot claim another PhysX body's shapes.
      foreach ( var top in sources.OfType<Rigidbody>().Where( b => b != null ).Select( BodyRoot ).Distinct().ToArray() ) {
        foreach ( var related in top.GetComponentsInChildren<Rigidbody>( true ) )
          sources.Add( related );
      }
      foreach ( var source in sources ) {
        var candidate = candidates.FirstOrDefault( c => c.Source == source && source != null );
        if ( candidate == null ) {
          plan.Errors.Add( "A selected source is missing or outside the conversion scope. Refresh the view." );
          continue;
        }
        plan.Candidates.Add( candidate );
        var label = candidate.Source.gameObject.name + ": ";
        plan.Errors.AddRange( candidate.Errors.Select( e => label + e ) );
        plan.Warnings.AddRange( candidate.Warnings.Select( w => label + w ) );
      }
      return plan;
    }

    private static Rigidbody BodyRoot( Rigidbody body )
    {
      var top = body;
      for ( var parent = body.transform.parent; parent != null; parent = parent.parent ) {
        var parentBody = parent.GetComponent<Rigidbody>();
        if ( parentBody != null )
          top = parentBody;
      }
      return top;
    }

    private static void ValidateBody( Rigidbody body, HashSet<Rigidbody> jointBodies, Candidate candidate )
    {
      if ( body.GetComponent<AGXUnity.RigidBody>() != null )
        candidate.Errors.Add( "An AGX rigidbody already exists on this object." );
      if ( jointBodies.Contains( body ) )
        candidate.Errors.Add( "A joint references this rigidbody; joint conversion is not supported." );
      if ( !body.isKinematic && !body.useGravity )
        candidate.Errors.Add( "Per-body gravity disabling has no supported AGX mapping." );
      if ( body.constraints != RigidbodyConstraints.None )
        candidate.Errors.Add( "Frozen rigidbody axes require AGX constraints and are not supported yet." );
      if ( body.interpolation != RigidbodyInterpolation.None )
        candidate.Warnings.Add( "PhysX interpolation is not transferred." );
      if ( body.collisionDetectionMode != CollisionDetectionMode.Discrete )
        candidate.Warnings.Add( "PhysX collision detection mode is not transferred." );
    }

    private static Rigidbody ColliderOwner( Collider collider )
    {
      if ( collider.attachedRigidbody != null )
        return collider.attachedRigidbody;
      // Inactive objects and persistent prefab assets may not have a PhysX
      // attachment. The nearest body in the hierarchy still owns the collider.
      for ( var transform = collider.transform; transform != null; transform = transform.parent ) {
        var body = transform.GetComponent<Rigidbody>();
        if ( body != null )
          return body;
      }
      return null;
    }

    private static void ValidateCollider( Collider collider, Candidate candidate, HashSet<Terrain> pagedTerrains )
    {
      var label = collider.GetType().Name + ": ";
      if ( !( collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
              collider is MeshCollider || collider is TerrainCollider ) ) {
        candidate.Errors.Add( label + "Conversion is not supported." );
        return;
      }
      var scale = Abs( collider.transform.lossyScale );
      if ( Mathf.Min( scale.x, Mathf.Min( scale.y, scale.z ) ) < Shape.MinimumSize )
        candidate.Errors.Add( label + "Zero or near-zero transform scale." );
      if ( !( collider is MeshCollider ) && HasShear( collider.transform.localToWorldMatrix ) )
        candidate.Errors.Add( label + "Sheared primitive transforms cannot be represented exactly." );
      if ( collider.sharedMaterial != null )
        candidate.Warnings.Add( label + "Physics material friction, restitution and combine modes are not transferred." );
      if ( collider is MeshCollider mesh ) {
        if ( mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0 )
          candidate.Errors.Add( label + "Missing or empty source mesh." );
        else if ( !mesh.sharedMesh.isReadable )
          candidate.Errors.Add( label + "Enable Read/Write on the source mesh before converting." );
        else if ( !EditorUtility.IsPersistent( mesh.sharedMesh ) )
          candidate.Errors.Add( label + "Save the source mesh as an asset before converting." );
        else if ( mesh.sharedMesh.triangles.Length == 0 )
          candidate.Errors.Add( label + "The source mesh has no triangles." );
        if ( mesh.convex )
          candidate.Errors.Add( label + "Convex mesh conversion requires precomputed AGX collision meshes and is not supported yet." );
      }
      if ( collider is TerrainCollider terrainCollider ) {
        var terrain = collider.GetComponent<Terrain>();
        if ( terrain == null || terrain.terrainData == null || terrain.terrainData != terrainCollider.terrainData )
          candidate.Errors.Add( label + "A Terrain with matching TerrainData is required on this object." );
        if ( ColliderOwner( collider ) != null )
          candidate.Errors.Add( label + "Only static terrain conversion is supported." );
        if ( collider.GetComponent<AGXUnity.Model.DeformableTerrainBase>() != null ||
             collider.GetComponent<AGXUnity.Collide.HeightField>() != null || pagedTerrains.Contains( terrain ) )
          candidate.Errors.Add( label + "AGX terrain collision already exists on this object." );
        if ( Quaternion.Angle( collider.transform.rotation, Quaternion.identity ) > 0.001f ||
             ( collider.transform.lossyScale - Vector3.one ).sqrMagnitude > 1.0E-8f )
          candidate.Errors.Add( label + "Deformable terrain requires unrotated, unit-scale terrain." );
        if ( collider.isTrigger )
          candidate.Errors.Add( label + "Deformable terrain does not support trigger conversion." );
        candidate.Warnings.Add( label + "Conversion creates DeformableTerrain; configure its soil and depth properties before simulation." );
        if ( terrainCollider.terrainData != null ) {
          var data = terrainCollider.terrainData;
          var holes = data.GetHoles( 0, 0, data.holesResolution, data.holesResolution );
          if ( holes.Cast<bool>().Any( solid => !solid ) )
            candidate.Errors.Add( label + "Terrain holes are not supported by deformable terrain." );
        }
      }
    }

  }
}
