using System;
using System.Linq;
using System.Threading.Tasks;

namespace AGXUnity.Collide
{
  public class CollisionMeshGenerator : IDisposable
  {
    /// <summary>
    /// Collision mesh result given options and original mesh.
    /// </summary>
    public struct Result
    {
      /// <summary>
      /// Original mesh with source objects.
      /// </summary>
      public Mesh Mesh;

      /// <summary>
      /// Options how the collision meshes should be generated.
      /// </summary>
      public CollisionMeshOptions Options;

      /// <summary>
      /// Resulting collision meshes.
      /// </summary>
      public CollisionMeshData[] CollisionMeshes;
    }

    /// <summary>
    /// True if ready to generate collision meshes, i.e.,
    /// no task is currently generating meshes. If false it's
    /// not valid to call Generate or GenerateAsync.
    /// </summary>
    public bool IsReady
    {
      get
      {
        return m_task == null || m_task.IsCompleted;
      }
    }

    /// <summary>
    /// True while the collision meshes are generated, otherwise false.
    /// </summary>
    public bool IsRunning
    {
      get { return m_task != null && !m_task.IsCompleted; }
    }

    /// <summary>
    /// Current progress ranging [0, 1].
    /// </summary>
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

    /// <summary>
    /// Start task to generate the collision meshes given an array
    /// of AGXUnity.Collide.Mesh instances and optionally mesh options.
    /// If the array of options is given it has to be of the same length
    /// as <paramref name="meshes"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [ContextMenu( "GameObject/Meshing/Generate Convexes" )]
    /// private void GenerateConvexes( MenuCommand command )
    /// {
    ///   var go = Selection.GetFiltered<GameObject>( SelectionMode.TopLevel |
    ///                                               SelectionMode.Editable ).FirstOrDefault();
    ///   if ( go == null )
    ///     return;
    /// 
    ///   var meshes = go.GetComponentsInChildren<AGXUnity.Collide.Mesh>();
    ///   // "get or create options".
    ///   var meshOptions = meshes.Select( mesh => mesh.Options ??
    ///                                            new AGXUnity.Collide.CollisionMeshOptions() ).ToArray();
    ///   foreach ( var options in meshOptions )
    ///     options.Mode = AGXUnity.Collide.CollisionMeshOptions.MeshMode.Convex;
    /// 
    ///   var generator = new AGXUnity.Collide.CollisionMeshGenerator();
    ///   generator.GenerateAsync( meshes, meshOptions );
    ///   while ( generator.IsRunning ) {
    ///     EditorUtility.DisplayProgressBar( "Generating collision meshes...",
    ///                                       string.Empty,
    ///                                       generator.Progress );
    ///   }
    ///   EditorUtility.ClearProgressBar();
    /// 
    ///   var results = generator.CollectResults();
    ///   foreach ( var result in results ) {
    ///     Undo.RecordObject( result.Mesh, "Convex collision mesh" );
    ///     result.Mesh.Options = result.Options;
    ///     result.Mesh.PrecomputedCollisionMeshes = result.CollisionMeshes;
    ///   }
    /// }
    /// </code>
    /// </example>
    /// <param name="meshes">Array of meshes to generate collision meshes for.</param>
    /// <param name="options">Optional array of meshing options, mesh.Options is used if this argument isn't given.</param>
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

    /// <summary>
    /// Generates collision meshes, blocking, and returns the results.
    /// </summary>
    /// <param name="meshes">Array of meshes to generate collision meshes for.</param>
    /// <param name="options">Optional array of meshing options, mesh.Option is used if this argument isn't given.</param>
    /// <returns></returns>
    public Result[] Generate( Mesh[] meshes, CollisionMeshOptions[] options = null )
    {
      GenerateAsync( meshes, options );
      return CollectResults();
    }

    /// <summary>
    /// Waits for the collision mesh generator task to finish and returns
    /// the result.
    /// </summary>
    /// <returns>Array of results matching the arguments given to Generate or GenerateAsync.</returns>
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

    /// <summary>
    /// Wait for the collision mesh generator task to finish.
    /// </summary>
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
            var collisionMesh = CreateDataOptionallyReduce( mesh,
                                                            options,
                                                            merger );
            var result = new Result()
            {
              Mesh = mesh,
              Options = options,
              CollisionMeshes = collisionMesh != null ?
                                  new CollisionMeshData[] { collisionMesh } :
                                  null
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
              CollisionMeshes = collisionMesh != null ?
                                  new CollisionMeshData[] { collisionMesh } :
                                  null
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
            if ( convexes.Count == 0 )
              UnityEngine.Debug.LogWarning( $"Convex Decomposition of {mesh} resulted in zero convex shapes.", mesh );

            var collisionMeshes = ( from convexRef in convexes
                                    let collisionMesh = CreateDataOptionallyReduce( mesh,
                                                                                    options,
                                                                                    merger,
                                                                                    convexRef.get() )
                                    where collisionMesh != null
                                    select collisionMesh ).ToArray();
            if ( collisionMeshes.Length > 0 )
              result.CollisionMeshes = collisionMeshes;

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
      var reductionEnabled = options != null && options.ReductionEnabled;
      var orgNumVertices = merger.Vertices.Count;
      if ( reductionEnabled )
        merger.Reduce( options.ReductionRatio, options.ReductionAggressiveness );

      if ( merger.Vertices.Count == 0 ) {
        if ( reductionEnabled && orgNumVertices > 0 )
          UnityEngine.Debug.LogWarning( $"Vertex Reduction reduced a collision mesh from {orgNumVertices} vertices to zero. " +
                                        "Ignoring collision mesh.", mesh );
        else
          UnityEngine.Debug.LogWarning( $"Mesh \"{mesh.name}\" doesn't contain any vertices for the collision mesh. " +
                                        "Ignoring collision mesh.", mesh );
        return null;
      }

      // Next, if merge nearby vertices is enabled, do that too:
      var mergeNearbyEnabled = options != null && options.MergeNearbyEnabled;
      orgNumVertices = merger.Vertices.Count;
      if (mergeNearbyEnabled) { 
        merger.MergeNearby(options.MergeNearbyDistance);
      }

      if (merger.Vertices.Count == 0)
      {
        if (mergeNearbyEnabled && orgNumVertices > 0)
          UnityEngine.Debug.LogWarning($"Merging nearby Vertices reduced a collision mesh from {orgNumVertices} vertices to zero. " +
                                        "Ignoring collision mesh.", mesh);
        else
          UnityEngine.Debug.LogWarning($"Mesh \"{mesh.name}\" doesn't contain any vertices for the collision mesh. " +
                                        "Ignoring collision mesh.", mesh);

        return null;
      }

      var meshData = new CollisionMeshData();
      meshData.Apply( merger.Vertices, merger.Indices );

      return meshData;
    }

    private Task<Result[]> m_task = null;
    private float m_progress = 0.0f;
  }
}
