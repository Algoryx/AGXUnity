﻿using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Infinite plane object - probably not completely working.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Plane" )]
  public sealed class Plane : Shape
  {
    /// <summary>
    /// Returns native plane if created.
    /// </summary>
    public agxCollide.Plane Native { get { return m_shape as agxCollide.Plane; } }

    /// <summary>
    /// Debug rendering scale is one since size isn't a thing for planes.
    /// </summary>
    public override Vector3 GetScale()
    {
      return new Vector3( 1, 1, 1 );
    }

    /// <summary>
    /// Creates native plane object given current transform up vector.
    /// </summary>
    /// <returns></returns>
    protected override agxCollide.Shape CreateNative()
    {
      return new agxCollide.Plane( transform.up.ToHandedVec3(), 0 );
    }
  }
}
