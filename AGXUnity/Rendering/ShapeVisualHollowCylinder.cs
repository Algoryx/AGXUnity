using System;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual for shape type HollowCylinder.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisualHollowCylinder : ShapeVisual
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
      Collide.HollowCylinder hollowCylinder = shape as Collide.HollowCylinder;
      Mesh mesh = new Mesh();

      float outerRadius = Mathf.Max(0, hollowCylinder.Radius);
      float innerRadius = Mathf.Max(0, hollowCylinder.Radius - hollowCylinder.Thickness);
      float halfHeight = Mathf.Max(0, hollowCylinder.Height) / 2f;

      int triangleCount = m_resolution * 8;
      int vertexCount = m_resolution * 8;

      //Vector2[] uvs = new Vector2[vertexCount];
      Vector3[] vertices = new Vector3[vertexCount];
      Vector3[] normals = new Vector3[vertexCount];
      int[] triangles = new int[triangleCount * 3];

      // Loop over each "pie slice" of the hollow cylinder. Vertices two per point of the slice, triangles also arranged looping around the slice
      for (int i = 0; i < m_resolution; i++)
      {
        int currentVertex = i * 8;
        int nextCurrentVertex = ((i + 1) % m_resolution) * 8;

        float angle = i * Mathf.PI * 2 / m_resolution;
        float sinI = Mathf.Sin(angle);
        float cosI = Mathf.Cos(angle);

        vertices[currentVertex]     = vertices[currentVertex + 7] = new Vector3(sinI * innerRadius, halfHeight, cosI * innerRadius);
        vertices[currentVertex + 1] = vertices[currentVertex + 2] = new Vector3(sinI * outerRadius, halfHeight, cosI * outerRadius);
        vertices[currentVertex + 3] = vertices[currentVertex + 4] = new Vector3(sinI * outerRadius, -halfHeight, cosI * outerRadius);
        vertices[currentVertex + 5] = vertices[currentVertex + 6] = new Vector3(sinI * innerRadius, -halfHeight, cosI * innerRadius);

        normals[currentVertex]     = normals[currentVertex + 1] = new Vector3(0, 1, 0);
        normals[currentVertex + 2] = normals[currentVertex + 3] = new Vector3(sinI, 0, cosI);
        normals[currentVertex + 4] = normals[currentVertex + 5] = new Vector3(0, -1, 0);
        normals[currentVertex + 6] = normals[currentVertex + 7] = new Vector3(-sinI, 0, -cosI);

        for (int j = 0; j < 8; j += 2)
        {
          int currentTriangle = i * 4 * 6 + j * 3;
          int nextJ = (j + 1) % 8;

          triangles[currentTriangle]     = currentVertex + j;
          triangles[currentTriangle + 1] = currentVertex + nextJ;
          triangles[currentTriangle + 2] = nextCurrentVertex + nextJ;

          triangles[currentTriangle + 3] = currentVertex + j;
          triangles[currentTriangle + 4] = nextCurrentVertex + nextJ;
          triangles[currentTriangle + 5] = nextCurrentVertex + j;
        }
      }

      mesh.vertices = vertices;
      //m_mesh.uv = uvs; // TODO, possibly
      mesh.triangles = triangles;
      mesh.normals = normals;

      return mesh;
    }
  }
}
