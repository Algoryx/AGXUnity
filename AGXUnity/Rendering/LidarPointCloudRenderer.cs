using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class LidarPointCloudRenderer : MonoBehaviour
{
  public Mesh QuadMesh; // Assign a quad mesh
  public Color StartColor = Color.red; // Start color for intensity lerp
  public Color EndColor = Color.blue; // End color for intensity lerp
  public float PointSize = 0.02f;

  private int m_pointCount = 0;
  private Material m_pointCloudMaterialInstance; // Instance of the material for this renderer
  private ComputeBuffer m_instanceBuffer; // Stores particle data
  private ComputeBuffer m_argsBuffer; // Stores arguments for the draw call



  struct PointData
  {
    public Vector3 position;
    public float intensity;
  }

  private void Start()
  {
    //m_particleMaterialInstance = Instantiate(particleMaterialTemplate);
    m_pointCloudMaterialInstance = new Material( Resources.Load<Shader>( "Shaders/Built-In/PointCloudShader" ) );
    m_pointCloudMaterialInstance.SetColor("_ColorStart", StartColor);
    m_pointCloudMaterialInstance.SetColor("_ColorEnd", EndColor);
  }

  private void InitializeBuffers(int count)
  {
    if (m_instanceBuffer != null)
      m_instanceBuffer.Release();

    m_pointCount = count;

    // Create instance buffer
    m_instanceBuffer = new ComputeBuffer(m_pointCount, sizeof(float) * 4, ComputeBufferType.Structured);
    PointData[] points = new PointData[m_pointCount];

    // Initialize points with default values (zeroed out)
    for (int i = 0; i < m_pointCount; i++)
    {
      points[i].position = new Vector3();
      points[i].intensity = 0;
    }

    m_instanceBuffer.SetData(points);

    // Create args buffer (indirect draw arguments)
    uint[] args = new uint[5];
    args[0] = (uint)QuadMesh.GetIndexCount(0); // Index count per instance
    args[1] = (uint)m_pointCount; // Number of instances
    args[2] = (uint)QuadMesh.GetIndexStart(0); // Start index location
    args[3] = (uint)QuadMesh.GetBaseVertex(0); // Base vertex location
    args[4] = 0; // Padding

    if (m_argsBuffer != null)
      m_argsBuffer.Release();
    m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    m_argsBuffer.SetData(args);

    // Assign the instance buffer to the material
    m_pointCloudMaterialInstance.SetBuffer("particleBuffer", m_instanceBuffer);
    m_pointCloudMaterialInstance.SetFloat("_PointSize", PointSize);
  }

  public void SetData(agxSensor.RtVec4fView lidarPoints)
  {
    int count = (int)lidarPoints.size();

    if (count == 0)
      return;

    if (count > m_pointCount)
      InitializeBuffers(count);

    PointData[] points = new PointData[m_pointCount];

    for (int i = 0; i < count; i++)
    {
      var point = lidarPoints[i];
      points[i].position = new Vector3(point.x, point.y, point.z);
      points[i].intensity = point.w;
    }

    for (int i = count; i < m_pointCount; i++)
    {
      points[i].position = Vector3.zero;
      points[i].intensity = 0.0f;
    }

    m_instanceBuffer.SetData(points);
  }

  private void LateUpdate()
  {
    if (m_pointCount == 0)
      return;

    m_pointCloudMaterialInstance.SetFloat("_PointSize", PointSize);
    m_pointCloudMaterialInstance.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

    Graphics.DrawMeshInstancedIndirect(
      QuadMesh,
      0,
      m_pointCloudMaterialInstance,
      new Bounds(Vector3.zero, Vector3.one * 50f), // A large enough bounds to enclose all points
      m_argsBuffer
    );
  }

  private void OnDestroy()
  {
    if (m_instanceBuffer != null) m_instanceBuffer.Release();
    if (m_argsBuffer != null) m_argsBuffer.Release();
    if (m_pointCloudMaterialInstance != null) Destroy(m_pointCloudMaterialInstance);
  }
}
