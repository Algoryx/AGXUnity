using System;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Base class for visualization of shapes.
  /// </summary>
  [AddComponentMenu( "" )]
  [ExecuteInEditMode]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisual : ScriptComponent
  {
    /// <summary>
    /// Create given shape of supported type (SupportsShapeVisual == true).
    /// </summary>
    /// <param name="shape">Shape to create visual for.</param>
    /// <returns>Game object with ShapeVisual component if successful, otherwise null.</returns>
    public static GameObject Create( Collide.Shape shape )
    {
      if ( !SupportsShapeVisual( shape ) )
        return null;

      return shape is Collide.Mesh ?
               CreateInstance( shape, ( shape as Collide.Mesh ).SourceObjects, DefaultMaterial, false ) :
               CreateInstance( shape );
    }

    /// <summary>
    /// Create render data for given shape. Render data is when you have
    /// your own representation of the mesh and material.
    /// 
    /// The component added will be ShapeVisualRenderData regardless of the
    /// type of the shape.
    /// 
    /// If meshes.Length == 1 the MeshFilter and MeshRenderer will be added
    /// to the returned game object. If meshes.Length > 1 the filters and
    /// renderer will be added as children.
    /// </summary>
    /// <param name="shape">Shape to create render data for.</param>
    /// <param name="meshes">Render meshes for the shape.</param>
    /// <param name="material">Material.</param>
    /// <returns>Game object with ShapeVisual component if successful, otherwise null.</returns>
    public static GameObject CreateRenderData( Collide.Shape shape, Mesh[] meshes, Material material )
    {
      return CreateInstance( shape, meshes, material, true );
    }

    /// <summary>
    /// Find ShapeVisual instance given shape.
    /// </summary>
    /// <param name="shape">Shape.</param>
    /// <returns>ShapeVisual instance if present, otherwise null.</returns>
    public static ShapeVisual Find( Collide.Shape shape )
    {
      return shape != null ?
               shape.GetComponentsInChildren<ShapeVisual>().FirstOrDefault( instance => instance.Shape == shape ) :
               null;
    }

    /// <summary>
    /// True if the given shape has a visual instance.
    /// </summary>
    /// <param name="shape">Shape.</param>
    /// <returns>True if the given shape has a visual instance.</returns>
    public static bool HasShapeVisual( Collide.Shape shape )
    {
      return Find( shape ) != null;
    }

    /// <summary>
    /// True if the given shape supports native creation of visual data, otherwise false.
    /// </summary>
    /// <param name="shape">Shape.</param>
    /// <returns>True if the given shape supports native creation of visual data.</returns>
    public static bool SupportsShapeVisual( Collide.Shape shape )
    {
      return shape != null &&
             (
               shape is Collide.Box ||
               shape is Collide.Sphere ||
               shape is Collide.Cylinder ||
               shape is Collide.HollowCylinder ||
               shape is Collide.Cone ||
               shape is Collide.HollowCone ||
               shape is Collide.Capsule ||
               shape is Collide.Plane ||
               shape is Collide.Mesh
             );
    }

    /// <summary>
    /// Creates default material that should be used by default when the visuals are created.
    /// </summary>
    /// <returns>New instance of the default material.</returns>
    public static Material CreateDefaultMaterial()
    {
      var material = new Material( Shader.Find( "Standard" ) );

      material.SetVector( "_Color", Color.Lerp( Color.white, Color.blue, 0.07f ) );
      material.SetFloat( "_Metallic", 0.3f );
      material.SetFloat( "_Glossiness", 0.8f );

      return material;
    }

    /// <summary>
    /// Path to material given Resources.Load.
    /// </summary>
    public static string DefaultMaterialPathResources { get { return @"Materials/ShapeVisualDefaultMaterial"; } }

    /// <summary>
    /// Default material used.
    /// </summary>
    public static Material DefaultMaterial { get { return PrefabLoader.Load<Material>( DefaultMaterialPathResources ); } }

    /// <summary>
    /// Assigns material to renderer in <paramref name="go"/>, including all sub-meshes/materials.
    /// </summary>
    /// <param name="go">Game object with renderer and mesh filter.</param>
    /// <param name="material">Material to assign.</param>
    public static void SetMaterial( GameObject go, Material material )
    {
      if ( go == null )
        return;

      SetMaterial( go.GetComponent<MeshFilter>(), go.GetComponent<MeshRenderer>(), material );
    }

    /// <summary>
    /// Assigns material to all meshes associated to <paramref name="filter"/>.
    /// </summary>
    /// <param name="filter">Mesh filter with mesh(es).</param>
    /// <param name="renderer">Mesh renderer with material(s).</param>
    /// <param name="material">Material to assign.</param>
    public static void SetMaterial( MeshFilter filter, MeshRenderer renderer, Material material )
    {
      if ( renderer == null )
        return;

      var numMaterials = filter == null || filter.sharedMesh == null ? 1 : filter.sharedMesh.subMeshCount;
      renderer.sharedMaterials = Enumerable.Repeat( material, numMaterials ).ToArray();
    }

    [SerializeField]
    private Collide.Shape m_shape = null;

    /// <summary>
    /// Shape this object is visualizing.
    /// </summary>
    public Collide.Shape Shape
    {
      get { return m_shape; }
      protected set { m_shape = value; }
    }

    /// <summary>
    /// Assign material to all shared meshes in this object.
    /// </summary>
    /// <param name="material">New material.</param>
    public void SetMaterial( Material material )
    {
      var renderers = GetComponentsInChildren<MeshRenderer>();
      foreach ( var renderer in renderers )
        SetMaterial( renderer.GetComponent<MeshFilter>(), renderer, material );
    }

    /// <summary>
    /// Replace <paramref name="oldMaterial"/> with new material <paramref name="newMaterial"/>.
    /// </summary>
    /// <param name="oldMaterial">Old material in this shape visual.</param>
    /// <param name="newMaterial">New material to replace <paramref name="oldMaterial"/>.</param>
    public void ReplaceMaterial( Material oldMaterial, Material newMaterial )
    {
      ReplaceMaterial( Array.IndexOf( GetMaterials(), oldMaterial ), newMaterial );
    }

    /// <summary>
    /// Replace material given index from GetMaterials() array.
    /// </summary>
    /// <param name="i">Index in material array.</param>
    /// <param name="newMaterial">New material to replace material with index <paramref name="i"/>.</param>
    public void ReplaceMaterial( int i, Material newMaterial )
    {
      if ( i < 0 ) {
        Debug.LogWarning( "Unable to replace material. Old material not found.", this );
        return;
      }

      int counter = 0;
      var renderers = GetComponentsInChildren<MeshRenderer>();
      foreach ( var renderer in renderers ) {
        for ( int materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; ++materialIndex ) {
          if ( counter == i ) {
            // We're receiving a copy of the array and have to assign the modified.
            var sharedMaterials = renderer.sharedMaterials;
            sharedMaterials[ materialIndex ] = newMaterial;
            renderer.sharedMaterials = sharedMaterials;
            return;
          }
          ++counter;
        }
      }

      Debug.LogWarning( "Unable to replace material with index: " + i + ", number of materials: " + GetMaterials().Length );
    }

    /// <summary>
    /// Finds all materials, including children.
    /// </summary>
    /// <returns>Array containing all materials.</returns>
    public Material[] GetMaterials()
    {
      return ( from renderer
               in GetComponentsInChildren<MeshRenderer>()
               from material
               in renderer.sharedMaterials
               select material ).ToArray();
    }

    /// <summary>
    /// Find material given mesh.
    /// </summary>
    /// <param name="source">Source mesh.</param>
    /// <returns>Material used for source mesh.</returns>
    public Material GetMaterial( Mesh source )
    {
      return ( from filter
               in GetComponentsInChildren<MeshFilter>()
               where filter.sharedMesh == source
               select filter.GetComponent<MeshRenderer>().sharedMaterial ).FirstOrDefault();
    }

    /// <summary>
    /// Callback from Shape when its size has been changed.
    /// </summary>
    public virtual void OnSizeUpdated()
    {
      transform.localScale = GetUnscaledScale();
    }

    /// <summary>
    /// Callback when this component has been added to a game object.
    /// </summary>
    protected virtual void OnConstruct()
    {
    }

    /// <summary>
    /// Execute-in-edit-mode is active - handles default scaling (trying to remove scale).
    /// </summary>
    protected virtual void Update()
    {
      if ( Shape != null && m_lastLossyScale != Shape.transform.lossyScale ) {
        OnSizeUpdated();
        m_lastLossyScale = Shape.transform.lossyScale;
      }
    }

    /// <summary>
    /// Shape scale divided with our parent lossy scale.
    /// </summary>
    /// <returns>shape.GetScale() / shape.parent.lossyScale</returns>
    protected Vector3 GetUnscaledScale()
    {
      if ( Shape == null )
        return Vector3.one;

      var lossyScale = Shape.transform.lossyScale;
      return Vector3.Scale( new Vector3( 1.0f / lossyScale.x, 1.0f / lossyScale.y, 1.0f / lossyScale.z ), Shape.GetScale() );
    }

    /// <summary>
    /// Creates game object and ShapeVisual component given shape and if this is
    /// pure render data or not.
    /// </summary>
    /// <param name="shape">Shape to create ShapeVisual for.</param>
    /// <returns>Game object with ShapeVisual component if successful, otherwise null.</returns>
    protected static GameObject CreateInstance( Collide.Shape shape )
    {
      if ( shape == null )
        return null;

      GameObject go = CreateGameObject( shape, false );
      if ( go == null )
        return null;

      var visual = AddVisualComponent( go, shape, false );
      if ( visual == null ) {
        Debug.LogWarning( "Unsupported shape type for visual: " + shape.GetType().FullName );
        return null;
      }

      CreateOnSelectionProxy( go, shape );

      visual.SetMaterial( DefaultMaterial );
      visual.OnSizeUpdated();

      return go;
    }

    /// <summary>
    /// Creates instance with one or more render meshes. Each mesh will
    /// be child to parent ShapeVisualRenderData object if number of
    /// meshes > 1. If meshes.Length == 1 the mesh renderer and filter
    /// will be added directly in the returned game object.
    /// </summary>
    /// <param name="shape">Shape to add render data for.</param>
    /// <param name="meshes">Array of meshes.</param>
    /// <param name="material">Material.</param>
    /// <param name="isRenderData">True if render data, i.e., decoupled from Collide.Mesh source objects.</param>
    /// <returns>Visual game object as child to shape game object if successful - otherwise null.</returns>
    protected static GameObject CreateInstance( Collide.Shape shape, Mesh[] meshes, Material material, bool isRenderData )
    {
      if ( shape == null || meshes.Length == 0 )
        return null;

      var parent = CreateGameObject( shape, isRenderData );
      if ( parent == null )
        return null;

      CreateOnSelectionProxy( parent, shape );

      var visual = AddVisualComponent( parent, shape, isRenderData );
      if ( visual == null )
        return null;

      for ( int i = 0; i < meshes.Length; ++i )
        AddChildMesh( shape, 
                      parent,
                      meshes[ i ],
                      parent.name + "_" + ( i + 1 ).ToString(),
                      material,
                      i > 0 );

      return parent;
    }

    /// <summary>
    /// Adds child game object to visual parent (the one with ShapeVisual component).
    /// </summary>
    /// <param name="shape">Parent shape this visual belongs to.</param>
    /// <param name="shapeVisualParent">Parent game object (the one with ShapeVisual component).</param>
    /// <param name="mesh">Visual mesh.</param>
    /// <param name="name">Name of the child.</param>
    /// <param name="material">Material.</param>
    /// <returns>Created child game object.</returns>
    protected static GameObject AddChildMesh( Collide.Shape shape,
                                              GameObject shapeVisualParent,
                                              Mesh mesh,
                                              string name,
                                              Material material,
                                              bool createNewGameObject )
    {
      GameObject child = shapeVisualParent;
      if ( createNewGameObject ) {
        child = new GameObject( "" );
        child.name = name;

        shapeVisualParent.AddChild( child );
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.hideFlags = HideFlags.NotEditable;

        child.AddComponent<MeshFilter>();
        child.AddComponent<MeshRenderer>();
      }

      var filter   = child.GetComponent<MeshFilter>();
      var renderer = child.GetComponent<MeshRenderer>();
      filter.sharedMesh = mesh;

      SetMaterial( filter, renderer, material );

      CreateOnSelectionProxy( child, shape );

      return child;
    }

    /// <summary>
    /// Create visual game object and adds it as child to shape game object.
    /// </summary>
    /// <param name="shape">Shape instance.</param>
    /// <param name="isRenderData">If true we wont try to load predefined mesh from resources.</param>
    /// <returns>Visual game object if successful - otherwise null.</returns>
    protected static GameObject CreateGameObject( Collide.Shape shape, bool isRenderData )
    {
      GameObject go = null;
      try {
        go = isRenderData || shape is Collide.Mesh || shape is Collide.HollowCylinder || shape is Collide.Cone || shape is Collide.HollowCone ?
               new GameObject( "" ) :
               PrefabLoader.Instantiate<GameObject>( @"Debug/" + shape.GetType().Name + "Renderer" );

        if ( go == null ) {
          Debug.LogWarning( "Unable to find shape visual resource: " + @"Debug/" + shape.GetType().Name + "Renderer", shape );
          return null;
        }

        go.name                = shape.name + "_Visual";
        go.transform.hideFlags = HideFlags.NotEditable;

        shape.gameObject.AddChild( go );
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        if ( go != null )
          GameObject.DestroyImmediate( go );
      }

      return go;
    }

    /// <summary>
    /// Adds OnSelectionProxy to <paramref name="visualParent"/> and all its children.
    /// </summary>
    /// <param name="visualParent">Visual parent game object.</param>
    /// <param name="shape">Shape reference.</param>
    private static void CreateOnSelectionProxy( GameObject visualParent, Collide.Shape shape )
    {
      visualParent.GetOrCreateComponent<OnSelectionProxy>().Component = shape;
      foreach ( Transform child in visualParent.transform )
        child.gameObject.GetOrCreateComponent<OnSelectionProxy>().Component = shape;
    }

    /// <summary>
    /// Adds shape visual type given shape type and <paramref name="isRenderData"/>.
    /// </summary>
    /// <param name="go">Game object to add ShapeVisual component to.</param>
    /// <param name="shape">Shape ShapeVisual is referring.</param>
    /// <param name="isRenderData">True if the component should be ShapeVisualRenderData regardless of shape type.</param>
    /// <returns></returns>
    private static ShapeVisual AddVisualComponent( GameObject go, Collide.Shape shape, bool isRenderData )
    {
      ShapeVisual instance = null;
      if ( isRenderData )
        instance = go.AddComponent<ShapeVisualRenderData>();
      else if ( shape is Collide.Box )
        instance = go.AddComponent<ShapeVisualBox>();
      else if ( shape is Collide.Sphere )
        instance = go.AddComponent<ShapeVisualSphere>();
      else if ( shape is Collide.Cylinder )
        instance = go.AddComponent<ShapeVisualCylinder>();
      else if ( shape is Collide.HollowCylinder )
        instance = go.AddComponent<ShapeVisualHollowCylinder>();
      else if (shape is Collide.Cone)
        instance = go.AddComponent<ShapeVisualCone>();
      else if (shape is Collide.HollowCone)
        instance = go.AddComponent<ShapeVisualHollowCone>();
      else if ( shape is Collide.Capsule )
        instance = go.AddComponent<ShapeVisualCapsule>();
      else if ( shape is Collide.Plane )
        instance = go.AddComponent<ShapeVisualPlane>();
      else if ( shape is Collide.Mesh )
        instance = go.AddComponent<ShapeVisualMesh>();

      if ( instance != null ) {
        instance.hideFlags = HideFlags.NotEditable;
        instance.Shape     = shape;

        instance.OnConstruct();
      }

      return instance;
    }

    private Vector3 m_lastLossyScale = Vector3.one;
  }
}
