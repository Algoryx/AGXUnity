using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Pair = AGXUnity.Rendering.SoilParticleBridges.Pair;
using Face = AGXUnity.Rendering.SoilParticleFaces.Face;

namespace AGXUnity.Rendering.Burst
{
  /// <summary>Persistent adjacency and face buffers; only copied indices enter the Burst job.</summary>
  public sealed class BurstSoilParticleFaces : SoilParticleFaces.IAccelerator
  {
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Register() => SoilParticleFaces.CreateAccelerator =
      () => BurstCompiler.IsEnabled ? new BurstSoilParticleFaces() : null;
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor() => Register();
#endif
    private NativeArray<Pair> m_pairs;
    private NativeArray<int> m_offsets, m_cursor, m_neighbors;
    private NativeList<Face> m_faces;

    public void Build( int particleCount, Pair[] pairs, int pairCount, ref Face[] faces, out int count )
    {
      Grow( ref m_pairs, pairCount ); Grow( ref m_neighbors, pairCount );
      Grow( ref m_offsets, particleCount+1 ); Grow( ref m_cursor, particleCount );
      if ( !m_faces.IsCreated ) m_faces = new NativeList<Face>( 1024, Allocator.Persistent );
      NativeArray<Pair>.Copy( pairs, 0, m_pairs, 0, pairCount );
      new BuildJob { Pairs = m_pairs, PairCount = pairCount, ParticleCount = particleCount,
        Offsets = m_offsets, Cursor = m_cursor, Neighbors = m_neighbors, Faces = m_faces }.Run();
      count = m_faces.Length;
      if ( faces.Length < count ) Array.Resize( ref faces, Mathf.NextPowerOfTwo( count ) );
      NativeArray<Face>.Copy( m_faces.AsArray(), 0, faces, 0, count );
    }
    private static void Grow<T>( ref NativeArray<T> array, int count ) where T : struct
    {
      if ( array.IsCreated && array.Length >= count ) return;
      if ( array.IsCreated ) array.Dispose();
      array = new NativeArray<T>( Mathf.NextPowerOfTwo( count ), Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
    }
    public void Dispose()
    {
      if ( m_pairs.IsCreated ) m_pairs.Dispose(); if ( m_neighbors.IsCreated ) m_neighbors.Dispose();
      if ( m_offsets.IsCreated ) m_offsets.Dispose(); if ( m_cursor.IsCreated ) m_cursor.Dispose();
      if ( m_faces.IsCreated ) m_faces.Dispose();
      m_pairs = default; m_offsets = m_cursor = m_neighbors = default; m_faces = default;
    }
    [BurstCompile( CompileSynchronously = true )]
    private struct BuildJob : IJob
    {
      [ReadOnly] public NativeArray<Pair> Pairs;
      public int PairCount, ParticleCount;
      public NativeArray<int> Offsets, Cursor, Neighbors;
      public NativeList<Face> Faces;
      public void Execute()
      {
        Faces.Clear();
        for ( int i = 0; i <= ParticleCount; ++i ) Offsets[i] = 0;
        for ( int i = 0; i < PairCount; ++i ) ++Offsets[(int)Pairs[i].A+1];
        for ( int i = 0; i < ParticleCount; ++i ) { Offsets[i+1] += Offsets[i]; Cursor[i] = Offsets[i]; }
        for ( int i = 0; i < PairCount; ++i ) { var pair = Pairs[i]; int slot = Cursor[(int)pair.A]; Neighbors[slot] = (int)pair.B; Cursor[(int)pair.A] = slot+1; }
        for ( int a = 0; a < ParticleCount; ++a ) Neighbors.GetSubArray( Offsets[a], Offsets[a+1]-Offsets[a] ).Sort();
        for ( int a = 0; a < ParticleCount; ++a ) {
          int endA = Offsets[a+1];
          for ( int edge = Offsets[a]; edge < endA; ++edge ) {
            int b = Neighbors[edge], i = edge+1, j = Offsets[b], endB = Offsets[b+1];
            while ( i < endA && j < endB ) {
              int c = Neighbors[i], other = Neighbors[j];
              if ( c < other ) ++i;
              else if ( other < c ) ++j;
              else { Faces.Add( new Face { A = (uint)a, B = (uint)b, C = (uint)c } ); ++i; ++j; }
            }
          }
        }
      }
    }
  }
}
