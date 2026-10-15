using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>Local convex hulls connect physical spheres without inflating them.</summary>
  public sealed class SoilParticleBridges : IDisposable
  {
    public interface IAccelerator : IDisposable
    {
      void Build( TerrainParticleSnapshotProvider.Particle[] particles, int count, float supportScale,
                  ref Pair[] pairs, out int pairCount, out int candidateCount );
    }
    // Installed by the optional Burst assembly. Core AGXUnity has no new package dependency.
    public static Func<IAccelerator> CreateAccelerator;
    private IAccelerator m_accelerator;
    private readonly bool m_allowAcceleration;
    public string Backend => m_accelerator == null ? "Managed" : "Burst Jobs";
    public SoilParticleBridges( bool allowAcceleration = true ) { m_allowAcceleration = allowAcceleration; }
    public void Dispose() { m_accelerator?.Dispose(); m_accelerator = null; }
    [StructLayout( LayoutKind.Sequential )]
    public struct Pair { public uint A, B; }
    // GPU records retain the rock's physical radius for neighbor search, but its
    // reconstruction primitive is a point at its center, never its outer sphere.
    public const uint CenterAnchor = 2;

    public Pair[] Pairs { get; private set; } = new Pair[1024];
    public int Count { get; private set; }
    public int CandidateCount { get; private set; }
    // A large particle must not put every small particle into one quadratic bucket.
    // Each power-of-two radius level has its own grid. Small particles query their
    // own and larger levels; large particles never scan the dense smaller grids.
    private readonly Dictionary<CellKey, int>[] m_cells = new Dictionary<CellKey, int>[32];
    private readonly float[] m_cellSizes = new float[32];
    private readonly float[] m_maxRadii = new float[32];
    private int[] m_next = new int[1024];
    private int[] m_levels = new int[1024];

    public void Build( TerrainParticleSnapshotProvider.Particle[] particles, int count, float supportScale )
    {
      Count = 0;
      CandidateCount = 0;
      if ( count < 2 ) return;
      if ( m_allowAcceleration && m_accelerator == null && CreateAccelerator != null ) m_accelerator = CreateAccelerator();
      if ( m_accelerator != null ) {
        var pairs = Pairs;
        m_accelerator.Build( particles, count, supportScale, ref pairs, out int pairCount, out int candidateCount );
        Pairs = pairs; Count = pairCount; CandidateCount = candidateCount;
        return;
      }
      for ( int level = 0; level < m_cells.Length; ++level ) { m_cells[level]?.Clear(); m_maxRadii[level] = 0; }
      if ( count > m_next.Length ) {
        m_next = new int[Mathf.NextPowerOfTwo( count )];
        m_levels = new int[m_next.Length];
      }
      float minRadius = float.PositiveInfinity, maxRadius = 0;
      for ( int i = 0; i < count; ++i ) {
        minRadius = Mathf.Min( minRadius, particles[i].Radius );
        maxRadius = Mathf.Max( maxRadius, particles[i].Radius );
      }
      float baseSize = 2 * minRadius * supportScale;
      if ( baseSize <= 0 || !SoilParticleSelection.Finite( 2 * maxRadius * supportScale ) ) return;
      // Bound the number of levels even for unusually wide radius distributions.
      baseSize = Mathf.Max( baseSize, 2 * maxRadius * supportScale / 1073741824f );
      for ( int level = 0; level < m_cellSizes.Length; ++level ) m_cellSizes[level] = baseSize * Mathf.Pow( 2, level );
      int highestLevel = 0;
      for ( int i = 0; i < count; ++i ) {
        var p = particles[i];
        int level = 0;
        while ( level < 31 && m_cellSizes[level] < 2 * p.Radius * supportScale ) ++level;
        m_levels[i] = level;
        highestLevel = Mathf.Max( highestLevel, level );
        m_maxRadii[level] = Mathf.Max( m_maxRadii[level], p.Radius );
      }
      for ( int level = 0; level <= highestLevel; ++level ) m_cellSizes[level] = 2 * m_maxRadii[level] * supportScale;
      for ( int i = 0; i < count; ++i ) {
        int level = m_levels[i];
        var p = particles[i];
        if ( m_cells[level] == null ) m_cells[level] = new Dictionary<CellKey, int>();
        var cell = Cell( p.Position, m_cellSizes[level] );
        m_next[i] = m_cells[level].TryGetValue( cell, out int head ) ? head : -1;
        m_cells[level][cell] = i;
      }
      for ( int i = 0; i < count; ++i ) {
        var p = particles[i];
        float positionEpsilon = Mathf.Max( 1e-6f, Mathf.Max( Mathf.Abs( p.Position.x ), Mathf.Max( Mathf.Abs( p.Position.y ), Mathf.Abs( p.Position.z ) ) )*2.3841858e-7f );
        for ( int level = m_levels[i]; level <= highestLevel; ++level ) {
          var grid = m_cells[level];
          if ( grid == null || grid.Count == 0 ) continue;
          float cellSize = m_cellSizes[level];
          // Widen only the broad phase to cover float rounding at cell and
          // tangent boundaries. The physical pair test below stays exact.
          float maximumReach = supportScale * (p.Radius + m_maxRadii[level])*1.00001f+positionEpsilon;
          var low = Cell( p.Position - Vector3.one * maximumReach, cellSize );
          var high = Cell( p.Position + Vector3.one * maximumReach, cellSize );
          for ( int z = low.Z; z <= high.Z; ++z )
            for ( int y = low.Y; y <= high.Y; ++y )
              for ( int x = low.X; x <= high.X; ++x ) {
                // Skip cells outside the spherical query, before hashing or
                // examining any of their particles.
                float cx = Mathf.Max( 0, Mathf.Max( x*cellSize-p.Position.x, p.Position.x-(x+1)*cellSize ) );
                float cy = Mathf.Max( 0, Mathf.Max( y*cellSize-p.Position.y, p.Position.y-(y+1)*cellSize ) );
                float cz = Mathf.Max( 0, Mathf.Max( z*cellSize-p.Position.z, p.Position.z-(z+1)*cellSize ) );
                if ( cx*cx+cy*cy+cz*cz > maximumReach*maximumReach ) continue;
                if ( !grid.TryGetValue( new CellKey( x, y, z ), out int j ) ) continue;
                for ( ; j >= 0; j = m_next[j] ) {
                  if ( level == m_levels[i] && j <= i ) continue;
                  ++CandidateCount;
                  ref readonly var q = ref particles[j];
                  if ( p.MaterialIndex != q.MaterialIndex || (p.RenderingEligible == CenterAnchor && q.RenderingEligible == CenterAnchor) ) continue;
                  // Explicit double arithmetic avoids Mono's extended float
                  // temporaries disagreeing with Burst at exact thresholds.
                  double reach = (double)supportScale * ((double)p.Radius + q.Radius);
                  double dx = (double)p.Position.x-q.Position.x;
                  if ( dx > reach || dx < -reach ) continue;
                  double dy = (double)p.Position.y-q.Position.y;
                  if ( dy > reach || dy < -reach ) continue;
                  double dz = (double)p.Position.z-q.Position.z;
                  if ( dz > reach || dz < -reach ) continue;
                  double distanceSquared = dx*dx+dy*dy+dz*dz;
                  double contained = (p.RenderingEligible == CenterAnchor ? 0.0 : (double)p.Radius) - (q.RenderingEligible == CenterAnchor ? 0.0 : (double)q.Radius);
                  // A contained sphere adds no new hull; coincident centers have no tangent axis.
                  if ( distanceSquared > reach * reach || distanceSquared <= contained * contained + 1e-12 ) continue;
                  if ( Count == Pairs.Length ) {
                    var grown = Pairs;
                    Array.Resize( ref grown, checked( Pairs.Length * 2 ) );
                    Pairs = grown;
                  }
                  Pairs[Count++] = new Pair { A = (uint)(i < j ? i : j), B = (uint)(i < j ? j : i) };
                }
              }
        }
      }
    }

    private readonly struct CellKey : IEquatable<CellKey>
    {
      public readonly int X, Y, Z;
      public CellKey( int x, int y, int z ) { X = x; Y = y; Z = z; }
      public bool Equals( CellKey other ) => X == other.X && Y == other.Y && Z == other.Z;
      public override bool Equals( object other ) => other is CellKey key && Equals( key );
      public override int GetHashCode() => unchecked( X * 73856093 ^ Y * 19349663 ^ Z * 83492791 );
    }

    private static CellKey Cell( Vector3 position, float size ) => new CellKey(
      Mathf.FloorToInt( position.x / size ), Mathf.FloorToInt( position.y / size ), Mathf.FloorToInt( position.z / size ) );
  }
}
