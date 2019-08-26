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
    public struct Triangle
    {
      public Vector3 Vertex1;
      public Vector3 Vertex2;
      public Vector3 Vertex3;
      public Vector3 Normal;
    }

    public struct Result
    {
      public bool Hit;

      public Vector3 Point;

      public float Distance;

      public Triangle Triangle;

      public Edge ClosestEdge;

      public GameObject Target;

      public Mesh Mesh;

      public static readonly Result Invalid = new Result() { Hit = false };

      public static implicit operator bool( Result result )
      {
        return result.Hit;
      }
    }

    public static Result Intersect( Ray ray, Mesh mesh, Matrix4x4 localToWorld, GameObject target )
    {
      if ( mesh == null )
        return new Result() { Hit = false };

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
          Mesh     = mesh,
          Target   = target
        };

        FindClosestEdge( ray, ref result );

        return result;
      }
      else {
        return new Result()
        {
          Hit = false
        };
      }
    }

    public static Result Intersect( Ray ray, MeshFilter[] meshes )
    {
      var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
      for ( int i = 0; i < meshes.Length; ++i ) {
        var result = Intersect( ray, meshes[ i ].sharedMesh, meshes[ i ].transform.localToWorldMatrix, meshes[ i ].gameObject );
        if ( result && result.Distance < bestResult.Distance )
          bestResult = result;
      }

      return bestResult;
    }

    public static Result Intersect( Ray ray, Shape shape )
    {
      var parentUnscale = Vector3.one;
      if ( shape != null && shape.transform.parent != null )
        parentUnscale = new Vector3( 1.0f / shape.transform.parent.lossyScale.x,
                                     1.0f / shape.transform.parent.lossyScale.y,
                                     1.0f / shape.transform.parent.lossyScale.z );

      if ( shape is AGXUnity.Collide.Mesh ) {
        var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
        foreach ( var mesh in ( shape as AGXUnity.Collide.Mesh ).SourceObjects ) {
          var result = Intersect( ray, mesh, shape.transform.localToWorldMatrix * Matrix4x4.Scale( parentUnscale ), shape.gameObject );
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

        var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
        foreach ( var result in results )
          if ( result && result.Distance < bestResult.Distance )
            bestResult = result;

        return bestResult;
      }
      else if ( shape != null ) {
        var primitive = GetOrCreatePrimitive( shape );
        if ( primitive != null ) {
          var filters = primitive.GetComponentsInChildren<MeshFilter>();
          var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
          foreach ( var filter in filters ) {
            var result = Intersect( ray,
                                    filter.sharedMesh,
                                    shape.transform.localToWorldMatrix *
                                      filter.transform.localToWorldMatrix *
                                      Matrix4x4.Scale( Vector3.Scale( shape.GetScale(), parentUnscale ) ),
                                    shape.gameObject );
            if ( result && result.Distance < bestResult.Distance )
              bestResult = result;
          }

          return bestResult;
        }
      }

      return new Result() { Hit = false };
    }

    public static Result Intersect( Ray ray, Shape[] shapes )
    {
      var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
      foreach ( var shape in shapes ) {
        var result = Intersect( ray, shape );
        if ( result && result.Distance < bestResult.Distance )
          bestResult = result;
      }

      return bestResult;
    }

    public static Result Intersect( Ray ray, GameObject target, bool includeChildren = false )
    {
      if ( target == null )
        return new Result() { Hit = false };

      var hasShape = target.GetComponent<Shape>() != null;
      if ( includeChildren ) {
        return hasShape ?
                 Intersect( ray, target.GetComponentsInChildren<Shape>() ) :
                 Intersect( ray, target.GetComponentsInChildren<MeshFilter>() );
      }
      else {
        var filter = target.GetComponent<MeshFilter>();
        return hasShape ?
                 Intersect( ray, target.GetComponent<Shape>() ) :
               filter != null ?
                 Intersect( ray, filter.sharedMesh, target.transform.localToWorldMatrix, filter.gameObject ) :
                 new Result() { Hit = false };
      }
    }

    public static Result[] IntersectChildren( Ray ray, GameObject parent, Predicate<GameObject> predicate = null )
    {
      var results = new List<Result>();
      if ( parent == null )
        return results.ToArray();

      parent.TraverseChildren( go =>
      {
        if ( predicate == null || predicate( go ) ) {
          var result = Intersect( ray, go );
          if ( result )
            results.Add( result );
        }
      } );

      results.Sort( ( r1, r2 ) => { return r1.Distance < r2.Distance ? -1 : 1; } );

      return results.ToArray();
    }

    private static object[] m_args = new object[] { null, null, null, null };
    private static MethodInfo m_intersectMethod = null;

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

    private static void FindClosestEdge( Ray ray, ref Result result )
    {
      var rayStart = ray.GetPoint( 0 );
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
