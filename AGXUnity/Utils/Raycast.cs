using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class Raycast
  {
    public class TriangleHit
    {
      public static TriangleHit Invalid { get { return new TriangleHit() { Target = null, Vertices = new Vector3[ 0 ], Distance = float.PositiveInfinity }; } }

      public bool Valid { get { return Vertices.Length > 0 && Distance != float.PositiveInfinity; } }

      public GameObject Target { get; set; }

      public Vector3[] Vertices { get; set; }

      public MeshUtils.Edge[] Edges
      {
        get
        {
          return new MeshUtils.Edge[]
          {
            new MeshUtils.Edge( Vertices[ 0 ], Vertices[ 1 ], Normal ),
            new MeshUtils.Edge( Vertices[ 1 ], Vertices[ 2 ], Normal ),
            new MeshUtils.Edge( Vertices[ 2 ], Vertices[ 0 ], Normal )
          };
        }
      }

      public MeshUtils.Edge ClosestEdge { get; set; }

      public Vector3 Point { get; set; }

      public Vector3 Normal { get; set; }

      public float Distance { get; set; }
    }

    public class ClosestEdgeHit
    {
      public static ClosestEdgeHit Invalid { get { return new ClosestEdgeHit() { Target = null, Edge = null, Distance = float.PositiveInfinity }; } }

      public bool Valid { get { return Edge != null && Distance != float.PositiveInfinity; } }

      public GameObject Target { get; set; }

      public MeshUtils.Edge Edge { get; set; }

      public float Distance { get; set; }
    }

    public class Hit
    {
      public static Hit Invalid { get { return new Hit() { Triangle = TriangleHit.Invalid, ClosestEdge = ClosestEdgeHit.Invalid }; } }

      public TriangleHit Triangle { get; set; }

      public ClosestEdgeHit ClosestEdge { get; set; }

      public Hit()
      {
        Triangle    = new TriangleHit();
        ClosestEdge = new ClosestEdgeHit();
      }
    }

    public GameObject Target { get; set; }

    public Hit LastHit { get; private set; }

    public Hit Test( Ray ray, float rayLength = 500.0f )
    {
      LastHit = Hit.Invalid;

      if ( Target == null )
        return Hit.Invalid;

      Hit hit = new Hit();

      Collide.Shape shape = Target.GetComponent<Collide.Shape>();
      if ( shape != null ) {
        if ( shape is Collide.Mesh ) {
          hit.Triangle = MeshUtils.FindClosestTriangle( ( shape as Collide.Mesh ).SourceObjects, shape.gameObject, ray, rayLength );
        }
        else if ( shape is Collide.HeightField )
          hit.Triangle = TriangleHit.Invalid;
        else {
          GameObject tmp = PrefabLoader.Instantiate<GameObject>( Rendering.DebugRenderData.GetPrefabName( shape.GetType().Name ) );

          if ( tmp != null ) {
            tmp.hideFlags            = HideFlags.HideAndDontSave;
            tmp.transform.position   = shape.transform.position;
            tmp.transform.rotation   = shape.transform.rotation;
            tmp.transform.localScale = shape.GetScale();

            hit.Triangle        = MeshUtils.FindClosestTriangle( tmp, ray, rayLength );
            hit.Triangle.Target = shape.gameObject;

            GameObject.DestroyImmediate( tmp );
          }
        }
      }
      else {
        MeshFilter filter = Target.GetComponent<MeshFilter>();
        hit.Triangle = filter != null ? MeshUtils.FindClosestTriangle( filter.sharedMesh, Target, ray, rayLength ) : TriangleHit.Invalid;
      }

      if ( hit.Triangle.Valid )
        hit.Triangle.ClosestEdge = ShapeUtils.FindClosestEdgeToSegment( ray.GetPoint( 0 ), ray.GetPoint( rayLength ), hit.Triangle.Edges ).Edge;

      List<MeshUtils.Edge> allEdges = FindPrincipalEdges( shape, 10.0f ).ToList();
      if ( hit.Triangle.Valid )
        allEdges.Add( hit.Triangle.ClosestEdge );

      var closestEdgeToSegmentResult = ShapeUtils.FindClosestEdgeToSegment( ray.GetPoint( 0 ), ray.GetPoint( rayLength ), allEdges.ToArray() );
      hit.ClosestEdge.Target         = Target;
      hit.ClosestEdge.Edge           = closestEdgeToSegmentResult.Edge;
      hit.ClosestEdge.Distance       = closestEdgeToSegmentResult.Distance;

      return ( LastHit = hit );
    }

    public static bool HasRayCompatibleComponents( GameObject gameObject )
    {
      return gameObject != null &&
             (
               gameObject.GetComponent<MeshFilter>() != null ||
               ( gameObject.GetComponent<Collide.Shape>() != null && gameObject.GetComponent<Collide.HeightField>() == null )
             );
    }

    public static Hit Test( GameObject target, Ray ray, float rayLength = 500.0f, bool includeAllChildren = false )
    {
      if ( target == null )
        return Hit.Invalid;

      if ( !includeAllChildren )
        return ( new Raycast() { Target = target } ).Test( ray, rayLength );

      List<Hit> hitList = new List<Hit>()
      {
        ( new Raycast() { Target = target } ).Test( ray, rayLength )
      };

      foreach ( Transform child in target.transform )
        hitList.Add( Test( child.gameObject, ray, rayLength, true ) );

      return FindBestHit( hitList );
    }

    public static List<Hit> TestChildren( GameObject parent, Ray ray, float rayLength = 500.0f, Predicate<GameObject> objectPredicate = null )
    {
      List<Hit> result = new List<Hit>();
      if ( parent == null )
        return result;

      parent.TraverseChildren( obj =>
      {
        if ( objectPredicate == null || objectPredicate( obj ) ) {
          Hit hit = Test( obj, ray, rayLength );
          if ( hit.Triangle.Valid )
            result.Add( hit );
        }
      } );

      result.Sort( ( hit1, hit2 ) => { return hit1.Triangle.Distance < hit2.Triangle.Distance ? -1 : 1; } );

      return result;
    }

    private static Hit FindBestHit( List<Hit> hitList )
    {
      Hit bestHit = Hit.Invalid;
      foreach ( Hit hit in hitList ) {
        if ( hit.Triangle.Valid && hit.Triangle.Distance < bestHit.Triangle.Distance )
          bestHit.Triangle = hit.Triangle;
        if ( hit.ClosestEdge.Valid && hit.ClosestEdge.Distance < bestHit.ClosestEdge.Distance )
          bestHit.ClosestEdge = hit.ClosestEdge;
      }

      return bestHit;
    }

    private MeshUtils.Edge[] FindPrincipalEdges( Collide.Shape shape, float principalEdgeExtension )
    {
      if ( shape != null && shape.GetUtils() != null )
        return shape.GetUtils().GetPrincipalEdgesWorld( principalEdgeExtension );

      Mesh mesh = shape is Collide.Mesh ?
                    ( shape as Collide.Mesh ).SourceObjects.FirstOrDefault() :
                  Target.GetComponent<MeshFilter>() != null ?
                    Target.GetComponent<MeshFilter>().sharedMesh :
                  null;

      Vector3 halfExtents = 0.5f * Vector3.one;
      if ( mesh != null )
        halfExtents = mesh.bounds.extents;

      MeshUtils.Edge[] edges = ShapeUtils.ExtendAndTransformEdgesToWorld( Target.transform,
                                new MeshUtils.Edge[]
                                {
                                  new MeshUtils.Edge( BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Negative_X ),
                                                      BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Positive_X ),
                                                      ShapeUtils.GetLocalFaceDirection( ShapeUtils.Direction.Positive_Y ),
                                                      MeshUtils.Edge.EdgeType.Principal ),
                                  new MeshUtils.Edge( BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Negative_Y ),
                                                      BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Positive_Y ),
                                                      ShapeUtils.GetLocalFaceDirection( ShapeUtils.Direction.Positive_Z ),
                                                      MeshUtils.Edge.EdgeType.Principal ),
                                  new MeshUtils.Edge( BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Negative_Z ),
                                                      BoxShapeUtils.GetLocalFace( halfExtents, ShapeUtils.Direction.Positive_Z ),
                                                      ShapeUtils.GetLocalFaceDirection( ShapeUtils.Direction.Positive_X ),
                                                      MeshUtils.Edge.EdgeType.Principal )
                                },
                                principalEdgeExtension );

      return edges;
    }
  }
}
