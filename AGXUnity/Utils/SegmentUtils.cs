using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SegmentUtils
{
  /// <summary>
  /// Creates a transformation matrix which positions a cylinder (1x1x1) between the start and end points
  /// </summary>
  /// <param name="start">The start point of the segment</param>
  /// <param name="end">The end point of the segment</param>
  /// <param name="radius">The radius of the segment</param>
  /// <returns>A transformation matrix which positions a cylinder at the given segment</returns>
  public static Matrix4x4 CalculateCylinderTransform( Vector3 start, Vector3 end, float radius )
  {
    CalculateCylinderTransform( start,
                                end,
                                radius,
                                out var position,
                                out var rotation,
                                out var scale );
    return Matrix4x4.TRS( position, rotation, scale );
  }

  /// <summary>
  /// Creates raw position, rotation and scale values which positions a cylinder (1x1x1) between the start and end points
  /// </summary>
  /// <param name="start">The start point of the segment</param>
  /// <param name="end">The end point of the segment</param>
  /// <param name="radius">The radius of the segment</param>
  /// <param name="position">The calculated position of the cylinder</param>
  /// <param name="rotation">The calculated rotation of the cylinder</param>
  /// <param name="scale">The calculated scale of the cylinder</param>
  public static void CalculateCylinderTransform( Vector3 start,
                                                 Vector3 end,
                                                 float radius,
                                                 out Vector3 position,
                                                 out Quaternion rotation,
                                                 out Vector3 scale )
  {
    var dir = end - start;
    var length = dir.magnitude;
    position = 0.5f * ( start + end );
    rotation = Quaternion.FromToRotation( Vector3.up, dir );
    scale = new Vector3( 2.0f * radius, 1f * length, 2.0f * radius );
  }
}
