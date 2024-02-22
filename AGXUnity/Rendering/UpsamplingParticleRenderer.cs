using agxTerrain;
using AGXUnity.Model;
using AGXUnity.Utils;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using static agx.agxSWIG.UnityHelpers;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Upsampling Particle Renderer" )]
  [RequireComponent( typeof( DeformableTerrainBase ) )]
  public class UpsamplingParticleRenderer : ScriptComponent
  {
    private static float PACKING_RATIO = 0.67f;
    private static int INITIAL_VOXEL_BUFFER_SIZE = 128;
    private static int INITIAL_COARSE_PARTICLE_BUFFER_SIZE = 1024;
    // TODO: Make buffer size dynamically increasing based on particle mass
    private static int PARTICLE_BUFFER_SIZE = 8388608*8;
    private static int FINE_PARTICLE_SIZE_BYTES = 32;

    [StructLayout( LayoutKind.Explicit, Size = 112 )]
    struct VoxelEntry
    {
      [FieldOffset(0)] public Vector3Int index;
      [FieldOffset(12)] public int room;
      [FieldOffset(16)] public Vector3 position;
      [FieldOffset(28)] public float originalMass;
      [FieldOffset(32)] public Vector3 velocity;
      //4 bytes padding
      [FieldOffset(48)] public Vector3 minBound;
      //4 bytes padding
      [FieldOffset(64)] public Vector3 maxBound;
      //4 bytes padding
      [FieldOffset(48)] public Vector3 innerMinBound;
      //4 bytes padding
      [FieldOffset(64)] public Vector3 innerMaxBound;
      //4 bytes padding
    }

    [StructLayout( LayoutKind.Explicit, Size = 32 )]
    struct CoarseParticle
    {
      [FieldOffset(0)] public Vector3 position;
      [FieldOffset(12)] public float radius;
      [FieldOffset(16)] public Vector3 velocity;
      [FieldOffset(28)] public float mass;
    }

    [StructLayout( LayoutKind.Explicit, Size = 16 )]
    struct VoxelIndex
    {
      [FieldOffset(0)] public Vector3Int index;
      ////4 bytes padding
    }

    public enum ParticleRenderMode
    {
      Impostor,
      Mesh
    };

    [Range(1.0f, 5000.0f)]
    [SerializeField]
    private float m_upscaling = 100;

    public float Upscaling
    {
      get { return m_upscaling; }
      set
      {
        m_upscaling = value;
        if ( State == States.INITIALIZED )
          RecalculateFineParticleProperties();
      }
    }

    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public OptionalOverrideValue<float> VoxelSize { get; set; } = new OptionalOverrideValue<float>( 0.5f );

    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public float EaseStepSize { get; set; } = 0.1f;

    [field: SerializeField]
    public ParticleRenderMode RenderMode { get; set; } = ParticleRenderMode.Impostor;

    [HideInInspector]
    public float FineParticleMass { get; private set; }
    [HideInInspector]
    public float FineParticleRadius { get; private set; }
    [HideInInspector]
    public Color color1 = Color.white;
    [HideInInspector]
    public Color color2 = Color.white;

    [HideInInspector]
    public Mesh GranuleMesh;
    [HideInInspector]
    public Material GranuleMaterial;

    private ComputeBuffer m_hashTable;
    private ComputeBuffer m_hashTableOccupancy;
    private ComputeBuffer m_activeVoxelBuffer;
    private ComputeBuffer m_coarseParticlesBuffer;
    private ComputeBuffer m_fineParticlesBuffer;
    private ComputeBuffer m_fineParticlesSwapBuffer;
    private ComputeBuffer m_dispatchArgs;
    private ComputeShader m_particleUpsamplingShader;
    private ComputeShader m_moveParticlesShader;

    private agx.Vec3i[] m_activeVoxelIndices = new agx.Vec3i[INITIAL_VOXEL_BUFFER_SIZE];
    private VoxelIndex[] m_activeVoxelIndicesUnity = new VoxelIndex[INITIAL_VOXEL_BUFFER_SIZE];
    private GranularData[] m_coarseParticles = new GranularData[INITIAL_COARSE_PARTICLE_BUFFER_SIZE];

    // Draw args contains index count, instance count (doubles as counter for number of fine particles), index start, base vertex, start instance location.
    // An extra element is added to the end and used as the temporary counter "NEW_NUM_FINE_PARTICLES" in the stream compaction kernels. Easier than having an extra buffer
    private ComputeBuffer m_drawCallArgsBuffer = null;

    private ParticleRenderMode m_persistedRenderMode = ParticleRenderMode.Impostor;
    private Mesh m_quadMesh;
    private Material m_impostorMaterial;

    private bool m_nominalRadiusFetched = false;
    private float m_nominalRadiusCache;
    private int m_numCoarseParticles = 0;
    private int m_numActiveVoxels = 0;
    private uint m_groupSizeVoxels;
    private uint m_groupSizeZeroing;

    private DeformableTerrainBase m_particleProvider = null;
    [HideInInspector]
    public DeformableTerrainBase ParticleProvider
    {
      get
      {
        if ( m_particleProvider == null )
          m_particleProvider = GetComponent<DeformableTerrainBase>();
        return m_particleProvider;
      }
    }

    private TerrainMaterial.ParticleProperties GetParticleProperties()
    {
      if ( ParticleProvider is DeformableTerrain a )
        return a.Native.getTerrainMaterial().getParticleProperties();
      else if ( ParticleProvider is MovableTerrain b )
        return b.Native.getTerrainMaterial().getParticleProperties();
      else if ( ParticleProvider is DeformableTerrainPager c )
        return c.Native.getTemplateTerrain().getTerrainMaterial().getParticleProperties();
      else
        return null;
    }

    private void RecalculateFineParticleProperties()
    {
      if ( ParticleProvider != null ) {
        m_nominalRadiusFetched = true;
        float nominalRadius = Mathf.Pow(3.0f * PACKING_RATIO / (4.0f * Mathf.PI), 1.0f / 3.0f) * ParticleProvider.ElementSize;
        m_nominalRadiusCache = nominalRadius;
        FineParticleRadius = nominalRadius / Mathf.Pow( Upscaling, 1.0f / 3.0f );

        var particleProperties = GetParticleProperties();
        var nominalMass = (float)particleProperties.getParticleDensity() * 4 / 3 * Mathf.PI * Mathf.Pow(nominalRadius, 3);
        FineParticleMass = nominalMass / Upscaling;
      }
    }

    private void SetStaticBuffers()
    {
      int updateGrid = m_particleUpsamplingShader.FindKernel("UpdateGrid");
      int applyParticleMass = m_particleUpsamplingShader.FindKernel("ApplyParticleMass");
      int compactFineParticles = m_particleUpsamplingShader.FindKernel("CompactFineParticles");
      int swapParticleBuffers = m_particleUpsamplingShader.FindKernel("SwapParticleBuffers");
      int spawnParticles = m_particleUpsamplingShader.FindKernel("SpawnParticles");
      int clearTable = m_particleUpsamplingShader.FindKernel("ClearTable");

      m_particleUpsamplingShader.GetKernelThreadGroupSizes( updateGrid, out m_groupSizeVoxels, out _, out _ );
      m_particleUpsamplingShader.GetKernelThreadGroupSizes( clearTable, out m_groupSizeZeroing, out _, out _ );

      int moveParticles = m_moveParticlesShader.FindKernel("MoveParticles");

      m_particleUpsamplingShader.SetBuffer( updateGrid, "drawCallArgs", m_drawCallArgsBuffer );
      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "drawCallArgs", m_drawCallArgsBuffer );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "drawCallArgs", m_drawCallArgsBuffer );
      m_particleUpsamplingShader.SetBuffer( swapParticleBuffers, "drawCallArgs", m_drawCallArgsBuffer );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "drawCallArgs", m_drawCallArgsBuffer );

      m_moveParticlesShader.SetBuffer( moveParticles, "drawCallArgs", m_drawCallArgsBuffer );

      m_particleUpsamplingShader.SetBuffer( updateGrid, "fineParticles", m_fineParticlesBuffer );
      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "fineParticles", m_fineParticlesBuffer );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "fineParticles", m_fineParticlesBuffer );
      m_particleUpsamplingShader.SetBuffer( swapParticleBuffers, "fineParticles", m_fineParticlesBuffer );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "fineParticles", m_fineParticlesBuffer );

      m_moveParticlesShader.SetBuffer( moveParticles, "fineParticles", m_fineParticlesBuffer );

      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "fineParticlesNew", m_fineParticlesSwapBuffer );
      m_particleUpsamplingShader.SetBuffer( swapParticleBuffers, "fineParticlesNew", m_fineParticlesSwapBuffer );

      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "dispatchArgs", m_dispatchArgs );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "dispatchArgs", m_dispatchArgs );
      m_particleUpsamplingShader.SetBuffer( swapParticleBuffers, "dispatchArgs", m_dispatchArgs );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "dispatchArgs", m_dispatchArgs );

      m_moveParticlesShader.SetBuffer( moveParticles, "dispatchArgs", m_dispatchArgs );
    }

    private void SetDynamicBuffers()
    {
      int updateGrid = m_particleUpsamplingShader.FindKernel("UpdateGrid");
      int applyParticleMass = m_particleUpsamplingShader.FindKernel("ApplyParticleMass");
      int compactFineParticles = m_particleUpsamplingShader.FindKernel("CompactFineParticles");
      int spawnParticles = m_particleUpsamplingShader.FindKernel("SpawnParticles");
      int clearTable = m_particleUpsamplingShader.FindKernel("ClearTable");

      int moveParticles = m_moveParticlesShader.FindKernel("MoveParticles");

      m_particleUpsamplingShader.SetBuffer( updateGrid, "activeVoxelIndices", m_activeVoxelBuffer );
      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "activeVoxelIndices", m_activeVoxelBuffer );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "activeVoxelIndices", m_activeVoxelBuffer );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "activeVoxelIndices", m_activeVoxelBuffer );

      m_particleUpsamplingShader.SetBuffer( updateGrid, "coarseParticles", m_coarseParticlesBuffer );

      m_particleUpsamplingShader.SetBuffer( updateGrid, "hashTableBuffer", m_hashTable );
      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "hashTableBuffer", m_hashTable );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "hashTableBuffer", m_hashTable );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "hashTableBuffer", m_hashTable );
      m_particleUpsamplingShader.SetBuffer( clearTable, "hashTableBuffer", m_hashTable );

      m_particleUpsamplingShader.SetBuffer( updateGrid, "hashTableOccupancy", m_hashTableOccupancy );
      m_particleUpsamplingShader.SetBuffer( applyParticleMass, "hashTableOccupancy", m_hashTableOccupancy );
      m_particleUpsamplingShader.SetBuffer( compactFineParticles, "hashTableOccupancy", m_hashTableOccupancy );
      m_particleUpsamplingShader.SetBuffer( spawnParticles, "hashTableOccupancy", m_hashTableOccupancy );
      m_particleUpsamplingShader.SetBuffer( clearTable, "hashTableOccupancy", m_hashTableOccupancy );

      m_moveParticlesShader.SetBuffer( moveParticles, "hashTableOccupancy", m_hashTableOccupancy );
      m_moveParticlesShader.SetBuffer( moveParticles, "hashTableBuffer", m_hashTable );

      m_particleUpsamplingShader.SetInt( "tableSize", m_hashTable.count );
      m_moveParticlesShader.SetInt( "tableSize", m_hashTable.count );
    }

    protected override bool Initialize()
    {
      m_particleUpsamplingShader = Resources.Load<ComputeShader>( "Shaders/Compute/ParticleUpsample" );
      m_moveParticlesShader = Resources.Load<ComputeShader>( "Shaders/Compute/MoveParticles" );
      m_impostorMaterial = new Material( Resources.Load<Shader>( "Shaders/Built-In/ParticleImpostor" ) );

      var RP = RenderingUtils.DetectPipeline();
      if ( GranuleMaterial == null ) {
        if ( RP == RenderingUtils.PipelineType.BuiltIn )
          GranuleMaterial = new Material( Resources.Load<Shader>( "Shaders/Built-In/UpsampledParticle" ) );
        else
          GranuleMaterial = new Material( Resources.Load<Shader>( "Shaders/Instanced Terrain Particle" ) );
      }
      if ( !GranuleMaterial.SupportsPipeline( RP ) )
        Debug.LogError( "The selected granule material does not support the currently active Rendering Pipeline.Rendering might be incorrect.", this );

      if ( GranuleMesh == null )
        GranuleMesh = Resources.Load<Mesh>( "Debug/Models/Icosahedron" );

      ParticleProvider.GetInitialized();
      if ( ParticleProvider == null ) {
        Debug.LogError( "DeformableTerrainParticleRenderer parent game object '" + gameObject.name + "' has no particle provider!" );
        return false;
      }

      RecalculateFineParticleProperties();

      m_drawCallArgsBuffer = new ComputeBuffer( 1, 6 * sizeof( uint ), ComputeBufferType.IndirectArguments );
      m_dispatchArgs = new ComputeBuffer( 1, 3 * sizeof( int ), ComputeBufferType.IndirectArguments );
      m_dispatchArgs.SetData( new int[] { 1, 1, 1 } );
      m_activeVoxelBuffer = new ComputeBuffer( INITIAL_VOXEL_BUFFER_SIZE, Marshal.SizeOf( typeof( VoxelIndex ) ) );
      m_coarseParticlesBuffer = new ComputeBuffer( INITIAL_COARSE_PARTICLE_BUFFER_SIZE, Marshal.SizeOf( typeof( GranularData ) ) );
      m_fineParticlesBuffer = new ComputeBuffer( PARTICLE_BUFFER_SIZE, FINE_PARTICLE_SIZE_BYTES );
      m_fineParticlesSwapBuffer = new ComputeBuffer( PARTICLE_BUFFER_SIZE, FINE_PARTICLE_SIZE_BYTES );
      //Hash Table buffers
      m_hashTable = new ComputeBuffer( INITIAL_VOXEL_BUFFER_SIZE * 2, Marshal.SizeOf( typeof( VoxelEntry ) ) );
      m_hashTableOccupancy = new ComputeBuffer( INITIAL_VOXEL_BUFFER_SIZE * 2, Marshal.SizeOf( typeof( uint ) ) );

      SetStaticBuffers();
      SetDynamicBuffers();

      int clearTable = m_particleUpsamplingShader.FindKernel("ClearTable");

      m_particleUpsamplingShader.Dispatch( clearTable, m_hashTable.count / (int)m_groupSizeZeroing, 1, 1 );

      m_quadMesh = new Mesh();

      m_quadMesh.vertices = new Vector3[ 4 ] {
        new Vector3(-0.5f, -0.5f, 0),
        new Vector3(-0.5f, 0.5f, 0),
        new Vector3(0.5f, 0.5f, 0),
        new Vector3(0.5f, -0.5f, 0)
      };

      m_quadMesh.triangles = new int[ 6 ] {
        0,3,1,
        3,2,1
      };

      m_drawCallArgsBuffer.SetData( new uint[ 6 ] { m_quadMesh.GetIndexCount( 0 ), 0, m_quadMesh.GetIndexStart( 0 ), m_quadMesh.GetBaseVertex( 0 ), 0, 0 } );

      Synchronize();
      return true;
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

      Simulation.Instance.StepCallbacks.SimulationPre += Synchronize;
    }

    protected override void OnDestroy()
    {
      m_activeVoxelBuffer.Release();
      m_coarseParticlesBuffer.Release();
      m_fineParticlesBuffer.Release();
      m_fineParticlesSwapBuffer.Release();
      m_hashTable.Release();
      m_hashTableOccupancy.Release();
      m_dispatchArgs.Release();
      m_drawCallArgsBuffer.Release();

      m_activeVoxelBuffer.Dispose();
      m_coarseParticlesBuffer.Dispose();
      m_fineParticlesBuffer.Dispose();
      m_fineParticlesSwapBuffer.Dispose();
      m_hashTable.Dispose();
      m_hashTableOccupancy.Dispose();
      m_dispatchArgs.Dispose();
      m_drawCallArgsBuffer.Dispose();
    }

    protected override void OnDisable()
    {
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      Camera.onPreCull -= Render;

      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.SimulationPre -= Synchronize;
    }

    void CreateCoarseParticleAndVoxelData()
    {
      var coarseParticles = ParticleProvider.GetParticles();
      if ( coarseParticles == null )
        return;
      m_numCoarseParticles = (int)coarseParticles.size();

      while ( m_numCoarseParticles > m_coarseParticles.Length ) {
        System.Array.Resize( ref m_coarseParticles, m_coarseParticles.Length * 2 );
        m_coarseParticlesBuffer.Release();
        m_coarseParticlesBuffer.Dispose();
        m_coarseParticlesBuffer = new ComputeBuffer( m_coarseParticles.Length, Marshal.SizeOf( typeof( GranularData ) ) );

        SetDynamicBuffers();
      }

      GetGranularDatas( coarseParticles, m_coarseParticles );

      m_coarseParticlesBuffer.SetData( m_coarseParticles );

      m_numActiveVoxels = GenerateVoxelGrid( coarseParticles, m_activeVoxelIndices, VoxelSize.ValueOrDefault( ParticleProvider.ElementSize ) );

      while ( m_numActiveVoxels > m_activeVoxelIndices.Length ) {
        System.Array.Resize( ref m_activeVoxelIndices, m_activeVoxelIndices.Length * 2 );
        System.Array.Resize( ref m_activeVoxelIndicesUnity, m_activeVoxelIndicesUnity.Length * 2 );
        m_activeVoxelBuffer.Release();
        m_activeVoxelBuffer.Dispose();
        m_activeVoxelBuffer = new ComputeBuffer( m_activeVoxelIndicesUnity.Length, Marshal.SizeOf( typeof( VoxelIndex ) ) );

        m_hashTable.Release();
        m_hashTable.Dispose();
        m_hashTable = new ComputeBuffer( m_activeVoxelIndicesUnity.Length * 2, Marshal.SizeOf( typeof( VoxelEntry ) ) );

        m_hashTableOccupancy.Release();
        m_hashTableOccupancy.Dispose();
        m_hashTableOccupancy = new ComputeBuffer( m_hashTable.count, Marshal.SizeOf( typeof( uint ) ) );

        SetDynamicBuffers();


        m_numActiveVoxels = GenerateVoxelGrid( coarseParticles, m_activeVoxelIndices, VoxelSize.ValueOrDefault( ParticleProvider.ElementSize ) );
      }

      for ( int i = 0; i < m_numActiveVoxels; i++ ) {
        m_activeVoxelIndicesUnity[ i ].index.x = (int)m_activeVoxelIndices[ i ].x;
        m_activeVoxelIndicesUnity[ i ].index.y = (int)m_activeVoxelIndices[ i ].y;
        m_activeVoxelIndicesUnity[ i ].index.z = (int)m_activeVoxelIndices[ i ].z;
      }

      m_activeVoxelBuffer.SetData( m_activeVoxelIndicesUnity );
    }

    private void StepFinePartileSimulation()
    {
      int updateGrid = m_particleUpsamplingShader.FindKernel("UpdateGrid");
      int applyParticleMass = m_particleUpsamplingShader.FindKernel("ApplyParticleMass");
      int compactFineParticles = m_particleUpsamplingShader.FindKernel("CompactFineParticles");
      int swapParticleBuffers = m_particleUpsamplingShader.FindKernel("SwapParticleBuffers");
      int spawnParticles = m_particleUpsamplingShader.FindKernel("SpawnParticles");
      int moveParticles = m_moveParticlesShader.FindKernel("MoveParticles");
      int clearTable = m_particleUpsamplingShader.FindKernel("ClearTable");

      CreateCoarseParticleAndVoxelData();

      m_particleUpsamplingShader.SetInt( "numActiveVoxels", m_numActiveVoxels );
      m_particleUpsamplingShader.SetInt( "numCoarseParticles", m_numCoarseParticles );

      m_particleUpsamplingShader.SetInt( "time", (int)System.DateTime.Now.Ticks );
      m_particleUpsamplingShader.SetFloat( "voxelSize", VoxelSize.ValueOrDefault( ParticleProvider.ElementSize ) );
      m_particleUpsamplingShader.SetFloat( "fineParticleMass", FineParticleMass );
      m_particleUpsamplingShader.SetFloat( "animationSpeed", EaseStepSize );
      m_particleUpsamplingShader.SetFloat( "nominalRadius", m_nominalRadiusCache );

      m_moveParticlesShader.SetFloat( "voxelSize", VoxelSize.ValueOrDefault( ParticleProvider.ElementSize ) );
      m_moveParticlesShader.SetFloat( "timeStep", (float)Simulation.Instance.TimeStep );
      m_moveParticlesShader.SetFloat( "animationSpeed", EaseStepSize );

      int numVoxelThreadGroups = Mathf.CeilToInt((float)m_numActiveVoxels / m_groupSizeVoxels);

      m_particleUpsamplingShader.Dispatch( clearTable, Mathf.CeilToInt( (float)m_hashTable.count / m_groupSizeZeroing ), 1, 1 );

      if ( numVoxelThreadGroups > 0 )
        m_particleUpsamplingShader.Dispatch( updateGrid, numVoxelThreadGroups, 1, 1 );

      m_particleUpsamplingShader.DispatchIndirect( applyParticleMass, m_dispatchArgs );
      m_particleUpsamplingShader.DispatchIndirect( compactFineParticles, m_dispatchArgs );
      m_particleUpsamplingShader.DispatchIndirect( swapParticleBuffers, m_dispatchArgs );

      if ( numVoxelThreadGroups > 0 )
        m_particleUpsamplingShader.Dispatch( spawnParticles, numVoxelThreadGroups, 1, 1 );

      m_moveParticlesShader.DispatchIndirect( moveParticles, m_dispatchArgs );
    }

    private void Synchronize()
    {
      var RP = RenderingUtils.DetectPipeline();
      if ( RenderMode != ParticleRenderMode.Mesh && RP != RenderingUtils.PipelineType.BuiltIn ) {
        Debug.LogWarning( "Impostor rendering is currently only supported for the Built-in renderer. Switching to mesh rendering of terrain granules.", this );
        RenderMode = ParticleRenderMode.Mesh;
      }

      if ( RenderMode != m_persistedRenderMode ) {
        if ( RenderMode == ParticleRenderMode.Impostor )
          m_drawCallArgsBuffer.SetData( new uint[ 6 ] { m_quadMesh.GetIndexCount( 0 ), 0, m_quadMesh.GetIndexStart( 0 ), m_quadMesh.GetBaseVertex( 0 ), 0, 0 } );
        else if ( RenderMode == ParticleRenderMode.Mesh )
          m_drawCallArgsBuffer.SetData( new uint[ 6 ] { GranuleMesh.GetIndexCount( 0 ), 0, GranuleMesh.GetIndexStart( 0 ), GranuleMesh.GetBaseVertex( 0 ), 0, 0 } );

        m_persistedRenderMode = RenderMode;
      }

      if ( !m_nominalRadiusFetched )
        RecalculateFineParticleProperties();

      StepFinePartileSimulation();
    }

    private void SRPRender( ScriptableRenderContext context, Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      Render( cam );
    }

    private void Render( Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      if ( m_numCoarseParticles == 0 )
        return;

      var agxBounds = ParticleProvider.GetSoilSimulationInterface().getSoilParticleBound();
      var bound = new Bounds(agxBounds.mid().ToHandedVector3(), agxBounds.size().ToVector3());

      if ( m_persistedRenderMode == ParticleRenderMode.Impostor ) {
        m_impostorMaterial.SetFloat( "fineRadius", FineParticleRadius );
        m_impostorMaterial.SetBuffer( "fineParticles", m_fineParticlesBuffer );
        m_impostorMaterial.SetColor( "_ColorLow", color1 );
        m_impostorMaterial.SetColor( "_ColorHigh", color2 );

        Graphics.DrawMeshInstancedIndirect( m_quadMesh, 0, m_impostorMaterial, bound, m_drawCallArgsBuffer, camera: cam );
      }
      else {
        GranuleMaterial.SetFloat( "fineRadius", FineParticleRadius );
        GranuleMaterial.SetBuffer( "fineParticles", m_fineParticlesBuffer );
        GranuleMaterial.SetVector( "offset", bound.center );

        Graphics.DrawMeshInstancedIndirect( GranuleMesh, 0, GranuleMaterial, bound, m_drawCallArgsBuffer, camera: cam, castShadows: ShadowCastingMode.On );
      }
    }
  }
}