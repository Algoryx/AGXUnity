using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class MeshSplitter
  {
    public class SubMeshData
    {
      private Dictionary<Vector3, int> m_vertexToIndexTable = new Dictionary<Vector3, int>();
      private List<Vector3> m_vertices = new List<Vector3>();
      private List<Vector2> m_uvs = null;
      private List<int> m_indices = new List<int>();
      private Mesh m_mesh = null;

      public int NumVertices { get { return m_vertices.Count; } }
      public int NumIndices { get { return m_indices.Count; } }
      public Mesh Mesh { get { return m_mesh; } }

      public SubMeshData( bool hasUvs, int capacity = Int16.MaxValue )
      {
        m_vertices.Capacity = capacity;
        m_indices.Capacity = capacity;
        if ( hasUvs )
          m_uvs = new List<Vector2>( capacity );
      }

      public void Add( Vector3 v1, Vector3 v2, Vector3 v3 )
      {
        Add( v1 );
        Add( v2 );
        Add( v3 );
      }

      public void Add( Vector3 v1,
                       Vector3 v2,
                       Vector3 v3,
                       Vector2 uv1,
                       Vector2 uv2,
                       Vector2 uv3 )
      {
        Add( v1, uv1 );
        Add( v2, uv2 );
        Add( v3, uv3 );
      }

      public void CreateMesh( agx.Vec3Vector vertices,
                              agx.UInt32Vector indices,
                              agx.Vec2Vector uvs,
                              Func<agx.Vec3, Vector3> vertexTransformer )
      {
        m_vertices.AddRange( from v in vertices select vertexTransformer( v ) );
        if ( uvs != null )
          m_uvs.AddRange( from uv in uvs select uv.ToVector2() );
        for ( int i = 0; i < indices.Count; i += 3 ) {
          m_indices.Add( Convert.ToInt32( indices[ i + 0 ] ) );
          m_indices.Add( Convert.ToInt32( indices[ i + 2 ] ) );
          m_indices.Add( Convert.ToInt32( indices[ i + 1 ] ) );
        }

        CreateMesh();
      }

      public void CreateMesh( Vector3[] vertices,
                              int[] indices )
      {
        m_vertices.AddRange( vertices.Select( v => v.ToLeftHanded() ) );
        for ( int i = 0; i < indices.Length; i += 3 ) {
          m_indices.Add( indices[ i + 0 ] );
          m_indices.Add( indices[ i + 2 ] );
          m_indices.Add( indices[ i + 1 ] );
        }

        CreateMesh();
      }

      public void CreateMesh()
      {
        if ( m_mesh != null )
          return;

        m_mesh = new Mesh();
        if ( m_vertices.Count > UInt16.MaxValue )
          m_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        m_mesh.SetVertices( m_vertices );
        m_mesh.SetTriangles( m_indices, 0, false );

        if ( m_uvs != null )
          m_mesh.SetUVs( 0, m_uvs );

        m_mesh.RecalculateBounds();
        m_mesh.RecalculateNormals();
        m_mesh.RecalculateTangents();
      }

      private int Add( Vector3 v )
      {
        int index;
        if ( !m_vertexToIndexTable.TryGetValue( v, out index ) ) {
          index = m_vertices.Count;
          m_vertexToIndexTable.Add( v, index );
          m_vertices.Add( v );
        }

        m_indices.Add( index );

        return index;
      }

      private void Add( Vector3 v, Vector2 uv )
      {
        var index = Add( v );
        if ( index >= m_uvs.Count ) {
          Debug.Assert( index == m_uvs.Count,
                        $"Vertex index = {index}, UV count = {m_uvs.Count}" );
          m_uvs.Add( uv );
        }
      }
    }

    public static MeshSplitter Split( agx.Vec3Vector vertices,
                                      agx.UInt32Vector indices,
                                      Func<agx.Vec3, Vector3> transformer )
    {
      return Split( vertices, indices, null, transformer );
    }

    public static MeshSplitter Split( agx.Vec3Vector vertices,
                                      agx.UInt32Vector indices,
                                      agx.Vec2Vector uvs,
                                      Func<agx.Vec3, Vector3> vertexTransformer )
    {
      var splitter = new MeshSplitter();
      var maxNumVertices = GetMaxNumVertices( vertices.Count );

      if ( vertices.Count <= maxNumVertices ) {
        splitter.m_subMeshData.Add( new SubMeshData( uvs != null, vertices.Count ) );
        splitter.m_subMeshData.Last().CreateMesh( vertices, indices, uvs, vertexTransformer );
        return splitter;
      }

      // This works but isn't correct. It's not possible to recover
      // the triangles list for a mesh with #vertices < MaxValue.
      splitter.m_vertices = new List<Vector3>( vertices.Count );
      for ( int i = 0; i < vertices.Count; ++i )
        splitter.m_vertices.Add( vertexTransformer( vertices[ i ] ) );

      var hasUvs = uvs != null && uvs.Count == vertices.Count;
      for ( int i = 0; i < indices.Count; i += 3 ) {
        // Potentially adding three new vertices below, create new sub-mesh
        // when current number of vertices is max - 3.
        if ( i == 0 || splitter.m_subMeshData.Last().NumVertices >= maxNumVertices - 2 )
          splitter.m_subMeshData.Add( new SubMeshData( hasUvs ) );

        if ( hasUvs ) {
          splitter.m_subMeshData.Last().Add( splitter.m_vertices[ Convert.ToInt32( indices[ i + 0 ] ) ],
                                             splitter.m_vertices[ Convert.ToInt32( indices[ i + 2 ] ) ],
                                             splitter.m_vertices[ Convert.ToInt32( indices[ i + 1 ] ) ],
                                             uvs[ Convert.ToInt32( indices[ i + 0 ] ) ].ToVector2(),
                                             uvs[ Convert.ToInt32( indices[ i + 2 ] ) ].ToVector2(),
                                             uvs[ Convert.ToInt32( indices[ i + 1 ] ) ].ToVector2() );
        }
        else {
          splitter.m_subMeshData.Last().Add( splitter.m_vertices[ Convert.ToInt32( indices[ i + 0 ] ) ],
                                             splitter.m_vertices[ Convert.ToInt32( indices[ i + 2 ] ) ],
                                             splitter.m_vertices[ Convert.ToInt32( indices[ i + 1 ] ) ] );
        }
      }

      foreach ( var data in splitter.m_subMeshData )
        data.CreateMesh();

      return splitter;
    }

    /// <summary>
    /// Assuming vertices and indices are stored in AGX mesh format.
    /// </summary>
    public static MeshSplitter Split( Vector3[] vertices,
                                      int[] indices )
    {
      var splitter = new MeshSplitter();
      var maxNumVertices = GetMaxNumVertices( vertices.Length );

      if ( vertices.Length <= maxNumVertices ) {
        splitter.m_subMeshData.Add( new SubMeshData( false, vertices.Length ) );
        splitter.m_subMeshData.Last().CreateMesh( vertices, indices );
        return splitter;
      }

      for ( int i = 0; i < indices.Length; i += 3 ) {
        // Potentially adding three new vertices below, create new sub-mesh
        // when current number of vertices is max - 3.
        if ( i == 0 || splitter.m_subMeshData.Last().NumVertices >= maxNumVertices - 2 )
          splitter.m_subMeshData.Add( new SubMeshData( false ) );

        splitter.m_subMeshData.Last().Add( vertices[ indices[ i + 0 ] ],
                                           vertices[ indices[ i + 2 ] ],
                                           vertices[ indices[ i + 1 ] ] );
      }

      foreach ( var data in splitter.m_subMeshData )
        data.CreateMesh();

      return splitter;
    }

    private static uint GetMaxNumVertices( int numVertices )
    {
      return numVertices <= UInt16.MaxValue ? UInt16.MaxValue : UInt32.MaxValue;
    }

    private List<Vector3> m_vertices = new List<Vector3>();
    private List<SubMeshData> m_subMeshData = new List<SubMeshData>();

    public Mesh[] Meshes { get { return ( from data in m_subMeshData select data.Mesh ).ToArray(); } }
  }
}
