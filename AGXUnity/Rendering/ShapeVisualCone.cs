using System;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual for shape type Cone.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisualCone : ShapeVisual
  {
    private Mesh m_mesh = null;

    private const int m_resolution = 40;

    /// <summary>
    /// Callback when constructed.
    /// </summary>
    protected override void OnConstruct()
    {
      gameObject.AddComponent<MeshFilter>();
      gameObject.AddComponent<MeshRenderer>();

      m_mesh = GenerateMesh(Shape);
      gameObject.GetComponent<MeshFilter>().sharedMesh = m_mesh;
    }

    /// <summary>
    /// Callback from Shape when its size has been changed.
    /// </summary>
    public override void OnSizeUpdated()
    {
      transform.localScale = GetUnscaledScale();

      m_mesh = GenerateMesh(Shape);
      gameObject.GetComponent<MeshFilter>().sharedMesh = m_mesh;
    }

    /// <summary>
    /// Generates custom shape mesh
    /// </summary>
    public static Mesh GenerateMesh(Collide.Shape shape)
    {
      Collide.Cone cone = shape as Collide.Cone;
      Mesh mesh = new Mesh();

      mesh = new Mesh();

      float topRadius = Mathf.Max(0, cone.TopRadius);
      float bottomRadius = Mathf.Max(0, cone.BottomRadius);
      float height = Mathf.Max(0, cone.Height);

      int triangleCount = m_resolution * 4;
      int vertexCount = m_resolution * 6;

      //Vector2[] uvs = new Vector2[vertexCount];
      Vector3[] vertices = new Vector3[vertexCount];
      Vector3[] normals = new Vector3[vertexCount];
      int[] triangles = new int[triangleCount * 3];

      float externalNormalY = (bottomRadius - topRadius) / height;
      

      // Loop over each "pie slice" of the cone. Create vertices on one side, six per slice with two on each point where side meets top/bottom, triangles arranged looping around the slice
      for (int i = 0; i < m_resolution; i++)
      {
        int currentVertex = i * 6;
        int nextCurrentVertex = ((i + 1) % m_resolution) * 6;

        float angle = i * Mathf.PI * 2 / m_resolution;
        float sinI = Mathf.Sin(angle);
        float cosI = Mathf.Cos(angle);

        vertices[currentVertex]     = new Vector3(0, height, 0);
        vertices[currentVertex + 1] = vertices[currentVertex + 2] = new Vector3(sinI * topRadius, height, cosI * topRadius);
        vertices[currentVertex + 3] = vertices[currentVertex + 4] = new Vector3(sinI * bottomRadius, 0, cosI * bottomRadius);
        vertices[currentVertex + 5] = new Vector3(0, 0, 0);

        normals[currentVertex]     = normals[currentVertex + 1] = new Vector3(0, 1, 0);
        normals[currentVertex + 2] = normals[currentVertex + 3] = new Vector3(sinI, externalNormalY, cosI).normalized;
        normals[currentVertex + 4] = normals[currentVertex + 5] = new Vector3(0, -1, 0); ;

        int currentTriangle = i * 4 * 3;

        triangles[currentTriangle]      = currentVertex;
        triangles[currentTriangle + 1]  = currentVertex + 1;
        triangles[currentTriangle + 2]  = nextCurrentVertex + 1;

        triangles[currentTriangle + 3]  = currentVertex + 2;
        triangles[currentTriangle + 4]  = currentVertex + 3;
        triangles[currentTriangle + 5]  = nextCurrentVertex + 3;

        triangles[currentTriangle + 6]  = currentVertex + 2;
        triangles[currentTriangle + 7]  = nextCurrentVertex + 3;
        triangles[currentTriangle + 8]  = nextCurrentVertex + 2;

        triangles[currentTriangle + 9]  = currentVertex + 4;
        triangles[currentTriangle + 11] = nextCurrentVertex + 4;
        triangles[currentTriangle + 10] = currentVertex + 5;
      }

      mesh.vertices = vertices;
      //m_mesh.uv = uvs; // TODO, possibly
      mesh.triangles = triangles;
      mesh.normals = normals;

      return mesh;
    }
  }
}
