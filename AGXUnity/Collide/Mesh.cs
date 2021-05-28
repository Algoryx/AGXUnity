using System.Linq;
using System.Collections.Generic;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Mesh object, convex or general trimesh, given source object
  /// render data.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Mesh" )]
  public sealed class Mesh : Shape
  {
    /// <summary>
    /// Deprecated source object instance - m_sourceObjects list is used now.
    /// </summary>
    [UnityEngine.Serialization.FormerlySerializedAs( "m_sourceObject" )]
    [SerializeField]
    private UnityEngine.Mesh m_legacySourceObject = null;

    /// <summary>
    /// List of source mesh objects to include in the physical mesh.
    /// </summary>
    [SerializeField]
    private List<UnityEngine.Mesh> m_sourceObjects = new List<UnityEngine.Mesh>();

    /// <summary>
    /// Returns all source objects added to this shape.
    /// </summary>
    [HideInInspector]
    public UnityEngine.Mesh[] SourceObjects
    {
      get { return m_sourceObjects.ToArray(); }
    }

    [SerializeField]
    private PrecomputedCollisionMeshData m_precomputedMeshData = null;

    /// <summary>
    /// Optional precomputed mesh data which could for example be convex,
    /// convex decomposition and/or reduced mesh.
    /// </summary>
    [IgnoreSynchronization]
    public PrecomputedCollisionMeshData PrecomputedMeshData
    {
      get { return m_precomputedMeshData; }
      set
      {
        m_precomputedMeshData = value;
      }
    }

    /// <summary>
    /// Returns native mesh object if created.
    /// </summary>
    public agxCollide.Mesh Native { get { return NativeShape?.asMesh(); } }

    /// <summary>
    /// Single source object assignment. All meshes that has been added before
    /// will be removed and <paramref name="mesh"/> added.
    /// </summary>
    /// <param name="mesh"></param>
    /// <returns></returns>
    public bool SetSourceObject( UnityEngine.Mesh mesh )
    {
      var sources = SourceObjects;
      foreach ( var source in sources )
        RemoveSourceObject( source );

      // Returning true if mesh.SetSourceObject( null ) is made to clear source objects.
      return mesh == null || AddSourceObject( mesh );
    }

    /// <summary>
    /// Add source mesh object to this shape.
    /// </summary>
    /// <param name="mesh">Source mesh.</param>
    /// <returns>True if added - otherwise false.</returns>
    public bool AddSourceObject( UnityEngine.Mesh mesh )
    {
      if ( mesh == null || m_sourceObjects.Contains( mesh ) )
        return false;

      if ( !mesh.isReadable ) {
        Debug.LogWarning( "Trying to add source mesh: " + mesh.name + ", which vertices/triangles isn't readable. Ignoring source.", this );
        return false;
      }

      m_sourceObjects.Add( mesh );

      OnSourceObject( mesh, true );

      return true;
    }

    /// <summary>
    /// Remove source mesh object from this shape.
    /// </summary>
    /// <param name="mesh">Source object to remove.</param>
    /// <returns>True if removed.</returns>
    public bool RemoveSourceObject( UnityEngine.Mesh mesh )
    {
      bool removed = m_sourceObjects.Remove( mesh );
      if ( removed )
        OnSourceObject( mesh, false );

      return removed;
    }

    /// <summary>
    /// Moves old single source to source list.
    /// </summary>
    /// <returns>True if changes were made.</returns>
    public bool PatchSingleSourceToSourceList()
    {
      if ( m_legacySourceObject == null )
        return false;

      SetSourceObject( m_legacySourceObject );
      m_legacySourceObject = null;

      return true;
    }

    /// <summary>
    /// Resets gizmos rendering meshes when there has been changes
    /// in the mesh data.
    /// </summary>
    public void OnPrecomputedCollisionMeshDataDirty()
    {
      ResetRenderMeshes();
    }

    /// <summary>
    /// Scale of meshes are inherited by the parents and supports non-uniform scaling.
    /// </summary>
    public override Vector3 GetScale()
    {
      return Vector3.one;
    }

    /// <summary>
    /// Creates a native instance of the mesh and returns it. Performance warning.
    /// </summary>
    public override agxCollide.Geometry CreateTemporaryNative()
    {
      return CreateNative();
    }

    /// <summary>
    /// Create the native mesh object given the current source mesh.
    /// </summary>
    protected override agxCollide.Geometry CreateNative()
    {
      return Create( SourceObjects );
    }

    /// <summary>
    /// Override of initialize, only to delete any reference to a
    /// cached native object.
    /// </summary>
    protected override bool Initialize()
    {
      if ( m_legacySourceObject != null ) {
        if ( m_sourceObjects.Count == 0 )
          m_sourceObjects.Add( m_legacySourceObject );
        m_legacySourceObject = null;
      }

      return base.Initialize();
    }

    /// <summary>
    /// Called when any source object has been added or removed.
    /// </summary>
    /// <param name="source">Source object that has been added or removed.</param>
    /// <param name="added">True if <paramref name="source"/> has been added - otherwise false.</param>
    private void OnSourceObject( UnityEngine.Mesh source, bool added )
    {
      var debugRenderData = GetComponent<Rendering.ShapeDebugRenderData>();
      if ( debugRenderData != null && debugRenderData.Node != null )
        DestroyImmediate( debugRenderData.Node );

      Rendering.ShapeVisualMesh.HandleMeshSource( this, source, added );

      ResetRenderMeshes();
    }

    /// <summary>
    /// Merges all source objects to one mesh and creates a native trimesh.
    /// </summary>
    /// <param name="meshes">Source meshes.</param>
    /// <returns>Native trimesh.</returns>
    private agxCollide.Geometry Create( UnityEngine.Mesh[] meshes )
    {
      var geometry = new agxCollide.Geometry();
      if ( m_precomputedMeshData != null && m_precomputedMeshData.CreateShapes( transform ) is var shapes && shapes != null ) {
        foreach ( var shape in shapes )
          geometry.add( shape, GetNativeGeometryOffset() );
      }
      else {
        if ( m_precomputedMeshData != null )
          Debug.LogWarning( "AGXUnity.Mesh: Failed to create shapes from precomputed data - using Trimesh as fallback.", this );

        var merger = MeshMerger.Merge( transform, meshes );
        geometry.add( new agxCollide.Trimesh( merger.Vertices,
                                              merger.Indices,
                                              "AGXUnity.Mesh: Trimesh" ) );
      }

      if ( geometry.getShapes().Count == 0 ) {
        geometry.Dispose();
        geometry = null;
      }

      return geometry;
    }

    private new void Reset()
    {
      if ( SourceObjects.Length == 0 ) {
        var visual = Rendering.ShapeVisual.Find( this );
        if ( visual != null )
          DestroyImmediate( visual.gameObject );

        var filter = GetComponent<MeshFilter>();
        if ( filter != null )
          SetSourceObject( filter.sharedMesh );
      }

      base.Reset();

      ResetRenderMeshes();
    }

    private void OnDrawGizmosSelected()
    {
      if ( m_renderMeshes.Length == 0 && m_sourceObjects.Count > 0 )
        CreateRenderMeshes();
      var prevColor = Gizmos.color;
      for ( int i = 0; i < m_renderMeshes.Length; ++i ) {
        Gizmos.color = m_renderColors[ i ];
        Gizmos.DrawWireMesh( m_renderMeshes[ i ],
                             transform.position,
                             transform.rotation,
                             transform.lossyScale );
      }
      Gizmos.color = prevColor;
    }

    private void ResetRenderMeshes()
    {
      m_renderMeshes = new UnityEngine.Mesh[] { };
      m_renderColors = new Color[] { };
    }

    private void CreateRenderMeshes()
    {
      ResetRenderMeshes();

      if ( m_precomputedMeshData != null ) {
        var renderMeshes = new List<UnityEngine.Mesh>();
        var renderColors = new List<Color>();

        var prevState = Random.state;
        Random.InitState( GetInstanceID() );
        foreach ( var collisionMesh in m_precomputedMeshData.CollisionMeshes ) {
          // TODO: Remove this. It shouldn't be null.
          if ( collisionMesh == null )
            continue;

          var meshes = collisionMesh.CreateRenderMeshes( transform );
          renderMeshes.AddRange( meshes );
          var color = Random.ColorHSV();
          renderColors.AddRange( Enumerable.Repeat( color, meshes.Length ) );
        }
        Random.state = prevState;

        m_renderMeshes = renderMeshes.ToArray();
        m_renderColors = renderColors.ToArray();
      }
      else {
        m_renderMeshes = SourceObjects;

        var prevState = Random.state;
        Random.InitState( GetInstanceID() );
        var color = Random.ColorHSV();
        m_renderColors = Enumerable.Repeat( color, m_renderMeshes.Length ).ToArray();
        Random.state = prevState;
      }
    }

    [System.NonSerialized]
    private UnityEngine.Mesh[] m_renderMeshes = new UnityEngine.Mesh[] { };
    [System.NonSerialized]
    private Color[] m_renderColors = new Color[] { };
  }
}
