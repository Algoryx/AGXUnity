using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AGXUnity.Model;
using AGXUnity.Utils;
using Unity.Profiling;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// One material-scoped, rendering-only snapshot on demand after PostStepForward. Native
  /// proxies never escape this component. Cameras consume the same managed data.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [AddComponentMenu( "AGXUnity/Rendering/Terrain Particle Snapshot Provider" )]
  public class TerrainParticleSnapshotProvider : ScriptComponent
  {
    [StructLayout( LayoutKind.Sequential, Size = 64 )]
    public struct Particle
    {
      public Vector3 Position;
      public float Radius;
      public Quaternion Rotation;
      public Color Color;
      public uint Id;
      public uint RenderingEligible;
      public int TerrainInstanceId;
      public uint MaterialIndex;
    }

    [field: SerializeField]
    [Tooltip( "Restrict ownership to the default particle material. When disabled, also include this terrain's material patches." )]
    public bool FilterParticles { get; set; }

    [field: SerializeField]
    [Tooltip( "Additional initialized particle shape materials assigned through the runtime terrain API." )]
    public List<ShapeMaterial> AdditionalParticleMaterials { get; set; } = new List<ShapeMaterial>();

    [HideInInspector] public DeformableTerrainBase Terrain { get; private set; }
    [HideInInspector] public Particle[] Particles { get; private set; } = new Particle[1024];
    public int Count { get; protected set; }
    public uint Version { get; protected set; }
    public event Action Updated;

    private agxTerrain.SoilSimulationInterface m_interface;
    private agx.GranularBodySystem m_system;
    private agxTerrain.Terrain m_nativeTerrain;
    private bool m_capturePending = true;
    public uint CaptureCount { get; private set; }
    public int NativeParticleCount { get; private set; }
    public string ReadbackPath { get; private set; }
    private readonly List<agx.Uuid> m_materials = new List<agx.Uuid>( 8 );
    private readonly List<TerrainMaterialPatch> m_patches = new List<TerrainMaterialPatch>();
    private static readonly List<TerrainParticleSnapshotProvider> s_providers = new List<TerrainParticleSnapshotProvider>();
    private static readonly List<TerrainParticleSnapshotProvider> s_captureGroup = new List<TerrainParticleSnapshotProvider>();
    private static bool s_capturing;
    private static agx.agxSWIG.UnityHelpers.GranularData[] s_bulkData = new agx.agxSWIG.UnityHelpers.GranularData[0];
    private static agx.AffineMatrix4x4f[] s_bulkMatrices = new agx.AffineMatrix4x4f[0];
    // The existing wrapper's bulk paths benchmark slower, including dense cases.
    // Default to scalar. Internal flags let the isolated benchmark verify other hosts:
    // 0: scalar; 1: bulk position/radius; 3: bulk including matrices.
    private static int s_readbackMode;
    public static uint NativeTraversalCount { get; private set; }
    private int m_terrainInstanceId;
    private static readonly ProfilerMarker s_snapshot = new ProfilerMarker( "AGX Soil.Snapshot" );

    /// <summary>Publish from a derived acquisition backend under the same synchronization contract.</summary>
    protected void Publish( Particle[] particles, int count )
    {
      if ( particles == null || count < 0 || count > particles.Length ) throw new ArgumentOutOfRangeException( nameof( count ) );
      Particles = particles;
      Count = count;
      ++Version;
      Updated?.Invoke();
    }

    protected override bool Initialize()
    {
      Terrain = GetComponent<DeformableTerrainBase>().GetInitialized<DeformableTerrainBase>();
      if ( Terrain == null )
        return false;
      m_terrainInstanceId = Terrain.GetInstanceID();
      m_interface = Terrain.GetSoilSimulationInterface();
      m_system = m_interface?.getGranularBodySystem();
      if ( Terrain is DeformableTerrain deformable ) m_nativeTerrain = deformable.Native;
      else if ( Terrain is MovableTerrain movable ) m_nativeTerrain = movable.Native;
      else if ( Terrain is DeformableTerrainPager pager ) m_nativeTerrain = pager.Native.getTemplateTerrain();
      Capture();
      return true;
    }

    protected override void OnEnable()
    {
      if ( !s_providers.Contains( this ) ) s_providers.Add( this );
      m_capturePending = true;
      Simulation.Instance.StepCallbacks.PostStepForward += MarkPending;
    }

    protected override void OnDisable()
    {
      s_providers.Remove( this );
      foreach ( var provider in s_providers ) provider.m_capturePending = true;
      ClearMaterials();
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= MarkPending;
      m_capturePending = true;
      Count = 0;
      ++Version;
      Updated?.Invoke();
    }

    private void MarkPending() => m_capturePending = true;

    /// <summary>Read the latest completed step once. Simulation stepping and rendering run on Unity's main thread.</summary>
    public void RefreshIfNeeded()
    {
      if ( m_capturePending && isActiveAndEnabled ) Capture();
    }

    // Simulation.DoStep is synchronous on the main thread; no native handles are
    // retained across steps. Multiple completed steps coalesce before this read.
    private void Capture()
    {
      if ( s_capturing ) return;
      if ( m_interface == null || !isActiveAndEnabled || Terrain == null || !Terrain.isActiveAndEnabled ) {
        if ( Count > 0 ) { Count = 0; ++Version; Updated?.Invoke(); }
        return;
      }
      // SSI returns the global soil array. Dispatch once per native system,
      // instead of retrieving and walking that array for every terrain.
      var system = agx.GranularBodySystem.getCPtr( m_system ).Handle;
      s_captureGroup.Clear();
      foreach ( var provider in s_providers ) {
        var renderer = provider.GetComponent<TerrainParticleSurfaceRenderer>();
        if ( provider.isActiveAndEnabled && provider.Terrain != null && provider.Terrain.isActiveAndEnabled &&
             (provider == this || (renderer != null && renderer.isActiveAndEnabled)) &&
             provider.m_interface != null && agx.GranularBodySystem.getCPtr( provider.m_system ).Handle == system )
          s_captureGroup.Add( provider );
      }
      s_capturing = true;
      try {
        using ( s_snapshot.Auto() ) {
          foreach ( var provider in s_captureGroup ) {
            provider.m_capturePending = false;
            ++provider.CaptureCount;
            provider.Count = 0;
            provider.RefreshMaterials();
          }
          m_system?.updateShouldRender();
          // getSoilParticles returns an owned array by VALUE, not a live view.
          // Acquire once per synchronized update and promptly dispose its wrapper.
          // Retaining it across updates would miss particle creation/removal.
          using var nativeParticles = m_interface.getSoilParticles();
          ++NativeTraversalCount;
          int size = checked( (int)nativeParticles.size() );
          bool bulkData = (s_readbackMode & 1) != 0;
          bool bulkMatrices = (s_readbackMode & 2) != 0;
          if ( bulkData && size > s_bulkData.Length ) Array.Resize( ref s_bulkData, Mathf.NextPowerOfTwo( size ) );
          if ( bulkMatrices && size > s_bulkMatrices.Length ) Array.Resize( ref s_bulkMatrices, Mathf.NextPowerOfTwo( size ) );
          foreach ( var provider in s_captureGroup ) { provider.NativeParticleCount = size; provider.ReadbackPath = bulkData ? "Bulk position/radius" : "Scalar"; }
          if ( size > 0 ) {
            if ( bulkData ) agx.agxSWIG.UnityHelpers.GetGranularDatas( nativeParticles, s_bulkData );
            if ( bulkMatrices ) agx.agxSWIG.UnityHelpers.PopulateMatrices( nativeParticles, s_bulkMatrices, 0 );
          }
          bool single = s_captureGroup.Count == 1;
          var singleOwner = single ? s_captureGroup[0] : null;
          var singleRecords = single ? singleOwner.Particles : null;
          int singleCount = 0;
          {
            for ( uint index = 0; index < size; ++index ) {
              var native = nativeParticles.at( index );
              try {
                TerrainParticleSnapshotProvider owner = singleOwner;
                uint materialIndex = 0;
                if ( single ) { if ( !singleOwner.Owns( native, out materialIndex ) ) continue; }
                else
                  for ( int group = 0; group < s_captureGroup.Count; ++group )
                    if ( s_captureGroup[group].Owns( native, out materialIndex ) ) { owner = s_captureGroup[group]; break; }
                if ( !single && owner == null ) continue;
                float enabled = native.getEnableRendering();
                if ( !SoilParticleSelection.Finite( enabled ) || enabled <= 0 )
                  continue;
                float radius = bulkData ? s_bulkData[index].radius : (float)native.getRadius();
                var color = native.getColor();
                if ( !SoilParticleSelection.Finite( radius ) || radius <= 0 || color.w <= 0 ||
                     !SoilParticleSelection.Finite( color.x ) || !SoilParticleSelection.Finite( color.y ) ||
                     !SoilParticleSelection.Finite( color.z ) || !SoilParticleSelection.Finite( color.w ) )
                  continue;
                var position = bulkData ? s_bulkData[index].position.ToHandedVector3() : native.position().ToHandedVector3();
                var rotation = bulkMatrices ? ReadBulkRotation( s_bulkMatrices[index], radius ) : native.rotation().ToHandedQuaternion();
                if ( !Finite( position ) || !SoilParticleSelection.Finite( rotation.x ) ||
                     !SoilParticleSelection.Finite( rotation.y ) || !SoilParticleSelection.Finite( rotation.z ) ||
                     !SoilParticleSelection.Finite( rotation.w ) )
                  continue;
                var particle = new Particle {
                  Id = native.getId(), Position = position, Radius = radius,
                  Rotation = rotation, Color = new Color( color.x, color.y, color.z, 1 ),
                  RenderingEligible = 1, TerrainInstanceId = owner.m_terrainInstanceId, MaterialIndex = materialIndex
                };
                if ( single ) {
                  if ( singleCount == singleRecords.Length ) Array.Resize( ref singleRecords, Mathf.NextPowerOfTwo( singleCount+1 ) );
                  singleRecords[singleCount++] = particle;
                }
                else {
                  if ( owner.Count == owner.Particles.Length ) {
                    var grown = owner.Particles;
                    Array.Resize( ref grown, Mathf.NextPowerOfTwo( owner.Count+1 ) ); owner.Particles = grown;
                  }
                  owner.Particles[owner.Count++] = particle;
                }
              }
              finally { native.ReturnToPool(); }
            }
          }
          if ( single ) { singleOwner.Particles = singleRecords; singleOwner.Count = singleCount; }
          foreach ( var provider in s_captureGroup ) ++provider.Version;
        }
        foreach ( var provider in s_captureGroup ) provider.Updated?.Invoke();
      }
      finally { s_captureGroup.Clear(); s_capturing = false; }
    }

    private bool IncludesMaterial( agx.Uuid uuid )
    {
      foreach ( var material in m_materials ) if ( material.EqualWith( uuid ) ) return true;
      return false;
    }

    // Native AffineMatrix uses row vectors; its rows are Unity's rotation columns.
    // Decode directly instead of another native quaternion call per particle.
    private static Quaternion ReadBulkRotation( agx.AffineMatrix4x4f m, float radius )
    {
      float inverseScale = 0.5f/radius;
      float a = m.e00*inverseScale, b = m.e10*inverseScale, c = m.e20*inverseScale;
      float d = m.e01*inverseScale, e = m.e11*inverseScale, f = m.e21*inverseScale;
      float g = m.e02*inverseScale, h = m.e12*inverseScale, i = m.e22*inverseScale;
      float trace = a+e+i;
      if ( trace > 0 ) {
        float s = (float)System.Math.Sqrt( trace+1 )*2;
        return new Quaternion( (h-f)/s, (c-g)/s, (d-b)/s, s*0.25f );
      }
      if ( a > e && a > i ) {
        float s = (float)System.Math.Sqrt( 1+a-e-i )*2;
        return new Quaternion( s*0.25f, (b+d)/s, (c+g)/s, (h-f)/s );
      }
      if ( e > i ) {
        float s = (float)System.Math.Sqrt( 1+e-a-i )*2;
        return new Quaternion( (b+d)/s, s*0.25f, (f+h)/s, (c-g)/s );
      }
      else {
        float s = (float)System.Math.Sqrt( 1+i-a-e )*2;
        return new Quaternion( (c+g)/s, (f+h)/s, s*0.25f, (d-b)/s );
      }
    }

    private bool Owns( agx.GranularBodyPtr particle, out uint materialIndex )
    {
      materialIndex = 0;
      // One native predicate replaces UUID acquisition/comparison transitions
      // for the usual single-material terrain. It is exactly AGX's predicate.
      if ( m_materials.Count == 1 && m_nativeTerrain != null ) return m_nativeTerrain.hasSameParticleMaterial( particle );
      var uuid = particle.getMaterialUuid();
      try {
        for ( int index = 0; index < m_materials.Count; ++index )
          if ( m_materials[index].EqualWith( uuid ) ) { materialIndex = (uint)index; return true; }
        return false;
      }
      finally { uuid.ReturnToPool(); }
    }

    private void ClearMaterials()
    {
      foreach ( var material in m_materials ) material.ReturnToPool();
      m_materials.Clear();
    }

    private void RefreshMaterials()
    {
      ClearMaterials();
      var primary = Terrain.GetParticleMaterialUuid();
      if ( primary != null ) m_materials.Add( primary );
      if ( FilterParticles || m_nativeTerrain == null ) return;
      // Native getSoilParticles returns the GLOBAL soil system. Its ownership
      // predicate is material identity (Terrain::hasCreatedParticle does the
      // same check). Include patch materials without using spatial membership.
      Terrain.GetComponentsInChildren<TerrainMaterialPatch>( true, m_patches );
      foreach ( var patch in m_patches ) {
        if ( patch.ParentTerrain != Terrain || patch.TerrainMaterial?.Native == null ) continue;
        using var associated = m_nativeTerrain.getAssociatedMaterial( patch.TerrainMaterial.Native );
        if ( associated != null ) {
          var uuid = associated.getUuid();
          if ( IncludesMaterial( uuid ) ) uuid.ReturnToPool();
          else m_materials.Add( uuid );
        }
      }
      if ( AdditionalParticleMaterials != null )
        foreach ( var material in AdditionalParticleMaterials ) {
          if ( material?.Native == null ) continue;
          var uuid = material.Native.getUuid();
          if ( IncludesMaterial( uuid ) ) uuid.ReturnToPool();
          else m_materials.Add( uuid );
        }
    }

    private static bool Finite( Vector3 p ) => SoilParticleSelection.Finite( p.x ) &&
      SoilParticleSelection.Finite( p.y ) && SoilParticleSelection.Finite( p.z );
  }
}
