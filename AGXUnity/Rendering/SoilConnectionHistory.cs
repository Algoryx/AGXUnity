using System;
using System.Collections.Generic;
using Particle = AGXUnity.Rendering.TerrainParticleSnapshotProvider.Particle;
using Pair = AGXUnity.Rendering.SoilParticleBridges.Pair;

namespace AGXUnity.Rendering
{
  /// <summary>Retain only old edges outside the join radius but inside the release radius.</summary>
  public sealed class SoilConnectionHistory : IDisposable
  {
    public interface IAccelerator : IDisposable
    {
      void Apply( Particle[] particles, int count, float scale, float hysteresis, ref Pair[] pairs, ref int pairCount );
      void Clear();
    }
    public static Func<IAccelerator> CreateAccelerator;
    public struct Entry { public uint A, B; public int IndexA, IndexB; }
    private IAccelerator m_accelerator;
    private readonly bool m_allowAcceleration;
    private readonly Dictionary<uint, int> m_indices = new Dictionary<uint, int>();
    private Entry[] m_history = new Entry[1024];
    private int m_count;
    public SoilConnectionHistory( bool allowAcceleration = true ) { m_allowAcceleration = allowAcceleration; }
    public void Clear() { m_count = 0; m_indices.Clear(); m_accelerator?.Clear(); }
    public void Dispose() { Clear(); m_accelerator?.Dispose(); m_accelerator = null; }

    public void Apply( Particle[] particles, int count, float scale, float hysteresis, ref Pair[] pairs, ref int pairCount )
    {
      if ( count < 2 || hysteresis <= 0 || !SoilParticleSelection.Finite( hysteresis ) ) { Clear(); return; }
      if ( m_allowAcceleration && m_accelerator == null && CreateAccelerator != null ) m_accelerator = CreateAccelerator();
      if ( m_accelerator != null ) { m_accelerator.Apply( particles, count, scale, hysteresis, ref pairs, ref pairCount ); return; }
      m_indices.Clear();
      for ( int i = 0; i < count; ++i ) m_indices[particles[i].Id] = i;
      for ( int i = 0; i < m_count; ++i ) {
        var old = m_history[i];
        int a = old.IndexA, b = old.IndexB;
        // Most packed indices survive unchanged. Validate identity before using
        // the cached index; only reordered records need a hash lookup.
        if ( a >= count || particles[a].Id != old.A ) { if ( !m_indices.TryGetValue( old.A, out a ) ) continue; }
        if ( b >= count || particles[b].Id != old.B ) { if ( !m_indices.TryGetValue( old.B, out b ) ) continue; }
        if ( a == b ) continue;
        ref readonly var p = ref particles[a]; ref readonly var q = ref particles[b];
        if ( p.MaterialIndex != q.MaterialIndex || (p.RenderingEligible == SoilParticleBridges.CenterAnchor && q.RenderingEligible == SoilParticleBridges.CenterAnchor) ) continue;
        double dx = (double)p.Position.x-q.Position.x, dy = (double)p.Position.y-q.Position.y, dz = (double)p.Position.z-q.Position.z;
        double distance = dx*dx+dy*dy+dz*dz;
        double join = (double)scale*((double)p.Radius+q.Radius), release = join*(1.0+hysteresis);
        // Inside the join threshold the normal search already emitted the edge.
        if ( distance <= join*join || distance > release*release ) continue;
        double contained = (p.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : p.Radius)-(q.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : q.Radius);
        if ( distance <= contained*contained+1e-12 ) continue;
        if ( pairCount == pairs.Length ) Array.Resize( ref pairs, checked( pairs.Length*2 ) );
        pairs[pairCount++] = new Pair { A = (uint)Math.Min( a, b ), B = (uint)Math.Max( a, b ) };
      }
      if ( m_history.Length < pairCount ) Array.Resize( ref m_history, UnityEngine.Mathf.NextPowerOfTwo( pairCount ) );
      for ( int i = 0; i < pairCount; ++i )
        m_history[i] = new Entry { A = particles[pairs[i].A].Id, B = particles[pairs[i].B].Id,
          IndexA = (int)pairs[i].A, IndexB = (int)pairs[i].B };
      m_count = pairCount;
    }
  }
}
