using System;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Class containing UNSCALED collision mesh data in AGX Dynamics format.
  /// </summary>
  [DoNotGenerateCustomEditor]
  [Serializable]
  public class CollisionMeshData
  {
    /// <summary>
    /// Vertices stored in AGX Dynamics format, right handed.
    /// </summary>
    public Vector3[] Vertices = new Vector3[] { };

    /// <summary>
    /// Triangle indices stored in AGX Dynamics format - 0, 2, 1.
    /// </summary>
    public int[] Indices = new int[] { };

    public void Apply( agx.Vec3Vector vertices, agx.UInt32Vector indices )
    {
      Vertices = vertices.Select( v => v.ToVector3() ).ToArray();
      Indices = indices.Select( i => (int)i ).ToArray();
    }

    public agxCollide.Mesh CreateShape( Func<Vector3, Vector3> transformer,
                                        CollisionMeshOptions.MeshMode mode )
    {
      // The transformer will return the vertex in left handed frame since
      // it has been scaled.
      var vertices = new agx.Vec3Vector( Vertices.Select( v => transformer( v ).ToHandedVec3() ).ToArray() );
      var indices = new agx.UInt32Vector( Indices.Select( i => (uint)i ).ToArray() );
      return mode == CollisionMeshOptions.MeshMode.Trimesh ?
               new agxCollide.Trimesh( vertices, indices, "AGXUnity.Mesh: Trimesh" ) :
               new agxCollide.Convex( vertices, indices, "AGXUnity.Mesh: " + mode.ToString() );
    }

    public UnityEngine.Mesh[] CreateRenderMeshes( Transform transform )
    {
      // This will convert to left handed and change triangle order.
      return MeshSplitter.Split( Vertices, Indices ).Meshes;
    }
  }
}
