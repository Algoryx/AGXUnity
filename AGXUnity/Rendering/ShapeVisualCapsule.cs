using AGXUnity.Utils;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual for shape type Capsule.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisualCapsule : ShapeVisual
  {
    private Mesh m_mesh = null;

    private const float m_resolution = 1;

    /// <summary>
    /// Callback when constructed.
    /// </summary>
    protected override void OnConstruct()
    {
      gameObject.AddComponent<MeshFilter>();
      gameObject.AddComponent<MeshRenderer>();

      m_mesh = GenerateMesh( Shape );
      gameObject.GetComponent<MeshFilter>().sharedMesh = m_mesh;
    }

    /// <summary>
    /// Callback from Shape when its size has been changed.
    /// </summary>
    public override void OnSizeUpdated()
    {
      transform.localScale = GetUnscaledScale();

      m_mesh = GenerateMesh( Shape );
      gameObject.GetComponent<MeshFilter>().sharedMesh = m_mesh;
    }

    /// <summary>
    /// Generates custom shape mesh
    /// </summary>
    public static Mesh GenerateMesh( Collide.Shape shape )
    {
      Collide.Capsule capsule = shape as Collide.Capsule;
      return CapsuleShapeUtils.CreateCapsuleMesh( capsule.Radius, capsule.Height, m_resolution );
    }
  }
}
