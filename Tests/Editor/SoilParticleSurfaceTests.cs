using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using AGXUnity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class SoilParticleSurfaceTests
  {
    [Test]
    public void GpuRecordLayoutMatchesHlsl()
    {
      Assert.AreEqual( 64, Marshal.SizeOf<TerrainParticleSnapshotProvider.Particle>() );
      Assert.AreEqual( 12, Marshal.OffsetOf<TerrainParticleSnapshotProvider.Particle>( "Radius" ).ToInt32() );
      Assert.AreEqual( 16, Marshal.OffsetOf<TerrainParticleSnapshotProvider.Particle>( "Rotation" ).ToInt32() );
      Assert.AreEqual( 32, Marshal.OffsetOf<TerrainParticleSnapshotProvider.Particle>( "Color" ).ToInt32() );
      Assert.AreEqual( 48, Marshal.OffsetOf<TerrainParticleSnapshotProvider.Particle>( "Id" ).ToInt32() );
      Assert.AreEqual( 60, Marshal.OffsetOf<TerrainParticleSnapshotProvider.Particle>( "MaterialIndex" ).ToInt32() );
      Assert.AreEqual( 8, Marshal.SizeOf<SoilParticleBridges.Pair>() );
    }

    [TestCase( 0, 0, 0 )]
    [TestCase( 180, 0, 0 )]
    [TestCase( 0, 180, 0 )]
    [TestCase( 0, 0, 180 )]
    [TestCase( 123, 67, -41 )]
    public void BulkMatrixRotationPreservesAllQuaternionBranches( float x, float y, float z )
    {
      const float radius = 0.37f;
      var expected = Quaternion.Euler( x, y, z );
      var m = Matrix4x4.TRS( new Vector3( 3, -7, 2 ), expected, Vector3.one*2*radius );
      var native = new agx.AffineMatrix4x4f {
        e00 = m.m00, e01 = m.m10, e02 = m.m20,
        e10 = m.m01, e11 = m.m11, e12 = m.m21,
        e20 = m.m02, e21 = m.m12, e22 = m.m22
      };
      var method = typeof( TerrainParticleSnapshotProvider ).GetMethod( "ReadBulkRotation", BindingFlags.Static | BindingFlags.NonPublic );
      var actual = (Quaternion)method.Invoke( null, new object[] { native, radius } );
      Assert.Greater( Mathf.Abs( Quaternion.Dot( expected, actual ) ), 0.99999f );
    }

    [Test]
    public void BridgeReachConnectsPhysicalSpheresWithoutChangingTheirRadii()
    {
      var particles = new[] {
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( -1.2f, 0, 0 ), Radius = 1 },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 1.2f, 0, 0 ), Radius = 1 }
      };
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, 2, 1 );
      Assert.AreEqual( 0, bridges.Count );
      bridges.Build( particles, 2, 1.5f );
      Assert.AreEqual( 1, bridges.Count );
      Assert.AreEqual( 0u, bridges.Pairs[0].A );
      Assert.AreEqual( 1u, bridges.Pairs[0].B );
      Assert.AreEqual( 1, particles[0].Radius );
      Assert.AreEqual( 1, particles[1].Radius );
      bridges.Build( particles, 1, 10 );
      Assert.AreEqual( 0, bridges.Count );
    }

    [Test]
    public void BridgesRespectMaterialsContainmentAndNeighborCells()
    {
      var particles = new[] {
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( -0.1f, 0, 0 ), Radius = 1 },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 0.1f, 0, 0 ), Radius = 0.1f },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 1.9f, 0, 0 ), Radius = 1, MaterialIndex = 1 },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( -2.1f, 0, 0 ), Radius = 1 }
      };
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, 4, 1.15f );
      Assert.AreEqual( 1, bridges.Count );
      Assert.AreEqual( 0u, bridges.Pairs[0].A );
      Assert.AreEqual( 3u, bridges.Pairs[0].B );
      bridges.Build( particles, 0, 1.15f );
      Assert.AreEqual( 0, bridges.Count );
    }

    [Test]
    public void MultilevelBridgeSearchMatchesExhaustivePairsForMixedRadii()
    {
      var random = new System.Random( 4817 );
      var particles = new TerrainParticleSnapshotProvider.Particle[240];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( (float)random.NextDouble()*8-4, (float)random.NextDouble()*8-4, (float)random.NextDouble()*8-4 );
        particles[i].Radius = 0.02f*Mathf.Pow( 2, i%9 );
        particles[i].MaterialIndex = (uint)(i%3);
      }
      var expected = new HashSet<ulong>();
      for ( int i = 0; i < particles.Length; ++i )
        for ( int j = i+1; j < particles.Length; ++j ) {
          double dx = (double)particles[i].Position.x-particles[j].Position.x;
          double dy = (double)particles[i].Position.y-particles[j].Position.y;
          double dz = (double)particles[i].Position.z-particles[j].Position.z;
          double distance = dx*dx+dy*dy+dz*dz;
          double reach = 1.5*((double)particles[i].Radius+particles[j].Radius);
          double contained = (double)particles[i].Radius-particles[j].Radius;
          if ( particles[i].MaterialIndex == particles[j].MaterialIndex && distance <= reach*reach && distance > contained*contained+1e-12 )
            expected.Add( ((ulong)i << 32) | (uint)j );
        }
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, particles.Length, 1.5f );
      var actual = new HashSet<ulong>();
      for ( int i = 0; i < bridges.Count; ++i )
        Assert.IsTrue( actual.Add( ((ulong)bridges.Pairs[i].A << 32) | bridges.Pairs[i].B ), "Duplicate bridge" );
      Assert.AreEqual( expected.Count, actual.Count );
      Assert.IsTrue( expected.SetEquals( actual ) );
    }

    [Test]
    public void IndividualRocksAnchorAtTheirCentersAndNeverBridgeEachOther()
    {
      var particles = new[] {
        new TerrainParticleSnapshotProvider.Particle { Position = Vector3.zero, Radius = 1 },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 0.9f, 0, 0 ), Radius = 0.1f, RenderingEligible = SoilParticleBridges.CenterAnchor },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 1.2f, 0, 0 ), Radius = 1, RenderingEligible = SoilParticleBridges.CenterAnchor },
        new TerrainParticleSnapshotProvider.Particle { Position = new Vector3( 2.2f, 0, 0 ), Radius = 1, RenderingEligible = SoilParticleBridges.CenterAnchor }
      };
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, particles.Length, 1.15f );
      var actual = new HashSet<ulong>();
      for ( int i = 0; i < bridges.Count; ++i ) actual.Add( ((ulong)bridges.Pairs[i].A << 32) | bridges.Pairs[i].B );
      Assert.AreEqual( 2, bridges.Count );
      Assert.IsTrue( actual.Contains( 2ul ) );
      Assert.IsTrue( actual.Contains( 3ul ) );
      // The center inside the soil sphere needs no bridge, and anchor-anchor
      // links would add soil where only individual particles were requested.
      Assert.IsFalse( actual.Contains( 1ul ) );
    }

    [TestCase( 41, 1.0f )]
    [TestCase( 178, 1.5f )]
    [TestCase( 951, 2.3f )]
    public void AcceleratedSearchMatchesManagedWithAnchorsAndMixedMaterials( int seed, float support )
    {
      var random = new System.Random( seed );
      var particles = new TerrainParticleSnapshotProvider.Particle[700];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( (float)random.NextDouble()*6-3, (float)random.NextDouble()*6-3, (float)random.NextDouble()*6-3 );
        particles[i].Radius = 0.013f*Mathf.Pow( 2, i%9 );
        particles[i].MaterialIndex = (uint)(i%4);
        particles[i].RenderingEligible = i%7 == 0 ? SoilParticleBridges.CenterAnchor : 1;
      }
      using var managed = new SoilParticleBridges( false );
      using var accelerated = new SoilParticleBridges();
      managed.Build( particles, particles.Length, support );
      accelerated.Build( particles, particles.Length, support );
      var expected = new HashSet<ulong>();
      for ( int i = 0; i < managed.Count; ++i ) expected.Add( ((ulong)managed.Pairs[i].A << 32) | managed.Pairs[i].B );
      Assert.AreEqual( expected.Count, accelerated.Count );
      for ( int i = 0; i < accelerated.Count; ++i ) Assert.IsTrue( expected.Remove( ((ulong)accelerated.Pairs[i].A << 32) | accelerated.Pairs[i].B ) );
      Assert.AreEqual( 0, expected.Count );
      // Reuse after emptying and release/recreation must not leave stale pairs or native storage.
      accelerated.Build( particles, 0, support ); Assert.AreEqual( 0, accelerated.Count );
      accelerated.Dispose(); accelerated.Build( particles, particles.Length, support );
      Assert.AreEqual( managed.Count, accelerated.Count );
    }

    [Test]
    public void DenseParallelOutputHasNoTruncationOrDuplicatesAfterShrinking()
    {
      var particles = new TerrainParticleSnapshotProvider.Particle[320];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( i*0.001f, 0, 0 ); particles[i].Radius = 0.5f;
      }
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, particles.Length, 1 );
      Assert.AreEqual( 320*319/2, bridges.Count );
      var pairs = new HashSet<ulong>();
      for ( int i = 0; i < bridges.Count; ++i ) Assert.IsTrue( pairs.Add( ((ulong)bridges.Pairs[i].A << 32) | bridges.Pairs[i].B ) );
      bridges.Build( particles, 64, 1 );
      Assert.AreEqual( 64*63/2, bridges.Count );
    }

    [TestCase( 0f )]
    [TestCase( 17f )]
    [TestCase( 120f )]
    public void TangentGridBoundariesMatchExhaustiveDoublePrecision( float offset )
    {
      var particles = new TerrainParticleSnapshotProvider.Particle[384];
      for ( int i = 0; i < particles.Length; ++i ) {
        particles[i].Position = new Vector3( (i%32)*0.095f+offset, ((i/32)%4)*0.095f, (i/128)*0.095f );
        particles[i].Radius = 0.04f+0.04f*(i%7)/6;
        particles[i].RenderingEligible = SoilParticleSelection.IsIndividualParticle( (uint)i, 0.05 ) ? SoilParticleBridges.CenterAnchor : 1;
      }
      var expected = new HashSet<ulong>();
      for ( int i = 0; i < particles.Length; ++i ) for ( int j = i+1; j < particles.Length; ++j ) {
        var a = particles[i]; var b = particles[j];
        if ( a.RenderingEligible == SoilParticleBridges.CenterAnchor && b.RenderingEligible == SoilParticleBridges.CenterAnchor ) continue;
        double dx = (double)a.Position.x-b.Position.x, dy = (double)a.Position.y-b.Position.y, dz = (double)a.Position.z-b.Position.z;
        double reach = 1.5*((double)a.Radius+b.Radius);
        double contained = (a.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : (double)a.Radius)-(b.RenderingEligible == SoilParticleBridges.CenterAnchor ? 0.0 : (double)b.Radius);
        double distance = dx*dx+dy*dy+dz*dz;
        if ( distance <= reach*reach && distance > contained*contained+1e-12 ) expected.Add( ((ulong)i << 32) | (uint)j );
      }
      foreach ( bool accelerate in new[] { false, true } ) {
        using var bridges = new SoilParticleBridges( accelerate );
        bridges.Build( particles, particles.Length, 1.5f );
        var actual = new HashSet<ulong>();
        for ( int i = 0; i < bridges.Count; ++i ) Assert.IsTrue( actual.Add( ((ulong)bridges.Pairs[i].A << 32) | bridges.Pairs[i].B ) );
        Assert.IsTrue( expected.SetEquals( actual ), $"{bridges.Backend} at offset {offset}: expected {expected.Count}, actual {actual.Count}" );
      }
    }

    [TestCase( 0f, 1f )]
    [TestCase( float.MaxValue, 2f )]
    public void EmptyOrUnrepresentableGridDoesNotReadUninitializedLevels( float radius, float support )
    {
      var particles = new[] {
        new TerrainParticleSnapshotProvider.Particle { Radius = radius },
        new TerrainParticleSnapshotProvider.Particle { Radius = radius, Position = Vector3.right }
      };
      using var bridges = new SoilParticleBridges();
      bridges.Build( particles, particles.Length, support );
      Assert.AreEqual( 0, bridges.Count );
    }

    [TestCase( 0u, 0u )]
    [TestCase( 1u, 0x688990c0u )]
    [TestCase( 2u, 0xd1132181u )]
    public void ReferenceHashUsesStrictThreshold( uint identity, uint expectedHash )
    {
      double threshold = (double)expectedHash / 4294967296.0;
      Assert.IsFalse( SoilParticleSelection.IsIndividualParticle( identity, threshold ) );
      Assert.IsTrue( SoilParticleSelection.IsIndividualParticle( identity, threshold + 1.0 / 4294967296.0 ) );
    }

    [Test]
    public void FractionsPartitionAllIdentitiesAndRemainStable()
    {
      var original = new Dictionary<uint, bool>();
      int selected = 0;
      for ( uint id = 0; id < 100000; ++id ) {
        Assert.IsFalse( SoilParticleSelection.IsIndividualParticle( id, 0 ) );
        Assert.IsTrue( SoilParticleSelection.IsIndividualParticle( id, 1 ) );
        bool individual = SoilParticleSelection.IsIndividualParticle( id, 0.05 );
        if ( individual ) ++selected;
        if ( id % 7 == 0 ) original.Add( id, individual );
      }
      Assert.That( selected, Is.InRange( 4500, 5500 ) );
      // Reordered surviving records carry identity, never their new array index.
      for ( uint id = 99995; id > 0; id -= 7 )
        Assert.AreEqual( original[id], SoilParticleSelection.IsIndividualParticle( id, 0.05 ) );
    }
  }
}
