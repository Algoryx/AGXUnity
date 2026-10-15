using System;
using System.Collections.Generic;
using AGXUnity.Utils;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [DisallowMultipleComponent]
  [RequireComponent( typeof( TerrainParticleSnapshotProvider ) )]
  [AddComponentMenu( "AGXUnity/Rendering/Terrain Particle Surface Renderer" )]
  public class TerrainParticleSurfaceRenderer : MonoBehaviour
  {
    public enum SurfaceInputMode { Sphere, Mesh }
    public enum DisplayMode { OrdinaryParticles, Surface }
    public enum TextureMappingMode { Particle, World }

    [SerializeField] private SurfaceInputMode m_surfaceInput;
    [SerializeField] private DisplayMode m_display = DisplayMode.Surface;
    [SerializeField] private float m_supportRadiusScale = 1.15f;
    [SerializeField, Range( 0, 0.5f ), Tooltip( "Existing connections survive this fraction beyond the support distance. 0 disables hysteresis; 0.05 gives a 5% release margin." )]
    private float m_connectionHysteresis = 0.05f;
    [SerializeField, Range( 0, 2 )] private float m_smoothing = 0.6f;
    [SerializeField] private float m_depthThreshold = 1;
    [SerializeField, Range( 0.25f, 1 )] private float m_resolutionScale = 1;
    [SerializeField, Range( 0, 1 )] private float m_inputNormalContribution = 1;
    [SerializeField] private float m_textureRepeatDistance = 0.25f;
    [SerializeField, Tooltip( "Particle mapping follows simulated translation and rotation. World mapping projects a stationary texture over the soil." )]
    private TextureMappingMode m_textureMapping = TextureMappingMode.Particle;
    [SerializeField, Range( 0, 1 )] private float m_individualParticleFraction;
    [SerializeField, Range( 0, 1 )] private float m_smoothness = 0.2f;
    [SerializeField] private Mesh m_particleMesh;
    [SerializeField] private Material m_particleMaterial;
    [SerializeField] private Material m_soilMaterial;
    [SerializeField] private Texture2D m_soilTexture;
    [SerializeField] private Color m_soilTint = new Color( 0.45f, 0.31f, 0.18f );
    [SerializeField, Range( 0, 1 ), Tooltip( "How much native AGX particle color multiplies the soil texture. Set to zero to match an untinted TerrainLayer texture." )]
    private float m_particleColorContribution = 1;

    public SurfaceInputMode SurfaceInput { get => m_surfaceInput; set { if ( Enum.IsDefined( typeof( SurfaceInputMode ), value ) ) m_surfaceInput = value; } }
    public DisplayMode Display { get => m_display; set { if ( Enum.IsDefined( typeof( DisplayMode ), value ) && m_display != value ) { m_display = value; m_dirty = true; } } }
    [Tooltip( "Connect particles when center distance is at most this scale times the sum of their physical radii. Does not enlarge particles." )]
    public float SupportRadiusScale {
      get => m_supportRadiusScale;
      set {
        float next = SoilParticleSelection.Positive( value, m_supportRadiusScale );
        if ( next == m_supportRadiusScale ) return;
        m_supportRadiusScale = next;
        m_resetConnectionHistory = true;
        m_dirty = true;
      }
    }
    public float ConnectionHysteresis {
      get => m_connectionHysteresis;
      set {
        float next = SoilParticleSelection.Range( value, 0, 0.5f, m_connectionHysteresis );
        if ( next == m_connectionHysteresis ) return;
        m_connectionHysteresis = next; m_resetConnectionHistory = true; m_dirty = true;
      }
    }
    public float Smoothing { get => m_smoothing; set => m_smoothing = SoilParticleSelection.Range( value, 0, 2, m_smoothing ); }
    public float DepthThreshold { get => m_depthThreshold; set => m_depthThreshold = SoilParticleSelection.Positive( value, m_depthThreshold ); }
    public float ResolutionScale { get => m_resolutionScale; set => m_resolutionScale = SoilParticleSelection.Range( value, 0.25f, 1, m_resolutionScale ); }
    public float InputNormalContribution { get => m_inputNormalContribution; set => m_inputNormalContribution = SoilParticleSelection.Range( value, 0, 1, m_inputNormalContribution ); }
    public float TextureRepeatDistance { get => m_textureRepeatDistance; set => m_textureRepeatDistance = SoilParticleSelection.Positive( value, m_textureRepeatDistance ); }
    public TextureMappingMode TextureMapping { get => m_textureMapping; set { if ( Enum.IsDefined( typeof( TextureMappingMode ), value ) ) m_textureMapping = value; } }
    public float IndividualParticleFraction {
      get => m_individualParticleFraction;
      set {
        float next = SoilParticleSelection.Range( value, 0, 1, m_individualParticleFraction );
        if ( next == m_individualParticleFraction ) return;
        m_individualParticleFraction = next;
        m_dirty = true;
      }
    }
    public float Smoothness { get => m_smoothness; set => m_smoothness = SoilParticleSelection.Range( value, 0, 1, m_smoothness ); }
    [Tooltip( "Centered mesh with vertices within radius 0.5; transform scale is exactly twice the physical radius." )]
    public Mesh ParticleMesh { get => m_particleMesh; set => m_particleMesh = value; }
    public Material ParticleMaterial { get => m_particleMaterial; set => m_particleMaterial = value; }
    [Tooltip( "Optional material using Hidden/AGXUnity/TerrainParticleSurface. Controls below are applied per draw." )]
    public Material SoilMaterial {
      get => m_soilMaterial;
      set { if ( value == null || value.shader.name == "Hidden/AGXUnity/TerrainParticleSurface" ) m_soilMaterial = value; }
    }
    public Texture2D SoilTexture { get => m_soilTexture; set => m_soilTexture = value; }
    public Color SoilTint { get => m_soilTint; set { if ( Finite( value ) ) m_soilTint = value; } }
    public float ParticleColorContribution {
      get => m_particleColorContribution;
      set => m_particleColorContribution = SoilParticleSelection.Range( value, 0, 1, m_particleColorContribution );
    }
    public TerrainParticleSnapshotProvider Snapshot { get; private set; }
    public GraphicsBuffer SurfaceBuffer { get; private set; }
    public GraphicsBuffer BridgeBuffer { get; private set; }
    public GraphicsBuffer FaceBuffer { get; private set; }
    public int FaceCount => m_faces.Count;
    public string FaceBackend => m_faces.Backend;
    public int BridgeCount => m_bridges.Count;
    public string BridgeBackend => m_bridges.Backend;
    public int SurfaceCount { get; private set; }
    public int IndividualCount { get; private set; }
    public int UploadedParticleCount => SurfaceCount + IndividualCount;
    public uint PackCount { get; private set; }
    public bool IsSurfaceMaterialValid => SoilMaterial == null || (SoilMaterial.shader.isSupported &&
      (SurfaceInput != SurfaceInputMode.Mesh || SoilMaterial.enableInstancing));

    // URP installs a camera-specific capability check. Other pipelines fall back.
    public static Func<Camera, bool> CameraSupportsSurface;
    public static readonly List<TerrainParticleSurfaceRenderer> Active = new List<TerrainParticleSurfaceRenderer>();
    private TerrainParticleSnapshotProvider.Particle[] m_packed = new TerrainParticleSnapshotProvider.Particle[1024];
    private TerrainParticleSnapshotProvider.Particle[] m_anchors = new TerrainParticleSnapshotProvider.Particle[1024];
    private Matrix4x4[] m_surfaceMatrices = new Matrix4x4[1024];
    private Matrix4x4[] m_individualMatrices = new Matrix4x4[1024];
    private uint m_version = uint.MaxValue;
    private readonly SoilParticleBridges m_bridges = new SoilParticleBridges();
    private readonly SoilParticleFaces m_faces = new SoilParticleFaces();
    private bool m_dirty = true;
    private bool m_resetConnectionHistory;
    private bool m_prepareSurface = true, m_hasPreparedSurface;
    private bool m_warned;
    private Material m_defaultMaterial;
    private readonly List<Behaviour> m_suppressed = new List<Behaviour>();
    private static readonly ProfilerMarker s_pack = new ProfilerMarker( "AGX Soil.PackUpload" );

    private void OnEnable()
    {
      Snapshot = GetComponent<TerrainParticleSnapshotProvider>();
      Snapshot.Updated += SnapshotUpdated;
      Active.Add( this );
      // These components draw the whole terrain list. Suppress only ones enabled
      // when we take ownership, and restore them on disable.
      Suppress( GetComponent<DeformableTerrainParticleRenderer>() );
      Suppress( GetComponent<UpsamplingParticleRenderer>() );
      Camera.onPreCull += RenderBuiltIn;
      RenderPipelineManager.beginCameraRendering += RenderSRP;
      m_dirty = true;
    }

    private void Suppress( Behaviour renderer )
    {
      if ( renderer != null && renderer.enabled ) {
        if ( !m_suppressed.Contains( renderer ) ) m_suppressed.Add( renderer );
        renderer.enabled = false;
      }
    }

    private void Start()
    {
      Snapshot.GetInitialized<TerrainParticleSnapshotProvider>();
      m_dirty = true;
    }

    private void SnapshotUpdated()
    {
      m_dirty = true;
      if ( Snapshot.Count == 0 ) {
        SurfaceCount = IndividualCount = 0;
        m_bridges.Build( m_packed, 0, SupportRadiusScale );
        m_faces.Build( 0, m_bridges.Pairs, 0 );
      }
    }

    private void OnDisable()
    {
      Active.Remove( this );
      if ( Snapshot != null ) Snapshot.Updated -= SnapshotUpdated;
      Camera.onPreCull -= RenderBuiltIn;
      RenderPipelineManager.beginCameraRendering -= RenderSRP;
      SurfaceBuffer?.Dispose();
      SurfaceBuffer = null;
      BridgeBuffer?.Dispose();
      BridgeBuffer = null;
      FaceBuffer?.Dispose();
      FaceBuffer = null;
      m_faces.Build( 0, m_bridges.Pairs, 0 );
      m_bridges.Build( m_packed, 0, SupportRadiusScale );
      m_bridges.Dispose();
      m_faces.Dispose();
      m_hasPreparedSurface = false;
      SurfaceCount = IndividualCount = 0;
      foreach ( var renderer in m_suppressed ) if ( renderer != null ) renderer.enabled = true;
      m_suppressed.Clear();
      if ( m_defaultMaterial != null ) Destroy( m_defaultMaterial );
      m_defaultMaterial = null;
    }

    private void OnValidate()
    {
      m_supportRadiusScale = SoilParticleSelection.Positive( m_supportRadiusScale, 1.15f );
      m_connectionHysteresis = SoilParticleSelection.Range( m_connectionHysteresis, 0, 0.5f, 0.05f );
      m_resetConnectionHistory = true;
      m_smoothing = SoilParticleSelection.Range( m_smoothing, 0, 2, 0.6f );
      m_depthThreshold = SoilParticleSelection.Positive( m_depthThreshold, 1 );
      m_resolutionScale = SoilParticleSelection.Range( m_resolutionScale, 0.25f, 1, 1 );
      m_inputNormalContribution = SoilParticleSelection.Range( m_inputNormalContribution, 0, 1, 1 );
      m_textureRepeatDistance = SoilParticleSelection.Positive( m_textureRepeatDistance, 0.25f );
      m_individualParticleFraction = SoilParticleSelection.Range( m_individualParticleFraction, 0, 1, 0 );
      m_smoothness = SoilParticleSelection.Range( m_smoothness, 0, 1, 0.2f );
      m_particleColorContribution = SoilParticleSelection.Range( m_particleColorContribution, 0, 1, 1 );
      if ( !Finite( m_soilTint ) ) m_soilTint = new Color( 0.45f, 0.31f, 0.18f );
      if ( m_soilMaterial != null && m_soilMaterial.shader.name != "Hidden/AGXUnity/TerrainParticleSurface" ) m_soilMaterial = null;
      m_dirty = true;
    }

    private void Pack()
    {
      if ( Snapshot == null || !isActiveAndEnabled ) return;
      using ( s_pack.Auto() ) {
        ++PackCount;
        int count = Snapshot.Count;
        if ( count > m_packed.Length ) {
          int capacity = Mathf.NextPowerOfTwo( count );
          m_packed = new TerrainParticleSnapshotProvider.Particle[capacity];
          m_anchors = new TerrainParticleSnapshotProvider.Particle[capacity];
          m_surfaceMatrices = new Matrix4x4[capacity];
          m_individualMatrices = new Matrix4x4[capacity];
        }
        SurfaceCount = IndividualCount = 0;
        for ( int i = 0; i < count; ++i ) {
          var p = Snapshot.Particles[i];
          if ( p.RenderingEligible == 0 ) continue;
          var matrix = Matrix4x4.TRS( p.Position, p.Rotation, Vector3.one * (2 * p.Radius) );
          if ( SoilParticleSelection.IsIndividualParticle( p.Id, IndividualParticleFraction ) ) {
            p.RenderingEligible = SoilParticleBridges.CenterAnchor;
            m_anchors[IndividualCount] = p;
            m_individualMatrices[IndividualCount++] = matrix;
          }
          else {
            m_surfaceMatrices[SurfaceCount] = matrix;
            m_packed[SurfaceCount++] = p;
          }
        }
        Array.Copy( m_anchors, 0, m_packed, SurfaceCount, IndividualCount );
        // OnValidate can run on Unity's loading thread. Mutate native history
        // only here, on the main thread before rebuilding connections.
        if ( m_resetConnectionHistory ) { m_bridges.ClearHistory(); m_resetConnectionHistory = false; }
        m_bridges.Build( m_packed, m_prepareSurface && SurfaceCount > 0 ? UploadedParticleCount : 0, SupportRadiusScale, ConnectionHysteresis );
        m_faces.Build( UploadedParticleCount, m_bridges.Pairs, m_bridges.Count );
        if ( m_prepareSurface && SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders && SurfaceCount > 0 ) {
          if ( SurfaceBuffer == null || SurfaceBuffer.count < UploadedParticleCount ) {
            SurfaceBuffer?.Dispose();
            SurfaceBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, m_packed.Length, 64 );
          }
          SurfaceBuffer.SetData( m_packed, 0, 0, UploadedParticleCount );
          if ( FaceCount > 0 ) {
            if ( FaceBuffer == null || FaceBuffer.count < FaceCount ) {
              FaceBuffer?.Dispose();
              FaceBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, m_faces.Faces.Length, 12 );
            }
            FaceBuffer.SetData( m_faces.Faces, 0, 0, FaceCount );
          }
          if ( BridgeCount > 0 ) {
            if ( BridgeBuffer == null || BridgeBuffer.count < BridgeCount ) {
              BridgeBuffer?.Dispose();
              BridgeBuffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, m_bridges.Pairs.Length, 8 );
            }
            BridgeBuffer.SetData( m_bridges.Pairs, 0, 0, BridgeCount );
          }
        }
        m_version = Snapshot.Version;
        m_hasPreparedSurface = m_prepareSurface;
        m_dirty = false;
      }
    }

    private void RenderBuiltIn( Camera camera )
    {
      if ( GraphicsSettings.currentRenderPipeline == null ) Render( camera );
    }
    private void RenderSRP( ScriptableRenderContext context, Camera camera ) => Render( camera );

    private void Render( Camera camera )
    {
      if ( Snapshot == null || !Snapshot.isActiveAndEnabled || Snapshot.Terrain == null || !Snapshot.Terrain.isActiveAndEnabled ) return;
      Suppress( GetComponent<DeformableTerrainParticleRenderer>() );
      Suppress( GetComponent<UpsamplingParticleRenderer>() );
      if ( !RenderingUtils.CameraShouldRender( camera, gameObject ) ||
           (camera.cullingMask & (1 << gameObject.layer)) == 0 ) return;
      Snapshot.RefreshIfNeeded();
      if ( ParticleMesh == null ) ParticleMesh = Resources.Load<Mesh>( "Debug/Models/Icosahedron" );
      if ( ParticleMaterial == null && m_defaultMaterial == null ) {
        m_defaultMaterial = RenderingUtils.CreateDefaultMaterial();
        m_defaultMaterial.enableInstancing = true;
      }
      var material = ParticleMaterial != null ? ParticleMaterial : m_defaultMaterial;
      if ( material == null || ParticleMesh == null ) return;
      bool surface = Display == DisplayMode.Surface && IsSurfaceMaterialValid && (CameraSupportsSurface?.Invoke( camera ) ?? false);
      m_prepareSurface = surface;
      if ( m_dirty || m_version != Snapshot.Version || (surface && !m_hasPreparedSurface) ) Pack();
      if ( !surface && Display == DisplayMode.Surface && !m_warned ) {
        Debug.LogWarning( "Soil surface unavailable for this camera/configuration; using ordinary particles. See soil surface usage instructions.", this );
        m_warned = true;
      }
      Draw( camera, material, m_surfaceMatrices, SurfaceCount, surface ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On );
      Draw( camera, material, m_individualMatrices, IndividualCount, ShadowCastingMode.On );
    }

    private static bool Finite( Color color ) => SoilParticleSelection.Finite( color.r ) && SoilParticleSelection.Finite( color.g ) &&
      SoilParticleSelection.Finite( color.b ) && SoilParticleSelection.Finite( color.a );

    private void Draw( Camera camera, Material material, Matrix4x4[] matrices, int count, ShadowCastingMode shadows )
    {
      var parameters = new RenderParams( material ) {
        camera = camera, layer = gameObject.layer, shadowCastingMode = shadows,
        receiveShadows = true
      };
      if ( SystemInfo.supportsInstancing && material.enableInstancing ) {
        for ( int start = 0; start < count; start += 511 )
          Graphics.RenderMeshInstanced( parameters, ParticleMesh, 0, matrices, Mathf.Min( 511, count - start ), start );
      }
      else {
        for ( int i = 0; i < count; ++i ) Graphics.RenderMesh( parameters, ParticleMesh, 0, matrices[i] );
      }
    }
  }
}
