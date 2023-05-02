using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Given a 3D curve, this object solves for a segment length given
  /// desired number of segments.
  /// </summary>
  public class PointCurve : IEnumerable<Vector3>
  {
    /// <summary>
    /// Segment type in callbacks to know if the segment is first or last.
    /// </summary>
    public enum SegmentType
    {
      First,
      Intermediate,
      Last
    }

    /// <summary>
    /// Result of segmentation containing the resulting segment length,
    /// final error and number of iterations.
    /// </summary>
    public struct SegmentationResult
    {
      /// <summary>
      /// Resulting segment length given number of segments.
      /// </summary>
      public float SegmentLength;

      /// <summary>
      /// Signed error in the resulting segment length.
      /// </summary>
      public float Error;

      /// <summary>
      /// Given number of segments.
      /// </summary>
      public int NumSegments;

      /// <summary>
      /// Number of iterations to reach the result.
      /// </summary>
      public int NumIterations;

      /// <summary>
      /// True if the error is within the given tolerance, otherwise false.
      /// </summary>
      public bool Successful;
    }

    /// <summary>
    /// Segment point, i.e., a point on a segment (between two given control points).
    /// </summary>
    public struct SegmentPoint
    {
      /// <summary>
      /// Segment begin point (of given point).
      /// </summary>
      public Vector3 Begin;

      /// <summary>
      /// Segment end point (of given point).
      /// </summary>
      public Vector3 End;

      /// <summary>
      /// Point between Begin and End @ LocalTime.
      /// </summary>
      public Vector3 Point;

      /// <summary>
      /// Time between Begin and End.
      /// </summary>
      public float LocalTime;

      /// <summary>
      /// (global) Time along curve.
      /// </summary>
      public float Time;
    }

    /// <summary>
    /// Default error function is evaluating how far we're from the last point
    /// given the current segment length.
    /// </summary>
    /// <param name="curve">Point curve.</param>
    /// <param name="curr">Current segment point.</param>
    /// <param name="next">Next segment point.</param>
    /// <param name="segmentType">Segment type.</param>
    /// <returns>0.0f unless segmentType == last, then distance from next to last point in list.</returns>
    public static float DefaultErrorFunc( PointCurve curve, SegmentPoint curr, SegmentPoint next, SegmentType segmentType )
    {
      if ( segmentType == SegmentType.Last )
        return Mathf.Sign( 1.0f - next.Time ) * Vector3.Distance( next.Point, curve.Evaluate( 1.0f ).Point );
      return 0.0f;
    }

    private List<Vector3> m_points          = new List<Vector3>();
    private List<float> m_time              = new List<float>();
    private SegmentationResult m_lastResult = new SegmentationResult() { Successful = false, Error = float.PositiveInfinity };

    /// <summary>
    /// Total length of the unsegmented curve.
    /// </summary>
    public float TotalLength
    {
      get
      {
        if ( m_points.Count < 2 )
          return 0;

        var length = 0.0f;
        for ( int i = 1; i < m_points.Count; ++i )
          length += Vector3.Distance( m_points[ i - 1 ], m_points[ i ] );

        return length;
      }
    }

    /// <summary>
    /// Number of points added.
    /// </summary>
    public int NumPoints { get { return m_points.Count; } }

    /// <summary>
    /// Last successful result from FindSegmentLength.
    /// </summary>
    public SegmentationResult LastSuccessfulResult { get { return m_lastResult; } set { m_lastResult = value; } }

    /// <summary>
    /// Access operator to added points.
    /// </summary>
    /// <param name="index">Index of point.</param>
    /// <returns>Point at given index.</returns>
    public Vector3 this[ int index ]
    {
      get { return m_points[ index ]; }
      set { m_points[ index ] = value; }
    }

    /// <summary>
    /// Add control point.
    /// </summary>
    /// <param name="point">Point to add.</param>
    public void Add( Vector3 point )
    {
      m_points.Add( point );
      m_time.Clear();
    }

    /// <summary>
    /// Insert point at index.
    /// </summary>
    /// <param name="point">Point to insert.</param>
    /// <param name="index">Insert index.</param>
    public void Insert( Vector3 point, int index )
    {
      m_points.Insert( index, point );
      m_time.Clear();
    }

    /// <summary>
    /// Remove point at index.
    /// </summary>
    /// <param name="index">Index to remove point.</param>
    public void RemoveAt( int index )
    {
      m_points.RemoveAt( index );
      m_time.Clear();
    }

    /// <summary>
    /// Clears all interal data.
    /// </summary>
    public void Clear()
    {
      m_points.Clear();
      m_time.Clear();
    }

    /// <summary>
    /// Evaluate curve at given time [0, 1].
    /// </summary>
    /// <param name="t">Time along curve [0, 1].</param>
    /// <returns>Segment point at given time.</returns>
    public SegmentPoint Evaluate( float t )
    {
      if ( m_points.Count < 2 ) {
        Debug.LogWarning( "PointCurve.Evaluate called with an undefined curve #points < 2." );
        return default( SegmentPoint );
      }

      if ( m_points.Count != m_time.Count && !Finalize() ) {
        Debug.LogWarning( "PointCurve.Finalize failed - length of curve is undefined: " + TotalLength );
        return default( SegmentPoint );
      }

      var index = FindIndex( t );
      if ( index + 1 >= m_points.Count )
        index = m_points.Count - 2;

      var segment       = new SegmentPoint();
      segment.Begin     = m_points[ index ];
      segment.End       = m_points[ index + 1 ];
      segment.Time      = t;
      if ( m_time[ index ] == m_time[ index + 1 ] ) {
        segment.LocalTime = 1.0f;
        segment.Point     = segment.End;
      }
      else {
        segment.LocalTime = ( t - m_time[ index ] ) / ( m_time[ index + 1 ] - m_time[ index ] );
        segment.Point     = segment.Begin + segment.LocalTime * ( segment.End - segment.Begin );
      }

      return segment;
    }

    /// <summary>
    /// Traverse all segments given segment length and tolerance.
    /// </summary>
    /// <param name="callback">Callback with current and next segment point and segment type.</param>
    /// <param name="segmentLength">Desired segment length between curr.Point and next.Point.</param>
    /// <param name="tolerance">Error tolerance of the actual segment length as a factor of the total length.</param>
    public void Traverse( Action<SegmentPoint, SegmentPoint, SegmentType> callback,
                          float segmentLength,
                          float tolerance = 1.0E-6f )
    {
      if ( segmentLength <= 0.0f )
        return;

      var totalLength = TotalLength;
      if ( totalLength == 0.0f )
        return;

      var dt          = segmentLength / totalLength;
      var prevT       = 0.0f;
      var prev        = Evaluate( prevT );
      var segmentType = SegmentType.First;
      var done        = false;
      while ( !done ) {
        var currT      = prevT + dt;
        var curr       = Evaluate( currT );
        var prevToCurr = Vector3.Distance( prev.Point, curr.Point );

        int maxIterations = 100;
        int i = 0;
        while ( prevToCurr > 0.0f && currT < 1.0f + 0.5f * dt && !Math.Equivalent( prevToCurr, segmentLength, tolerance * TotalLength ) && i++ < maxIterations) {
          var overshoot = prevToCurr - segmentLength;
          currT        -= overshoot / totalLength;
          curr          = Evaluate( currT );
          prevToCurr    = Vector3.Distance( prev.Point, curr.Point );
        }

        done = currT > 1.0f + dt;
        done = done || ( ( currT + 0.5f * dt >= 1.0f ) && Vector3.Distance( curr.Point, m_points.Last() ) < 0.5f * segmentLength );

        if ( done )
          segmentType = SegmentType.Last;

        callback( prev, curr, segmentType );

        if ( segmentType == SegmentType.First )
          segmentType = SegmentType.Intermediate;

        prevT = currT;
        prev  = curr;
      }
    }

    /// <summary>
    /// Find optimal segment length given number of segments and error function (signed error).
    /// Newton's method is used to find the segment length.
    /// </summary>
    /// <param name="numSegments">Desired number of segments along the curve.</param>
    /// <param name="errorFunc">Error function that calculates the signed error given segment points.</param>
    /// <param name="globalTolerance">Global tolerance when to exit Newton's method.</param>
    /// <param name="localTolerance">Local curve tolerance when traversing this curve.</param>
    /// <param name="maxNumIterations">Maximum number of iterations before exiting.</param>
    /// <returns>Segmentation result with resulting segment length and error.</returns>
    public SegmentationResult FindSegmentLength( int numSegments,
                                                 Func<PointCurve, SegmentPoint, SegmentPoint, SegmentType, float> errorFunc,
                                                 float globalTolerance = 1.0E-3f,
                                                 float localTolerance = 1.0E-5f,
                                                 int maxNumIterations = 100 )
    {
      var result = new SegmentationResult()
      {
        NumSegments = numSegments,
        Error = float.PositiveInfinity,
        NumIterations = 0,
        SegmentLength = -1.0f,
        Successful = false
      };

      var totalLength = TotalLength;
      if ( totalLength <= 0.0f || numSegments < 1 )
        return result;

      result.SegmentLength = totalLength / numSegments;

      var dl         = 1.0E-3f / numSegments;
      var bestResult = new SegmentationResult()
      {
        NumSegments = numSegments,
        Error = float.PositiveInfinity,
        NumIterations = 0,
        SegmentLength = -1.0f,
        Successful = false
      };
      
      try {
        var done = false;
        while ( !done && result.NumIterations++ < maxNumIterations ) {
          var ePrev = 0.0f;
          var eCurr = 0.0f;
          var eNext = 0.0f;

          Traverse( ( s1, s2, sType ) =>
          {
            ePrev += errorFunc( this, s1, s2, sType );
          }, result.SegmentLength - dl, localTolerance );

          Traverse( ( s1, s2, sType ) =>
          {
            eCurr += errorFunc( this, s1, s2, sType );
          }, result.SegmentLength, localTolerance );

          Traverse( ( s1, s2, sType ) =>
          {
            eNext += errorFunc( this, s1, s2, sType );
          }, result.SegmentLength + dl, localTolerance );

          if ( eCurr < bestResult.Error ) {
            bestResult = result;
            bestResult.Error = eCurr;
          }

          var dr = -2.0f * dl * eCurr / ( eNext - ePrev );
          result.SegmentLength += dr;

          if ( eNext == ePrev ||
               result.NumIterations == maxNumIterations ||
               result.SegmentLength - dl < 1.0E-6f )
            return bestResult;

          result.Error = 0.0f;
          Traverse( ( s1, s2, sType ) =>
          {
            result.Error += errorFunc( this, s1, s2, sType );
          }, result.SegmentLength, localTolerance );

          done = Mathf.Abs( result.Error ) < globalTolerance;
        }

        result.Successful = done;
        if ( result.Successful )
          m_lastResult = result;

        return result;
      }
      catch ( System.Exception ) {
        return bestResult;
      }
    }

    /// <summary>
    /// Calculates the time along this curve used when traversing and solving.
    /// When the points has changed (moved), this method has to be called
    /// before traversing and/or solving.
    /// </summary>
    /// <returns>True if length of the curve is > 0, otherwise false.</returns>
    public bool Finalize()
    {
      m_time.Clear();

      var totalLength = TotalLength;
      if ( totalLength <= 0.0f )
        return false;

      m_time.Capacity = m_points.Count;
      var accumulatedTime = 0.0f;
      m_time.Add( accumulatedTime );
      for ( int i = 1; i < m_points.Count; ++i ) {
        accumulatedTime += Vector3.Distance( m_points[ i - 1 ], m_points[ i ] ) / totalLength;
        m_time.Add( accumulatedTime );
      }
      m_time[ m_points.Count - 1 ] = 1.0f;

      return true;
    }

    /// <summary>
    /// Finds index given current time.
    /// </summary>
    /// <param name="t">Current time.</param>
    /// <returns>Index of point before (or at) time t.</returns>
    public int FindIndex( float t )
    {
      if ( t <= 1.0E-6f )
        return 0;
      else if ( t >= 1.0f - 1.0E-6f )
        return m_points.Count - 1;

      var index = m_time.BinarySearch( t );
      if ( index < 0 )
        index = ~index;
      return index - 1;
    }

    public IEnumerator<Vector3> GetEnumerator()
    {
      return m_points.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
