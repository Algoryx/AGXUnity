using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnityEditor.Rendering
{
  /// <summary>Historical scalar readback on the SAME frozen native system as the optimized capture.</summary>
  internal sealed class SoilAcquisitionBenchmark
  {
    private delegate bool OwnsParticle( agx.GranularBodyPtr particle, out uint material );
    private sealed class Entry
    {
      public TerrainParticleSnapshotProvider Provider;
      public agxTerrain.SoilSimulationInterface Interface;
      public agx.GranularBodySystem System;
      public OwnsParticle Owns;
      public Action Refresh;
      public TerrainParticleSnapshotProvider.Particle[] Records;
      public int Count;
    }
    private readonly List<Entry> m_entries = new List<Entry>();
    private static bool Finite( float value ) => !float.IsNaN( value ) && !float.IsInfinity( value );
    public SoilAcquisitionBenchmark()
    {
      foreach ( var renderer in TerrainParticleSurfaceRenderer.Active ) {
        var provider = renderer.Snapshot;
        if ( provider == null || !provider.isActiveAndEnabled || provider.Terrain == null || !provider.Terrain.isActiveAndEnabled ) continue;
        var soil = provider.Terrain.GetSoilSimulationInterface();
        m_entries.Add( new Entry {
          Provider = provider, Interface = soil, System = soil.getGranularBodySystem(),
          Owns = (OwnsParticle)typeof( TerrainParticleSnapshotProvider ).GetMethod( "Owns", BindingFlags.NonPublic | BindingFlags.Instance ).CreateDelegate( typeof( OwnsParticle ), provider ),
          Refresh = (Action)typeof( TerrainParticleSnapshotProvider ).GetMethod( "RefreshMaterials", BindingFlags.NonPublic | BindingFlags.Instance ).CreateDelegate( typeof( Action ), provider ),
          Records = new TerrainParticleSnapshotProvider.Particle[Mathf.NextPowerOfTwo( Mathf.Max( 1, provider.Count ) )]
        } );
      }
    }

    private void Capture()
    {
      foreach ( var entry in m_entries ) {
        entry.Count = 0; entry.System.updateShouldRender(); entry.Refresh();
        using var particles = entry.Interface.getSoilParticles();
        uint size = particles.size();
        for ( uint i = 0; i < size; ++i ) {
          var native = particles.at( i );
          try {
            if ( !entry.Owns( native, out uint material ) ) continue;
            float enabled = native.getEnableRendering();
            if ( !Finite( enabled ) || enabled <= 0 ) continue;
            float radius = (float)native.getRadius(); var color = native.getColor();
            if ( !Finite( radius ) || radius <= 0 || color.w <= 0 ||
                 !Finite( color.x ) || !Finite( color.y ) ||
                 !Finite( color.z ) || !Finite( color.w ) ) continue;
            var position = native.position().ToHandedVector3(); var rotation = native.rotation().ToHandedQuaternion();
            if ( !Finite( position.x ) || !Finite( position.y ) || !Finite( position.z ) ||
                 !Finite( rotation.x ) || !Finite( rotation.y ) || !Finite( rotation.z ) || !Finite( rotation.w ) ) continue;
            if ( entry.Count == entry.Records.Length ) Array.Resize( ref entry.Records, entry.Records.Length*2 );
            entry.Records[entry.Count++] = new TerrainParticleSnapshotProvider.Particle {
              Position = position, Rotation = rotation, Radius = radius, Id = native.getId(),
              Color = new Color( color.x, color.y, color.z, 1 ), MaterialIndex = material,
              RenderingEligible = 1, TerrainInstanceId = entry.Provider.Terrain.GetInstanceID()
            };
          }
          finally { native.ReturnToPool(); }
        }
      }
    }

    public double Measure( out int records, out int providers )
    {
      for ( int i = 0; i < 10; ++i ) Capture();
      var watch = Stopwatch.StartNew();
      for ( int i = 0; i < 30; ++i ) Capture();
      watch.Stop(); records = 0; providers = m_entries.Count;
      foreach ( var entry in m_entries ) {
        records += entry.Count;
        if ( entry.Count != entry.Provider.Count ) throw new Exception( "Scalar/optimized acquisition count mismatch" );
        for ( int i = 0; i < entry.Count; ++i ) {
          var a = entry.Records[i]; var b = entry.Provider.Particles[i];
          if ( a.Id != b.Id || Vector3.Distance( a.Position, b.Position ) > 1e-4f || Mathf.Abs( a.Radius-b.Radius ) > 1e-6f ||
               Mathf.Abs( Quaternion.Dot( a.Rotation, b.Rotation ) ) < 0.99999f || a.Color != b.Color || a.MaterialIndex != b.MaterialIndex )
            throw new Exception( "Scalar/optimized acquisition record mismatch" );
        }
      }
      return watch.Elapsed.TotalMilliseconds/30;
    }
  }
}
