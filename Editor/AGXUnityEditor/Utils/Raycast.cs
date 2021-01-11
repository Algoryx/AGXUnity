using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Utils;

using Mesh = UnityEngine.Mesh;

// TODO:
//     - Patch runtime pick handler.

namespace AGXUnityEditor.Utils
{
  public static class Raycast
  {
    /// <summary>
    /// Raycast result triangle vertices and normal given
    /// in world coordinate system.
    /// </summary>
    public struct Triangle
    {
      /// <summary>
      /// First vertex of the triangle given in world coordinate system.
      /// </summary>
      public Vector3 Vertex1;

      /// <summary>
      /// Second vertex of the triangle given in world coordinate system.
      /// </summary>
      public Vector3 Vertex2;

      /// <summary>
      /// Third vertex of the triangle given in world coordinate system.
      /// </summary>
      public Vector3 Vertex3;

      /// <summary>
      /// Triangle normal given in world coordinate system.
      /// </summary>
      public Vector3 Normal;
    }

    /// <summary>
    /// Editor raycast result which contains valid data when Hit == true.
    /// </summary>
    public struct Result
    {
      /// <summary>
      /// True if the ray intersects object - otherwise false.
      /// </summary>
      public bool Hit;

      /// <summary>
      /// Ray intersection point given in world coordinate system.
      /// </summary>
      public Vector3 Point;

      /// <summary>
      /// Distance from ray origin to ray intersection point.
      /// </summary>
      public float Distance;

      /// <summary>
      /// Triangle data of the ray intersection.
      /// </summary>
      public Triangle Triangle;

      /// <summary>
      /// Closest triangle edge to the ray.
      /// </summary>
      public Edge ClosestEdge;

      /// <summary>
      /// Game object the ray is intersecting.
      /// </summary>
      public GameObject Target;

      /// <summary>
      /// Invalid or default construct of raycast result.
      /// </summary>
      public static readonly Result Invalid = new Result()
      {
        Hit      = false,
        Distance = float.PositiveInfinity
      };

      /// <summary>
      /// Instance evaluates to true when Hit == true, e.g.,
      /// var result = Raycast.Intersect( ray, @object );
      /// if ( result )
      ///   DoSomething();
      /// </summary>
      public static implicit operator bool( Result result )
      {
        return result.Hit;
      }
    }

    /// <summary>
    /// Calculates ray intersection of given mesh with arbitrary transform (local-to-world).
    /// The target is assumed to relate to the given mesh and is propagated to the result
    /// if the ray intersects the mesh.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="mesh">Mesh instance.</param>
    /// <param name="localToWorld">Mesh vertex transform.</param>
    /// <param name="target">Target object (mesh subject), assigned to the result if the
    ///                      ray intersects the mesh.</param>
    /// <returns>The result of the intersection test.</returns>
    public static Result Intersect( Ray ray, Mesh mesh, Matrix4x4 localToWorld, GameObject target )
    {
      if ( mesh == null )
        return Result.Invalid;

      m_args[ 0 ] = ray;
      m_args[ 1 ] = mesh;
      m_args[ 2 ] = localToWorld;
      m_args[ 3 ] = null;

      var hit = (bool)IntersectMethod.Invoke( null, m_args );
      var raycastHit = (RaycastHit)m_args[ 3 ];

      if ( hit ) {
        var vertices  = mesh.vertices;
        var triangles = mesh.triangles;
        var result    = new Result()
        {
          Hit      = true,
          Point    = raycastHit.point,
          Distance = raycastHit.distance,
          Triangle = new Triangle()
          {
            Vertex1 = localToWorld.MultiplyPoint( vertices[ triangles[ 3 * raycastHit.triangleIndex + 0 ] ] ),
            Vertex2 = localToWorld.MultiplyPoint( vertices[ triangles[ 3 * raycastHit.triangleIndex + 1 ] ] ),
            Vertex3 = localToWorld.MultiplyPoint( vertices[ triangles[ 3 * raycastHit.triangleIndex + 2 ] ] ),
            Normal  = raycastHit.normal
          },
          Target   = target
        };

        FindClosestEdge( ray, ref result );

        return result;
      }

      return Result.Invalid;
    }

    /// <summary>
    /// Calculates ray intersection of given mesh filters shared meshes and their transforms.
    /// The result closest to the ray origin is returned in case of several valid intersections.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="meshes">Mesh filters.</param>
    /// <returns>The result closest to the ray origin in case of intersections.</returns>
    public static Result Intersect( Ray ray, MeshFilter[] meshes )
    {
      var bestResult = Result.Invalid;
      for ( int i = 0; i < meshes.Length; ++i ) {
        var result = Intersect( ray, meshes[ i ].sharedMesh, meshes[ i ].transform.localToWorldMatrix, meshes[ i ].gameObject );
        if ( result && result.Distance < bestResult.Distance )
          bestResult = result;
      }

      return bestResult;
    }

