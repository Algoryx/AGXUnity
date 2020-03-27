using UnityEngine;
using AGXUnity.Collide;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Debug rendering component which is added to all game objects
  /// containing Collide.Shape components. DebugRenderManager manages
  /// these objects.
  /// </summary>
  [AddComponentMenu( "" )]
  public class ShapeDebugRenderData : DebugRenderData
  {
    /// <summary>
    /// Find the debug rendering mesh filters for a given shape (if debug rendered).
    /// </summary>
    public static MeshFilter[] GetMeshFilters( Shape shape )
    {
      ShapeDebugRenderData debugRenderData = null;
      if ( shape == null || ( debugRenderData = shape.GetComponent<ShapeDebugRenderData>() ) == null )
        return new MeshFilter[] { };

      return debugRenderData.MeshFilters;
    }

    /// <summary>
    /// Sets scale to capsule debug rendering prefab, assuming three children:
    ///   1: (half) sphere upper
    ///   2: cylinder
    ///   3: (half) sphere lower
    /// </summary>
    /// <param name="node">Capsule prefab node with three children.</param>
    /// <param name="radius">Radius of the capsule.</param>
    /// <param name="height">Height of the capsule.</param>
    /// <param name="unscaleParentLossyScale">
    /// True to use parent lossy scale to unscale size and position for persistent size
    /// of this capsule.
    /// </param>
    public static void SetCapsuleSize( GameObject node, float radius, float height, bool unscaleParentLossyScale = true )
    {
      if ( node == null )
        return;

      if ( node.transform.childCount != 3 )
        throw new Exception( "Capsule debug rendering node doesn't contain three children." );

      var additionalScale = Vector3.one;
      if ( unscaleParentLossyScale && node.transform.parent != null ) {
        var ls = node.transform.parent.lossyScale;
        additionalScale = new Vector3( 1.0f / ls.x, 1.0f / ls.y, 1.0f / ls.z );
      }

      Transform sphereUpper = node.transform.GetChild( 0 );
      Transform cylinder = node.transform.GetChild( 1 );
      Transform sphereLower = node.transform.GetChild( 2 );

      cylinder.localScale = Vector3.Scale( new Vector3( 2.0f * radius, height, 2.0f * radius ), additionalScale );

      sphereUpper.localScale    = Vector3.Scale( 2.0f * radius * Vector3.one, additionalScale );
      sphereUpper.localPosition = Vector3.Scale( 0.5f * height * Vector3.up, additionalScale );

      sphereLower.localScale    = Vector3.Scale( 2.0f * radius * Vector3.one, additionalScale );
      sphereLower.localPosition = Vector3.Scale( 0.5f * height * Vector3.down, additionalScale );
    }

    /// <summary>
    /// Type name is shape type - prefabs in Resources folder has been
    /// named to fit these names.
    /// </summary>
    /// <returns></returns>
    public override string GetTypeName()
    {
      return GetShape().GetType().Name;
    }

    /// <returns>The Collide.Shape component.</returns>
    public Shape GetShape() { return GetComponent<Shape>(); }

    /// <summary>
    /// Lossy scale (of the shape) stored to know when to rescale the
    /// debug rendered mesh of Collide.Mesh objects.
    /// </summary>
    [SerializeField]
    private Vector3 m_storedLossyScale = Vector3.one;

    /// <summary>
    /// Parameter of adjustable mesh to keep track of when to regenerate
    /// </summary>
    [SerializeField]
    private float m_topRadius = 0f;
    /// <summary>
    /// Parameter of adjustable mesh to keep track of when to regenerate
    /// </summary>
    [SerializeField]
    private float m_bottomRadius = 0f;
    /// <summary>
    /// Parameter of adjustable mesh to keep track of when to regenerate
    /// </summary>
    [SerializeField]
    private float m_thickness = 0f;
    /// <summary>
    /// Parameter of adjustable mesh to keep track of when to regenerate
    /// </summary>
    [SerializeField]
    private float m_height = 0f;


    /// <summary>
    /// Creates debug rendering node if it doesn't already exist and
    /// synchronizes the rendered object transform to be the same as the shape.
    /// </summary>
    /// <param name="manager"></param>
    public override void Synchronize( DebugRenderManager manager )
    {
      try {
        Shape shape      = GetShape();
        bool nodeCreated = TryInitialize( shape, manager );

        if ( Node == null )
          return;

        // Node created - set properties and extra components.
        if ( nodeCreated ) {
          Node.hideFlags           = HideFlags.DontSave;
          Node.transform.hideFlags = HideFlags.DontSave | HideFlags.HideInInspector;

          Node.GetOrCreateComponent<OnSelectionProxy>().Component = shape;
          foreach ( Transform child in Node.transform )
            child.gameObject.GetOrCreateComponent<OnSelectionProxy>().Component = shape;
        }

        // Forcing the debug render node to be parent to the static DebugRenderManger.
        if ( Node.transform.parent != manager.Root.transform )
          manager.Root.AddChild( Node );

        Node.transform.position = shape.transform.position;
        Node.transform.rotation = shape.transform.rotation;

        SynchronizeScale( shape );
      }
      catch ( System.Exception ) {
      }
    }

    /// <summary>
    /// Synchronize the scale/size of the debug render object to match the shape size.
    /// Scaling is ignore if the node hasn't been created (i.e., this method doesn't
    /// create the render node).
    /// </summary>
    /// <param name="shape">Shape this component belongs to.</param>
    public void SynchronizeScale( Shape shape )
    {
      if ( Node == null )
        return;

      Node.transform.localScale = shape.GetScale();

      Cone cone = shape as Cone;
      HollowCone hollowCone = shape as HollowCone;
      HollowCylinder hollowCylinder = shape as HollowCylinder;

      if ( shape is Collide.Mesh ) {
        if ( m_storedLossyScale != transform.lossyScale ) {
          var mesh = shape as Collide.Mesh;
          for ( int i = 0; i < mesh.SourceObjects.Length; ++i ) {
            var sub = mesh.SourceObjects[ i ];
            RescaleRenderedMesh( mesh, sub, Node.transform.GetChild( i ).GetComponent<MeshFilter>() );
          }
          m_storedLossyScale = transform.lossyScale;
        }
      }
      else if (cone != null)
      {
        if (m_height != cone.Height || m_topRadius != cone.TopRadius || m_bottomRadius != cone.BottomRadius)
        {
          Node.GetComponent<MeshFilter>().sharedMesh = ShapeVisualCone.GenerateMesh(shape);
          m_height = cone.Height;
          m_topRadius = cone.TopRadius;
          m_bottomRadius = cone.BottomRadius;
        }
      }
      else if (hollowCone != null)
      {
        if (m_height != hollowCone.Height || m_topRadius != hollowCone.TopRadius || m_bottomRadius != hollowCone.BottomRadius || m_thickness != hollowCone.Thickness)
        {
          Node.GetComponent<MeshFilter>().sharedMesh = ShapeVisualHollowCone.GenerateMesh(shape);
          m_height = hollowCone.Height;
          m_topRadius = hollowCone.TopRadius;
          m_bottomRadius = hollowCone.BottomRadius;
          m_thickness = hollowCone.Thickness;
        }
      }
      else if (hollowCylinder != null)
      {
        if (m_height != hollowCylinder.Height || m_bottomRadius != hollowCylinder.Radius || m_thickness != hollowCylinder.Thickness)
        {
          Node.GetComponent<MeshFilter>().sharedMesh = ShapeVisualHollowCylinder.GenerateMesh(shape);
          m_height = hollowCylinder.Height;
          m_bottomRadius = hollowCylinder.Radius;
          m_thickness = hollowCylinder.Thickness;
        }
      }
      else if (shape is Collide.HollowCylinder)
      {
        Node.GetComponent<MeshFilter>().sharedMesh = ShapeVisualHollowCylinder.GenerateMesh(shape);
      }
      else if ( shape is Capsule ) {
        Capsule capsule = shape as Capsule;
        SetCapsuleSize( Node, capsule.Radius, capsule.Height );
      }
    }

    /// <summary>
    /// If no "Node" instance, this method tries to create one
    /// given the Collide.Shape component in this game object.
    /// </summary>
    /// <returns>True if the node was created - otherwise false.</returns>
    private bool TryInitialize( Shape shape, DebugRenderManager manager )
    {
      if ( Node != null )
        return false;

      Collide.Mesh mesh             = shape as Collide.Mesh;
      HeightField heightField       = shape as HeightField;
      Cone cone                     = shape as Cone;
      HollowCone hollowCone         = shape as HollowCone;
      HollowCylinder hollowCylinder = shape as HollowCylinder;
      if ( mesh != null )
        Node = InitializeMesh( mesh );
      else if ( heightField != null )
        Node = InitializeHeightField( heightField );
      else if (hollowCone != null)
      {
        Node = new GameObject(PrefabName);
        Node.AddComponent<MeshRenderer>().sharedMaterial = manager.ShapeRenderMaterial;
        Node.AddComponent<MeshFilter>().sharedMesh = ShapeVisualHollowCone.GenerateMesh(shape);
      }
      else if (hollowCylinder != null)
      {
        Node = new GameObject(PrefabName);
        Node.AddComponent<MeshRenderer>().sharedMaterial = manager.ShapeRenderMaterial;
        Node.AddComponent<MeshFilter>().sharedMesh = ShapeVisualHollowCylinder.GenerateMesh(shape);
      }
      else if (cone != null)
      {
        Node = new GameObject(PrefabName);
        Node.AddComponent<MeshRenderer>().sharedMaterial = manager.ShapeRenderMaterial;
        Node.AddComponent<MeshFilter>().sharedMesh = ShapeVisualCone.GenerateMesh(shape);
      }
      else
      {
        Node = PrefabLoader.Instantiate<GameObject>( PrefabName );
        Node.transform.localScale = GetShape().GetScale();
      }

      if ( Node != null ) {
        var renderers = Node.GetComponentsInChildren<Renderer>();
        foreach ( var renderer in renderers )
          renderer.sharedMaterial = manager.ShapeRenderMaterial;
      }

      return Node != null;
    }

    /// <summary>
    /// Initializes and returns a game object if the Collide.Shape type
    /// is of type mesh. Fails if the shape type is different from mesh.
    /// </summary>
    /// <returns>Game object with mesh renderer.</returns>
    private GameObject InitializeMesh( Collide.Mesh mesh )
    {
      return InitializeMeshGivenSourceObject( mesh );
    }

    /// <summary>
    /// Initializes debug render object given the source object of the
    /// Collide.Mesh component.
    /// </summary>
    private GameObject InitializeMeshGivenSourceObject( Collide.Mesh mesh )
    {
      if ( mesh.SourceObjects.Length == 0 )
        throw new Exception( "Mesh has no source." );

      GameObject meshData = new GameObject( "MeshData" );

      foreach ( var sub in mesh.SourceObjects ) {
        GameObject subMesh = new GameObject( "SubMeshData" );
        subMesh.transform.parent = meshData.transform;
        subMesh.transform.localPosition = Vector3.zero;
        subMesh.transform.localRotation = Quaternion.identity;

        subMesh.AddComponent<MeshRenderer>();
        MeshFilter filter = subMesh.AddComponent<MeshFilter>();

        RescaleRenderedMesh( mesh, sub, filter );
      }

      m_storedLossyScale = mesh.transform.lossyScale;

      return meshData;
    }

    /// <summary>
    /// Debug rendering of HeightField is currently not supported.
    /// </summary>
    private GameObject InitializeHeightField( HeightField hf )
    {
      return new GameObject( "HeightFieldData" );
    }

    private void RescaleRenderedMesh( Collide.Mesh mesh, UnityEngine.Mesh source, MeshFilter filter )
    {
      if ( source == null )
        throw new AGXUnity.Exception( "Source object is null during rescale." );

      Vector3[] vertices = null;
      if ( filter.sharedMesh == null ) {
        filter.sharedMesh = Instantiate( source );
        vertices = filter.sharedMesh.vertices;
      }
      else
        vertices = filter.sharedMesh.vertices;

      if ( vertices.Length != source.vertexCount )
        throw new AGXUnity.Exception( "Shape debug render mesh mismatch." );

      // Transforms each vertex from local to world given scales, then
      // transforms each vertex back to local again - unscaled.
      Matrix4x4 scaledToWorld  = mesh.transform.localToWorldMatrix;
      Vector3[] sourceVertices = source.vertices;
      for ( int i = 0; i < vertices.Length; ++i ) {
        var worldVertex = scaledToWorld * sourceVertices[ i ];
        vertices[ i ] = mesh.transform.InverseTransformDirection( worldVertex );
      }

      filter.sharedMesh.vertices = vertices;

      filter.sharedMesh.RecalculateBounds();
      filter.sharedMesh.RecalculateNormals();
    }
  }
}
