using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AGXUnity.Collide
{
  public class CollisionMeshGenerator : IDisposable
  {
    public struct Result
    {
      public Mesh Mesh;
      public CollisionMeshOptions Options;
      public CollisionMeshData[] CollisionMeshes;
    }

    public bool IsReady
    {
      get
      {
        return m_task == null || m_task.IsCompleted;
      }
    }

    public bool IsRunning
    {
      get { return m_task != null && !m_task.IsCompleted; }
    }

    public float Progress
    {
      get
      {
        if ( IsReady )
          return 1.0f;
        return m_progress;
      }
      private set
      {
        m_progress = value;
      }
    }

    public void GenerateAsync( Mesh[] meshes, CollisionMeshOptions[] options = null )
    {
      if ( !IsReady ) {
        UnityEngine.Debug.LogError( "CollisionMeshGenerator: All previous tasks hasn't completed - invalid to generate new meshes." );
        return;
      }

      if ( options != null && meshes.Length != options.Length ) {
        UnityEngine.Debug.LogError( "CollisionMeshGenerator: Given CollisionMeshOptions array length has to match the meshes." );
        return;
      }

      if ( options == null )
        options = meshes.Select( mesh => mesh.Options ).ToArray();

      m_task = Create( meshes, options );
    }

    public Result[] Generate( Mesh[] meshes, CollisionMeshOptions[] options = null )
    {
      GenerateAsync( meshes, options );
      return CollectResults();
    }

    public Result[] CollectResults()
    {
      if ( m_task == null )
        return new Result[] { };

      Wait();

      var results = m_task.Result;
      m_task.Dispose();
      m_task = null;

      return results;
    }

    public void Wait()
    {
      m_task?.Wait();
    }

    public void Dispose()
    {
      Wait();
    }

    private Task<Result[]> Create( Mesh[] meshes, CollisionMeshOptions[] meshOptions )
    {
      var mergers = meshes.Select( mesh => Utils.MeshMerger.Merge( null, mesh.SourceObjects ) ).ToArray();
      var totNumVertices = mergers.Sum( merger => merger.Vertices.Count );
      Progress = 0.0f;
      var numProcessedVertices = 0;
      return Task.Run( () =>
      {
        var results = new Result[ meshes.Length ];

        NativeHandler.Instance.RegisterCurrentThread();

        for ( int i = 0; i < meshes.Length; ++i ) {
          var mesh = meshes[ i ];
          var options = meshOptions[ i ];

          var merger = mergers[ i ];
          numProcessedVertices += merger.Vertices.Count;
          if ( options == null || options.Mode == CollisionMeshOptions.MeshMode.Trimesh ) {
            var result = new Result()
            {
              Mesh = mesh,
              Options = options,
              CollisionMeshes = new CollisionMeshData[] { CreateDataOptionallyReduce( mesh,
                                                                                      options,
                                                                                      merger ) }
            };
            results[ i ] = result;
          }
          else if ( options.Mode == CollisionMeshOptions.MeshMode.Convex ) {
            CollisionMeshData collisionMesh = null;
            using ( var tmpConvex = agxUtil.agxUtilSWIG.createConvexRef( merger.Vertices ) )
              collisionMesh = CreateDataOptionallyReduce( mesh,
                                                          options,
                                                          merger,
                                                          tmpConvex.get() );

            var result = new Result()
            {
              Mesh = mesh,
              Options = options,
              CollisionMeshes = new CollisionMeshData[] { collisionMesh }
            };
            results[ i ] = result;
          }
          else if ( options.Mode == CollisionMeshOptions.MeshMode.ConvexDecomposition ) {
            var result = new Result()
            {
              Mesh = mesh,
              Options = options,
              CollisionMeshes = null
            };

            var convexes = new agxCollide.ConvexVector();
            var elementsPerAxis = options.ElementResolutionPerAxis;
            agxUtil.agxUtilSWIG.createVHACDConvexDecomposition( merger.Vertices,
                                                                merger.Indices,
                                                                convexes,
                                                                (uint)elementsPerAxis );
            result.CollisionMeshes = convexes.Select( convex => CreateDataOptionallyReduce( mesh,
                                                                                            options,
                                                                                            merger,
                                                                                            convex.get() ) ).ToArray();
            results[ i ] = result;
          }

          Progress = (float)numProcessedVertices / totNumVertices;
        }

        NativeHandler.Instance.UnregisterCurrentThread();

        return results;
      } );
    }

    private static CollisionMeshData CreateDataOptionallyReduce( Mesh mesh,
                                                                 CollisionMeshOptions options,
                                                                 Utils.MeshMerger merger,
                                                                 agxCollide.Convex convex )
    {
      merger.Vertices = convex.getMeshData().getVertices();
      merger.Indices = convex.getMeshData().getIndices();
      return CreateDataOptionallyReduce( mesh, options, merger );
    }

    private static CollisionMeshData CreateDataOptionallyReduce( Mesh mesh,
                                                                 CollisionMeshOptions options,
                                                                 Utils.MeshMerger merger )
    {
      if ( options != null && options.ReductionEnabled )
        merger.Reduce( options.ReductionRatio, options.ReductionAggressiveness );

      var meshData = new CollisionMeshData();
      meshData.Apply( merger.Vertices, merger.Indices );

      return meshData;
    }

    private Task<Result[]> m_task = null;
    private float m_progress = 0.0f;
  }
}
