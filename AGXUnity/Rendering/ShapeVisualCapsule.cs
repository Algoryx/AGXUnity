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
      Mesh mesh = new Mesh();

      mesh = new Mesh();
      mesh.name = "Capsule";

      float radius = Mathf.Max(0, capsule.Radius);
      float height = Mathf.Max(0, capsule.Height);

      agxCollide.MeshData meshData = agxUtil.PrimitiveMeshGenerator.createCapsule( radius, height, m_resolution ).getMeshData();

      mesh.SetVertices( meshData.getVertices().Select( v => v.ToHandedVector3() ).ToArray() );

      int[] triangles = meshData.getIndices().Select(triangle => (int)triangle).ToArray();

      // Flip winding order
      for ( int i = 0; i < triangles.Length; i+=3 )
        (triangles[ i+2 ], triangles[ i+1 ]) = (triangles[ i+1 ], triangles[ i+2 ]);

      mesh.SetTriangles(triangles, 0, calculateBounds: false );

      mesh.RecalculateNormals();

      return mesh;
    }
  }
}
