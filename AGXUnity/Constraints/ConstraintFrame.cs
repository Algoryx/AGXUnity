using System;
using UnityEngine;

namespace AGXUnity
{
  [Serializable]
  public class ConstraintFrame : IFrame
  {
    /// <summary>
    /// Create constraint frame given parent, local position (parent frame) and
    /// local rotation (parent frame)
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="localPosition">Local position in parent frame.</param>
    /// <param name="localRotation">Local rotation in parent frame.</param>
    /// <returns>New constraint frame.</returns>
    public static ConstraintFrame CreateLocal( GameObject parent, Vector3 localPosition, Quaternion localRotation )
    {
      return new ConstraintFrame( parent, localPosition, localRotation );
    }

    /// <summary>
    /// Create constraint frame given parent, local position (parent frame) and
    /// local axis (parent frame)
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="localPosition">Local position in parent frame.</param>
    /// <param name="localAxis">Local axis in parent frame.</param>
    /// <returns>New constraint frame.</returns>
    public static ConstraintFrame CreateLocal( GameObject parent, Vector3 localPosition, Vector3 localAxis )
    {
      return new ConstraintFrame( parent, localPosition, localAxis );
    }

    /// <summary>
    /// Create constraint frame given parent, world position and world rotation.
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="worldPosition">World position.</param>
    /// <param name="worldRotation">World rotation.</param>
    /// <returns>New constraint frame.</returns>
    public static ConstraintFrame CreateWorld( GameObject parent, Vector3 worldPosition, Quaternion worldRotation )
    {
      var frame = new ConstraintFrame( parent );
      frame.Position = worldPosition;
      frame.Rotation = worldRotation;
      return frame;
    }

    /// <summary>
    /// Create constraint frame given parent, world position and world axis.
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="worldPosition">World position.</param>
    /// <param name="worldAxis">World axis.</param>
    /// <returns>New constraint frame.</returns>
    public static ConstraintFrame CreateWorld( GameObject parent, Vector3 worldPosition, Vector3 worldAxis )
    {
      return CreateWorld( parent, worldPosition, Quaternion.FromToRotation( Vector3.forward, worldAxis ) );
    }

    /// <summary>
    /// Default constructor with null parent and zero position and identity rotation.
    /// </summary>
    public ConstraintFrame()
    {
    }

    /// <summary>
    /// Construct given parent. The transform of this frame will by default
    /// be at center of the parent (this.Position == parent.transform.position,
    /// this.Rotation == parent.transform.rotation).
    /// </summary>
    /// <param name="parent"></param>
    public ConstraintFrame( GameObject parent )
    {
      SetParent( parent, false );
    }

    /// <summary>
    /// Construct given parent, local position (parent frame) and
    /// local rotation (parent frame)
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="localPosition">Local position in parent frame.</param>
    /// <param name="localRotation">Local rotation in parent frame.</param>
    public ConstraintFrame( GameObject parent, Vector3 localPosition, Quaternion localRotation )
    {
      SetParent( parent );
      LocalPosition = localPosition;
      LocalRotation = localRotation;
    }

    /// <summary>
    /// Construct given parent, local position (parent frame) and
    /// local axis (parent frame)
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="localPosition">Local position in parent frame.</param>
    /// <param name="localAxis">Local axis in parent frame.</param>
    public ConstraintFrame( GameObject parent, Vector3 localPosition, Vector3 localAxis )
      : this( parent, localPosition, Quaternion.FromToRotation( Vector3.forward, localAxis ) )
    {
    }
  }
}
