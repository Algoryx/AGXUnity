using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Collide
{
  [DoNotGenerateCustomEditor]
  public class PrecomputedCollisionMeshData : ScriptableObject
  {
    public CollisionMeshData[] CollisionMeshes
    {
      get { return m_collisionMeshes.ToArray(); }
    }

    public CollisionMeshOptions Options
    {
      get { return m_options; }
      set
      {
        m_options = value;
      }
    }

    public void Apply( Mesh mesh )
    {
      // TODO: Error handling.

      DestroyCollisionMeshes();

      // Vertices in AGX Dynamics format - right handed and indices 0, 2, 1.
      var merger = MeshMerger.Merge( null, mesh.SourceObjects );
      if ( Options.Mode == CollisionMeshOptions.MeshMode.Trimesh ) {
        m_collisionMeshes.Add( CreateDataOptionallyReduce( merger ) );
      }
      else if ( Options.Mode == CollisionMeshOptions.MeshMode.Convex ) {
        using ( var tmpComvex = agxUtil.agxUtilSWIG.createConvexRef( merger.Vertices ) ) {
          merger.Vertices = tmpComvex.getMeshData().getVertices();
          merger.Indices  = tmpComvex.getMeshData().getIndices();

          m_collisionMeshes.Add( CreateDataOptionallyReduce( merger ) );
        }
      }
      else if ( Options.Mode == CollisionMeshOptions.MeshMode.ConvexDecomposition ) {
        var convexes = new agxCollide.ConvexVector();
        agxUtil.agxUtilSWIG.createVHACDConvexDecomposition( merger.Vertices,
                                                            merger.Indices,
                                                            convexes,
                                                            (uint)Options.ElementResolutionPerAxis );
        for ( int i = 0; i < convexes.Count; ++i ) {
          merger.Vertices = convexes[ i ].getMeshData().getVertices();
          merger.Indices  = convexes[ i ].getMeshData().getIndices();

          m_collisionMeshes.Add( CreateDataOptionallyReduce( merger ) );
        }
      }

      mesh.OnPrecomputedCollisionMeshDataDirty();
    }

    public agxCollide.Mesh[] CreateShapes( Transform transform )
    {
      if ( transform == null || CollisionMeshes.Length == 0 )
        return null;

      // The vertices are assumed to be stored in local coordinates of the
      // given transform. For the scale to be correct w
      var toWorld = transform.localToWorldMatrix;
      Func<Vector3, Vector3> transformer = v =>
      {
        return transform.InverseTransformDirection( toWorld * v.ToLeftHanded() );
      };

      return CollisionMeshes.Select( collisionMesh => collisionMesh.CreateShape( transformer, Options.Mode ) ).ToArray();
    }

    public void DestroyCollisionMeshes()
    {
      m_collisionMeshes.Clear();
    }

    private CollisionMeshData CreateDataOptionallyReduce( MeshMerger merger )
    {
      if ( Options.ReductionEnabled )
        merger.Reduce( Options.ReductionRatio, Options.ReductionAggressiveness );

      var meshData = new CollisionMeshData();
      meshData.Apply( merger.Vertices, merger.Indices );

      return meshData;
    }

    [SerializeField]
    private List<CollisionMeshData> m_collisionMeshes = new List<CollisionMeshData>();

    [SerializeField]
    private CollisionMeshOptions m_options = null;
  }
}
