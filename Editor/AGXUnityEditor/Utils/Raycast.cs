using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;

using Mesh = UnityEngine.Mesh;

// TODO:
//     * Handle shapes.
//     * Principal axes (not here, but EdgeDetectionTool).

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

    public static Result Intersect( Ray ray, Mesh mesh, Matrix4x4 localToWorld )
    {
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
          Mesh     = mesh
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
        var result = Intersect( ray, meshes[ i ].sharedMesh, meshes[ i ].transform.localToWorldMatrix );
        if ( result && result.Distance < bestResult.Distance )
          bestResult = result;
      }

      return bestResult;
    }

    public static Result Intersect( Ray ray, Shape shape )
    {
      if ( shape is AGXUnity.Collide.Mesh ) {
        var bestResult = new Result() { Hit = false, Distance = float.PositiveInfinity };
        foreach ( var mesh in ( shape as AGXUnity.Collide.Mesh ).SourceObjects ) {
          var result = Intersect( ray, mesh, shape.transform.localToWorldMatrix );
          if ( result && result.Distance < bestResult.Distance )
            bestResult = result;
        }
        return bestResult;
      }
      else if ( shape is HeightField ) {
        // Is this possible?
        return new Result() { Hit = false };
      }
      else if ( shape != null ) {
        var tmp = Resources.Load<GameObject>( AGXUnity.Rendering.DebugRenderData.GetPrefabName( shape.GetType().Name ) );
      }

      return new Result() { Hit = false };
    }

    public static Result Intersect( Ray ray, GameObject target, bool includeChildren = false )
    {
      if ( target == null )
        return new Result() { Hit = false };

      if ( includeChildren )
        return Intersect( ray, target.GetComponentsInChildren<MeshFilter>() );

      var filter = target.GetComponent<MeshFilter>();
      if ( filter == null )
        return new Result() { Hit = false };

      return Intersect( ray, filter.sharedMesh, filter.transform.localToWorldMatrix );
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

    private static void FindClosestEdge( Ray ray, ref Result result )
    {
      var rayStart = ray.GetPoint( 0 );
      var rayEnd   = ray.GetPoint( 5000.0f );
      var d1 = AGXUnity.Utils.ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
                                                                         rayEnd,
                                                                         result.Triangle.Vertex1,
                                                                         result.Triangle.Vertex2 );
      var d2 = AGXUnity.Utils.ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
                                                                         rayEnd,
                                                                         result.Triangle.Vertex2,
                                                                         result.Triangle.Vertex3);
      var d3 = AGXUnity.Utils.ShapeUtils.ShortestDistanceSegmentSegment( rayStart,
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
