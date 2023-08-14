using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class MeshMerger
  {
    public static MeshMerger Merge( Transform transform, Mesh[] meshes )
    {
      var merger = new MeshMerger( transform );

      foreach ( var mesh in meshes ) {
        var triangles = mesh.triangles;
        var vertices  = mesh.vertices;
        for ( int i = 0; i < triangles.Length; i += 3 )
          merger.Add( vertices[ triangles[ i + 0 ] ],
                      vertices[ triangles[ i + 2 ] ],
                      vertices[ triangles[ i + 1 ] ] );
      }

      return merger;
    }

    private Dictionary<Vector3, int> m_vertexToIndexTable = new Dictionary<Vector3, int>();
    private agx.Vec3Vector m_vertices = new agx.Vec3Vector();
    private agx.UInt32Vector m_indices = new agx.UInt32Vector();

    private Transform m_transform = null;
    private Matrix4x4 m_toWorld = Matrix4x4.identity;

    public agx.Vec3Vector Vertices { get { return m_vertices; } set { m_vertices = value; } }
    public agx.UInt32Vector Indices { get { return m_indices; } set { m_indices = value; } }

    public MeshMerger( Transform transform )
    {
      m_transform = transform;
      if ( m_transform != null )
        m_toWorld = transform.localToWorldMatrix;
    }

    public bool MergeNearby(float mergeNearbyDistance)
    {
      if (m_vertices.Count == 0)
        return false;

      var reducedVertices = new agx.Vec3Vector();
      var reducedIndices = new agx.UInt32Vector();
      agxUtil.agxUtilSWIG.mergeDuplicateVertices(Vertices,
                                           Indices,
                                           reducedVertices,
                                           reducedIndices,
                                           mergeNearbyDistance);

      m_vertices = reducedVertices;
      m_indices = reducedIndices;

      return true;
    }

    public bool Reduce( float reductionRatio, float reductionAggressiveness )
    {
      if ( m_vertices.Count == 0 )
        return false;

      var reducedVertices = new agx.Vec3Vector();
      var reducedIndices = new agx.UInt32Vector();
      if ( agxUtil.agxUtilSWIG.reduceMesh( Vertices,
                                           Indices,
                                           reducedVertices,
                                           reducedIndices,
                                           reductionRatio,
                                           reductionAggressiveness ) is var success ) {
        m_vertices = reducedVertices;
        m_indices = reducedIndices;
      }

      return success;
    }

    public bool Reduce( agx.Vec3Vector vertices,
                        agx.UInt32Vector indices,
                        float reductionRatio,
                        float reductionAggressiveness )
    {
      if ( vertices.Count < 3 || ( indices.Count % 3 ) != 0 )
        return false;

      m_vertices = vertices;
      m_indices = indices;

      return Reduce( reductionRatio, reductionAggressiveness );
    }

    public void Add( Vector3 v1, Vector3 v2, Vector3 v3 )
    {
      Add( v1 );
      Add( v2 );
      Add( v3 );
    }

    private void Add( Vector3 v )
    {
      int index;
      if ( !m_vertexToIndexTable.TryGetValue( v, out index ) ) {
        index = m_vertices.Count;
        m_vertexToIndexTable.Add( v, index );

        if ( m_transform != null )
          m_vertices.Add( m_transform.InverseTransformDirection( m_toWorld * v ).ToHandedVec3() );
        else
          m_vertices.Add( v.ToHandedVec3() );
      }

      m_indices.Add( Convert.ToUInt32( index ) );
    }
  }
}
