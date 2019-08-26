using UnityEngine;

namespace AGXUnity.Utils
{
  public static class MeshUtils
  {
    /// <summary>
    /// Intersection test triangle vs ray with resulting point in the triangle and time t along the ray.
    /// </summary>
    /// <param name="ray">Ray in same coordinate system as the vertices.</param>
    /// <param name="rayLength">Length of the ray.</param>
    /// <param name="v1">First vertex of the triangle.</param>
    /// <param name="v2">Second vertex of the triangle.</param>
    /// <param name="v3">Third vertex of the triangle.</param>
    /// <param name="normal">Normal of the triangle.</param>
    /// <param name="result">If hit, resulting point inside the triangle.</param>
    /// <param name="t">Time along the ray (0, 1).</param>
    /// <returns>True if the ray intersects the triangle.</returns>
    //public static bool IntersectRayTriangle( Ray ray, float rayLength, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, ref Vector3 result, ref float t )
    //{
    //  float epsilon    = 1.0E-12f;
    //  Vector3 lineP1   = ray.GetPoint( 0 );
    //  Vector3 lineP2   = ray.GetPoint( rayLength );
    //  Vector3 lineP1P2 = lineP2 - lineP1;

    //  float d = -Vector3.Dot( lineP1P2, normal );
    //  // Parallel or back face.
    //  if ( d < epsilon )
    //    return false;

    //  Vector3 triangleP1LineP1 = lineP1 - v1;
    //  t = Vector3.Dot( triangleP1LineP1, normal );

    //  if ( t < epsilon )
    //    return false;
    //  if ( t > d - epsilon )
    //    return false;

    //  t /= d;

    //  result = lineP1 + lineP1P2 * t;

    //  return ShapeUtils.IsPointInTriangle( result, v1, v2, v3, epsilon );
    //}

    //public class TriangleTestResult
    //{
    //  public int     TriangleIndex;
    //  public float   Time;
    //  public Vector3 PointInTriangle;
    //  public Vector3 Normal;
    //  public bool    Hit;

    //  public TriangleTestResult()
    //  {
    //    TriangleIndex   = int.MaxValue;
    //    Time            = float.MaxValue;
    //    PointInTriangle = Vector3.zero;
    //    Normal          = Vector3.zero;
    //    Hit             = false;
    //  }
    //}

    ///// <summary>
    ///// Test single triangle given ray in same coordinate system as the vertices and normal.
    ///// </summary>
    ///// <param name="ray">Ray in same coordinate system as the vertices and normal.</param>
    ///// <param name="rayLength">Length of the ray.</param>
    ///// <param name="triangleIndex">Triangle index of current triangle.</param>
    ///// <param name="v1">First vertex.</param>
    ///// <param name="v2">Second vertex.</param>
    ///// <param name="v3">Third vertex.</param>
    ///// <param name="normal">Normal of the triangle.</param>
    ///// <returns>Local result of test.</returns>
    //public static TriangleTestResult TestTriangle( Ray ray, float rayLength, int triangleIndex, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal )
    //{
    //  TriangleTestResult result = new TriangleTestResult() { Normal = normal, TriangleIndex = triangleIndex };
    //  result.Hit = IntersectRayTriangle( ray, rayLength, v1, v2, v3, normal, ref result.PointInTriangle, ref result.Time );
    //  return result;
    //}

    ///// <summary>
    ///// Test all triangles in a mesh to find the first triangle that intersects the ray.
    ///// </summary>
    ///// <param name="worldRay">Ray given in world coordinates (e.g., HandleUtility.GUIPointToWorldRay( Event.current.mousePosition )).</param>
    ///// <param name="rayLength">Length of the ray.</param>
    ///// <param name="mesh">The mesh.</param>
    ///// <param name="parent">Parent game object that transforms the mesh.</param>
    ///// <returns>Result of the test.</returns>
    //public static Raycast.TriangleHit TestAllTriangles( Ray worldRay, float rayLength, UnityEngine.Mesh mesh, GameObject parent )
    //{
    //  if ( mesh == null || parent == null )
    //    return Raycast.TriangleHit.Invalid;

    //  // Mesh bounds are in local coordinates - transform ray to local.
    //  Ray localRay = new Ray( parent.transform.InverseTransformPoint( worldRay.origin ), parent.transform.InverseTransformVector( worldRay.direction ).normalized );
    //  if ( !mesh.bounds.IntersectRay( localRay ) )
    //    return Raycast.TriangleHit.Invalid;

    //  int[] triangles               = mesh.triangles;
    //  Vector3[] vertices            = mesh.vertices;
    //  TriangleTestResult testResult = new TriangleTestResult();
    //  for ( int i = 0; i < triangles.Length; i += 3 ) {
    //    Vector3 v1              = vertices[ triangles[ i + 0 ] ];
    //    Vector3 v2              = vertices[ triangles[ i + 1 ] ];
    //    Vector3 v3              = vertices[ triangles[ i + 2 ] ];
    //    Vector3 normal          = Vector3.Cross( v2 - v1, v3 - v1 ).normalized;
    //    TriangleTestResult test = TestTriangle( localRay, rayLength, i, v1, v2, v3, normal );
    //    if ( test.Hit && test.Time < testResult.Time )
    //      testResult = test;
    //  }

