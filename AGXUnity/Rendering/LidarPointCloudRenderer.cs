using AGXUnity.Sensor;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

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
    private int m_preservedDatas = 0;

    /// <summary>
    /// When greater than 0, the prior n timesteps' outputs will be renderered in addition to the current frame's output.
    /// </summary>
    [Tooltip( "When greater than 0, the prior n timesteps' outputs will be renderered in addition to the current frame's output." )]
    [ClampAboveZeroInInspector( true )]
    public int PreserveDataSets
    {
      get => m_preservedDatas;
      set
      {
        var old = m_preservedDatas;
        m_preservedDatas = value;
        if ( value != old && Application.isPlaying )
          ResizeBufferPool();
      }
    }

    private Mesh m_pointMesh;
    private Material m_pointCloudMaterialInstance;
    private ComputeBuffer[] m_instanceBuffers;
    private ComputeBuffer[] m_argsBuffers;
    private MaterialPropertyBlock[] m_propertyBlocks;
    private int m_currentIndex = 0;
    private agx.Vec4f[] m_pointArray;
    private uint[] m_indirectArgs = new uint[5];

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

      // Use quad mesh for rendering
      m_pointMesh = Resources.GetBuiltinResource<Mesh>( "Quad.fbx" );

      try {
        m_pointCloudMaterialInstance = new Material( Resources.Load<Shader>( "Shaders/Built-In/PointCloudShader" ) );
        m_pointCloudMaterialInstance.SetColor( "_ColorStart", LowIntensityColor );
        m_pointCloudMaterialInstance.SetColor( "_ColorEnd", HighIntensityColor );
        m_pointCloudMaterialInstance.SetFloat( "_PointSize", PointSize );
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

      ResizeBufferPool();

      m_output = new LidarOutput
      {
        agxSensor.RtOutput.Field.XYZ_VEC3_F32,
        agxSensor.RtOutput.Field.INTENSITY_F32
      };

      m_sensor.Add( m_output );

      Simulation.Instance.StepCallbacks.PostStepForward += UpdatePoints;

      return true;
    }

    private void ResizeBufferPool()
    {
      var oldInstances = m_instanceBuffers;
      var oldArgs = m_argsBuffers;
      var oldMPBs = m_propertyBlocks;

      m_instanceBuffers = new ComputeBuffer[ PreserveDataSets + 1 ];
      m_argsBuffers = new ComputeBuffer[ PreserveDataSets + 1 ];
      m_propertyBlocks = new MaterialPropertyBlock[ PreserveDataSets + 1 ];

      int oldCount = oldInstances?.Length ?? 0;
      int newCount = PreserveDataSets + 1;

      if ( oldInstances != null ) {
        int i1 = m_currentIndex + oldCount;

        for ( int i2 = 0; i2 < Mathf.Min( oldCount, newCount ); i2++, i1-- ) {
          m_instanceBuffers[ i2 ] = oldInstances[ i1 % oldCount ];
          m_argsBuffers[ i2 ] = oldArgs[ i1 % oldCount ];
          m_propertyBlocks[ i2 ] = oldMPBs[ i1 % oldCount ];
        }
      }

      m_indirectArgs[ 1 ] = 0;
      for ( int i = oldCount; i < newCount; i++ ) {
        m_argsBuffers[ i ] = new ComputeBuffer( 1, m_indirectArgs.Length * sizeof( uint ), ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic );
        m_argsBuffers[ i ].SetData( m_indirectArgs );
        m_propertyBlocks[ i ] = new MaterialPropertyBlock();
      }

      for ( int i = oldCount - newCount; i > 0; i-- ) {
        oldArgs[ i % oldCount ].Release();
        oldInstances[ i % oldCount ].Release();
      }

      m_currentIndex = 0;
    }

    private ComputeBuffer EnsureBuffer( ComputeBuffer current, int count )
    {
      if ( current != null ) {
        if ( current.count > count )
          return current;
        current.Release();
      }

      return new ComputeBuffer( count, sizeof( float ) * 4, ComputeBufferType.Structured, ComputeBufferMode.Dynamic );
    }

    private void UpdatePoints()
    {
      Profiler.BeginSample( "UpdatePoints" );

      m_pointArray = m_output.View<agx.Vec4f>( out uint count, m_pointArray );

      m_instanceBuffers[ m_currentIndex ] = EnsureBuffer( m_instanceBuffers[ m_currentIndex ], Mathf.Max( (int)count, 1 ) );

      m_indirectArgs[ 1 ] = count;

      m_instanceBuffers[ m_currentIndex ].SetData( m_pointArray, 0, 0, (int)count );
      m_argsBuffers[ m_currentIndex ].SetData( m_indirectArgs );

      var mat = transform.localToWorldMatrix;
      m_propertyBlocks[ m_currentIndex ].SetMatrix( "_ObjectToWorld", mat );

      m_currentIndex = ( m_currentIndex + 1 ) % ( PreserveDataSets + 1 );
      Profiler.EndSample();
    }

    protected void Update()
    {
      if ( m_pointArray == null ||  m_pointArray.Count() == 0 )
        return;

      for ( int i = 0; i < PreserveDataSets + 1; i++ ) {
        if ( m_instanceBuffers[ i ] == null )
          continue;
        var mpb = m_propertyBlocks[i];
        mpb.SetBuffer( "pointBuffer", m_instanceBuffers[ i ] );
        Graphics.DrawMeshInstancedIndirect(
          m_pointMesh,
          0,
          m_pointCloudMaterialInstance,
          new Bounds( transform.position, Vector3.one * Mathf.Min( m_sensor.LidarRange.Max * 2f, float.MaxValue ) ),
          m_argsBuffers[ i ],
          0,
          mpb,
          UnityEngine.Rendering.ShadowCastingMode.Off,
          false
        );
      }
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= UpdatePoints;

      if ( m_instanceBuffers != null )
        foreach ( var ib in m_instanceBuffers )
          ib?.Release();
      m_instanceBuffers = null;
      if ( m_argsBuffers != null )
        foreach ( var ab in m_argsBuffers )
          ab?.Release();
      m_argsBuffers = null;
      if ( m_pointCloudMaterialInstance != null ) Destroy( m_pointCloudMaterialInstance );
      base.OnDestroy();
    }
  }
}
