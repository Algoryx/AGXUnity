using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Model or "Robot", the root element of an URDF file, containing
  /// all data read in Unity/C# friendly structures (instead of XML and strings).
  /// </summary>
  public class Model : Element
  {
    /// <summary>
    /// Read an URDF file into intermediate data structures.
    /// </summary>
    /// <param name="filename">URDF filename including path to it.</param>
    /// <returns>Read model, throws System.Xml.XmlException or AGXUnity.IO.URDF.UrdfIOException on errors.</returns>
    public static Model Read( string filename )
    {
      var fileInfo = new FileInfo( filename );
      if ( !fileInfo.Exists )
        throw new UrdfIOException( $"URDF file {fileInfo.FullName} doesn't exist." );
      if ( fileInfo.Extension.ToLower() != ".urdf" )
        throw new UrdfIOException( $"Unknown file extension {fileInfo.Extension}." );

      var document = XDocument.Load( fileInfo.OpenRead(), LoadOptions.SetLineInfo );
      if ( document == null )
        throw new UrdfIOException( $"Unable to parse URDF file {filename}." );
      if ( document.Root == null )
        throw new UrdfIOException( "Unable to parse URDF file - file seems empty." );
      if ( document.Root.Name != "robot" )
        throw new UrdfIOException( $"Expecting root attribute name 'robot', got '{document.Root.Name}'." );

      var version = Version.Parse( document.Declaration.Version );
      if ( version.Major != 1 && version.Minor != 0 )
        throw new UrdfIOException( $"Unsupported version {version.ToString()}, supported version is 1.0." );

      return new Model( document.Root );
    }

    /// <summary>
    /// Reads and instantiates a valid model to the scene. The <paramref name="resourceLoader"/>
    /// should load (e.g., Resources.Load) any dependent resource.
    /// </summary>
    /// <param name="filename">URDF filename including path to it.</param>
    /// <param name="resourceLoader">Callback when a resources is required.</param>
    /// <param name="onCreate">Optional callback when a game object or component is created.</param>
    /// <returns>Created instance, throws System.Xml.XmlException or AGXUnity.IO.URDF.UrdfIOException on errors.</returns>
    public static GameObject ReadAndInstantiate( string filename,
                                                 Func<string, GameObject> resourceLoader,
                                                 Action<UnityEngine.Object> onCreate = null )
    {
      return Read( filename ).Instantiate( resourceLoader, onCreate );
    }

    /// <summary>
    /// Creates a resource loader if none is provided. This resource loader replaces
    /// "package:/" with <paramref name="dataDirectory"/> and <paramref name="dataDirectory"/>
    /// must be relative to a resource folder, i.e., if you put the data in Something/Resources/foo
    /// the <paramref name="dataDirectory"/> should be "foo".
    /// 
    /// When Collada is a resource any rotation of the loaded game objects are removed.
    /// </summary>
    /// <remarks>
    ///   - If a Collada resource is missing, this loader checks if there's a .obj file with
    ///     the same name.
    ///   - If this method is used within the Editor, <paramref name="dataDirectory"/> should be relative
    ///     to the project directory (i.e, start with "Assets").
    /// </remarks>
    /// <param name="dataDirectory">Directory relative to a resource folder. Default: "" assuming
    ///                             the data to be structured as referenced in the URDF file inside
    ///                             a "Resources" folder.</param>
    /// <param name="resourceLoad">The actual call to Load. UnityEngine.Resources.Load by default (if null) and, e.g.,
    ///                            AssetDataBase.LoadAssetAtPath can be used when in the editor.</param>
    /// <returns>Resource loader function.</returns>
    public static Func<string, GameObject> CreateDefaultResourceLoader( string dataDirectory = "",
                                                                        Func<string, GameObject> resourceLoad = null )
    {
      if ( dataDirectory == null )
        dataDirectory = string.Empty;
      else
        dataDirectory.Replace( '\\', '/' );

      Func<string, GameObject> resourceLoader = resourceFilename =>
      {
        if ( resourceFilename.StartsWith( "package:/" ) )
          resourceFilename = dataDirectory + resourceFilename.Substring( "package:/".Length );
        if ( !File.Exists( resourceFilename ) && resourceFilename.EndsWith( ".dae" ) )
          resourceFilename = resourceFilename.Substring( 0, resourceFilename.Length - 3 ) + "obj";
        var resource = resourceLoad != null ?
                         resourceLoad( resourceFilename ) :
                         Resources.Load<GameObject>( resourceFilename );
        // Unity adds a -90 rotation about x for unknown reasons - reverting that.
        if ( resource != null && resourceFilename.EndsWith( ".dae" ) ) {
          var transforms = resource.GetComponentsInChildren<Transform>();
          foreach ( var transform in transforms )
            transform.rotation = Quaternion.identity;
        }
        return resource;
      };
      return resourceLoader;
    }

    /// <summary>
    /// True if this model requires additional resources when instantiated.
    /// </summary>
    public bool RequiresResourceLoader { get; private set; } = false;

    /// <summary>
    /// Enumerate visual materials read from a URDF file.
    /// </summary>
    public IEnumerable<Material> Materials { get { return m_materials; } }

    /// <summary>
    /// Enumerate links read from a URDF file.
    /// </summary>
    public IEnumerable<Link> Links { get { return m_links; } }

    /// <summary>
    /// Enumerate joints read from a URDF file.
    /// </summary>
    public IEnumerable<UJoint> Joints { get { return m_joints; } }

    /// <summary>
    /// Find parsed material given name.
    /// </summary>
    /// <param name="name">Name of the material.</param>
    /// <returns>Material with the given name - null if not found.</returns>
    public Material GetMaterial( string name )
    {
      if ( m_materialTable.TryGetValue( name, out var materialIndex ) )
        return m_materials[ materialIndex ];
      return null;
    }
    
    /// <summary>
    /// Find UnityEngine.Material given name.
    /// </summary>
    /// <param name="name">Name of the material.</param>
    /// <returns>UnityEngine.Material with the given name - null if not found.</returns>
    public UnityEngine.Material GetRenderMaterial( string name )
    {
      if ( m_renderMaterialsTable.TryGetValue( name, out var renderMaterial ) )
        return renderMaterial;
      return null;
    }

    /// <summary>
    /// Find parsed link given name.
    /// </summary>
    /// <param name="name">Name of the link.</param>
    /// <returns>Link with the given name - null if not found.</returns>
    public Link GetLink( string name )
    {
      if ( m_linkTable.TryGetValue( name, out var linkIndex ) )
        return m_links[ linkIndex ];
      return null;
    }

    /// <summary>
    /// Find parsed joint given name.
    /// </summary>
    /// <param name="name">Name of the joint.</param>
    /// <returns>Joint with the given name - null if not found.</returns>
    public UJoint GetJoint( string name )
    {
      if ( m_jointTable.TryGetValue( name, out var jointIndex ) )
        return m_joints[ jointIndex ];
      return null;
    }

    /// <summary>
    /// Instantiate this model in the current scene.
    /// </summary>
    /// <param name="resourceLoader">Resource loader callback - default is used if null.
    ///                              <see cref="CreateDefaultResourceLoader(string, Func{string, GameObject})"/></param>
    /// <param name="onObjectCreate">Optional callback when a game object or component is created.</param>
    /// <returns>Created instance, throws System.Xml.XmlException or AGXUnity.IO.URDF.UrdfIOException on errors.</returns>
    public GameObject Instantiate( Func<string, GameObject> resourceLoader,
                                   Action<UnityEngine.Object> onObjectCreate = null )
    {
      m_resourceLoader = resourceLoader ?? CreateDefaultResourceLoader();
      m_onObjectCreate = onObjectCreate;

      GameObject robot = null;
      try {
        robot = GetOrCreateGameObject( Factory.CreateName( Name ) );
        GetOrCreateComponent<ArticulatedRoot>( robot );

        var linkInstanceTable = new Dictionary<string, GameObject>();
        foreach ( var link in Links ) {
          var linkGo = GetOrCreateGameObject( link.Name );
          linkGo.transform.parent = robot.transform;
          linkInstanceTable.Add( link.Name, linkGo );

          if ( link.IsWorld )
            continue;

          var rb = CreateRigidBody( linkGo, link );
          foreach ( var collision in link.Collisions )
            AddCollision( rb, collision );

          foreach ( var visual in link.Visuals )
            AddVisual( rb, visual );
        }

        foreach ( var joint in Joints )
          AddJoint( joint, linkInstanceTable );
      }
      catch ( System.Exception ) {
        if ( robot != null )
          GameObject.DestroyImmediate( robot );
        throw;
      }
      finally {
        m_resourceLoader = null;
        m_resourceCache  = null;
        m_onObjectCreate = null;
      }

      return robot;
    }

    /// <summary>
    /// Default constructor, call Read with root/robot element of the XML document.
    /// </summary>
    public Model()
    {
    }

    /// <summary>
    /// Construct given root/robot element in a XML document.
    /// </summary>
    /// <param name="root"></param>
    public Model( XElement root )
    {
      Read( root, false );
    }

    public override void Read( XElement root, bool optional )
    {
      // Reading mandatory 'name'.
      base.Read( root, false );

      // Reading optional materials.
      foreach ( var materialElement in root.Descendants( "material" ) ) {
        // This is some type of physics material.
        if ( materialElement.Parent.Name == "gazebo" )
          continue;

        var material = new Material( materialElement );
        // Ignoring material references for now.
        if ( material.IsReference )
          continue;

        if ( m_materialTable.ContainsKey( material.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( materialElement )}: Non-unique material name '{material.Name}'." );

        m_materialTable.Add( material.Name, m_materials.Count );
        m_materials.Add( material );

        // TODO URDF: Handle texture.
        var renderMaterial = new UnityEngine.Material( Shader.Find( "Standard" ) );
        renderMaterial.color = material.Color;
        m_renderMaterialsTable.Add( material.Name, renderMaterial );
      }

      foreach ( var linkElement in root.Elements( "link" ) ) {
        var link = new Link( linkElement );

        if ( m_linkTable.ContainsKey( link.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( linkElement )}: Non-unique link name '{link.Name}'." );

        var missingMaterials = link.Visuals.Where( visual => !string.IsNullOrEmpty( visual.Material ) &&
                                                    GetMaterial( visual.Material ) == null ).Select( visual => visual.Material ).ToArray();
        if ( missingMaterials.Length > 0 )
          throw new UrdfIOException( $"{Utils.GetLineInfo( linkElement )}: Visual element(s) in link '{link.Name}' is referring " +
                                     $"to material(s) '" + string.Join( "' '", missingMaterials ) + "' that doesn't exist." );

        m_linkTable.Add( link.Name, m_links.Count );
        m_links.Add( link );

        RequiresResourceLoader = RequiresResourceLoader || FindRequiresResourceLoader( link );
      }

      foreach ( var jointElement in root.Elements( "joint" ) ) {
        var joint = new UJoint( jointElement );

        if ( m_jointTable.ContainsKey( joint.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Non-unique link name '{joint.Name}'." );
        if ( GetLink( joint.Parent ) == null )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Joint parent link with name {joint.Parent} doesn't exist." );
        if ( GetLink( joint.Child ) == null )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Joint child link with name {joint.Child} doesn't exist." );

        m_jointTable.Add( joint.Name, m_joints.Count );
        m_joints.Add( joint );
      }
    }

    private bool FindRequiresResourceLoader( Link link )
    {
      if ( link == null || link.IsWorld )
        return false;
      return link.Collisions.Any( collision => collision.Geometry.Type == Geometry.GeometryType.Mesh ) ||
             link.Visuals.Any( visual => visual.Geometry.Type == Geometry.GeometryType.Mesh );
    }

    private GameObject GetOrCreateGameObject( string name )
    {
      var go = new GameObject( name );
      m_onObjectCreate?.Invoke( go );
      return go;
    }

    private T GetOrCreateComponent<T>( GameObject go )
      where T : MonoBehaviour
    {
      var component = go.GetComponent<T>();
      if ( component == null ) {
        component = go.AddComponent<T>();
        m_onObjectCreate?.Invoke( component );
      }
      return component;
    }

    private void SetTransform( Transform transform, Pose pose )
    {
      if ( pose == null )
        return;

      transform.position = pose.Xyz.ToLeftHanded();
      transform.rotation = pose.Rpy.RadEulerToLeftHanded();
    }

    private RigidBody CreateRigidBody( GameObject gameObject, Link link )
    {
      var rb = GetOrCreateComponent<RigidBody>( gameObject );
      if ( link.Inertial == null || link.IsStatic ) {
        rb.MotionControl = agx.RigidBody.MotionControl.STATIC;
        return rb;
      }

      var native = new agx.RigidBody();
      native.getMassProperties().setMass( link.Inertial.Mass, false );
      // Inertia tensor is given in the inertia frame. The rotation of the
      // CM frame can't be applied to the CM frame so we transform the inertia
      // CM frame and rotate the game object.
      var rotationMatrix = link.Inertial.Rpy.RadEulerToRotationMatrix();
      var inertia3x3 = (agx.Matrix3x3)link.Inertial.Inertia;
      // NOTE: Could be that this should be the other way around.
      inertia3x3 = rotationMatrix.transpose().Multiply( inertia3x3 ).Multiply( rotationMatrix );
      native.getMassProperties().setInertiaTensor( new agx.SPDMatrix3x3( inertia3x3 ) );
      native.getCmFrame().setLocalTranslate( link.Inertial.Xyz.ToVec3() );

      // TODO URDF: Off-diagonal inertia.
      rb.RestoreLocalDataFrom( native );

      return rb;
    }

    private GameObject AddCollision( RigidBody rb, Collision collision )
    {
      var shapeGoName = string.IsNullOrEmpty( collision.Name ) ?
                          CreateName( rb.transform, $"_{collision.Geometry.Type.ToString()}" ) :
                          collision.Name;
      var shapeGo = GetOrCreateGameObject( shapeGoName );
      try {
        if ( collision.Geometry.Type == Geometry.GeometryType.Box ) {
          var box = GetOrCreateComponent<Collide.Box>( shapeGo );
          box.HalfExtents = 0.5f * collision.Geometry.FullExtents;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Cylinder ) {
          // <cylinder> in URDF have their cylinder axis along z but in
          // Unity/AGX Dynamics the cylinder axis is along y. We're adding
          // an additional child which transform the axis to be z.
          var cylinderY2Z                = GetOrCreateGameObject( collision.Name + "_extra" );
          cylinderY2Z.transform.parent   = shapeGo.transform;
          cylinderY2Z.transform.rotation = Quaternion.FromToRotation( Vector3.up, Vector3.forward );

          var cylinder                   = GetOrCreateComponent<Collide.Cylinder>( cylinderY2Z );
          cylinder.Radius                = collision.Geometry.Radius;
          cylinder.Height                = collision.Geometry.Length;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Sphere ) {
          var sphere = GetOrCreateComponent<Collide.Sphere>( shapeGo );
          sphere.Radius = collision.Geometry.Radius;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Mesh ) {
          var meshResource = GetResource( collision.Geometry.Filename );
          if ( meshResource == null )
            throw new UrdfIOException( $"Mesh resource '{collision.Geometry.Filename}' is null." );
          shapeGo.transform.localScale = collision.Geometry.Scale;
          var sourceMeshes = ( from filter in meshResource.GetComponentsInChildren<MeshFilter>() select filter.sharedMesh ).ToArray();
          if ( sourceMeshes.Length == 0 )
            throw new UrdfIOException( $"Mesh resource '{collision.Geometry.Filename}' doesn't contain any meshes." );
          var mesh = GetOrCreateComponent<Collide.Mesh>( shapeGo );
          foreach ( var sourceMesh in sourceMeshes )
            mesh.AddSourceObject( sourceMesh );
        }
      }
      catch ( System.Exception ) {
        GameObject.DestroyImmediate( shapeGo );
        throw;
      }

      shapeGo.transform.parent = rb.transform;
      SetTransform( shapeGo.transform, collision );

      return shapeGo;
    }

    private GameObject AddVisual( RigidBody rb, Visual visual )
    {
      GameObject instance = null;
      if ( visual.Geometry.Type == Geometry.GeometryType.Mesh ) {
        var meshResource = GetResource( visual.Geometry.Filename );
        if ( meshResource == null )
          throw new UrdfIOException( $"Mesh resource '{visual.Geometry.Filename}' is null." );
        instance = GameObject.Instantiate<GameObject>( meshResource );
      }
      else {
        instance = InstantiateAndSizePrimitiveRenderer( visual );

        var renderers = ( from renderer in instance.GetComponentsInChildren<MeshRenderer>() select renderer ).ToArray();
        var renderMaterial = GetRenderMaterial( visual.Material ) ??
                             Rendering.ShapeVisual.DefaultMaterial;
        foreach ( var renderer in renderers )
          renderer.sharedMaterial = renderMaterial;
      }

      if ( string.IsNullOrEmpty( visual.Name ) )
        instance.name = CreateName( rb.transform, $"_Visual{visual.Geometry.Type.ToString()}" );
      else
        instance.name = visual.Name;

      var transforms = instance.GetComponentsInChildren<Transform>();
      foreach ( var transform in transforms )
        transform.gameObject.AddComponent<OnSelectionProxy>().Component = rb;

      var gameObjectToTransform = instance;
      // Additional rotation for <cylinder> when URDF have cylinder axis
      // along z and Unity/AGX along y.
      if ( visual.Geometry.Type == Geometry.GeometryType.Cylinder ) {
        gameObjectToTransform       = GetOrCreateGameObject( instance.name + "_extra" );
        instance.transform.parent   = gameObjectToTransform.transform;
        instance.transform.rotation = Quaternion.FromToRotation( Vector3.up, Vector3.forward );
      }

      gameObjectToTransform.transform.parent = rb.transform;
      SetTransform( gameObjectToTransform.transform, visual );

      return gameObjectToTransform;
    }

    private GameObject AddJoint( UJoint joint, Dictionary<string, GameObject> linkInstanceTable )
    {
      Func<string, GameObject> getGameObject = gameObjectName =>
      {
        if ( linkInstanceTable.TryGetValue( gameObjectName, out var linkInstance ) )
          return linkInstance;
        return null;
      };

      var parent = getGameObject( joint.Parent );
      var child  = getGameObject( joint.Child );
      if ( parent == null )
        throw new UrdfIOException( $"Unable to find parent link '{joint.Parent}' in joint '{joint.Name}'." );
      if ( child == null )
        throw new UrdfIOException( $"Unable to find child link '{joint.Child}' in joint '{joint.Name}'." );
      var childTransform = parent.transform.localToWorldMatrix * Matrix4x4.TRS( joint.Xyz.ToLeftHanded(),
                                                                                joint.Rpy.RadEulerToLeftHanded(),
                                                                                Vector3.one );
      child.transform.parent   = parent.transform;
      child.transform.position = childTransform.GetTranslate();
      child.transform.rotation = childTransform.GetRotation();

      GameObject constraintGameObject = null;
      if ( !joint.IsFloating &&
           child.GetComponent<RigidBody>() != null &&
           parent.GetComponent<RigidBody>() != null ) {
        constraintGameObject = Factory.Create( joint.Type,
                                               Vector3.zero,
                                               joint.Axis,
                                               child.GetComponent<RigidBody>(),
                                               parent.GetComponent<RigidBody>() );
        constraintGameObject.name             = joint.Name;
        constraintGameObject.transform.parent = parent.transform;
        var constraint                        = constraintGameObject.GetComponent<Constraint>();
        constraint.CollisionsState            = Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;
        if ( joint.Limit.Enabled ) {
          if ( joint.Limit.RangeEnabled ) {
            var rangeController = constraint.GetController<RangeController>();
            rangeController.Enable = true;
            rangeController.Range  = new RangeReal( joint.Limit.Lower, joint.Limit.Upper );
          }
          // TODO URDF: Velocity and Effort. Velocity is maximum speed and Effort
          //            is the force range of the motor.
          if ( joint.Limit.Effort > 0.0f ) {
            var targetSpeedController        = constraint.GetController<TargetSpeedController>();
            targetSpeedController.Enable     = true;
            targetSpeedController.ForceRange = new RangeReal( joint.Limit.Effort );
          }
        }
      }

      return constraintGameObject;
    }

    private GameObject InstantiateAndSizePrimitiveRenderer( Visual visual )
    {
      var rendererPath = $"Debug/{visual.Geometry.Type.ToString()}Renderer";
      var instance = PrefabLoader.Instantiate<GameObject>( rendererPath );
      if ( instance == null )
        throw new UrdfIOException( $"Unable to instantiate primitive renderer for {visual.Geometry.Type.ToString()} @ Resources/{rendererPath}." );

      instance.transform.localScale = visual.Geometry.Type == Geometry.GeometryType.Box ?
                                        visual.Geometry.FullExtents :
                                      visual.Geometry.Type == Geometry.GeometryType.Cylinder ?
                                        new Vector3( 2.0f * visual.Geometry.Radius, 0.5f * visual.Geometry.Length, 2.0f * visual.Geometry.Radius ) :
                                      visual.Geometry.Type == Geometry.GeometryType.Sphere ?
                                        2.0f * visual.Geometry.Radius * Vector3.one :
                                        Vector3.one;
      return instance;
    }

    private GameObject GetResource( string filename )
    {
      if ( m_resourceCache == null )
        m_resourceCache = new Dictionary<string, GameObject>();
      else if ( m_resourceCache.TryGetValue( filename, out var cachedResource ) )
        return cachedResource;

      var resource = m_resourceLoader?.Invoke( filename );
      if ( resource != null )
        m_resourceCache.Add( filename, resource );
      return resource;
    }

    private string CreateName( Transform parent, string postFix )
    {
      var name      = parent.name + postFix;
      var finalName = name;
      var counter   = 0;
      while ( parent.Find( finalName ) != null )
        finalName = $"{name} ({++counter})";

      return finalName;
    }

    private Dictionary<string, int> m_materialTable = new Dictionary<string, int>();
    private List<Material> m_materials = new List<Material>();
    private Dictionary<string, int> m_linkTable = new Dictionary<string, int>();
    private List<Link> m_links = new List<Link>();
    private Dictionary<string, int> m_jointTable = new Dictionary<string, int>();
    private List<UJoint> m_joints = new List<UJoint>();
    private Func<string, GameObject> m_resourceLoader = null;
    private Action<UnityEngine.Object> m_onObjectCreate = null;
    private Dictionary<string, GameObject> m_resourceCache = null;
    private Dictionary<string, UnityEngine.Material> m_renderMaterialsTable = new Dictionary<string, UnityEngine.Material>();
  }
}
