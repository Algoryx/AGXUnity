using UnityEngine;
using AGXUnity.Collide;

namespace AGXUnity.Utils
{
  public class BoxShapeUtils : ShapeUtils
  {
    public static Vector3 GetLocalFace( Vector3 halfExtents, Direction dir )
    {
      return Vector3.Scale( halfExtents, GetLocalFaceDirection( dir ) );
    }

    public override Vector3 GetLocalFace( Direction dir )
    {
      var box = GetShape<Box>();
      return GetLocalFace( box.HalfExtents, dir );
    }

    public override bool IsHalfSize( Direction direction )
    {
      return true;
    }

    public override void UpdateSize( ref Vector3 localChange, Direction dir )
    {
      var box = GetShape<Box>();

      var desiredDelta = Vector3.Scale( GetLocalFaceDirection( dir ), localChange );
      var oldHalfExtents = box.HalfExtents;

      box.HalfExtents += desiredDelta;

      localChange += GetSign( dir ) * ( box.HalfExtents - ( oldHalfExtents + desiredDelta ) );
    }
  }

  public class CapsuleShapeUtils : RadiusHeightShapeUtils<Capsule>
  {
    public override Vector3 GetLocalFace( Direction dir )
    {
      var capsule = GetShape<Capsule>();
      if ( ToPrincipal( dir ) == PrincipalAxis.Y )
        return ( capsule.Radius + 0.5f * capsule.Height ) * GetLocalFaceDirection( dir );
      else
        return capsule.Radius * GetLocalFaceDirection( dir );
    }
  }

  public class CylinderShapeUtils : RadiusHeightShapeUtils<Cylinder>
  {
    public override Vector3 GetLocalFace( Direction dir )
    {
      var cylinder = GetShape<Cylinder>();
      if ( ToPrincipal( dir ) == PrincipalAxis.Y )
        return 0.5f * cylinder.Height * GetLocalFaceDirection( dir );
      else
        return cylinder.Radius * GetLocalFaceDirection( dir );
    }
  }

  public abstract class RadiusHeightShapeUtils<T> : ShapeUtils
    where T : Shape
  {
    public override bool IsHalfSize( Direction direction )
    {
      return ToPrincipal( direction ) != PrincipalAxis.Y;
    }

    public override void UpdateSize( ref Vector3 localChange, Direction dir )
    {
      var shape = GetShape<T>();

      var heightOrRadiusProperty = PropertySynchronizer.GetValueProperty<float>( shape,
                                                                                 IsHalfSize( dir ) ?
                                                                                   "Radius" :
                                                                                   "Height" );
      var desiredDelta = Vector3.Dot( GetLocalFaceDirection( dir ), localChange );
      var oldValue = heightOrRadiusProperty.Value;
      heightOrRadiusProperty.Value += desiredDelta;

      localChange += ( heightOrRadiusProperty.Value - ( oldValue + desiredDelta ) ) * GetLocalFaceDirection( dir );
    }
  }

  public class SphereShapeUtils : ShapeUtils
  {
    public override Vector3 GetLocalFace( Direction dir )
    {
      var sphere = GetShape<Sphere>();
      return sphere.Radius * GetLocalFaceDirection( dir );
    }

    public override bool IsHalfSize( Direction direction )
    {
      return true;
    }

    public override void UpdateSize( ref Vector3 localChange, Direction dir )
    {
      var sphere = GetShape<Sphere>();
      var desiredDelta = Vector3.Dot( GetLocalFaceDirection( dir ), localChange );

      var oldRadius = sphere.Radius;
      sphere.Radius += desiredDelta;

      localChange += ( sphere.Radius - ( oldRadius + desiredDelta ) ) * GetLocalFaceDirection( dir );
    }
  }

  public abstract class ShapeUtils
  {
    public struct ShortestDistancePointSegmentResult
    {
      public float ShortestDistance;
      public Vector3 PointOnSegment;
      public float Time;
    }

    /// <summary>
    /// Calculates shortest distance between a point and a line segment.
    /// </summary>
    /// <param name="point">Point.</param>
    /// <param name="segmentStart">Segment start.</param>
    /// <param name="segmentEnd">Segment end.</param>
    /// <returns>Shortest distance between the given point and the line segment.</returns>
    public static ShortestDistancePointSegmentResult ShortestDistancePointSegment( Vector3 point,
                                                                                   Vector3 segmentStart,
                                                                                   Vector3 segmentEnd )
    {
      var segmentDir = segmentEnd - segmentStart;
      float divisor = segmentDir.sqrMagnitude;
      if ( divisor < 1.0E-6f )
        return new ShortestDistancePointSegmentResult()
        {
          ShortestDistance = Vector3.Distance( point, segmentStart ),
          PointOnSegment   = segmentStart,
          Time             = 0.0f
        };

      var result              = new ShortestDistancePointSegmentResult();
      result.Time             = Mathf.Clamp01( Vector3.Dot( ( point - segmentStart ), segmentDir ) / divisor );
      result.PointOnSegment   = ( 1.0f - result.Time ) * segmentStart + result.Time * segmentEnd;
      result.ShortestDistance = Vector3.Distance( point, result.PointOnSegment );

      return result;
    }

