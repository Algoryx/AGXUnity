using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Particle = AGXUnity.Rendering.TerrainParticleSnapshotProvider.Particle;
using Pair = AGXUnity.Rendering.SoilParticleBridges.Pair;
using Entry = AGXUnity.Rendering.SoilConnectionHistory.Entry;

namespace AGXUnity.Rendering.Burst
{
  public sealed class BurstSoilConnectionHistory : SoilConnectionHistory.IAccelerator
  {
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Register() => SoilConnectionHistory.CreateAccelerator =
      () => BurstCompiler.IsEnabled ? new BurstSoilConnectionHistory() : null;
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor() => Register();
#endif
    private NativeArray<Particle> m_particles;
    private NativeList<Pair> m_pairs;
    private NativeList<Entry> m_history;
    private NativeParallelHashMap<uint, int> m_indices;

    public void Clear() { if ( m_history.IsCreated ) m_history.Clear(); }
    public void Apply( Particle[] particles, int count, float scale, float hysteresis, ref Pair[] pairs, ref int pairCount )
    {
      if ( !m_particles.IsCreated || m_particles.Length < count ) {
        if ( m_particles.IsCreated ) m_particles.Dispose();
        if ( m_indices.IsCreated ) m_indices.Dispose();
        int capacity = Mathf.NextPowerOfTwo( count );
        m_particles = new NativeArray<Particle>( capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
        m_indices = new NativeParallelHashMap<uint, int>( capacity, Allocator.Persistent );
      }
      if ( !m_pairs.IsCreated ) { m_pairs = new NativeList<Pair>( 1024, Allocator.Persistent ); m_history = new NativeList<Entry>( 1024, Allocator.Persistent ); }
      NativeArray<Particle>.Copy( particles, 0, m_particles, 0, count );
      m_pairs.ResizeUninitialized( pairCount );
      NativeArray<Pair>.Copy( pairs, 0, m_pairs.AsArray(), 0, pairCount );
      new RetainJob { Particles = m_particles, Count = count, Scale = scale, Hysteresis = hysteresis,
        Indices = m_indices, Pairs = m_pairs, History = m_history }.Run();
      pairCount = m_pairs.Length;
      if ( pairs.Length < pairCount ) Array.Resize( ref pairs, Mathf.NextPowerOfTwo( pairCount ) );
      NativeArray<Pair>.Copy( m_pairs.AsArray(), 0, pairs, 0, pairCount );
    }
    public void Dispose()
    {
      if ( m_particles.IsCreated ) m_particles.Dispose(); if ( m_indices.IsCreated ) m_indices.Dispose();
      if ( m_pairs.IsCreated ) m_pairs.Dispose(); if ( m_history.IsCreated ) m_history.Dispose();
      m_particles = default; m_indices = default; m_pairs = default; m_history = default;
    }
    [BurstCompile( CompileSynchronously = true, FloatMode = FloatMode.Strict )]
    private struct RetainJob : IJob
    {
      [ReadOnly] public NativeArray<Particle> Particles;
      public int Count;
      public float Scale, Hysteresis;
      public NativeParallelHashMap<uint, int> Indices;
      public NativeList<Pair> Pairs;
      public NativeList<Entry> History;
      public void Execute()
      {
        Indices.Clear();
        for ( int i = 0; i < Count; ++i ) Indices.TryAdd( Particles[i].Id, i );
        for ( int i = 0; i < History.Length; ++i ) {
          var old = History[i];
          int a = old.IndexA, b = old.IndexB;
          if ( a >= Count || Particles[a].Id != old.A ) { if ( !Indices.TryGetValue( old.A, out a ) ) continue; }
          if ( b >= Count || Particles[b].Id != old.B ) { if ( !Indices.TryGetValue( old.B, out b ) ) continue; }
          if ( a == b ) continue;
          var p = Particles[a]; var q = Particles[b];
          if ( p.MaterialIndex != q.MaterialIndex || (p.RenderingEligible == SoilParticleBridges.CenterAnchor && q.RenderingEligible == SoilParticleBridges.CenterAnchor) ) continue;
          double dx = (double)p.Position.x-q.Position.x, dy = (double)p.Position.y-q.Position.y, dz = (double)p.Position.z-q.Position.z;
          double distance = dx*dx+dy*dy+dz*dz;
          double join = (double)Scale*((double)p.Radius+q.Radius), release = join*(1.0+Hysteresis);
          if ( distance <= join*join || distance > release*release ) continue;
          double contained = (p.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : p.Radius)-(q.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : q.Radius);
          if ( distance <= contained*contained+1e-12 ) continue;
          Pairs.Add( new Pair { A = (uint)Math.Min( a, b ), B = (uint)Math.Max( a, b ) } );
        }
        History.ResizeUninitialized( Pairs.Length );
        for ( int i = 0; i < Pairs.Length; ++i ) {
          var pair = Pairs[i]; History[i] = new Entry { A = Particles[(int)pair.A].Id, B = Particles[(int)pair.B].Id,
            IndexA = (int)pair.A, IndexB = (int)pair.B };
        }
      }
    }
  }
}
