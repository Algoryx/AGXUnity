using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>Cached triangle BVH in rigid local coordinates; shared CPU/GPU layout.</summary>
  public sealed class SoilBoundaryGeometry
  {
    [StructLayout( LayoutKind.Sequential )]
    public struct Triangle { public Vector4 A, B, C; }
    [StructLayout( LayoutKind.Sequential )]
    public struct Node { public Vector3 Minimum; public int Start; public Vector3 Maximum; public int Count; }
    public Triangle[] Triangles { get; }
    public Node[] Nodes { get; private set; }
    public int NodeCount { get; private set; }
    public Bounds Bounds { get; private set; }
    public bool IsPlanar { get; private set; }
    public Vector4 Plane { get; private set; }
    public bool IsClosed { get; private set; }
    public float ExteriorSide { get; private set; } = 1;
    private readonly List<Node> m_build = new List<Node>();

    public SoilBoundaryGeometry( Vector3[] vertices, int[] indices, bool classifyClosed = false )
    {
      var triangles = new List<Triangle>( indices.Length/3 );
      for ( int i = 0; i+2 < indices.Length; i += 3 ) {
        var a = vertices[indices[i]]; var b = vertices[indices[i+1]]; var c = vertices[indices[i+2]];
        if ( Vector3.Cross( b-a, c-a ).sqrMagnitude > 1e-20f ) triangles.Add( new Triangle { A = a, B = b, C = c } );
      }
      Triangles = triangles.ToArray();
      if ( classifyClosed ) ClassifyClosed();
      if ( Triangles.Length == 0 ) { Nodes = Array.Empty<Node>(); return; }
      m_build.Add( default ); Build( 0, 0, Triangles.Length );
      Nodes = m_build.ToArray(); NodeCount = Nodes.Length; m_build.Clear();
      Bounds = new Bounds( (Nodes[0].Minimum+Nodes[0].Maximum)*0.5f, Nodes[0].Maximum-Nodes[0].Minimum );
      var first = Triangles[0]; var normal = Vector3.Cross( (Vector3)first.B-(Vector3)first.A, (Vector3)first.C-(Vector3)first.A ).normalized;
      float height = Vector3.Dot( normal, first.A ); IsPlanar = true; Plane = new Vector4( normal.x, normal.y, normal.z, height );
      foreach ( var t in Triangles ) if ( Mathf.Abs( Vector3.Dot( normal, t.A )-height ) > 1e-5f ||
        Mathf.Abs( Vector3.Dot( normal, t.B )-height ) > 1e-5f || Mathf.Abs( Vector3.Dot( normal, t.C )-height ) > 1e-5f ) { IsPlanar = false; break; }
    }
    private void ClassifyClosed()
    {
      var welded = new Dictionary<Vector3, int>(); var edges = new Dictionary<ulong, int>(); double volume = 0;
      int Vertex( Vector3 p ) { if ( !welded.TryGetValue( p, out int index ) ) { index = welded.Count; welded.Add( p, index ); } return index; }
      void Edge( int a, int b ) { ulong key = ((ulong)Math.Min( a, b )<<32)|(uint)Math.Max( a, b ); edges.TryGetValue( key, out int count ); edges[key] = count+1; }
      foreach ( var t in Triangles ) {
        int a = Vertex( t.A ), b = Vertex( t.B ), c = Vertex( t.C ); Edge( a, b ); Edge( b, c ); Edge( c, a );
        volume += Vector3.Dot( t.A, Vector3.Cross( t.B, t.C ) );
      }
      IsClosed = edges.Count > 0; foreach ( int count in edges.Values ) if ( count != 2 ) { IsClosed = false; break; }
      ExteriorSide = volume < 0 ? -1 : 1;
    }
    private sealed class CenterComparer : IComparer<Triangle>
    {
      private readonly int m_axis;
      public CenterComparer( int axis ) { m_axis = axis; }
      public int Compare( Triangle a, Triangle b ) => (a.A[m_axis]+a.B[m_axis]+a.C[m_axis]).CompareTo( b.A[m_axis]+b.B[m_axis]+b.C[m_axis] );
    }
    private void Build( int node, int start, int count )
    {
      Vector3 lo = new Vector3( float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity ), hi = -lo;
      for ( int i = start; i < start+count; ++i ) {
        var t = Triangles[i]; lo = Vector3.Min( lo, Vector3.Min( t.A, Vector3.Min( t.B, t.C ) ) );
        hi = Vector3.Max( hi, Vector3.Max( t.A, Vector3.Max( t.B, t.C ) ) );
      }
      if ( count <= 6 ) { m_build[node] = new Node { Minimum = lo, Maximum = hi, Start = start, Count = count }; return; }
      var size = hi-lo; int axis = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
      Array.Sort( Triangles, start, count, new CenterComparer( axis ) );
      int child = m_build.Count; m_build.Add( default ); m_build.Add( default );
      m_build[node] = new Node { Minimum = lo, Maximum = hi, Start = child };
      int half = count/2; Build( child, start, half ); Build( child+1, start+half, count-half );
    }
    public bool Closest( Vector3 point, float reach, out Vector3 closest, out Vector3 normal )
    {
      float best = reach*reach; closest = normal = default;
      if ( NodeCount == 0 ) return false;
      Query( 0, point, ref best, ref closest, ref normal ); return normal.sqrMagnitude > 0;
    }
    private void Query( int index, Vector3 p, ref float best, ref Vector3 closest, ref Vector3 normal )
    {
      var node = Nodes[index]; if ( DistanceSquared( node, p ) > best ) return;
      if ( node.Count > 0 ) {
        for ( int i = node.Start; i < node.Start+node.Count; ++i ) {
          var triangle = Triangles[i]; var q = ClosestTriangle( p, triangle.A, triangle.B, triangle.C ); float d = (p-q).sqrMagnitude;
          if ( d >= best ) continue;
          best = d; closest = q; normal = Vector3.Cross( (Vector3)triangle.B-(Vector3)triangle.A, (Vector3)triangle.C-(Vector3)triangle.A ).normalized;
        }
      }
      else {
        int a = node.Start, b = a+1;
        if ( DistanceSquared( Nodes[a], p ) > DistanceSquared( Nodes[b], p ) ) { int swap = a; a = b; b = swap; }
        Query( a, p, ref best, ref closest, ref normal ); Query( b, p, ref best, ref closest, ref normal );
      }
    }
    public void Collect( Vector3 point, float radius, List<int> triangles ) { if ( NodeCount > 0 ) Collect( 0, point, radius*radius, triangles ); }
    private void Collect( int index, Vector3 point, float squareRadius, List<int> triangles )
    {
      var node = Nodes[index]; if ( DistanceSquared( node, point ) > squareRadius ) return;
      if ( node.Count > 0 ) { for ( int i = node.Start; i < node.Start+node.Count; ++i ) triangles.Add( i ); }
      else { Collect( node.Start, point, squareRadius, triangles ); Collect( node.Start+1, point, squareRadius, triangles ); }
    }
    private static float DistanceSquared( Node node, Vector3 p )
    {
      var delta = Vector3.Max( Vector3.zero, Vector3.Max( node.Minimum-p, p-node.Maximum ) ); return delta.sqrMagnitude;
    }
    public static Vector3 ClosestTriangle( Vector3 p, Vector3 a, Vector3 b, Vector3 c )
    {
      var ab = b-a; var ac = c-a; var ap = p-a; float d1 = Vector3.Dot( ab, ap ), d2 = Vector3.Dot( ac, ap );
      if ( d1 <= 0 && d2 <= 0 ) return a;
      var bp = p-b; float d3 = Vector3.Dot( ab, bp ), d4 = Vector3.Dot( ac, bp ); if ( d3 >= 0 && d4 <= d3 ) return b;
      float vc = d1*d4-d3*d2; if ( vc <= 0 && d1 >= 0 && d3 <= 0 ) return a+ab*(d1/(d1-d3));
      var cp = p-c; float d5 = Vector3.Dot( ab, cp ), d6 = Vector3.Dot( ac, cp ); if ( d6 >= 0 && d5 <= d6 ) return c;
      float vb = d5*d2-d1*d6; if ( vb <= 0 && d2 >= 0 && d6 <= 0 ) return a+ac*(d2/(d2-d6));
      float va = d3*d6-d5*d4; if ( va <= 0 && d4-d3 >= 0 && d5-d6 >= 0 ) return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
      float inverse = 1/(va+vb+vc); return a+ab*(vb*inverse)+ac*(vc*inverse);
    }
  }
}