    public struct ShortestDistanceSegmentSegmentResult
    {
      public Vector3 PointOnSegment1;
      public Vector3 PointOnSegment2;

      public float Distance { get { return Vector3.Distance( PointOnSegment1, PointOnSegment2 ); } }
    }

    public struct ClosestEdgeSegmentResult
    {
      public Edge Edge;
      public float Distance;
    }

    /// <summary>
    /// Finds shortest distance between two line segments.
    /// </summary>
    /// <param name="segment1Begin">Begin point, first segment.</param>
    /// <param name="segment1End">End point, first segment.</param>
    /// <param name="segment2Begin">Begin point, second segment.</param>
    /// <param name="segment2End">End point, second segment.</param>
    /// <returns>Shortest distance between the two line segments.</returns>
    public static ShortestDistanceSegmentSegmentResult ShortestDistanceSegmentSegment( Vector3 segment1Begin,
                                                                                       Vector3 segment1End,
                                                                                       Vector3 segment2Begin,
                                                                                       Vector3 segment2End )
    {
      float eps = float.Epsilon;
      Vector3 d1 = segment1End - segment1Begin;
      Vector3 d2 = segment2End - segment2Begin;
      Vector3 r = segment1Begin - segment2Begin;

      float d1Length2 = Vector3.Dot( d1, d1 );
      float d2Length2 = Vector3.Dot( d2, d2 );
      float d2r = Vector3.Dot( r, d2 );

      float t1 = 0.0f;
      float t2 = 0.0f;
      float pt1 = 0.0f;
      float pt2 = 0.0f;
      bool isParallel = false;

      if ( d1Length2 <= eps && d2Length2 <= eps )
        return new ShortestDistanceSegmentSegmentResult() { PointOnSegment1 = segment1Begin, PointOnSegment2 = segment2Begin };

      if ( d1Length2 <= eps ) {
        t1 = 0.0f;
        t2 = Mathf.Clamp01( d2r / d2Length2 );
      }
      else {
        float d1r = Vector3.Dot( d1, r );
        if ( d2Length2 <= eps ) {
          t2 = 0.0f;
          t1 = Mathf.Clamp01( -d1r / d1Length2 );
        }
        else {
          float d1d2 = Vector3.Dot( d1, d2 );
          float denom = d1Length2 * d2Length2 - d1d2 * d1d2;
          int numPairsToFind = 1;
          if ( denom <= eps ) {
            isParallel = true;
            numPairsToFind = 2;
            t1 = 0.0f;
          }
          else
            t1 = Mathf.Clamp01( ( d2r * d1d2 - d1r * d2Length2 ) / denom );

          while ( numPairsToFind > 0 ) {
            t2 = ( d1d2 * t1 + d2r ) / d2Length2;

            if ( t2 < 0.0f ) {
              t2 = 0.0f;
              t1 = Mathf.Clamp01( -d1r / d1Length2 );
            }
            else if ( t2 > 1.0f ) {
              t2 = 1.0f;
              t1 = Mathf.Clamp01( ( d1d2 - d1r ) / d1Length2 );
            }

            if ( numPairsToFind == 2 ) {
              pt1 = t1;
              pt2 = t2;
              t1 = 1.0f;
            }

            --numPairsToFind;
          }

          if ( isParallel ) {
            t1 = pt1;
            t2 = pt2;
          }
        }
      }

      return new ShortestDistanceSegmentSegmentResult()
      {
        PointOnSegment1 = segment1Begin + t1 * d1,
        PointOnSegment2 = segment2Begin + t2 * d2
      };
    }

    public static ClosestEdgeSegmentResult FindClosestEdgeToSegment( Vector3 segmentStart,
                                                                     Vector3 segmentEnd,
                                                                     Edge[] edges )
    {
      var result = new ClosestEdgeSegmentResult()
      {
        Edge = new Edge(),
        Distance = float.PositiveInfinity
      };
      for ( int i = 0; i < edges.Length; ++i ) {
        var edge = edges[ i ];
        if ( !edge.Valid )
          continue;

        float distance = ShortestDistanceSegmentSegment( segmentStart, segmentEnd, edge.Start, edge.End ).Distance;
        if ( distance < result.Distance ) {
          result.Edge     = edge;
          result.Distance = distance;
        }
      }

      return result;
    }

    public static bool IsPointInTriangle( Vector3 point, Vector3 v1, Vector3 v2, Vector3 v3, float epsilon )
    {
      Vector3 u = v2 - v1;
      Vector3 v = v3 - v1;
      Vector3 n = Vector3.Cross( u, v );
      Vector3 w = point - v1;

      float alpha = Vector3.Dot( Vector3.Cross( u, w ), n ) / n.sqrMagnitude;
      float beta  = Vector3.Dot( Vector3.Cross( w, v ), n ) / n.sqrMagnitude;
      float gamma = 1 - alpha - beta;
      return alpha >= -epsilon && alpha <= 1.0f + epsilon &&
             beta  >= -epsilon && beta  <= 1.0f + epsilon &&
             gamma >= -epsilon && gamma <= 1.0f + epsilon;
    }

