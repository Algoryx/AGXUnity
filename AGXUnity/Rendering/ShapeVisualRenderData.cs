using System;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual with given mesh data and material.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisualRenderData : ShapeVisual
  {
    /// <summary>
    /// Callback when shape size has been changed.
    /// </summary>
    public override void OnSizeUpdated()
    {
      // We don't do anything here since we support any type of scale of the meshes.
    }

    protected override void OnConstruct()
    {
      gameObject.AddComponent<MeshFilter>();
      gameObject.AddComponent<MeshRenderer>();
    }
  }
}
