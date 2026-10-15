using System;
using System.Runtime.InteropServices;

namespace AGXUnity.Rendering
{
  /// <summary>Triangles of mutual neighbors fill the gaps between pair hulls.</summary>
  public sealed class SoilParticleFaces : IDisposable
  {
    public interface IAccelerator : IDisposable
    {
      void Build( int particleCount, SoilParticleBridges.Pair[] pairs, int pairCount, ref Face[] faces, out int count );
    }
    public static Func<IAccelerator> CreateAccelerator;
    private IAccelerator m_accelerator;
    private readonly bool m_allowAcceleration;
    public SoilParticleFaces( bool allowAcceleration = true ) { m_allowAcceleration = allowAcceleration; }
    public string Backend => m_accelerator == null ? "Managed" : "Burst";
    public void Dispose() { m_accelerator?.Dispose(); m_accelerator = null; }
    [StructLayout( LayoutKind.Sequential )]
    public struct Face { public uint A, B, C; }
    public Face[] Faces { get; private set; } = new Face[1024];
    public int Count { get; private set; }
    private int[] m_offsets = new int[1025], m_cursor = new int[1024], m_neighbors = new int[1024];

    public void Build( int particleCount, SoilParticleBridges.Pair[] pairs, int pairCount )
    {
      Count = 0;
      if ( pairCount < 3 ) return;
      if ( m_allowAcceleration && m_accelerator == null && CreateAccelerator != null ) m_accelerator = CreateAccelerator();
      if ( m_accelerator != null ) {
        var faces = Faces;
        m_accelerator.Build( particleCount, pairs, pairCount, ref faces, out int count );
        Faces = faces; Count = count;
        return;
      }
      if ( particleCount > m_cursor.Length ) {
        int capacity = UnityEngine.Mathf.NextPowerOfTwo( particleCount );
        m_offsets = new int[capacity+1]; m_cursor = new int[capacity];
      }
      if ( pairCount > m_neighbors.Length ) m_neighbors = new int[UnityEngine.Mathf.NextPowerOfTwo( pairCount )];
      Array.Clear( m_offsets, 0, particleCount+1 );
      for ( int i = 0; i < pairCount; ++i ) ++m_offsets[(int)pairs[i].A+1];
      for ( int i = 0; i < particleCount; ++i ) m_offsets[i+1] = checked( m_offsets[i+1]+m_offsets[i] );
      Array.Copy( m_offsets, m_cursor, particleCount );
      for ( int i = 0; i < pairCount; ++i ) m_neighbors[m_cursor[pairs[i].A]++] = (int)pairs[i].B;
      for ( int a = 0; a < particleCount; ++a ) Array.Sort( m_neighbors, m_offsets[a], m_offsets[a+1]-m_offsets[a] );
      // Forward adjacency (A < B < C) emits each clique once, independent of
      // pair order, without allocations or a global convex hull across the pile.
      for ( int a = 0; a < particleCount; ++a ) {
        int endA = m_offsets[a+1];
        for ( int edge = m_offsets[a]; edge < endA; ++edge ) {
          int b = m_neighbors[edge], i = edge+1, j = m_offsets[b], endB = m_offsets[b+1];
          while ( i < endA && j < endB ) {
            int c = m_neighbors[i], other = m_neighbors[j];
            if ( c < other ) ++i;
            else if ( other < c ) ++j;
            else {
              if ( Count == Faces.Length ) { var grown = Faces; Array.Resize( ref grown, checked( Faces.Length*2 ) ); Faces = grown; }
              Faces[Count++] = new Face { A = (uint)a, B = (uint)b, C = (uint)c };
              ++i; ++j;
            }
          }
        }
      }
    }
  }
}
