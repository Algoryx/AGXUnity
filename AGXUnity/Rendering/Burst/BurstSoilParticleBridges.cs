using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Particle = AGXUnity.Rendering.TerrainParticleSnapshotProvider.Particle;
using Pair = AGXUnity.Rendering.SoilParticleBridges.Pair;

namespace AGXUnity.Rendering.Burst
{
  /// <summary>Exact two-pass parallel neighbor search. Containers are retained until the renderer is disabled.</summary>
  public sealed class BurstSoilParticleBridges : SoilParticleBridges.IAccelerator
  {
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Register() => SoilParticleBridges.CreateAccelerator =
      () => BurstCompiler.IsEnabled ? new BurstSoilParticleBridges() : null;
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor() => Register();
#endif

    private NativeArray<Particle> m_particles;
    private NativeArray<int> m_levels, m_counts, m_offsets, m_candidates, m_header;
    private NativeArray<float> m_sizes, m_maxRadii;
    private NativeArray<Pair> m_pairs;
    private NativeParallelMultiHashMap<int4, int> m_grid;

    public void Build( Particle[] particles, int count, float supportScale, ref Pair[] pairs, out int pairCount, out int candidateCount )
    {
      if ( !m_particles.IsCreated || m_particles.Length < count ) {
        Dispose();
        int capacity = Mathf.NextPowerOfTwo( count );
        m_particles = new NativeArray<Particle>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_levels = new NativeArray<int>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_counts = new NativeArray<int>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_offsets = new NativeArray<int>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_candidates = new NativeArray<int>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_header = new NativeArray<int>( 3, Allocator.Persistent );
        m_sizes = new NativeArray<float>( 32, Allocator.Persistent );
        m_maxRadii = new NativeArray<float>( 32, Allocator.Persistent );
        m_grid = new NativeParallelMultiHashMap<int4, int>( capacity, Allocator.Persistent );
      }
      NativeArray<Particle>.Copy( particles, 0, m_particles, 0, count );
      m_grid.Clear();
      new GridJob { Particles = m_particles, Count = count, Scale = supportScale, Levels = m_levels,
        Sizes = m_sizes, MaxRadii = m_maxRadii, Header = m_header, Grid = m_grid }.Run();
      var search = new Search { Particles = m_particles, Levels = m_levels, Sizes = m_sizes,
        MaxRadii = m_maxRadii, Header = m_header, Grid = m_grid, Scale = supportScale };
      new CountJob { Search = search, Counts = m_counts, Candidates = m_candidates }.Schedule( count, 64 ).Complete();
      new PrefixJob { Counts = m_counts, Candidates = m_candidates, Offsets = m_offsets, Header = m_header, Count = count }.Run();
      pairCount = m_header[1]; candidateCount = m_header[2];
      if ( pairCount < 0 || pairCount > 1073741824 ) throw new InvalidOperationException( "Soil bridge output exceeds supported array capacity." );
      if ( pairCount == 0 ) return;
      if ( !m_pairs.IsCreated || m_pairs.Length < pairCount ) {
        if ( m_pairs.IsCreated ) m_pairs.Dispose();
        m_pairs = new NativeArray<Pair>( Mathf.NextPowerOfTwo( pairCount ), Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
      }
      new EmitJob { Search = search, Offsets = m_offsets, Pairs = m_pairs }.Schedule( count, 64 ).Complete();
      if ( pairs.Length < pairCount ) Array.Resize( ref pairs, m_pairs.Length );
      NativeArray<Pair>.Copy( m_pairs, 0, pairs, 0, pairCount );
    }

    public void Dispose()
    {
      if ( m_particles.IsCreated ) m_particles.Dispose();
      if ( m_levels.IsCreated ) m_levels.Dispose();
      if ( m_counts.IsCreated ) m_counts.Dispose();
      if ( m_offsets.IsCreated ) m_offsets.Dispose();
      if ( m_candidates.IsCreated ) m_candidates.Dispose();
      if ( m_header.IsCreated ) m_header.Dispose();
      if ( m_sizes.IsCreated ) m_sizes.Dispose();
      if ( m_maxRadii.IsCreated ) m_maxRadii.Dispose();
      if ( m_pairs.IsCreated ) m_pairs.Dispose();
      if ( m_grid.IsCreated ) m_grid.Dispose();
      m_particles = default; m_levels = m_counts = m_offsets = m_candidates = m_header = default;
      m_sizes = m_maxRadii = default; m_pairs = default; m_grid = default;
    }

    [BurstCompile( CompileSynchronously = true, FloatMode = FloatMode.Strict )]
    private struct GridJob : IJob
    {
      [ReadOnly] public NativeArray<Particle> Particles;
      public int Count; public float Scale;
      public NativeArray<int> Levels, Header;
      public NativeArray<float> Sizes, MaxRadii;
      public NativeParallelMultiHashMap<int4, int> Grid;
      public void Execute()
      {
        float minimum = float.PositiveInfinity, maximum = 0;
        for ( int i = 0; i < Count; ++i ) { minimum = math.min( minimum, Particles[i].Radius ); maximum = math.max( maximum, Particles[i].Radius ); }
        float baseSize = math.max( 2*minimum*Scale, 2*maximum*Scale/1073741824f );
        Header[0] = -1;
        if ( baseSize <= 0 || !math.isfinite( 2*maximum*Scale ) ) return;
        float size = baseSize;
        for ( int level = 0; level < 32; ++level ) { Sizes[level] = size; size *= 2; MaxRadii[level] = 0; }
        for ( int i = 0; i < Count; ++i ) {
          float radius = Particles[i].Radius; int level = 0;
          while ( level < 31 && Sizes[level] < 2*radius*Scale ) ++level;
          Levels[i] = level; Header[0] = math.max( Header[0], level );
          MaxRadii[level] = math.max( MaxRadii[level], radius );
        }
        for ( int level = 0; level < 32; ++level ) Sizes[level] = 2*MaxRadii[level]*Scale;
        for ( int i = 0; i < Count; ++i ) {
          int level = Levels[i]; var p = Particles[i].Position;
          Grid.Add( new int4( (int3)math.floor( new float3( p.x, p.y, p.z )/Sizes[level] ), level ), i );
        }
      }
    }

    private struct Search
    {
      [ReadOnly] public NativeArray<Particle> Particles;
      [ReadOnly] public NativeArray<int> Levels, Header;
      [ReadOnly] public NativeArray<float> Sizes, MaxRadii;
      [ReadOnly] public NativeParallelMultiHashMap<int4, int> Grid;
      public float Scale;
      public int Visit( int i, bool emit, NativeArray<Pair> output, int start, out int candidates )
      {
        int found = 0; candidates = 0;
        if ( Header[0] < 0 ) return 0;
        var p = Particles[i]; float3 position = new float3( p.Position.x, p.Position.y, p.Position.z );
        float positionEpsilon = math.max( 1e-6f, math.cmax( math.abs( position ) )*2.3841858e-7f );
        for ( int level = Levels[i]; level <= Header[0]; ++level ) {
          float size = Sizes[level]; if ( size <= 0 ) continue;
          float maximumReach = Scale*(p.Radius+MaxRadii[level])*1.00001f+positionEpsilon;
          int3 low = (int3)math.floor( (position-maximumReach)/size ), high = (int3)math.floor( (position+maximumReach)/size );
          for ( int z = low.z; z <= high.z; ++z )
            for ( int y = low.y; y <= high.y; ++y )
              for ( int x = low.x; x <= high.x; ++x ) {
                float3 cell = new float3( x, y, z );
                float3 gap = math.max( 0, math.max( cell*size-position, position-(cell+1)*size ) );
                if ( math.dot( gap, gap ) > maximumReach*maximumReach ) continue;
                if ( !Grid.TryGetFirstValue( new int4( x, y, z, level ), out int j, out var iterator ) ) continue;
                do {
                  if ( level == Levels[i] && j <= i ) continue;
                  ++candidates;
                  var q = Particles[j];
                  if ( p.MaterialIndex != q.MaterialIndex || (p.RenderingEligible == SoilParticleBridges.CenterAnchor && q.RenderingEligible == SoilParticleBridges.CenterAnchor) ) continue;
                  double reach = (double)Scale*((double)p.Radius+q.Radius);
                  double dx = (double)p.Position.x-q.Position.x; if ( dx > reach || dx < -reach ) continue;
                  double dy = (double)p.Position.y-q.Position.y; if ( dy > reach || dy < -reach ) continue;
                  double dz = (double)p.Position.z-q.Position.z; if ( dz > reach || dz < -reach ) continue;
                  double distance = dx*dx+dy*dy+dz*dz;
                  double contained = (p.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : (double)p.Radius)-(q.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : (double)q.Radius);
                  if ( distance > reach*reach || distance <= contained*contained+1e-12 ) continue;
                  if ( emit ) output[start+found] = new Pair { A = (uint)math.min( i, j ), B = (uint)math.max( i, j ) };
                  ++found;
                } while ( Grid.TryGetNextValue( out j, ref iterator ) );
              }
        }
        return found;
      }
    }

    [BurstCompile( CompileSynchronously = true, FloatMode = FloatMode.Strict )]
    private struct CountJob : IJobParallelFor
    {
      public Search Search;
      [WriteOnly] public NativeArray<int> Counts, Candidates;
      public void Execute( int index ) { Counts[index] = Search.Visit( index, false, default, 0, out int candidates ); Candidates[index] = candidates; }
    }
    [BurstCompile( CompileSynchronously = true )]
    private struct PrefixJob : IJob
    {
      [ReadOnly] public NativeArray<int> Counts, Candidates;
      public NativeArray<int> Offsets, Header;
      public int Count;
      public void Execute()
      {
        long total = 0, candidates = 0;
        for ( int i = 0; i < Count; ++i ) { Offsets[i] = (int)total; total += Counts[i]; candidates += Candidates[i]; }
        Header[1] = total > int.MaxValue ? -1 : (int)total;
        Header[2] = (int)math.min( candidates, int.MaxValue );
      }
    }
    [BurstCompile( CompileSynchronously = true, FloatMode = FloatMode.Strict )]
    private struct EmitJob : IJobParallelFor
    {
      public Search Search;
      [ReadOnly] public NativeArray<int> Offsets;
      // Prefix sums assign disjoint output ranges to particles. No atomics or dropped pairs.
      [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<Pair> Pairs;
      public void Execute( int index ) => Search.Visit( index, true, Pairs, Offsets[index], out _ );
    }
  }
}
