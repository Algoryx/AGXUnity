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
      private List<int> m_indices = new List<int>();
      private Mesh m_mesh = null;

      public int NumVertices { get { return m_vertices.Count; } }
      public int NumIndices { get { return m_indices.Count; } }
      public Mesh Mesh { get { return m_mesh; } }

      public SubMeshData( int capacity = Int16.MaxValue )
      {
        m_vertices.Capacity = capacity;
        m_indices.Capacity = capacity;
      }

      public void Add( Vector3 v1, Vector3 v2, Vector3 v3 )
      {
        Add( v1 );
        Add( v2 );
        Add( v3 );
      }

      public void CreateMesh()
      {
        if ( m_mesh != null )
          return;

        m_mesh = new Mesh();
        m_mesh.SetVertices( m_vertices );
        m_mesh.SetTriangles( m_indices, 0, false );

        m_mesh.RecalculateBounds();
        m_mesh.RecalculateNormals();
        m_mesh.RecalculateTangents();
      }

      private void Add( Vector3 v )
      {
        int index;
        if ( !m_vertexToIndexTable.TryGetValue( v, out index ) ) {
          index = m_vertices.Count;
          m_vertexToIndexTable.Add( v, index );
          m_vertices.Add( v );
        }

        m_indices.Add( index );
      }
    }

    public static MeshSplitter Split( agx.Vec3Vector vertices,
                                      agx.UInt32Vector indices,
                                      Func<agx.Vec3, Vector3> transformer,
                                      int maxNumVertices = Int16.MaxValue )
    {
      var splitter = new MeshSplitter();
      splitter.m_vertices = new List<Vector3>( vertices.Count );
      for ( int i = 0; i < vertices.Count; ++i )
        splitter.m_vertices.Add( transformer( vertices[ i ] ) );

      for ( int i = 0; i < indices.Count; i += 3 ) {
        if ( i == 0 || splitter.m_subMeshData.Last().NumVertices >= maxNumVertices )
          splitter.m_subMeshData.Add( new SubMeshData( maxNumVertices ) );

        splitter.m_subMeshData.Last().Add( splitter.m_vertices[ Convert.ToInt32( indices[ i + 0 ] ) ],
                                           splitter.m_vertices[ Convert.ToInt32( indices[ i + 2 ] ) ],
                                           splitter.m_vertices[ Convert.ToInt32( indices[ i + 1 ] ) ] );
      }

      foreach ( var data in splitter.m_subMeshData )
        data.CreateMesh();

      return splitter;
    }

    private List<Vector3> m_vertices = new List<Vector3>();
    private List<SubMeshData> m_subMeshData = new List<SubMeshData>();

    public Mesh[] Meshes { get { return ( from data in m_subMeshData select data.Mesh ).ToArray(); } }
  }
}
