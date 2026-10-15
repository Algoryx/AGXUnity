using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Shape = AGXUnity.Rendering.SoilSurfaceBoundaries.GpuShape;
using Query = AGXUnity.Rendering.SoilSurfaceBoundaries.ProximityQuery;
using Hit = AGXUnity.Rendering.SoilSurfaceBoundaries.ProximityHit;
using Node = AGXUnity.Rendering.SoilBoundaryGeometry.Node;
using Triangle = AGXUnity.Rendering.SoilBoundaryGeometry.Triangle;

namespace AGXUnity.Rendering.Burst
{
  /// <summary>Parallel proximity queries over persistent, copied boundary data; no native AGX objects enter jobs.</summary>
  public sealed class BurstSoilBoundaryQueries : SoilSurfaceBoundaries.IQueryAccelerator
  {
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Register() => SoilSurfaceBoundaries.CreateQueryAccelerator = () => BurstCompiler.IsEnabled ? new BurstSoilBoundaryQueries() : null;
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor() => Register();
#endif
    private NativeArray<Shape> m_shapes;
    private NativeArray<Node> m_nodes;
    private NativeArray<Triangle> m_triangles;
    private NativeArray<Query> m_queries;
    private NativeArray<Hit> m_hits;
    private int m_geometryVersion = -1;
    public void Query( Shape[] shapes, int shapeCount, Node[] nodes, int nodeCount, Triangle[] triangles, int triangleCount,
                       int geometryVersion, Query[] queries, int queryCount, ref Hit[] hits )
    {
      Grow( ref m_shapes, shapeCount ); Grow( ref m_queries, queryCount ); Grow( ref m_hits, queryCount );
      NativeArray<Shape>.Copy( shapes, 0, m_shapes, 0, shapeCount ); NativeArray<Query>.Copy( queries, 0, m_queries, 0, queryCount );
      if ( m_geometryVersion != geometryVersion ) {
        Grow( ref m_nodes, nodeCount ); Grow( ref m_triangles, triangleCount );
        NativeArray<Node>.Copy( nodes, 0, m_nodes, 0, nodeCount ); NativeArray<Triangle>.Copy( triangles, 0, m_triangles, 0, triangleCount );
        m_geometryVersion = geometryVersion;
      }
      new QueryJob { Shapes = m_shapes, Nodes = m_nodes, Triangles = m_triangles, Queries = m_queries, Hits = m_hits }.Schedule( queryCount, 64 ).Complete();
      if ( hits.Length < queryCount ) Array.Resize( ref hits, Mathf.NextPowerOfTwo( queryCount ) ); NativeArray<Hit>.Copy( m_hits, 0, hits, 0, queryCount );
    }
    private static void Grow<T>( ref NativeArray<T> data, int count ) where T : struct
    {
      if ( data.IsCreated && data.Length >= Mathf.Max( 1, count ) ) return;
      if ( data.IsCreated ) data.Dispose(); data = new NativeArray<T>( Mathf.NextPowerOfTwo( Mathf.Max( 1, count ) ), Allocator.Persistent, NativeArrayOptions.UninitializedMemory );
    }
    public void Dispose()
    {
      if ( m_shapes.IsCreated ) m_shapes.Dispose(); if ( m_nodes.IsCreated ) m_nodes.Dispose(); if ( m_triangles.IsCreated ) m_triangles.Dispose();
      if ( m_queries.IsCreated ) m_queries.Dispose(); if ( m_hits.IsCreated ) m_hits.Dispose();
      m_shapes = default; m_nodes = default; m_triangles = default; m_queries = default; m_hits = default; m_geometryVersion = -1;
    }
    [BurstCompile( CompileSynchronously = true, FloatMode = FloatMode.Strict )]
    private struct QueryJob : IJobParallelFor
    {
      [ReadOnly] public NativeArray<Shape> Shapes;
      [ReadOnly] public NativeArray<Node> Nodes;
      [ReadOnly] public NativeArray<Triangle> Triangles;
      [ReadOnly] public NativeArray<Query> Queries;
      [WriteOnly] public NativeArray<Hit> Hits;
      private static float3 V( Vector3 p ) => new float3( p.x, p.y, p.z );
      private static float3 V( Vector4 p ) => new float3( p.x, p.y, p.z );
      private static float BoxDistance( Node node, float3 p ) { float3 gap = math.max( 0, math.max( V( node.Minimum )-p, p-V( node.Maximum ) ) ); return math.lengthsq( gap ); }
      public void Execute( int index )
      {
        var query = Queries[index]; var shape = Shapes[query.Shape]; var m = shape.WorldToLocal;
        var x = new float3( m.m00,m.m01,m.m02 ); var y = new float3( m.m10,m.m11,m.m12 ); var z = new float3( m.m20,m.m21,m.m22 );
        float3 world = V( query.Point ); float3 p = new float3( math.dot( x,world )+m.m03, math.dot( y,world )+m.m13, math.dot( z,world )+m.m23 );
        float3 closest = default, normal = default; float best = query.Reach*query.Reach; int kind = (int)shape.SizeKind.w;
        if ( kind == 1 ) {
          closest = new float3( p.x,0,p.z ); normal = new float3( 0,1,0 ); best = p.y*p.y;
          if ( best >= query.Reach*query.Reach ) normal = default;
        }
        else if ( kind != 0 && shape.Metadata.x >= 0 ) {
          var stack = new FixedList128Bytes<int>(); stack.Add( (int)shape.Metadata.x );
          while ( stack.Length > 0 ) {
            int last = stack.Length-1, current = stack[last]; stack.RemoveAt( last ); var node = Nodes[current];
            if ( BoxDistance( node,p ) > best ) continue;
            if ( node.Count > 0 ) {
              for ( int i = 0; i < node.Count; ++i ) {
                var t = Triangles[node.Start+i]; float3 a = V( t.A ), b = V( t.B ), c = V( t.C );
                var q = ClosestTriangle( p,a,b,c ); float distance = math.lengthsq( p-q );
                if ( distance >= best ) continue; best = distance; closest = q; normal = math.normalizesafe( math.cross( b-a,c-a ) );
              }
            }
            else {
              int a = node.Start, b = a+1; if ( BoxDistance( Nodes[a],p ) < BoxDistance( Nodes[b],p ) ) { int swap = a;a = b;b = swap; }
              stack.Add( a ); stack.Add( b );
            }
          }
        }
        // Inverse of the rigid world-to-local rotation is its transpose.
        float3 pointWorld = world+x*(closest.x-p.x)+y*(closest.y-p.y)+z*(closest.z-p.z);
        float3 normalWorld = x*normal.x+y*normal.y+z*normal.z;
        Hits[index] = new Hit { Point = new Vector3( pointWorld.x,pointWorld.y,pointWorld.z ), Normal = new Vector3( normalWorld.x,normalWorld.y,normalWorld.z ),
          Distance = math.sqrt( best ), Valid = math.lengthsq( normal ) > 0 ? 1 : 0 };
      }
      private static float3 ClosestTriangle( float3 p,float3 a,float3 b,float3 c )
      {
        float3 ab = b-a, ac = c-a, ap = p-a; float d1 = math.dot( ab,ap ), d2 = math.dot( ac,ap ); if ( d1 <= 0 && d2 <= 0 ) return a;
        float3 bp = p-b; float d3 = math.dot( ab,bp ), d4 = math.dot( ac,bp ); if ( d3 >= 0 && d4 <= d3 ) return b;
        float vc = d1*d4-d3*d2; if ( vc <= 0 && d1 >= 0 && d3 <= 0 ) return a+ab*(d1/(d1-d3));
        float3 cp = p-c; float d5 = math.dot( ab,cp ), d6 = math.dot( ac,cp ); if ( d6 >= 0 && d5 <= d6 ) return c;
        float vb = d5*d2-d1*d6; if ( vb <= 0 && d2 >= 0 && d6 <= 0 ) return a+ac*(d2/(d2-d6));
        float va = d3*d6-d5*d4; if ( va <= 0 && d4-d3 >= 0 && d5-d6 >= 0 ) return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
        float inverse = 1/(va+vb+vc); return a+ab*(vb*inverse)+ac*(vc*inverse);
      }
    }
  }
}
