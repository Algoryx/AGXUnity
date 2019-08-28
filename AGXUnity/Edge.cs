using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Triangle edge or object principal axis with start, end, normal and type.
  /// </summary>
  public struct Edge
  {
    /// <summary>
    /// Edge type - where this edge originates from.
    /// </summary>
    public enum EdgeType
    {
      /// <summary>
      /// This edge is a triangle edge.
      /// </summary>
      Triangle,
      /// <summary>
      /// This edge is an objects principal axis.
      /// </summary>
      Principal
    }

    /// <summary>
    /// Edge type.
    /// </summary>
    public EdgeType Type;

    /// <summary>
    /// Start of this edge in world coordinate system.
    /// </summary>
    public Vector3 Start;

    /// <summary>
    /// End of this edge in world coordinate system.
    /// </summary>
    public Vector3 End;

    /// <summary>
    /// Normal (originated from the triangle) of this edge in world coordinate system.
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// Center of this edge in world coordinate system.
    /// </summary>
    public Vector3 Center { get { return 0.5f * ( Start + End ); } }

    /// <summary>
    /// Normalized direction of this edge in world coordinate system.
    /// </summary>
    public Vector3 Direction { get { return Vector3.Normalize( End - Start ); } }

    /// <summary>
    /// Length of this edge.
    /// </summary>
    public float Length { get { return Vector3.Distance( Start, End ); } }

    /// <summary>
    /// True if valid, i.e., length > 0 - otherwise false.
    /// </summary>
    public bool Valid { get { return !Start.Equals( End ); } }

    /// <summary>
    /// Reset all values to zero.
    /// </summary>
    public void Reset()
    {
      Start = End = Normal = Vector3.zero;
      Type  = EdgeType.Triangle;
    }
  }
}
