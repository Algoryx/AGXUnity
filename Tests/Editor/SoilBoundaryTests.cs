using System;
using System.Runtime.InteropServices;
using AGXUnity.Rendering;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Particle = AGXUnity.Rendering.TerrainParticleSnapshotProvider.Particle;

namespace AGXUnityTesting.Editor
{
  public class SoilBoundaryTests
  {
    [Test]
    public void BoundaryGpuLayoutsMatchShader()
    {
      Assert.AreEqual( 32, Marshal.SizeOf<SoilBoundaryGeometry.Node>() );
      Assert.AreEqual( 48, Marshal.SizeOf<SoilBoundaryGeometry.Triangle>() );
      Assert.AreEqual( 96, Marshal.SizeOf<SoilSurfaceBoundaries.GpuShape>() );
      Assert.AreEqual( 32, Marshal.SizeOf<SoilSurfaceBoundaries.Contacts>() );
      Assert.AreEqual( 20, Marshal.SizeOf<SoilSurfaceBoundaries.ProximityQuery>() ); Assert.AreEqual( 32, Marshal.SizeOf<SoilSurfaceBoundaries.ProximityHit>() );
    }
    [Test]
    public void BvhClosestMatchesExhaustiveTriangleSearch()
    {
      var random = new System.Random( 17 ); Vector3 Point() => new Vector3( (float)random.NextDouble()*8-4, (float)random.NextDouble()*8-4, (float)random.NextDouble()*8-4 );
      var vertices = new Vector3[600]; var indices = new int[600];
      for ( int i = 0; i < vertices.Length; ++i ) { vertices[i] = Point(); indices[i] = i; }
      var geometry = new SoilBoundaryGeometry( vertices, indices );
      for ( int sample = 0; sample < 100; ++sample ) {
        var p = Point(); float best = float.PositiveInfinity;
        for ( int i = 0; i < indices.Length; i += 3 ) best = Mathf.Min( best, (p-SoilBoundaryGeometry.ClosestTriangle( p, vertices[i], vertices[i+1], vertices[i+2] )).sqrMagnitude );
        Assert.IsTrue( geometry.Closest( p, 100, out var q, out var normal ) );
        Assert.AreEqual( best, (p-q).sqrMagnitude, 1e-5f ); Assert.AreEqual( 1, normal.magnitude, 1e-5f );
      }
      Assert.IsFalse( geometry.Closest( Vector3.one*100, 0.1f, out _, out _ ) );
      var degenerate = new SoilBoundaryGeometry( new Vector3[3], new[] { 0,1,2 } );
      Assert.IsFalse( degenerate.Closest( Vector3.zero, 1, out _, out _ ) );
    }
    [Test]
    public void ClosestTriangleHandlesInteriorEdgesAndVertices()
    {
      var a = Vector3.zero; var b = Vector3.right; var c = Vector3.forward;
      Assert.AreEqual( new Vector3( 0.2f, 0, 0.3f ), SoilBoundaryGeometry.ClosestTriangle( new Vector3( 0.2f, 2, 0.3f ), a, b, c ) );
      Assert.AreEqual( a, SoilBoundaryGeometry.ClosestTriangle( new Vector3( -1, 2, -1 ), a, b, c ) );
      Assert.AreEqual( b, SoilBoundaryGeometry.ClosestTriangle( new Vector3( 2, 2, 0 ), a, b, c ) );
      Assert.AreEqual( c, SoilBoundaryGeometry.ClosestTriangle( new Vector3( 0, 2, 2 ), a, b, c ) );
      Assert.AreEqual( new Vector3( 0.5f, 0, 0.5f ), SoilBoundaryGeometry.ClosestTriangle( new Vector3( 2, 2, 2 ), a, b, c ) );
    }
    [Test]
    public void BurstProximityQueriesMatchManagedBvhAfterRigidTransformsAndGeometryUpdates()
    {
      if ( SoilSurfaceBoundaries.CreateQueryAccelerator == null ) Assert.Ignore( "Optional Burst assembly unavailable" );
      using var accelerator = SoilSurfaceBoundaries.CreateQueryAccelerator();
      if ( accelerator == null ) Assert.Ignore( "Burst disabled" );
      var random = new System.Random( 19 );
      var vertices = new Vector3[300]; var indices = new int[300];
      Vector3 Point() => new Vector3( (float)random.NextDouble()*4-2, (float)random.NextDouble()*4-2, (float)random.NextDouble()*4-2 );
      for ( int i = 0; i < vertices.Length; ++i ) { vertices[i] = Point(); indices[i] = i; }
      var pose = Matrix4x4.TRS( new Vector3( 7,2,-3 ), Quaternion.Euler( 23,37,11 ), Vector3.one );
      var shapes = new[] { new SoilSurfaceBoundaries.GpuShape { WorldToLocal = pose.inverse, SizeKind = new Vector4( 0,0,0,3 ), Metadata = Vector4.zero } };
      var queries = new SoilSurfaceBoundaries.ProximityQuery[100]; var hits = new SoilSurfaceBoundaries.ProximityHit[100];
      for ( int step = 1; step <= 2; ++step ) {
        if ( step == 2 ) for ( int i = 0; i < vertices.Length; ++i ) vertices[i].y += 0.3f;
        var geometry = new SoilBoundaryGeometry( vertices, indices );
        for ( int i = 0; i < queries.Length; ++i ) queries[i] = new SoilSurfaceBoundaries.ProximityQuery { Shape = 0, Reach = i%3 == 0 ? 0.1f : 10, Point = pose.MultiplyPoint3x4( Point() ) };
        accelerator.Query( shapes, 1, geometry.Nodes, geometry.NodeCount, geometry.Triangles, geometry.Triangles.Length, step, queries, queries.Length, ref hits );
        for ( int i = 0; i < queries.Length; ++i ) {
          var local = pose.inverse.MultiplyPoint3x4( queries[i].Point ); bool found = geometry.Closest( local, queries[i].Reach, out var q, out var n );
          Assert.AreEqual( found, hits[i].Valid != 0 ); if ( !found ) continue;
          Assert.Less( Vector3.Distance( pose.MultiplyPoint3x4( q ), hits[i].Point ), 0.0001f );
          Assert.AreEqual( Vector3.Distance( local,q ), hits[i].Distance, 0.0001f );
          Assert.Greater( Vector3.Dot( pose.MultiplyVector( n ), hits[i].Normal ), 0.999f );
        }
      }
    }
    [Test]
    public void ClosedMeshOrientationAndOpenMeshesAreDistinguished()
    {
      var v = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
      var indices = new[] { 0,2,1, 0,1,3, 0,3,2, 1,2,3 };
      var closed = new SoilBoundaryGeometry( v, indices, true ); Assert.IsTrue( closed.IsClosed ); Assert.AreEqual( 1, closed.ExteriorSide );
      for ( int i = 0; i < indices.Length; i += 3 ) { int swap = indices[i+1]; indices[i+1] = indices[i+2]; indices[i+2] = swap; }
      var reversed = new SoilBoundaryGeometry( v, indices, true ); Assert.IsTrue( reversed.IsClosed ); Assert.AreEqual( -1, reversed.ExteriorSide );
      Assert.IsFalse( new SoilBoundaryGeometry( v, new[] { 0,2,1 }, true ).IsClosed );
    }
    [Test]
    public void PrecomputedCollisionMeshesUseAgxHandednessAndScaledLocalCoordinates()
    {
      var go = new GameObject();
      try {
        go.transform.SetPositionAndRotation( new Vector3( 1, 2, 3 ), Quaternion.Euler( 0, 35, 0 ) ); go.transform.localScale = new Vector3( 2, 1, 3 );
        var shape = go.AddComponent<AGXUnity.Collide.Mesh>();
        shape.PrecomputedCollisionMeshes = new[] { new AGXUnity.Collide.CollisionMeshData {
          Vertices = new[] { new Vector3( 1,0,-1 ), new Vector3( 1,0,1 ), new Vector3( -1,0,-1 ), new Vector3( -1,0,1 ) },
          Indices = new[] { 0,2,1, 2,3,1 }
        } };
        using var boundaries = new SoilSurfaceBoundaries(); boundaries.Refresh( new AGXUnity.Collide.Shape[] { shape }, new Bounds( Vector3.zero, Vector3.one*100 ), true );
        var p = new[] { new Particle { Id = 10, Radius = 1, Position = go.transform.TransformPoint( Vector3.up*1.1f ), Rotation = Quaternion.identity } };
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.Greater( boundaries.PatchCount, 0 );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) {
          var local = go.transform.InverseTransformPoint( boundaries.Patches[i].A );
          Assert.AreEqual( 0.005f, local.y, 1e-4f ); Assert.LessOrEqual( Mathf.Abs( local.x ), 1.001f ); Assert.LessOrEqual( Mathf.Abs( local.z ), 1.001f );
        }
      }
      finally { Object.DestroyImmediate( go ); }
    }
    [Test]
    public void AdhesionHasReleaseMarginAndClearsOnDisableAndRemoval()
    {
      var go = new GameObject();
      try {
        var plane = go.AddComponent<AGXUnity.Collide.Plane>(); using var boundaries = new SoilSurfaceBoundaries();
        boundaries.Refresh( new AGXUnity.Collide.Shape[] { plane }, new Bounds( Vector3.zero, Vector3.one*100 ), true );
        var particles = new[] { new Particle { Id = 10, Radius = 1, Position = Vector3.up*1.3f, Rotation = Quaternion.identity } };
        void Build( float height, bool enabled = true ) { particles[0].Position.y = height; boundaries.Build( particles, 1, 1, enabled, 0.25f, 1, 1.5f, false ); }
        Build( 1.3f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.2f ); Assert.Greater( boundaries.PatchCount, 0 );
        Build( 1.3f ); Assert.Greater( boundaries.PatchCount, 0 );
        Build( 1.41f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.3f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.2f ); Build( 1.3f, false ); Build( 1.3f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.2f ); particles[0].Id = 20; Build( 1.3f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.2f ); boundaries.Build( particles, 0, 0, true, 0.25f, 1, 1.5f, false ); Build( 1.3f ); Assert.AreEqual( 0, boundaries.PatchCount );
        Build( 1.2f ); Assert.Greater( boundaries.AttachedParticleCount, 0 );
        foreach ( var triangle in new ArraySegment<SoilBoundaryGeometry.Triangle>( boundaries.Patches, 0, boundaries.PatchCount ) ) {
          Assert.AreEqual( 0.005f, triangle.A.y, 1e-5f ); Assert.AreEqual( 0.005f, triangle.B.y, 1e-5f ); Assert.AreEqual( 0.005f, triangle.C.y, 1e-5f );
        }
        Build( -1.1f ); Assert.AreEqual( 0, boundaries.PatchCount, "Buried particles must not paint soil through the ground" );
        plane.CollisionsEnabled = false; boundaries.Refresh( new AGXUnity.Collide.Shape[] { plane }, new Bounds( Vector3.zero, Vector3.one*100 ), true );
        Build( 1.2f ); Assert.AreEqual( 0, boundaries.PatchCount );
      }
      finally { Object.DestroyImmediate( go ); }
    }
    [Test]
    public void RemovingExplicitBoundaryRefreshesWithoutParticleMotion()
    {
      var go = new GameObject();
      try {
        var shape = go.AddComponent<AGXUnity.Collide.Plane>(); using var boundaries = new SoilSurfaceBoundaries();
        var selection = new AGXUnity.Collide.Shape[] { shape }; var area = new Bounds( Vector3.zero, Vector3.one*100 );
        boundaries.Refresh( selection, area, true );
        var p = new[] { new Particle { Id = 10, Radius = 1, Position = Vector3.up*1.1f, Rotation = Quaternion.identity } };
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.Greater( boundaries.PatchCount, 0 );
        selection[0] = null; Assert.IsTrue( boundaries.Refresh( selection, area, true ) );
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.AreEqual( 0, boundaries.PatchCount );
      }
      finally { Object.DestroyImmediate( go ); }
    }
    [Test]
    public void MovingAndRotatingBoundaryUsesItsCurrentRigidTransform()
    {
      var go = new GameObject();
      try {
        var plane = go.AddComponent<AGXUnity.Collide.Plane>(); using var boundaries = new SoilSurfaceBoundaries();
        var selection = new AGXUnity.Collide.Shape[] { plane }; var area = new Bounds( Vector3.zero, Vector3.one*100 );
        boundaries.Refresh( selection, area, true );
        var p = new[] { new Particle { Id = 10, Radius = 1, Position = Vector3.up*1.1f, Rotation = Quaternion.identity } };
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); int count = boundaries.PatchCount;
        go.transform.SetPositionAndRotation( new Vector3( 1, 2, 3 ), Quaternion.Euler( 17, 32, 11 ) );
        p[0].Position = go.transform.TransformPoint( Vector3.up*1.1f );
        Assert.IsTrue( boundaries.Refresh( selection, area, true ) ); boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false );
        Assert.Greater( boundaries.PatchCount, 0 );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) {
          var t = boundaries.Patches[i]; Assert.AreEqual( 0.005f, go.transform.InverseTransformPoint( t.A ).y, 1e-4f );
          Assert.AreEqual( 0.005f, go.transform.InverseTransformPoint( t.B ).y, 1e-4f );
        }
      }
      finally { Object.DestroyImmediate( go ); }
    }
    [Test]
    public void BoxFootprintRespectsDimensionsAndSensors()
    {
      var go = new GameObject();
      try {
        var box = go.AddComponent<AGXUnity.Collide.Box>(); box.HalfExtents = new Vector3( 2, 0.5f, 2 ); using var boundaries = new SoilSurfaceBoundaries();
        var selection = new AGXUnity.Collide.Shape[] { box }; var area = new Bounds( Vector3.zero, Vector3.one*100 );
        boundaries.Refresh( selection, area, true );
        var p = new[] { new Particle { Id = 10, Radius = 1, Position = Vector3.up*1.6f, Rotation = Quaternion.identity } };
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.Greater( boundaries.PatchCount, 0 );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) Assert.AreEqual( 0.505f, boundaries.Patches[i].A.y, 1e-4f );
        box.HalfExtents = new Vector3( 2, 0.7f, 2 ); boundaries.Refresh( selection, area, true );
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) Assert.AreEqual( 0.705f, boundaries.Patches[i].A.y, 1e-4f );
        box.IsSensor = true; boundaries.Refresh( selection, area, true ); boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.AreEqual( 0, boundaries.PatchCount );
      }
      finally { Object.DestroyImmediate( go ); }
    }
    [Test]
    public void TerrainTilesFollowHeightChangesAndHoles()
    {
      var data = new TerrainData { heightmapResolution = 33, size = new Vector3( 32, 8, 32 ) };
      var heights = new float[33,33]; for ( int z = 0; z < 33; ++z ) for ( int x = 0; x < 33; ++x ) heights[z,x] = 0.25f;
      data.SetHeights( 0, 0, heights ); var go = Terrain.CreateTerrainGameObject( data );
      try {
        var shape = go.AddComponent<AGXUnity.Collide.HeightField>(); using var boundaries = new SoilSurfaceBoundaries();
        var selection = new AGXUnity.Collide.Shape[] { shape }; var area = new Bounds( Vector3.zero, Vector3.one*100 );
        boundaries.Refresh( selection, area, true );
        var p = new[] { new Particle { Id = 10, Radius = 1, Position = new Vector3( 8, 3.1f, 8 ), Rotation = Quaternion.identity } };
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.Greater( boundaries.PatchCount, 0 );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) Assert.AreEqual( data.GetHeight( 8, 8 )+0.005f, boundaries.Patches[i].A.y, 1e-4f );
        for ( int z = 0; z < 33; ++z ) for ( int x = 0; x < 33; ++x ) heights[z,x] = 0.3f;
        data.SetHeights( 0, 0, heights ); boundaries.Refresh( selection, area, true );
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false );
        for ( int i = 0; i < boundaries.PatchCount; ++i ) Assert.AreEqual( 2.405f, boundaries.Patches[i].A.y, 0.001f );
        data.SetHoles( 0, 0, new bool[32,32] ); boundaries.Refresh( selection, area, true );
        boundaries.Build( p, 1, 1, true, 0.25f, 1, 1.5f, false ); Assert.AreEqual( 0, boundaries.PatchCount );
      }
      finally { Object.DestroyImmediate( go ); Object.DestroyImmediate( data ); }
    }
  }
}