    /// <summary>
    /// Calculates ray intersection of given shape, assuming no visual representation of
    /// the shape is present.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="shape">Shape instance.</param>
    /// <returns>The result of the intersection test.</returns>
    public static Result Intersect( Ray ray, Shape shape )
    {
      var parentUnscale = Vector3.one;
      if ( shape != null )
        parentUnscale = new Vector3( 1.0f / shape.transform.lossyScale.x,
                                     1.0f / shape.transform.lossyScale.y,
                                     1.0f / shape.transform.lossyScale.z );

      if ( shape is AGXUnity.Collide.Mesh ) {
        var bestResult = Result.Invalid;
        foreach ( var mesh in ( shape as AGXUnity.Collide.Mesh ).SourceObjects ) {
          var result = Intersect( ray, mesh, shape.transform.localToWorldMatrix, shape.gameObject );
          if ( result && result.Distance < bestResult.Distance )
            bestResult = result;
        }
        return bestResult;
      }
      else if ( shape is HeightField ) {
        // Is this possible?
      }
      else if ( shape is Capsule ) {
        var radius      = ( shape as Capsule ).Radius;
        var height      = ( shape as Capsule ).Height;
        var capsule     = GetOrCreatePrimitive( shape );
        var sphereUpper = capsule.transform.GetChild( 0 ).GetComponent<MeshFilter>();
        var cylinder    = capsule.transform.GetChild( 1 ).GetComponent<MeshFilter>();
        var sphereLower = capsule.transform.GetChild( 2 ).GetComponent<MeshFilter>();

        var results = new Result[]
        {
          Intersect( ray,
                     sphereUpper.sharedMesh,
                     shape.transform.localToWorldMatrix *
                       Matrix4x4.Translate( Vector3.Scale( 0.5f * height * Vector3.up, parentUnscale ) ) *
                       Matrix4x4.Scale( Vector3.Scale( 2.0f * radius * Vector3.one, parentUnscale ) ),
                     shape.gameObject ),
          Intersect( ray,
                     cylinder.sharedMesh,
                     shape.transform.localToWorldMatrix *
                       Matrix4x4.Scale( Vector3.Scale( new Vector3( 2.0f * radius, height, 2.0f * radius ), parentUnscale ) ),
                     shape.gameObject ),
          Intersect( ray,
                     sphereLower.sharedMesh,
                     shape.transform.localToWorldMatrix *
                       Matrix4x4.Translate( Vector3.Scale( 0.5f * height * Vector3.down, parentUnscale ) ) *
                       Matrix4x4.Scale( Vector3.Scale( 2.0f * radius * Vector3.one, parentUnscale ) ) *
                       Matrix4x4.Rotate( sphereLower.transform.localRotation ),
                     shape.gameObject )
        };

        var bestResult = Result.Invalid;
        foreach ( var result in results )
          if ( result && result.Distance < bestResult.Distance )
            bestResult = result;

        return bestResult;
      }
      else if ( shape != null ) {
        var primitive = GetOrCreatePrimitive( shape );
        var filters   = primitive != null ?
                          primitive.GetComponentsInChildren<MeshFilter>() :
                          shape.GetComponentsInChildren<MeshFilter>();
        var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
        foreach ( var filter in filters ) {
          // If we use 'primitive' it's by definition unit size so
          // we scale it given shape scale/size. For non-primitive
          // we try any child mesh filter.
          var localToWorld = primitive != null ?
                               shape.transform.localToWorldMatrix *
                                 filter.transform.localToWorldMatrix *
                                 Matrix4x4.Scale( Vector3.Scale( shape.GetScale(), parentUnscale ) ) :
                               filter.transform.localToWorldMatrix;
          var result = Intersect( ray,
                                  filter.sharedMesh,
                                  localToWorld,
                                  shape.gameObject );
          if ( result && result.Distance < bestResult.Distance )
            bestResult = result;
        }

        return bestResult;
      }

      return new Result() { Hit = false };
    }

    /// <summary>
    /// Calculates ray intersection of given shapes.
    /// The result closest to the ray origin is returned in case of several valid intersections.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="shapes">Shapes to test.</param>
    /// <returns>The result closest to the ray origin in case of intersections.</returns>
    public static Result Intersect( Ray ray, Shape[] shapes )
    {
      var bestResult = Result.Invalid;
      foreach ( var shape in shapes ) {
        var result = Intersect( ray, shape );
        if ( result && result.Distance < bestResult.Distance )
          bestResult = result;
      }

      return bestResult;
    }

    /// <summary>
    /// Calculates ray intersection of given game object. If the game object has
    /// a shape, the raycast will only test for shapes. If the game object doesn't
    /// have a shape, the raycast will only test for mesh filters.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="target">Target game object.</param>
    /// <param name="includeChildren">Default false, if true, all children of <paramref name="target"/> will
    ///                               be included in the test and the best hit will be returned.</param>
    /// <returns>The result closest to the ray origin in case of intersections.</returns>
    public static Result Intersect( Ray ray, GameObject target, bool includeChildren = false )
    {
      if ( target == null )
        return Result.Invalid;

      var rb = target.GetComponent<RigidBody>();
      var hasShape = rb != null && rb.Shapes.Length > 0;
      if ( includeChildren ) {
        return hasShape ?
                 Intersect( ray, rb.Shapes ) :
                 Intersect( ray, target.GetComponentsInChildren<MeshFilter>() );
      }
      else {
        var filter = target.GetComponent<MeshFilter>();
        var shape = target.GetComponent<Shape>();
        return shape != null ?
                 Intersect( ray, shape ) :
               filter != null ?
                 Intersect( ray, filter.sharedMesh, target.transform.localToWorldMatrix, filter.gameObject ) :
                 Result.Invalid;
      }
    }