    //  if ( !testResult.Hit )
    //    return Raycast.TriangleHit.Invalid;

    //  Vector3 worldIntersectionPoint = parent.transform.TransformPoint( testResult.PointInTriangle );
    //  return new Raycast.TriangleHit()
    //  {
    //    Target   = parent,
    //    Vertices = new Vector3[]
    //    {
    //      parent.transform.TransformPoint( vertices[ triangles[ testResult.TriangleIndex + 0 ] ] ),
    //      parent.transform.TransformPoint( vertices[ triangles[ testResult.TriangleIndex + 1 ] ] ),
    //      parent.transform.TransformPoint( vertices[ triangles[ testResult.TriangleIndex + 2 ] ] )
    //    },
    //    Point    = worldIntersectionPoint,
    //    Normal   = parent.transform.TransformDirection( testResult.Normal ),
    //    Distance = Vector3.Distance( worldRay.GetPoint( 0 ), worldIntersectionPoint )
    //  };
    //}

    ///// <summary>
    ///// Finds closest triangle to ray start.
    ///// </summary>
    ///// <param name="mesh">The mesh.</param>
    ///// <param name="parent">Parent game object that transforms the mesh.</param>
    ///// <param name="worldRay">Ray in world coordinate frame.</param>
    ///// <param name="rayLength">Length of the ray.</param>
    ///// <returns>Data with result, result.Valid == true if the ray intersects a triangle.</returns>
    //public static Raycast.TriangleHit FindClosestTriangle( Mesh mesh, GameObject parent, Ray worldRay, float rayLength = 500.0f )
    //{
    //  return TestAllTriangles( worldRay, rayLength, mesh, parent );
    //}

    ///// <summary>
    ///// Finds closest triangle to ray start given array of meshes.
    ///// </summary>
    ///// <param name="meshes">The meshes.</param>
    ///// <param name="parent">Parent game object that transforms the mesh.</param>
    ///// <param name="worldRay">Ray in world coordinate frame.</param>
    ///// <param name="rayLength">Length of the ray.</param>
    ///// <returns>Data with result, result.Valid == true if the ray intersects a triangle.</returns>
    //public static Raycast.TriangleHit FindClosestTriangle( Mesh[] meshes, GameObject parent, Ray worldRay, float rayLength = 500.0f )
    //{
    //  if ( meshes.Length == 0 )
    //    return Raycast.TriangleHit.Invalid;

    //  Raycast.TriangleHit[] results = new Raycast.TriangleHit[ meshes.Length ];
    //  for ( int i = 0; i < meshes.Length; ++i )
    //    results[ i ] = FindClosestTriangle( meshes[ i ], parent, worldRay, rayLength );

    //  return FindBestResult( results );
    //}

    ///// <summary>
    ///// Finds closest triangle to ray start in game object with one or many MeshFilters.
    ///// </summary>
    ///// <param name="parentGameObject">Object with render data (MeshFilters).</param>
    ///// <param name="worldRay">Ray given in world coordinate system.</param>
    ///// <param name="rayLength">Length of the ray.</param>
    ///// <returns>Data with result, result.Valid == true if the ray intersects a triangle.</returns>
    //public static Raycast.TriangleHit FindClosestTriangle( GameObject parentGameObject, UnityEngine.Ray worldRay, float rayLength = 500.0f )
    //{
    //  if ( parentGameObject == null )
    //    return Raycast.TriangleHit.Invalid;

    //  MeshFilter[] meshFilters = parentGameObject.GetComponentsInChildren<UnityEngine.MeshFilter>();
    //  if ( meshFilters.Length == 0 )
    //    return Raycast.TriangleHit.Invalid;

    //  Raycast.TriangleHit[] results = new Raycast.TriangleHit[ meshFilters.Length ];
    //  for ( int i = 0; i < meshFilters.Length; ++i )
    //    results[ i ] = FindClosestTriangle( meshFilters[ i ].sharedMesh, meshFilters[ i ].gameObject, worldRay, rayLength );

    //  return FindBestResult( results );
    //}

    //public static Raycast.TriangleHit FindBestResult( Raycast.TriangleHit[] results )
    //{
    //  if ( results.Length == 0 )
    //    return Raycast.TriangleHit.Invalid;

    //  Raycast.TriangleHit best = results[ 0 ];
    //  for ( int i = 1; i < results.Length; ++i )
    //    if ( results[ i ].Distance < best.Distance )
    //      best = results[ i ];

    //  return best;
    //}
  }
}
