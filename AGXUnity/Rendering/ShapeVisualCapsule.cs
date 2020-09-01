﻿using System;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual for shape type Capsule.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  public class ShapeVisualCapsule : ShapeVisual
  {
    /// <summary>
    /// Capsule visual is three game objects (2 x half sphere + 1 x cylinder),
    /// the size has to be updated to all of the children.
    /// </summary>
    public override void OnSizeUpdated()
    {
      ShapeDebugRenderData.SetCapsuleSize( gameObject,
                                           ( Shape as Collide.Capsule ).Radius,
                                           ( Shape as Collide.Capsule ).Height );
    }
  }
}
