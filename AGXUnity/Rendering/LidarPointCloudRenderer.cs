
using AGXUnity.Sensor;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Rendering/Lidar Point Cloud Renderer" )]
  [RequireComponent( typeof( LidarSensor ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class LidarPointCloudRenderer : ScriptComponent
  {
    [SerializeField]
    private Color m_lowIntensityColor = new Color(0.8f, 0.5f, 0); // Orange

    /// <summary>
    /// The color used to represent lidar points with low intensity
    /// </summary>
    [Tooltip( "The color used to represent lidar points with low intensity" )]
    public Color LowIntensityColor
    {
      get => m_lowIntensityColor;
      set
      {
        m_lowIntensityColor = value;
        if ( m_pointCloudMaterialInstance != null )
          m_pointCloudMaterialInstance.SetColor( "_ColorStart", m_lowIntensityColor );
      }
    }

    [SerializeField]
    private Color m_highIntensityColor = new Color(0.8f, 0.1f, 0); // Dark red

    /// <summary>
    /// The color used to represent lidar points with high intensity
    /// </summary>
    [Tooltip( "The color used to represent lidar points with high intensity" )]
    public Color HighIntensityColor
    {
      get => m_highIntensityColor;
      set
      {
        m_highIntensityColor = value;
        if ( m_pointCloudMaterialInstance != null )
          m_pointCloudMaterialInstance.SetColor( "_ColorEnd", m_highIntensityColor );
      }
    }

    [SerializeField]
    private float m_pointSize = 0.02f;

    /// <summary>
    /// The size of the rendered lidar points
    /// </summary>
    [ClampAboveZeroInInspector]
    [Tooltip( "The size of the rendered lidar points" )]
    public float PointSize
    {
      get => m_pointSize;
      set
      {
        m_pointSize = value;
        if ( m_pointCloudMaterialInstance != null )
          m_pointCloudMaterialInstance.SetFloat( "_PointSize", m_pointSize );
      }
    }

    [SerializeField]
    private float m_decayTime = 0.2f;

    /// <summary>
    /// Specifies the time that points should be visible for before they disappear, they will gradually fade over this time.
    /// </summary>
    [Tooltip( "Specifies the time that points should be visible for before they disappear, they will gradually fade over this time." )]
    [ClampAboveZeroInInspector( false )]
    public float DecayTime
    {
      get => m_decayTime;
      set
      {
        m_decayTime = value;
        if ( m_pointCloudMaterialInstance != null )
          m_pointCloudMaterialInstance.SetFloat( "_DecayTime", m_decayTime );
      }
    }

    private Mesh m_pointMesh;
    private Material m_pointCloudMaterialInstance;
    private ComputeShader m_pointcloudCompute;

    private ComputeBuffer m_pointBuffer;
    private ComputeBuffer m_ttlBuffer;
    private ComputeBuffer m_deadBuffer;
    private ComputeBuffer m_deadIndexBuffer;
    private ComputeBuffer m_insertionBuffer;

    private List<ComputeBuffer> m_deferredDeletion = new List<ComputeBuffer>();

    private ComputeBuffer m_argsBuffer;
    private MaterialPropertyBlock m_propertyBlock;
    private agx.Vec4f[] m_pointArray;
    private uint[] m_indirectArgs = new uint[5];
    private uint m_numPoints = 0;

    private List<(float,uint)> m_activePoints = new List<(float, uint)>();

    private LidarSensor m_sensor;
    private LidarOutput m_output;

    struct PointData
    {
      public Vector3 position;
      public float intensity;
    }

    protected override bool Initialize()
    {
      m_sensor = GetComponent<LidarSensor>().GetInitialized();

      m_pointcloudCompute = Resources.Load<ComputeShader>( "Shaders/Compute/PointCloud" );

      // Use quad mesh for rendering
      m_pointMesh = Resources.GetBuiltinResource<Mesh>( "Quad.fbx" );

      try {
        m_pointCloudMaterialInstance = new Material( Resources.Load<Shader>( "Shaders/Built-In/PointCloudShader" ) );
        m_pointCloudMaterialInstance.SetColor( "_ColorStart", LowIntensityColor );
        m_pointCloudMaterialInstance.SetColor( "_ColorEnd", HighIntensityColor );
        m_pointCloudMaterialInstance.SetFloat( "_PointSize", PointSize );
        m_pointCloudMaterialInstance.SetFloat( "_DecayTime", DecayTime );
      }
      catch {
        Debug.LogError( "Couldn't load point cloud material!" );
        return false;
      }

      m_indirectArgs[ 0 ] = (uint)m_pointMesh.GetIndexCount( 0 ); // Index count per instance
      m_indirectArgs[ 1 ] = (uint)0; // Number of instances
      m_indirectArgs[ 2 ] = (uint)m_pointMesh.GetIndexStart( 0 ); // Start index location
      m_indirectArgs[ 3 ] = (uint)m_pointMesh.GetBaseVertex( 0 ); // Base vertex location
      m_indirectArgs[ 4 ] = 0; // Padding

      m_deadIndexBuffer = new ComputeBuffer( 1, sizeof( int ) );
      m_argsBuffer = new ComputeBuffer( 1, 5 * sizeof( int ), ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic );
      m_propertyBlock = new MaterialPropertyBlock();

      EnsureBuffers( 16384 ); // Start out at 2^14 points

      m_output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };

      m_sensor.Add( m_output );

      return true;
    }

    private void EnsureBuffers( uint numPoints )
    {
      if ( m_numPoints >= numPoints ) return;

      int newPointCount = Mathf.NextPowerOfTwo((int)numPoints);

      if ( m_numPoints != 0 ) {
        m_insertionBuffer.Release();
        m_deadBuffer.Release();
      }

      var oldPoints = m_pointBuffer;
      var oldTTL = m_ttlBuffer;

      m_pointBuffer = new ComputeBuffer( newPointCount, 4 * sizeof( float ), ComputeBufferType.Structured );
      m_insertionBuffer = new ComputeBuffer( newPointCount, 4 * sizeof( float ), ComputeBufferType.Structured );
      m_deadBuffer = new ComputeBuffer( newPointCount, sizeof( int ), ComputeBufferType.Structured );
      m_ttlBuffer = new ComputeBuffer( newPointCount, sizeof( float ), ComputeBufferType.Structured );

      // Setup initial ttl data
      var kernel = m_pointcloudCompute.FindKernel("Initialize");

      m_pointcloudCompute.GetKernelThreadGroupSizes( kernel, out uint x, out _, out _ );
      m_pointcloudCompute.SetInt( "numPoints", (int)numPoints );
      m_pointcloudCompute.SetBuffer( kernel, "ttls", m_ttlBuffer );

      m_pointcloudCompute.Dispatch( kernel, (int)( numPoints / x ) + 1, 1, 1 );

      if ( m_numPoints != 0 ) {
        // Copy data from old buffers
        kernel = m_pointcloudCompute.FindKernel( "Copy" );
        m_pointcloudCompute.GetKernelThreadGroupSizes( kernel, out x, out _, out _ );

        m_pointcloudCompute.SetBuffer( kernel, "pointCloud", m_pointBuffer );
        m_pointcloudCompute.SetBuffer( kernel, "oldPointCloud", oldPoints );
        m_pointcloudCompute.SetBuffer( kernel, "ttls", m_ttlBuffer );
        m_pointcloudCompute.SetBuffer( kernel, "oldttls", oldTTL );
        m_pointcloudCompute.SetInt( "numPoints", (int)m_numPoints );

        m_pointcloudCompute.Dispatch( kernel, (int)( m_numPoints / x ) + 1, 1, 1 );

        m_deferredDeletion.Add( oldTTL );
        m_deferredDeletion.Add( oldPoints );
      }

      m_numPoints = numPoints;
    }

    private void UpdatePoints()
    {
      Profiler.BeginSample( "UpdatePoints" );
      foreach ( var b in m_deferredDeletion )
        b.Release();
      m_deferredDeletion.Clear();

      var dt = (float)Simulation.Instance.Native.getTimeStep();

      m_pointArray = m_output.View<agx.Vec4f>( out uint count, m_pointArray );

      m_activePoints = m_activePoints
        .Select( count => (count.Item1-dt, count.Item2) )
        .Where( count => count.Item1 >= 0 )
        .Append( (DecayTime, count) )
        .ToList();

      uint totalCount = (uint)m_activePoints.Sum(count => count.Item2);

      EnsureBuffers( totalCount );

      m_deadIndexBuffer.SetData( new int[] { 0 } );

      // Update points
      var kernel = m_pointcloudCompute.FindKernel( "Update" );
      m_pointcloudCompute.GetKernelThreadGroupSizes( kernel, out uint x, out _, out _ );

      m_pointcloudCompute.SetBuffer( kernel, "ttls", m_ttlBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "deadPoints", m_deadBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "deadIndex", m_deadIndexBuffer );
      m_pointcloudCompute.SetInt( "numPoints", (int)m_numPoints );
      m_pointcloudCompute.SetFloat( "dt", dt );

      m_pointcloudCompute.Dispatch( kernel, (int)( m_numPoints / x ) + 1, 1, 1 );

      // Insert new points
      m_insertionBuffer.SetData( m_pointArray, 0, 0, (int)count );

      kernel = m_pointcloudCompute.FindKernel( "Insert" );
      m_pointcloudCompute.GetKernelThreadGroupSizes( kernel, out x, out _, out _ );

      m_pointcloudCompute.SetBuffer( kernel, "deadPoints", m_deadBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "pointCloud", m_pointBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "ttls", m_ttlBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "newPoints", m_insertionBuffer );
      m_pointcloudCompute.SetBuffer( kernel, "deadIndex", m_deadIndexBuffer );
      m_pointcloudCompute.SetMatrix( "sensorToWorld", m_sensor.GlobalTransform );
      m_pointcloudCompute.SetFloat( "decayTime", DecayTime );
      m_pointcloudCompute.SetInt( "numPoints", (int)count );

      m_pointcloudCompute.Dispatch( kernel, (int)( count / x ) + 1, 1, 1 );

      m_indirectArgs[ 1 ] = m_numPoints;
      m_argsBuffer.SetData( m_indirectArgs );

      Profiler.EndSample();
    }

    private void SRPRender( ScriptableRenderContext _, Camera cam ) => Render( cam );

    private void Render( Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      if ( m_pointArray == null || m_pointArray.Count() == 0 )
        return;

      m_propertyBlock.SetBuffer( "pointBuffer", m_pointBuffer );
      m_propertyBlock.SetBuffer( "ttls", m_ttlBuffer );
      var range = m_sensor.Native.getModel().getRayRange().getRange();
      Graphics.DrawMeshInstancedIndirect(
        m_pointMesh,
        0,
        m_pointCloudMaterialInstance,
        new Bounds( transform.position, Vector3.one * Mathf.Min( range.upper() * 2f, float.MaxValue ) ),
        m_argsBuffer,
        0,
        m_propertyBlock,
        UnityEngine.Rendering.ShadowCastingMode.Off,
        false,
        gameObject.layer,
        cam
      );
    }

    protected override void OnEnable()
    {
      // We hook into the rendering process to render even when the application is paused.
      // For the Built-in render pipeline this is done by adding a callback to the Camera.OnPreCull event which is called for each camera in the scene.
      // For SRPs such as URP and HDRP the beginCameraRendering event serves a similar purpose.
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      RenderPipelineManager.beginCameraRendering += SRPRender;
      Camera.onPreCull -= Render;
      Camera.onPreCull += Render;
      Simulation.Instance.StepCallbacks.PostStepForward += UpdatePoints;
    }

    protected override void OnDisable()
    {
      Camera.onPreCull -= Render;
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= UpdatePoints;
    }

    protected override void OnDestroy()
    {
      if ( m_pointBuffer != null ) m_pointBuffer.Release();
      if ( m_deadBuffer != null ) m_deadBuffer.Release();
      if ( m_insertionBuffer != null ) m_insertionBuffer.Release();
      if ( m_ttlBuffer != null ) m_ttlBuffer.Release();
      if ( m_deadIndexBuffer != null ) m_deadIndexBuffer.Release();
      if ( m_argsBuffer != null ) m_argsBuffer.Release();

      foreach ( var buffer in m_deferredDeletion )
        buffer.Release();
      m_deferredDeletion.Clear();

      m_argsBuffer = null;
      if ( m_pointCloudMaterialInstance != null ) Destroy( m_pointCloudMaterialInstance );
      base.OnDestroy();
    }
  }
}