    /// <summary>
    /// Calculates ray intersections of children (only) to parent game object.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="parent">Parent game object.</param>
    /// <param name="predicate">Optional predicate to filter <paramref name="parent"/> children.</param>
    /// <param name="includeParent">Include parent in the intersection tests - similar to GetComponentsInChildren - default false.</param>
    /// <returns>Array of valid results, closest hit first.</returns>
    public static Result[] IntersectChildren( Ray ray,
                                              GameObject parent,
                                              Predicate<GameObject> predicate = null,
                                              bool includeParent = false )
    {
      var results = new List<Result>();
      if ( parent == null )
        return results.ToArray();

      Action<GameObject> doTest = go =>
      {
        if ( predicate == null || predicate( go ) ) {
          var result = Intersect( ray, go );
          if ( result )
            results.Add( result );
        }
      };

      if ( includeParent )
        doTest( parent );

      parent.TraverseChildren( doTest );

      results.Sort( ( r1, r2 ) => { return r1.Distance < r2.Distance ? -1 : 1; } );

      return results.ToArray();
    }

    private static object[] m_args = new object[] { null, null, null, null };
    private static MethodInfo m_intersectMethod = null;

    /// <summary>
    /// Reflected intersect method:
    ///     bool HandleUtility.IntersectRayMesh( UnityEngine.Ray,
    ///                                          UnityEngine.Mesh,
    ///                                          UnityEngine.Matrix4x4,
    ///                                          out UnityEngine.RaycastResult )
    /// </summary>
    private static MethodInfo IntersectMethod
    {
      get
      {
        if ( m_intersectMethod == null ) {
          m_intersectMethod = ( from type
                                in typeof( Editor ).Assembly.GetTypes()
                                where type.Name == "HandleUtility"
                                select type.GetMethod( "IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic ) ).FirstOrDefault();
        }

        return m_intersectMethod;
      }
    }

    private static Dictionary<Type, GameObject> m_shapePrimitiveCache = new Dictionary<Type, GameObject>();

    /// <summary>
    /// Cached unit shape resources.
    /// </summary>
    /// <param name="shape">Shape instance.</param>
    /// <returns>Unit primitive with mesh (not an instance).</returns>
    private static GameObject GetOrCreatePrimitive( Shape shape )
    {
      GameObject go = null;
      if ( m_shapePrimitiveCache.TryGetValue( shape.GetType(), out go ) )
        return go;

      go = Resources.Load<GameObject>( AGXUnity.Rendering.DebugRenderData.GetPrefabName( shape.GetType().Name ) );
      if ( go != null )
        m_shapePrimitiveCache.Add( shape.GetType(), go );

      return go;
    }

    /// <summary>
    /// Finds closest triangle edge given ray and valid raycast result. The
    /// Result.ClosestEdge field will be updated.
    /// </summary>
    /// <param name="ray">Ray in world coordinate system.</param>
    /// <param name="result">Reference to valid result.</param>
    private static void FindClosestEdge( Ray ray, ref Result result )
    {
      var rayStart = ray.origin;
      var rayEnd   = ray.GetPoint( 5000.0f );
      var d1 = ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
                                                          rayEnd,
                                                          result.Triangle.Vertex1,
                                                          result.Triangle.Vertex2 );
      var d2 = ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
                                                          rayEnd,
                                                          result.Triangle.Vertex2,
                                                          result.Triangle.Vertex3);
      var d3 = ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
                                                          rayEnd,
                                                          result.Triangle.Vertex3,
                                                          result.Triangle.Vertex1 );
      result.ClosestEdge = d1.Distance < d2.Distance && d1.Distance < d3.Distance ? new Edge()
                                                                                    {
                                                                                      Start  = result.Triangle.Vertex1,
                                                                                      End    = result.Triangle.Vertex2,
                                                                                      Normal = result.Triangle.Normal,
                                                                                      Type   = Edge.EdgeType.Triangle
                                                                                    } :
                           d2.Distance < d1.Distance && d2.Distance < d3.Distance ? new Edge()
                                                                                    {
                                                                                      Start  = result.Triangle.Vertex2,
                                                                                      End    = result.Triangle.Vertex3,
                                                                                      Normal = result.Triangle.Normal,
                                                                                      Type   = Edge.EdgeType.Triangle
                                                                                    } :
                                                                                    new Edge()
                                                                                    {
                                                                                      Start  = result.Triangle.Vertex3,
                                                                                      End    = result.Triangle.Vertex1,
                                                                                      Normal = result.Triangle.Normal,
                                                                                      Type   = Edge.EdgeType.Triangle
                                                                                    };
    }
  }
}