    public static ShapeUtils Create( Shape shape )
    {
      if ( shape is Box )
        return new BoxShapeUtils() { m_shape = shape };
      else if ( shape is Capsule )
        return new CapsuleShapeUtils() { m_shape = shape };
      else if ( shape is Cylinder )
        return new CylinderShapeUtils() { m_shape = shape };
      else if ( shape is Sphere )
        return new SphereShapeUtils() { m_shape = shape };

      return null;
    }

    public enum PrincipalAxis
    {
      X,
      Y,
      Z
    }

    public enum Direction
    {
      Positive_X,
      Negative_X,
      Positive_Y,
      Negative_Y,
      Positive_Z,
      Negative_Z
    }

    public abstract Vector3 GetLocalFace( Direction direction );

    public abstract bool IsHalfSize( Direction direction );

    public abstract void UpdateSize( ref Vector3 localChange, Direction dir );

    public static Vector3 GetLocalFaceDirection( Direction direction )
    {
      return m_unitFaces[ System.Convert.ToInt32( direction ) ];
    }

    public static PrincipalAxis ToPrincipal( Direction dir )
    {
      return (PrincipalAxis)( System.Convert.ToInt32( dir ) / 2 );
    }

    public static Direction[] ToDirection( PrincipalAxis axis )
    {
      int iAxis = 2 * System.Convert.ToInt32( axis );
      return new Direction[] { (Direction)iAxis, (Direction)( iAxis + 1 ) };
    }

    public static float GetSign( Direction dir )
    {
      return 1.0f - 2.0f * ( System.Convert.ToInt32( dir ) % 2 );
    }

    public Vector3 GetWorldFace( Direction direction )
    {
      return m_shape.transform.position + m_shape.transform.TransformDirection( GetLocalFace( direction ) );
    }

    public Vector3 GetWorldFaceDirection( Direction direction )
    {
      return m_shape.transform.TransformDirection( GetLocalFaceDirection( direction ) );
    }

    public Direction FindDirectionGivenWorldEdge( Edge worldEdge )
    {
      float bestResult      = float.NegativeInfinity;
      Vector3 edgeDirection = worldEdge.Direction;
      Direction result      = Direction.Negative_X;
      foreach ( Direction direction in System.Enum.GetValues( typeof( Direction ) ) ) {
        Vector3 worldDir = GetWorldFaceDirection( direction );
        float dotProduct = Vector3.Dot( worldDir, edgeDirection );
        if ( dotProduct > bestResult ) {
          bestResult = dotProduct;
          result = direction;
        }
      }

      return result;
    }

    public Edge[] GetPrincipalEdgesWorld( float principalEdgeExtension )
    {
      var edges = new Edge[]
      {
        new Edge()
        {
          Start  = GetLocalFace( Direction.Negative_X ),
          End    = GetLocalFace( Direction.Positive_X ),
          Normal = GetLocalFaceDirection( Direction.Positive_Y ),
          Type   = Edge.EdgeType.Principal
        },
        new Edge()
        {
          Start  = GetLocalFace( Direction.Negative_Y ),
          End    = GetLocalFace( Direction.Positive_Y ),
          Normal = GetLocalFaceDirection( Direction.Positive_Z ),
          Type   = Edge.EdgeType.Principal
        },
        new Edge()
        {
          Start  = GetLocalFace( Direction.Negative_Z ),
          End    = GetLocalFace( Direction.Positive_Z ),
          Normal = GetLocalFaceDirection( Direction.Positive_X ),
          Type   = Edge.EdgeType.Principal
        }
      };

      return ExtendAndTransformEdgesToWorld( m_shape.transform, edges, principalEdgeExtension );
    }

    public static Edge[] ExtendAndTransformEdgesToWorld( Transform transform,
                                                         Edge[] edges,
                                                         float principalEdgeExtension )
    {
      for ( int i = 0; i < edges.Length; ++i ) {
        edges[ i ].Start -= principalEdgeExtension * edges[ i ].Direction;
        edges[ i ].End   += principalEdgeExtension * edges[ i ].Direction;
        edges[ i ].Start  = transform.position + transform.TransformDirection( edges[ i ].Start );
        edges[ i ].End    = transform.position + transform.TransformDirection( edges[ i ].End );
        edges[ i ].Normal = transform.TransformDirection( edges[ i ].Normal );
      }

      return edges;
    }

    public T GetShape<T>() where T : Shape
    {
      return m_shape as T;
    }

    private static Vector3[] m_unitFaces = new Vector3[]{
                                                          new Vector3(  1,  0,  0 ),
                                                          new Vector3( -1,  0,  0 ),
                                                          new Vector3(  0,  1,  0 ),
                                                          new Vector3(  0, -1,  0 ),
                                                          new Vector3(  0,  0,  1 ),
                                                          new Vector3(  0,  0, -1 )
                                                        };

    private Shape m_shape = null;
  }
}
