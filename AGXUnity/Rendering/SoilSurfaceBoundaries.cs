using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Shape = AGXUnity.Collide.Shape;
using Particle = AGXUnity.Rendering.TerrainParticleSnapshotProvider.Particle;
using Triangle = AGXUnity.Rendering.SoilBoundaryGeometry.Triangle;
using Node = AGXUnity.Rendering.SoilBoundaryGeometry.Node;

namespace AGXUnity.Rendering
{
  /// <summary>Rendering-only collision footprints and local solid constraints. Never modifies AGX geometry.</summary>
  public sealed class SoilSurfaceBoundaries : IDisposable
  {
    public interface IQueryAccelerator : IDisposable
    {
      void Query( GpuShape[] shapes, int shapeCount, Node[] nodes, int nodeCount, Triangle[] triangles, int triangleCount,
                  int geometryVersion, ProximityQuery[] queries, int queryCount, ref ProximityHit[] hits );
    }
    public static Func<IQueryAccelerator> CreateQueryAccelerator;
    [StructLayout( LayoutKind.Sequential )]
    public struct ProximityQuery { public Vector3 Point; public float Reach; public int Shape; }
    [StructLayout( LayoutKind.Sequential )]
    public struct ProximityHit { public Vector3 Point; public float Distance; public Vector3 Normal; public int Valid; }
    private IQueryAccelerator m_queryAccelerator;
    private readonly bool m_allowAcceleration;
    public SoilSurfaceBoundaries( bool allowAcceleration = true ) { m_allowAcceleration = allowAcceleration; }
    public string QueryBackend => m_queryAccelerator == null ? "Managed" : "Burst Jobs";
    [StructLayout( LayoutKind.Sequential )]
    public struct GpuShape { public Matrix4x4 WorldToLocal; public Vector4 SizeKind, Metadata; }
    [StructLayout( LayoutKind.Sequential )]
    public struct Contacts { public Vector4 Indices, Sides; }
    private sealed class Proxy
    {
      public Shape Shape; public Terrain Terrain; public int TileX, TileZ, Index, Piece = -1, NodeOffset = -1, TriangleOffset, NodeCapacity, TriangleCapacity;
      public Model.DeformableTerrainBase TerrainSimulation; public TerrainCollider TerrainCollider;
      public SoilBoundaryGeometry Geometry; public Matrix4x4 Pose, Inverse, Scale;
      public Bounds Bounds; public Vector3 Size, Position, Normal; public int Kind, Version; public bool Dirty, FullTile, Active = true;
    }
    private sealed class FootprintCache
    {
      public readonly List<Triangle> Local = new List<Triangle>( 16 ), World = new List<Triangle>( 16 );
      public Vector3 Center; public float Radius, Spread, Reach; public int Version = -1, ParticleIndex = -1; public Matrix4x4 Pose;
    }
    private struct Contact { public Proxy Proxy; public Vector3 Point, Normal; public float Side, Distance, Score, AdhesionReach; public bool Attached; public FootprintCache Cache; }
    private sealed class State { public readonly Contact[] Contacts = new Contact[4]; public int Count, Seen; }
    private readonly List<Proxy> m_proxies = new List<Proxy>(), m_shapes = new List<Proxy>(), m_nearbyShapes = new List<Proxy>();
    private readonly List<Terrain> m_terrains = new List<Terrain>();
    private readonly Dictionary<(int, int, int), Proxy> m_tiles = new Dictionary<(int, int, int), Proxy>();
    private readonly HashSet<int> m_known = new HashSet<int>();
    private readonly Dictionary<uint, State> m_states = new Dictionary<uint, State>();
    private readonly List<uint> m_expired = new List<uint>();
    private readonly Contact[] m_selection = new Contact[4];
    private readonly List<int> m_candidates = new List<int>();
    private readonly List<int> m_nearFaces = new List<int>();
    private readonly Vector3[] m_contactPoints = new Vector3[3], m_contactNormals = new Vector3[3];
    private readonly Vector3[] m_polygonA = new Vector3[24], m_polygonB = new Vector3[24];
    private Contacts[] m_contacts = new Contacts[1024];
    private ProximityQuery[] m_queries = new ProximityQuery[4096]; private ProximityHit[] m_hits = new ProximityHit[4096];
    private int[] m_queryOffsets = new int[1025]; private int m_queryCount, m_geometryVersion;
    private GpuShape[] m_gpuShapes = new GpuShape[16];
    private Node[] m_nodes = new Node[1024]; private Triangle[] m_triangles = new Triangle[1024], m_patches = new Triangle[1024];
    private int m_nodeCount, m_triangleCount, m_frame = -1, m_tick, m_sourceHash;
    private float m_discoveryTime = float.NegativeInfinity;
    private bool m_subscribed, m_geometryDirty;
    public GraphicsBuffer ContactBuffer { get; private set; }
    public GraphicsBuffer ShapeBuffer { get; private set; }
    public GraphicsBuffer NodeBuffer { get; private set; }
    public GraphicsBuffer TriangleBuffer { get; private set; }
    public GraphicsBuffer PatchBuffer { get; private set; }
    public int PatchCount { get; private set; }
    public int AttachedParticleCount { get; private set; }
    public int QueryCount { get; private set; }
    public Triangle[] Patches => m_patches;
    public int ColliderCount => m_shapes.Count+m_terrains.Count;
    public bool Enabled { get; private set; }
    public float ClipRange { get; private set; }
    private static readonly Contacts s_empty = new Contacts { Indices = -Vector4.one };
    private static readonly Vector2[] s_octagon = MakeOctagon();
    private static Vector2[] MakeOctagon()
    {
      var result = new Vector2[8]; float radius = 1/Mathf.Cos( Mathf.PI/8 );
      for ( int i = 0; i < 8; ++i ) { float a = Mathf.PI/8+i*Mathf.PI/4; result[i] = new Vector2( Mathf.Cos( a ), Mathf.Sin( a ) )*radius; }
      return result;
    }

