using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [Serializable]
  public class Line
  {
    /// <summary>
    /// Create given start and end in parent frame. If parent isn't given
    /// the start and end will be in world frame.
    /// </summary>
    /// <param name="parent">Parent object.</param>
    /// <param name="localStart">Start of the line given in parent frame.</param>
    /// <param name="localEnd">End of the line given in parent frame.</param>
    /// <returns>New line instance.</returns>
    public static Line Create( GameObject parent, Vector3 localStart, Vector3 localEnd )
    {
      return new Line()
      {
        Start = new IFrame( parent )
        {
          LocalPosition = localStart
        },
        End = new IFrame( parent )
        {
          LocalPosition = localEnd
        }
      };
    }

    [SerializeField]
    private IFrame m_start = new IFrame();

    /// <summary>
    /// Start frame of this line.
    /// </summary>
    public IFrame Start
    {
      get { return m_start; }
      set { m_start = value ?? new IFrame(); }
    }

    [SerializeField]
    private IFrame m_end = new IFrame();

    /// <summary>
    /// End frame of this line.
    /// </summary>
    public IFrame End
    {
      get { return m_end; }
      set { m_end = value ?? new IFrame(); }
    }

    /// <summary>
    /// Center position in world coordinate frame of this line.
    /// </summary>
    public Vector3 Center
    {
      get { return 0.5f * ( Start.Position + End.Position ); }
    }

    /// <summary>
    /// Length of this line.
    /// </summary>
    public float Length
    {
      get { return Vector3.Distance( Start.Position, End.Position ); }
    }

    /// <summary>
    /// Direction of this line given in world coordinate frame.
    /// </summary>
    public Vector3 Direction
    {
      get { return Vector3.Normalize( End.Position - Start.Position ); }
    }

    /// <summary>
    /// True if this line has an extension - otherwise false.
    /// </summary>
    public bool Valid { get { return Length > 1.0E-6f; } }

    /// <summary>
    /// Calculates the local direction of this line in <paramref name="parent"/> frame.
    /// </summary>
    /// <param name="parent">Parent object in which frame the local direction should be calculated.</param>
    /// <returns>Direction in given parent coordinate frame.</returns>
    public Vector3 CalculateLocalDirection( GameObject parent )
    {
      return Vector3.Normalize( End.CalculateLocalPosition( parent ) - Start.CalculateLocalPosition( parent ) );
    }

    /// <summary>
    /// Convert to native agx.Edge. If <paramref name="parent"/> is given, the native
    /// edge will be given in the parents coordinate frame - otherwise world frame.
    /// </summary>
    /// <param name="parent">Parent in which this edge should be transformed to.</param>
    /// <returns>Native agx.Edge.</returns>
    public agx.Edge ToNativeEdge( GameObject parent )
    {
      return new agx.Edge( Start.CalculateLocalPosition( parent ).ToHandedVec3(),
                           End.CalculateLocalPosition( parent ).ToHandedVec3() );
    }
  }
}