    public bool Refresh( Shape[] explicitShapes, Bounds soilBounds, bool force = false )
    {
      if ( !force && m_frame == Time.frameCount ) return false;
      m_frame = Time.frameCount;
      if ( !m_subscribed ) { TerrainCallbacks.heightmapChanged += HeightmapChanged; TerrainCallbacks.textureChanged += TextureChanged; m_subscribed = true; }
      int hash = 17;
      if ( explicitShapes != null ) foreach ( var shape in explicitShapes ) hash = unchecked( hash*31+(shape != null ? shape.GetInstanceID() : 0) );
      bool changed = false;
      if ( hash != m_sourceHash ) { m_sourceHash = hash; ResetSources(); changed = true; }
      if ( Time.realtimeSinceStartup-m_discoveryTime >= 1 || force ) {
        bool removed = false;
        foreach ( var proxy in m_shapes ) if ( proxy.Shape == null ) removed = true;
        foreach ( var terrain in m_terrains ) if ( terrain == null ) removed = true;
        if ( removed ) { ResetSources(); changed = true; }
        m_discoveryTime = Time.realtimeSinceStartup;
        if ( explicitShapes != null && explicitShapes.Length > 0 ) {
          foreach ( var shape in explicitShapes ) changed |= Discover( shape );
        }
        else {
          foreach ( var shape in UnityEngine.Object.FindObjectsByType<Shape>( FindObjectsSortMode.None ) ) changed |= Discover( shape );
          foreach ( var terrain in Terrain.activeTerrains ) {
            if ( terrain.GetComponent<Collide.HeightField>() != null || terrain.GetComponent<Model.DeformableTerrainBase>() != null ||
                 (terrain.GetComponent<TerrainCollider>() is TerrainCollider collider && collider.enabled) ) changed |= DiscoverTerrain( terrain );
          }
        }
      }
      foreach ( var proxy in m_proxies ) {
        bool active = IsActive( proxy );
        if ( active != proxy.Active ) { if ( proxy.Bounds.Intersects( soilBounds ) ) changed = true; proxy.Active = active; }
        if ( !active ) continue;
        Transform transform = proxy.Shape != null ? proxy.Shape.transform : proxy.Terrain.transform;
        var pose = Matrix4x4.TRS( transform.position, proxy.Terrain != null ? Quaternion.identity : transform.rotation, Vector3.one );
        if ( pose != proxy.Pose ) {
          var previous = proxy.Bounds; proxy.Pose = pose; proxy.Inverse = pose.inverse; UpdateBounds( proxy );
          if ( previous.Intersects( soilBounds ) || proxy.Bounds.Intersects( soilBounds ) ) changed = true;
        }
        bool rebuild = false;
        if ( proxy.Shape is Collide.Box box && box.HalfExtents != proxy.Size ) { proxy.Size = box.HalfExtents; rebuild = true; }
        if ( proxy.Shape is Collide.Mesh mesh ) {
          var scale = proxy.Inverse*mesh.transform.localToWorldMatrix;
          if ( !SameBasis( scale, proxy.Scale ) ) { proxy.Scale = scale; rebuild = true; }
        }
        if ( rebuild ) { var previous = proxy.Bounds; Rebuild( proxy ); if ( previous.Intersects( soilBounds ) || proxy.Bounds.Intersects( soilBounds ) ) changed = true; }
        if ( proxy.Dirty && proxy.Bounds.Intersects( soilBounds ) ) changed = true;
        if ( proxy.Dirty && proxy.Terrain != null ) {
          var horizontal = proxy.Bounds; horizontal.Expand( Vector3.up*1e10f ); if ( horizontal.Intersects( soilBounds ) ) changed = true;
        }
      }
      return changed;
    }
    private static bool IsActive( Proxy proxy ) => proxy.Shape != null ? proxy.Shape.isActiveAndEnabled && proxy.Shape.CollisionsEnabled && !proxy.Shape.IsSensor :
      proxy.Terrain != null && proxy.Terrain.isActiveAndEnabled && (proxy.TerrainSimulation != null ? proxy.TerrainSimulation.isActiveAndEnabled :
        proxy.TerrainCollider != null && proxy.TerrainCollider.enabled && !proxy.TerrainCollider.isTrigger);
    private static bool SameBasis( Matrix4x4 a, Matrix4x4 b )
    {
      for ( int c = 0; c < 3; ++c ) for ( int r = 0; r < 3; ++r ) if ( Mathf.Abs( a[r,c]-b[r,c] ) > 1e-5f ) return false;
      return true;
    }
    private bool DiscoverTerrain( Terrain terrain )
    {
      if ( terrain == null || terrain.terrainData == null || m_terrains.Contains( terrain ) ) return false;
      m_terrains.Add( terrain ); return true;
    }
    private bool Discover( Shape shape )
    {
      if ( shape == null || m_known.Contains( shape.GetInstanceID() ) ) return false;
      if ( shape is Collide.HeightField ) { m_known.Add( shape.GetInstanceID() ); return DiscoverTerrain( shape.GetComponent<Terrain>() ); }
      int kind = shape is Collide.Plane ? 1 : shape is Collide.Box ? 2 : shape is Collide.Mesh ? 3 : 0;
      if ( kind == 0 ) return false;
      m_known.Add( shape.GetInstanceID() );
      if ( shape is Collide.Mesh collisionMesh && collisionMesh.PrecomputedCollisionMeshes.Length > 0 ) {
        for ( int piece = 0; piece < collisionMesh.PrecomputedCollisionMeshes.Length; ++piece ) CreateProxy( shape, kind, piece );
      }
      else CreateProxy( shape, kind, -1 );
      return true;
    }
    private void CreateProxy( Shape shape, int kind, int piece )
    {
      var proxy = new Proxy { Shape = shape, Kind = kind, Dirty = true,
        Piece = piece, Pose = Matrix4x4.TRS( shape.transform.position, shape.transform.rotation, Vector3.one ) };
      proxy.Inverse = proxy.Pose.inverse; proxy.Scale = proxy.Inverse*shape.transform.localToWorldMatrix;
      if ( shape is Collide.Box box ) proxy.Size = box.HalfExtents;
      Add( proxy ); m_shapes.Add( proxy ); Rebuild( proxy );
    }
    private void Add( Proxy proxy ) { proxy.Index = m_proxies.Count; m_proxies.Add( proxy ); }
    private void HeightmapChanged( Terrain terrain, RectInt region, bool synched )
    {
      foreach ( var proxy in m_proxies )
        if ( proxy.Terrain == terrain && region.Overlaps( new RectInt( proxy.TileX*8, proxy.TileZ*8, 9, 9 ) ) ) proxy.Dirty = true;
    }
    private void TextureChanged( Terrain terrain, string texture, RectInt region, bool synched )
    {
      if ( texture != "holes" ) return;
      foreach ( var proxy in m_proxies ) if ( proxy.Terrain == terrain ) proxy.Dirty = true;
    }
    private void Rebuild( Proxy proxy )
    {
      var vertices = new List<Vector3>(); var indices = new List<int>();
      if ( proxy.Shape is Collide.Box ) {
        for ( int i = 0; i < 8; ++i ) vertices.Add( Vector3.Scale( new Vector3( (i&1) == 0 ? -1 : 1, (i&2) == 0 ? -1 : 1, (i&4) == 0 ? -1 : 1 ), proxy.Size ) );
        indices.AddRange( new[] { 0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,4,2, 2,4,6, 1,3,5, 3,7,5, 0,1,4, 1,5,4, 2,6,3, 3,6,7 } );
      }
      else if ( proxy.Shape is Collide.Mesh mesh ) {
        var precomputed = mesh.PrecomputedCollisionMeshes;
        if ( precomputed.Length > 0 ) {
          for ( int piece = 0; piece < precomputed.Length; ++piece ) {
            var data = precomputed[piece]; if ( piece != proxy.Piece || data == null ) continue;
            int offset = vertices.Count;
            foreach ( var v in data.Vertices ) vertices.Add( proxy.Scale.MultiplyVector( new Vector3( -v.x, v.y, v.z ) ) );
            for ( int i = 0; i+2 < data.Indices.Length; i += 3 ) { indices.Add( offset+data.Indices[i] ); indices.Add( offset+data.Indices[i+2] ); indices.Add( offset+data.Indices[i+1] ); }
          }
        }
        else foreach ( var source in mesh.SourceObjects ) if ( source != null && source.isReadable ) {
          int offset = vertices.Count; foreach ( var v in source.vertices ) vertices.Add( proxy.Scale.MultiplyVector( v ) );
          foreach ( int index in source.triangles ) indices.Add( offset+index );
        }
      }
      else if ( proxy.Terrain != null ) {
        var data = proxy.Terrain.terrainData; int resolution = data.heightmapResolution;
        int x0 = proxy.TileX*8, z0 = proxy.TileZ*8, width = Mathf.Min( 9, resolution-x0 ), height = Mathf.Min( 9, resolution-z0 );
        for ( int z = 0; z < height; ++z ) for ( int x = 0; x < width; ++x )
          vertices.Add( new Vector3( (x0+x)*data.size.x/(resolution-1), data.GetHeight( x0+x, z0+z ), (z0+z)*data.size.z/(resolution-1) ) );
        for ( int z = 0; z < height-1; ++z ) for ( int x = 0; x < width-1; ++x ) {
          if ( data.IsHole( x0+x, z0+z ) ) continue;
          int a = z*width+x; indices.Add( a ); indices.Add( a+width ); indices.Add( a+1 );
          indices.Add( a+1 ); indices.Add( a+width ); indices.Add( a+width+1 );
        }
        proxy.FullTile = indices.Count == (width-1)*(height-1)*6;
      }
      proxy.Geometry = new SoilBoundaryGeometry( vertices.ToArray(), indices.ToArray(), proxy.Shape is Collide.Mesh ); proxy.Dirty = false; ++proxy.Version; ++m_geometryVersion;
      if ( proxy.Kind == 1 ) { UpdateBounds( proxy ); return; }
      // Geometry caches retain rigid local coordinates. Only matrices change as a bucket moves.
      if ( proxy.NodeOffset < 0 || proxy.Geometry.NodeCount > proxy.NodeCapacity || proxy.Geometry.Triangles.Length > proxy.TriangleCapacity ) {
        proxy.NodeOffset = m_nodeCount; proxy.TriangleOffset = m_triangleCount;
        proxy.NodeCapacity = proxy.Geometry.NodeCount; proxy.TriangleCapacity = proxy.Geometry.Triangles.Length;
        m_nodeCount += proxy.NodeCapacity; m_triangleCount += proxy.TriangleCapacity;
      }
      Grow( ref m_nodes, m_nodeCount ); Grow( ref m_triangles, m_triangleCount );
      for ( int i = 0; i < proxy.Geometry.NodeCount; ++i ) {
        var node = proxy.Geometry.Nodes[i]; node.Start += node.Count > 0 ? proxy.TriangleOffset : proxy.NodeOffset; m_nodes[proxy.NodeOffset+i] = node;
      }
      Array.Copy( proxy.Geometry.Triangles, 0, m_triangles, proxy.TriangleOffset, proxy.Geometry.Triangles.Length );
      m_geometryDirty = true; UpdateBounds( proxy );
    }
    private static void UpdateBounds( Proxy proxy )
    {
      proxy.Position = proxy.Pose.MultiplyPoint3x4( Vector3.zero ); proxy.Normal = proxy.Pose.MultiplyVector( Vector3.up );
      if ( proxy.Kind == 1 ) { proxy.Bounds = new Bounds( proxy.Position, Vector3.one*1e20f ); return; }
      if ( proxy.Geometry == null ) return;
      var bounds = proxy.Geometry.Bounds; var center = proxy.Pose.MultiplyPoint3x4( bounds.center ); var size = bounds.extents;
      var x = proxy.Pose.MultiplyVector( Vector3.right*size.x ); var y = proxy.Pose.MultiplyVector( Vector3.up*size.y ); var z = proxy.Pose.MultiplyVector( Vector3.forward*size.z );
      proxy.Bounds = new Bounds( center, 2*(Abs( x )+Abs( y )+Abs( z )) );
    }
    private static Vector3 Abs( Vector3 p ) => new Vector3( Mathf.Abs( p.x ), Mathf.Abs( p.y ), Mathf.Abs( p.z ) );
    private Proxy Tile( Terrain terrain, int x, int z )
    {
      var key = (terrain.GetInstanceID(), x, z);
      if ( !m_tiles.TryGetValue( key, out var proxy ) ) {
        var shape = terrain.GetComponent<Collide.HeightField>();
        proxy = new Proxy { Terrain = terrain, Shape = shape, TileX = x, TileZ = z, Kind = 3, Dirty = true,
          TerrainSimulation = terrain.GetComponent<Model.DeformableTerrainBase>(), TerrainCollider = terrain.GetComponent<TerrainCollider>(),
          Pose = Matrix4x4.TRS( terrain.transform.position, Quaternion.identity, Vector3.one ) };
        proxy.Active = IsActive( proxy ); proxy.Inverse = proxy.Pose.inverse; Add( proxy ); m_tiles.Add( key, proxy );
      }
      if ( proxy.Dirty ) Rebuild( proxy ); return proxy;
    }
    private bool Closest( Proxy proxy, Vector3 point, float reach, out Vector3 q, out Vector3 normal )
    {
      q = normal = default;
      if ( !proxy.Active ) return false;
      if ( proxy.Dirty ) Rebuild( proxy );
      if ( proxy.Kind != 1 && proxy.Bounds.SqrDistance( point ) > reach*reach ) return false;
      ++QueryCount;
      if ( proxy.Kind == 1 ) { normal = proxy.Normal; float distance = Vector3.Dot( point-proxy.Position, normal ); q = point-normal*distance; return Mathf.Abs( distance ) < reach; }
      var p = proxy.Inverse.MultiplyPoint3x4( point );
      if ( !proxy.Geometry.Closest( p, reach, out var local, out var n ) ) return false;
      q = proxy.Pose.MultiplyPoint3x4( local ); normal = proxy.Pose.MultiplyVector( n ); return true;
    }
    public Contacts GetContacts( int particle ) => m_contacts[particle];
    public void Build( Particle[] particles, int surfaceCount, int totalCount, bool enabled, float gap, float spread, float supportScale, bool upload = true )
    {
      Enabled = enabled; ClipRange = Mathf.Max( 3, 2*supportScale ); PatchCount = AttachedParticleCount = QueryCount = 0; ++m_tick;
      Grow( ref m_contacts, totalCount ); for ( int i = 0; i < totalCount; ++i ) m_contacts[i] = s_empty;
      m_nearbyShapes.Clear();
      if ( enabled && surfaceCount > 0 ) {
        var region = new Bounds( particles[0].Position, Vector3.zero );
        for ( int i = 0; i < surfaceCount; ++i ) region.Encapsulate( new Bounds( particles[i].Position, Vector3.one*(2*ClipRange*particles[i].Radius) ) );
        foreach ( var proxy in m_shapes ) if ( proxy.Active && proxy.Bounds.Intersects( region ) ) m_nearbyShapes.Add( proxy );
      }
      Grow( ref m_queryOffsets, surfaceCount+1 ); m_queryCount = 0;
      if ( enabled ) for ( int particle = 0; particle < surfaceCount; ++particle ) {
        ref readonly var p = ref particles[particle]; float reach = ClipRange*p.Radius; m_queryOffsets[particle] = m_queryCount;
        foreach ( var proxy in m_nearbyShapes ) AddQuery( proxy, p.Position, reach );
        foreach ( var terrain in m_terrains ) if ( terrain != null && terrain.isActiveAndEnabled && terrain.terrainData != null ) {
          var data = terrain.terrainData; var local = p.Position-terrain.transform.position; int cells = data.heightmapResolution-1;
          if ( local.x+reach < 0 || local.z+reach < 0 || local.x-reach > data.size.x || local.z-reach > data.size.z ) continue;
          int x0 = Mathf.Clamp( Mathf.FloorToInt( (local.x-reach)*cells/data.size.x )/8, 0, (cells-1)/8 );
          int x1 = Mathf.Clamp( Mathf.FloorToInt( (local.x+reach)*cells/data.size.x )/8, 0, (cells-1)/8 );
          int z0 = Mathf.Clamp( Mathf.FloorToInt( (local.z-reach)*cells/data.size.z )/8, 0, (cells-1)/8 );
          int z1 = Mathf.Clamp( Mathf.FloorToInt( (local.z+reach)*cells/data.size.z )/8, 0, (cells-1)/8 );
          for ( int z = z0; z <= z1; ++z ) for ( int x = x0; x <= x1; ++x ) AddQuery( Tile( terrain, x, z ), p.Position, reach );
        }
      }
      m_queryOffsets[surfaceCount] = m_queryCount; UpdateShapeData();
      if ( enabled && m_queryCount > 0 ) {
        if ( m_allowAcceleration && m_queryAccelerator == null && CreateQueryAccelerator != null ) m_queryAccelerator = CreateQueryAccelerator();
        Grow( ref m_hits, m_queryCount );
        if ( m_queryAccelerator != null ) {
          m_queryAccelerator.Query( m_gpuShapes, m_proxies.Count, m_nodes, m_nodeCount, m_triangles, m_triangleCount,
            m_geometryVersion, m_queries, m_queryCount, ref m_hits ); QueryCount = m_queryCount;
        }
        else for ( int i = 0; i < m_queryCount; ++i ) {
          var query = m_queries[i]; bool valid = Closest( m_proxies[query.Shape], query.Point, query.Reach, out var point, out var normal );
          m_hits[i] = new ProximityHit { Point = point, Normal = normal, Distance = Vector3.Distance( point, query.Point ), Valid = valid ? 1 : 0 };
        }
      }
      if ( enabled ) for ( int particle = 0; particle < surfaceCount; ++particle ) {
        ref readonly var p = ref particles[particle]; int selected = 0; m_states.TryGetValue( p.Id, out var state );
        for ( int query = m_queryOffsets[particle]; query < m_queryOffsets[particle+1]; ++query )
          if ( m_hits[query].Valid != 0 ) Select( m_proxies[m_queries[query].Shape], m_hits[query], p, gap, state, ref selected );
        if ( selected == 0 ) continue;
        if ( state == null ) { state = new State(); m_states.Add( p.Id, state ); }
        state.Count = selected; state.Seen = m_tick;
        var contacts = s_empty; bool attached = false;
        for ( int i = 0; i < selected; ++i ) {
          var contact = m_selection[i]; contacts.Indices[i] = contact.Proxy.Index; contacts.Sides[i] = contact.Side;
          if ( contact.Attached ) {
            if ( contact.Cache == null ) contact.Cache = new FootprintCache();
            CachedFootprint( contact, p, particle, spread ); attached = true;
          }
          state.Contacts[i] = contact;
        }
        if ( attached ) ++AttachedParticleCount; m_contacts[particle] = contacts;
      }
      m_expired.Clear(); foreach ( var entry in m_states ) if ( !enabled || entry.Value.Seen != m_tick ) m_expired.Add( entry.Key );
      foreach ( uint id in m_expired ) m_states.Remove( id );
      if ( upload ) Upload( totalCount );
    }
    private void AddQuery( Proxy proxy, Vector3 point, float reach )
    {
      if ( !proxy.Active || (proxy.Kind != 1 && proxy.Bounds.SqrDistance( point ) > reach*reach) ) return;
      Grow( ref m_queries, m_queryCount+1 ); m_queries[m_queryCount++] = new ProximityQuery { Point = point, Reach = reach, Shape = proxy.Index };
    }
    private void Select( Proxy proxy, ProximityHit hit, Particle p, float gap, State state, ref int selected )
    {
      for ( int i = 0; i < selected; ++i ) if ( m_selection[i].Proxy == proxy ) return;
      var q = hit.Point; var normal = hit.Normal;
      bool previous = false, attached = false; FootprintCache cache = null;
      float side = Vector3.Dot( p.Position-q, normal ) >= 0 ? 1 : -1;
      if ( state != null ) for ( int i = 0; i < state.Count; ++i ) if ( state.Contacts[i].Proxy == proxy ) {
        previous = true; attached = state.Contacts[i].Attached; side = state.Contacts[i].Side; cache = state.Contacts[i].Cache; break;
      }
      // Box and terrain have an unambiguous exterior. Open triangle meshes use
      // the side occupied by the particle at attachment, retained across updates.
      if ( proxy.Kind == 1 || proxy.Kind == 2 || proxy.Terrain != null ) side = 1;
      else if ( proxy.Geometry.IsClosed ) side = proxy.Geometry.ExteriorSide;
      float distance = hit.Distance, score = distance-(previous ? 0.15f*p.Radius : 0);
      bool exterior = Vector3.Dot( p.Position-q, normal*side ) >= -0.25f*p.Radius;
      var contact = new Contact { Proxy = proxy, Point = q, Normal = normal*side, Side = side, Distance = distance, Score = score, Cache = cache,
        AdhesionReach = p.Radius*(1+gap+(attached ? 0.15f : 0)), Attached = exterior && distance <= p.Radius*(1+gap+(attached ? 0.15f : 0)) };
      int slot = selected;
      if ( selected == 4 ) {
        slot = 0; for ( int i = 1; i < 4; ++i ) if ( m_selection[i].Score > m_selection[slot].Score ) slot = i;
        if ( score >= m_selection[slot].Score ) return;
      }
      else ++selected;
      m_selection[slot] = contact;
    }
    private void CachedFootprint( Contact contact, Particle p, int particle, float spread )
    {
      var cache = contact.Cache; var proxy = contact.Proxy; var center = proxy.Inverse.MultiplyPoint3x4( p.Position );
      if ( cache.Version != proxy.Version || cache.Radius != p.Radius || cache.Spread != spread || cache.Reach != contact.AdhesionReach ||
           (center-cache.Center).sqrMagnitude > p.Radius*p.Radius*0.0001f ) {
        int start = PatchCount; AddFootprint( contact, p, particle, spread ); cache.Local.Clear(); cache.World.Clear();
        for ( int i = start; i < PatchCount; ++i ) {
          var t = m_patches[i]; cache.World.Add( t );
          cache.Local.Add( new Triangle { A = proxy.Inverse.MultiplyPoint3x4( t.A ), B = proxy.Inverse.MultiplyPoint3x4( t.B ), C = proxy.Inverse.MultiplyPoint3x4( t.C ) } );
        }
        cache.Center = center; cache.Radius = p.Radius; cache.Spread = spread; cache.Reach = contact.AdhesionReach; cache.Version = proxy.Version; cache.Pose = proxy.Pose; cache.ParticleIndex = particle;
        return;
      }
      if ( cache.Pose != proxy.Pose ) {
        for ( int i = 0; i < cache.Local.Count; ++i ) {
          var t = cache.Local[i]; cache.World[i] = new Triangle { A = proxy.Pose.MultiplyPoint3x4( t.A ), B = proxy.Pose.MultiplyPoint3x4( t.B ), C = proxy.Pose.MultiplyPoint3x4( t.C ) };
        }
        cache.Pose = proxy.Pose;
        cache.ParticleIndex = -1;
      }
      Grow( ref m_patches, PatchCount+cache.World.Count );
      if ( cache.ParticleIndex != particle ) {
        for ( int i = 0; i < cache.World.Count; ++i ) { var t = cache.World[i]; t.A.w = particle; cache.World[i] = t; }
        cache.ParticleIndex = particle;
      }
      cache.World.CopyTo( m_patches, PatchCount ); PatchCount += cache.World.Count;
    }
    private static Vector3 Tangent( Proxy proxy, Vector3 normal )
    {
      var local = proxy.Inverse.MultiplyVector( normal );
      return proxy.Pose.MultiplyVector( Vector3.Cross( local, Mathf.Abs( local.y ) < 0.9f ? Vector3.up : Vector3.right ).normalized );
    }
    private void AddFootprint( Contact contact, Particle p, int particle, float spread )
    {
      var proxy = contact.Proxy; var n = contact.Normal; var q = contact.Point;
      float radius = p.Radius*spread, offset = Mathf.Max( 0.0001f, 0.005f*p.Radius );
      var tangent = Tangent( proxy, n );
      var bitangent = Vector3.Cross( n, tangent );
      if ( proxy.Kind == 1 ) {
        for ( int i = 0; i < 8; ++i ) m_polygonA[i] = q+n*offset+(tangent*s_octagon[i].x+bitangent*s_octagon[i].y)*radius;
        Grow( ref m_patches, PatchCount+6 );
        for ( int i = 1; i < 7; ++i ) {
          var a = m_polygonA[0]; m_patches[PatchCount++] = new Triangle { A = new Vector4( a.x,a.y,a.z,particle ), B = m_polygonA[i], C = m_polygonA[i+1] };
        }
        return;
      }
      // A single bucket mesh can contain floor and walls. Keep separate local
      // footprints for up to three substantially different nearby face normals.
      m_contactPoints[0] = q; m_contactNormals[0] = n; int contacts = 1;
      m_nearFaces.Clear(); proxy.Geometry.Collect( proxy.Inverse.MultiplyPoint3x4( p.Position ), contact.AdhesionReach, m_nearFaces );
      foreach ( int index in m_nearFaces ) {
        if ( contacts == 3 ) break;
        var face = proxy.Geometry.Triangles[index];
        var a = proxy.Pose.MultiplyPoint3x4( face.A ); var b = proxy.Pose.MultiplyPoint3x4( face.B ); var c = proxy.Pose.MultiplyPoint3x4( face.C );
        var normal = Vector3.Cross( b-a, c-a ).normalized*contact.Side;
        var point = SoilBoundaryGeometry.ClosestTriangle( p.Position, a, b, c );
        if ( Vector3.Distance( point, p.Position ) > contact.AdhesionReach ) continue;
        bool duplicate = false; for ( int i = 0; i < contacts; ++i ) if ( Vector3.Dot( normal, m_contactNormals[i] ) > 0.85f ) duplicate = true;
        if ( duplicate ) continue; m_contactPoints[contacts] = point; m_contactNormals[contacts++] = normal;
      }
      for ( int i = 0; i < contacts; ++i ) AddSurfaceFootprint( proxy, contact.Side, m_contactPoints[i], m_contactNormals[i], radius, offset, particle );
    }
    private void AddSurfaceFootprint( Proxy proxy, float side, Vector3 q, Vector3 n, float radius, float offset, int particle )
    {
      var tangent = Tangent( proxy, n );
      var bitangent = Vector3.Cross( n, tangent );
      m_candidates.Clear(); proxy.Geometry.Collect( proxy.Inverse.MultiplyPoint3x4( q ), radius*1.5f, m_candidates );
      foreach ( int index in m_candidates ) {
        var triangle = proxy.Geometry.Triangles[index];
        var a = proxy.Pose.MultiplyPoint3x4( triangle.A ); var b = proxy.Pose.MultiplyPoint3x4( triangle.B ); var c = proxy.Pose.MultiplyPoint3x4( triangle.C );
        var normal = Vector3.Cross( b-a, c-a ).normalized*side;
        if ( Vector3.Dot( normal, n ) < 0.1f ) continue;
        m_polygonA[0] = a; m_polygonA[1] = side > 0 ? b : c; m_polygonA[2] = side > 0 ? c : b;
        ClipFootprint( 3, q, normal, tangent, bitangent, radius, offset, particle );
      }
    }
    private void ClipFootprint( int count, Vector3 origin, Vector3 normal, Vector3 tangent, Vector3 bitangent, float radius, float offset, int particle )
    {
      var input = m_polygonA; var output = m_polygonB;
      for ( int plane = 0; plane < 10 && count >= 3; ++plane ) {
        float angle = plane*Mathf.PI/4;
        var direction = plane < 8 ? tangent*Mathf.Cos( angle )+bitangent*Mathf.Sin( angle ) : normal*(plane == 8 ? 1 : -1);
        int result = 0; var previous = input[count-1]; float d0 = Vector3.Dot( previous-origin, direction )-radius;
        for ( int i = 0; i < count; ++i ) {
          var current = input[i]; float d1 = Vector3.Dot( current-origin, direction )-radius;
          if ( (d0 <= 0) != (d1 <= 0) ) output[result++] = Vector3.LerpUnclamped( previous, current, d0/(d0-d1) );
          if ( d1 <= 0 ) output[result++] = current;
          previous = current; d0 = d1;
        }
        count = result; var swap = input; input = output; output = swap;
      }
      for ( int i = 1; i+1 < count; ++i ) {
        var a = input[0]+normal*offset; var b = input[i]+normal*offset; var c = input[i+1]+normal*offset;
        if ( Vector3.Cross( b-a, c-a ).sqrMagnitude < Mathf.Max( 1e-24f, radius*radius*radius*radius*1e-10f ) ) continue;
        Grow( ref m_patches, PatchCount+1 );
        m_patches[PatchCount++] = new Triangle { A = new Vector4( a.x, a.y, a.z, particle ), B = b, C = c };
      }
    }
    private void UpdateShapeData()
    {
      Grow( ref m_gpuShapes, m_proxies.Count );
      foreach ( var proxy in m_proxies ) {
        bool planar = proxy.Terrain != null && proxy.FullTile && proxy.Geometry != null && proxy.Geometry.IsPlanar;
        var plane = planar ? proxy.Geometry.Plane : Vector4.zero; Vector3 size = planar ? (Vector3)plane : proxy.Size;
        m_gpuShapes[proxy.Index] = new GpuShape { WorldToLocal = proxy.Inverse,
          SizeKind = new Vector4( size.x, size.y, size.z, proxy.Active ? proxy.Kind : 0 ),
          Metadata = new Vector4( proxy.Geometry != null && proxy.Geometry.NodeCount > 0 ? proxy.NodeOffset : -1, proxy.Terrain != null ? 1 : 0, planar ? 1 : 0, plane.w ) };
      }
    }
    private void Upload( int count )
    {
      ContactBuffer = Upload( ContactBuffer, m_contacts, count, 32 ); ShapeBuffer = Upload( ShapeBuffer, m_gpuShapes, m_proxies.Count, 96 );
      bool nodesChanged = NodeBuffer == null || NodeBuffer.count < Mathf.Max( 1, m_nodeCount );
      bool trianglesChanged = TriangleBuffer == null || TriangleBuffer.count < Mathf.Max( 1, m_triangleCount );
      if ( m_geometryDirty || nodesChanged ) NodeBuffer = Upload( NodeBuffer, m_nodes, m_nodeCount, 32 );
      if ( m_geometryDirty || trianglesChanged ) TriangleBuffer = Upload( TriangleBuffer, m_triangles, m_triangleCount, 48 );
      PatchBuffer = Upload( PatchBuffer, m_patches, PatchCount, 48 ); m_geometryDirty = false;
    }
    private static GraphicsBuffer Upload<T>( GraphicsBuffer buffer, T[] data, int count, int stride ) where T : struct
    {
      if ( buffer == null || buffer.count < Mathf.Max( 1, count ) ) { buffer?.Dispose(); buffer = new GraphicsBuffer( GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo( Mathf.Max( 1, count ) ), stride ); }
      if ( count > 0 ) buffer.SetData( data, 0, 0, count ); return buffer;
    }
    private static void Grow<T>( ref T[] data, int count ) { if ( data.Length < count ) Array.Resize( ref data, Mathf.NextPowerOfTwo( count ) ); }
    public void Bind( MaterialPropertyBlock properties )
    {
      properties.SetFloat( "_SoilBoundaryEnabled", Enabled ? 1 : 0 ); properties.SetFloat( "_SoilBoundaryClipRange", ClipRange );
      properties.SetBuffer( "_SoilBoundaryContacts", ContactBuffer ); properties.SetBuffer( "_SoilBoundaryShapes", ShapeBuffer );
      properties.SetBuffer( "_SoilBoundaryNodes", NodeBuffer ); properties.SetBuffer( "_SoilBoundaryTriangles", TriangleBuffer ); properties.SetBuffer( "_SoilBoundaryPatches", PatchBuffer );
    }
    public void Invalidate() { ResetSources(); m_discoveryTime = float.NegativeInfinity; m_frame = -1; }
    public void Clear() { m_states.Clear(); Enabled = false; PatchCount = AttachedParticleCount = QueryCount = 0; }
    private void ResetSources()
    {
      m_proxies.Clear(); m_shapes.Clear(); m_nearbyShapes.Clear(); m_terrains.Clear(); m_tiles.Clear(); m_known.Clear(); m_states.Clear();
      m_nodeCount = m_triangleCount = 0; m_geometryDirty = true; m_discoveryTime = float.NegativeInfinity;
      ++m_geometryVersion;
    }
    public void Dispose()
    {
      if ( m_subscribed ) { TerrainCallbacks.heightmapChanged -= HeightmapChanged; TerrainCallbacks.textureChanged -= TextureChanged; } m_subscribed = false;
      ContactBuffer?.Dispose(); ShapeBuffer?.Dispose(); NodeBuffer?.Dispose(); TriangleBuffer?.Dispose(); PatchBuffer?.Dispose();
      ContactBuffer = ShapeBuffer = NodeBuffer = TriangleBuffer = PatchBuffer = null; PatchCount = AttachedParticleCount = QueryCount = 0;
      m_queryAccelerator?.Dispose(); m_queryAccelerator = null;
      Enabled = false; ResetSources(); m_frame = -1;
    }
  }
}
