using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Model or "Robot", the root element of an URDF file, containing
  /// all data read in Unity/C# friendly structures (instead of XML and strings).
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Model : Element
  {
    /// <summary>
    /// Resource types the resource loader may request.
    /// </summary>
    public enum ResourceType
    {
      /// <summary>
      /// GameObject with collision mesh(es).
      /// </summary>
      CollisionMesh,
      /// <summary>
      /// GameObject to instantiate as visual.
      /// </summary>
      VisualMesh,
      /// <summary>
      /// Material texture.
      /// </summary>
      Texture,
      /// <summary>
      /// All resource requests has been made. Use this type to
      /// unload/destroy previously requested resources.
      /// </summary>
      FinalizedLoad
    }

    /// <summary>
    /// Read an URDF file into intermediate data structures.
    /// </summary>
    /// <param name="filename">URDF filename including path to it.</param>
    /// <returns>Read model, throws System.Xml.XmlException or AGXUnity.UrdfIOException on errors.</returns>
    public static Model Read( string filename )
    {
      var fileInfo = new FileInfo( filename );
      if ( !fileInfo.Exists )
        throw new UrdfIOException( $"URDF file {fileInfo.FullName} doesn't exist." );
      if ( fileInfo.Extension.ToLowerInvariant() != ".urdf" )
        throw new UrdfIOException( $"Unknown file extension {fileInfo.Extension}." );

      using ( var stream = fileInfo.OpenRead() ) {
        var document = XDocument.Load( fileInfo.OpenRead(), LoadOptions.SetLineInfo );
        if ( document == null )
          throw new UrdfIOException( $"Unable to parse URDF file {filename}." );
        if ( document.Root == null )
          throw new UrdfIOException( "Unable to parse URDF file - file seems empty." );
        if ( document.Root.Name != "robot" )
          throw new UrdfIOException( $"Expecting root attribute name 'robot', got '{document.Root.Name}'." );

        // How should we handle documents without: <?xml version="1.0" ?>.
        if ( document.Declaration != null ) {
          var version = Version.Parse( document.Declaration.Version );
          if ( version.Major != 1 && version.Minor != 0 )
            throw new UrdfIOException( $"Unsupported version {version.ToString()}, supported version is 1.0." );
        }
        return Instantiate<Model>( document.Root );
      }
    }

    /// <summary>
    /// Reads and instantiates a valid model to the scene. The <paramref name="resourceLoader"/>
    /// should load (e.g., Resources.Load) any dependent resource.
    /// </summary>
    /// <param name="filename">URDF filename including path to it.</param>
    /// <param name="resourceLoader">Callback when a resources is required.</param>
    /// <param name="onCreate">Optional callback when a game object or component is created.</param>
    /// <returns>Created instance, throws System.Xml.XmlException or AGXUnity.UrdfIOException on errors.</returns>
    public static GameObject ReadAndInstantiate( string filename,
                                                 Func<string, ResourceType, Object> resourceLoader,
                                                 Action<Object> onCreate = null )
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
    public static Func<string, ResourceType, Object> CreateDefaultResourceLoader( string dataDirectory = "",
                                                                                  Func<string, ResourceType, Object> resourceLoad = null )
    {
      var isPlayerResource = resourceLoad == null;
      if ( !string.IsNullOrEmpty( dataDirectory ) ) {
        dataDirectory.Replace( '\\', '/' );
        if ( !dataDirectory.EndsWith( "/" ) )
          dataDirectory += '/';
      }

      // The Unity URDF importer has a custom Collada importer that
      // does things to the transforms. If this importer is installed
      // we're reverting the modifications made.
      var hasUnityURDFImporter = Options.UnityURDFImporterInstalled;
      var loadedInstances = new List<GameObject>();
      Func<string, ResourceType, Object> resourceLoader = ( resourceFilename, type ) =>
      {
        if ( type == ResourceType.FinalizedLoad ) {
          loadedInstances.ForEach( instance => Object.DestroyImmediate( instance ) );
          loadedInstances = null;
          return null;
        }

        if ( resourceFilename.StartsWith( "package:/" ) )
          resourceFilename = dataDirectory + resourceFilename.Substring( "package://".Length );
        else if ( !string.IsNullOrEmpty( dataDirectory ) )
          resourceFilename = dataDirectory + resourceFilename;

        // Makes resourceFilename relative to the application root directory,
        // expands any ".." in the path and replaces any \ with /.
        resourceFilename = resourceFilename.PrettyPath();

        var hasExtension = Path.HasExtension( resourceFilename );
        var isStlFile    = hasExtension && Path.GetExtension( resourceFilename ).ToLowerInvariant() == ".stl";
        var isCollada    = hasExtension && !isStlFile && Path.GetExtension( resourceFilename ).ToLowerInvariant() == ".dae";

        // STL file we instantiate it and delete them at FinalizeLoad.
        if ( isStlFile ) {
          var stlInstances = StlFileImporter.Instantiate( resourceFilename );
          loadedInstances.AddRange( stlInstances );
          return stlInstances.FirstOrDefault();
        }
        // Remove file extension when using Resources.Load.
        else if ( isPlayerResource && hasExtension )
          resourceFilename = resourceFilename.Substring( 0, resourceFilename.LastIndexOf( '.' ) );

        // Search for .obj file instead of Collada if we're not loading from Resources.
        if ( !isPlayerResource &&
             !File.Exists( resourceFilename ) &&
             isCollada )
          resourceFilename = resourceFilename.Substring( 0, resourceFilename.Length - 3 ) + "obj";

        var resource = resourceLoad != null ?
                         resourceLoad( resourceFilename, type ) :
                         Resources.Load<Object>( resourceFilename );

        if ( !isPlayerResource && isCollada && resource != null ) {
          var colladaInfo = Utils.ParseColladaInfo( resourceFilename );
          var resourceGo = resource as GameObject;
          // Model implementation assumes Z-up, anything other than that
          // has to be transformed. Note that we have to "undo" the transforms
          // made by the Unity URDF importer Collada processor, if installed.
          if ( resourceGo != null && ( !colladaInfo.IsDefault || hasUnityURDFImporter ) ) {
            var colladaInstance = Object.Instantiate<GameObject>( resourceGo );
            var rotationPatch = hasUnityURDFImporter ?
                                  GetInverseUnityColladaImporterRotation( colladaInfo.UpAxis ) :
                                  Quaternion.identity;
            if ( colladaInfo.UpAxis == Utils.ColladaInfo.Axis.Y )
              colladaInstance.transform.rotation = Quaternion.Euler( -90, 0, 0 ) * rotationPatch * colladaInstance.transform.rotation;
            else if ( colladaInfo.UpAxis == Utils.ColladaInfo.Axis.X )
              colladaInstance.transform.rotation = Quaternion.Euler( -90, 0, 90 ) * rotationPatch * colladaInstance.transform.rotation;
            else
              colladaInstance.transform.rotation = rotationPatch * colladaInstance.transform.rotation;

            loadedInstances.Add( colladaInstance );
            resource = colladaInstance;
          }
        }

        return resource;
      };
      return resourceLoader;
    }

    private static Quaternion GetInverseUnityColladaImporterRotation( Utils.ColladaInfo.Axis axis )
    {
      return Quaternion.Inverse( Quaternion.Euler( axis == Utils.ColladaInfo.Axis.X ?
                                                     new Vector3( -90, 90, 90 ) :
                                                   axis == Utils.ColladaInfo.Axis.Y ?
                                                     new Vector3( -90, 90, 0 ) :
                                                     new Vector3( 0, 90, 0 ) ) );
    }

    /// <summary>
    /// Enumerate visual materials read from a URDF file.
    /// </summary>
    public Material[] Materials
    {
      get
      {
        if ( m_materials == null )
          m_materials = new Material[] { };
        return m_materials;
      }
    }

    /// <summary>
    /// Enumerate links read from a URDF file.
    /// </summary>
    public Link[] Links
    {
      get
      {
        if ( m_links == null )
          m_links = new Link[] { };
        return m_links;
      }
    }

    /// <summary>
    /// Enumerate joints read from a URDF file.
    /// </summary>
    public UJoint[] Joints
    {
      get
      {
        if ( m_joints == null )
          m_joints = new UJoint[] { };
        return m_joints;
      }
    }

    /// <summary>
    /// True if this model requires additional resources when instantiated.
    /// </summary>
    public bool RequiresResourceLoader { get { return m_requiresResourceLoader; } private set { m_requiresResourceLoader = value; } }

    /// <summary>
    /// Iterates required, unmodified, resources read from the model. E.g.,
    /// "package://folder/meshes/visual/link.stl".
    /// </summary>
    public IEnumerable<string> RequiredResources
    {
      get
      {
        if ( !RequiresResourceLoader )
          yield break;

        foreach ( var link in Links ) {
          foreach ( var collision in link.Collisions )
            if ( collision.Geometry.Type == Geometry.GeometryType.Mesh )
              yield return collision.Geometry.Filename;
          foreach ( var visual in link.Visuals ) {
            if ( visual.Geometry.Type == Geometry.GeometryType.Mesh )
              yield return visual.Geometry.Filename;
            if ( visual.Material != null && !string.IsNullOrEmpty( visual.Material.Texture ) )
              yield return visual.Material.Texture;
          }
        }
      }
    }

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
    ///                              <see cref="CreateDefaultResourceLoader(string, Func{string, Object})"/></param>
    /// <param name="onObjectCreate">Optional callback when a game object or component is created.</param>
    /// <returns>Created instance, throws System.Xml.XmlException or AGXUnity.UrdfIOException on errors.</returns>
    public GameObject Instantiate( Func<string, ResourceType, Object> resourceLoader,
                                   Action<Object> onObjectCreate = null )
    {
      m_resourceLoader = resourceLoader ?? CreateDefaultResourceLoader();
      m_onObjectCreate = onObjectCreate;

      GameObject robot = null;
      try {
        // Generate render materials that may be referenced from within the model.
        if ( m_renderMaterials.Count != m_materials.Length ) {
          m_renderMaterials.Clear();
          m_renderMaterialsTable.Clear();
          GenerateGloballyDeclaredRenderMaterials();
        }
        robot = GetOrCreateGameObject( Factory.CreateName( Name ) );
        GetOrCreateComponent<ArticulatedRoot>( robot );
        GetOrCreateComponent<ElementComponent>( robot ).SetElement( this );

        var linkInstanceTable = new Dictionary<string, GameObject>();
        foreach ( var link in Links ) {
          var linkGo = GetOrCreateGameObject( link.Name );
          linkGo.transform.parent = robot.transform;
          linkInstanceTable.Add( link.Name, linkGo );

          var rb = CreateRigidBody( linkGo, link );
          foreach ( var collision in link.Collisions )
            OnElementGameObject( AddCollision( rb, collision ), collision );

          foreach ( var visual in link.Visuals )
            OnElementGameObject( AddVisual( rb, visual ), visual );

          OnElementGameObject( linkGo, link );
        }

        var jointMimics        = new List<UJoint>();
        var jointInstanceTable = new Dictionary<string, GameObject>();
        foreach ( var joint in Joints ) {
          jointInstanceTable.Add( joint.Name,
                                  OnElementGameObject( AddJoint( joint,
                                                                 linkInstanceTable ),
                                                       joint ) );
          if ( joint.Mimic.Enabled )
            jointMimics.Add( joint );
        }

        foreach ( var jointMimic in jointMimics ) {
          var beginActuatorConstraint = jointInstanceTable[ jointMimic.Mimic.Joint ].GetComponent<Constraint>();
          var endActuatorConstraint   = jointInstanceTable[ jointMimic.Name ].GetComponent<Constraint>();
          if ( beginActuatorConstraint == null || endActuatorConstraint == null ) {
            Debug.LogWarning( $"{Utils.GetLineInfo( jointMimic.LineNumber )}: Unable to mimic joint - '{jointMimic.Mimic.Joint}' and/or " +
                              $"'{jointMimic.Name}' isn't actual constraint instances." );
            continue;
          }
          if ( jointMimic.Mimic.Offset != 0.0f )
            Debug.LogWarning( $"{Utils.GetLineInfo( jointMimic.LineNumber )}: '{jointMimic.Name}' mimic offset attribute != 0 ignored." );

          var gearConstraint                     = beginActuatorConstraint.gameObject.AddComponent<GearConstraint>();
          gearConstraint.BeginActuatorConstraint = beginActuatorConstraint;
          gearConstraint.EndActuatorConstraint   = endActuatorConstraint;

          // Gear ratio URDF specification to AGX Dynamics definition.
          gearConstraint.GearRatio = 1.0f / jointMimic.Mimic.Multiplier;
        }
      }
      catch ( System.Exception ) {
        if ( robot != null )
          GameObject.DestroyImmediate( robot );
        throw;
      }
      finally {
        m_resourceLoader?.Invoke( string.Empty, ResourceType.FinalizedLoad );
        m_resourceLoader = null;
        m_resourceCache = null;
        m_onObjectCreate = null;
      }

      return robot;
    }

    /// <summary>
    /// Read data given root/robot element in a XML document.
    /// </summary>
    /// <param name="root"></param>
    public override void Read( XElement root, bool optional )
    {
      // Reading mandatory 'name'.
      base.Read( root, false );

      // Reading material declarations - materials that can be referenced with name.
      var materials = new List<Material>();
      foreach ( var materialElement in root.Elements( "material" ) ) {
        var material = Instantiate<Material>( materialElement );

        // Material reference under the robot?
        // <material name="foo">
        //   <color rgba="1 0 0 1"/>
        // </material>
        // <material name="foo"/>
        // It's a no-op.
        if ( material.IsReference && GetMaterial( material.name ) != null ) {
          Object.DestroyImmediate( material );
          continue;
        }

        if ( m_materialTable.ContainsKey( material.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( materialElement )}: Non-unique material name '{material.Name}'." );

        m_materialTable.Add( material.Name, materials.Count );
        materials.Add( material );
      }
      m_materials = materials.ToArray();

      // Reading links defined under the "robot" scope.
      var links = new List<Link>();
      var localMaterials = new HashSet<string>();
      var warningBeginStr = GUI.AddColorTag( "URDF Warning", Color.yellow );
      foreach ( var linkElement in root.Elements( "link" ) ) {
        var link = Instantiate<Link>( linkElement );

        if ( m_linkTable.ContainsKey( link.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( linkElement )}: Non-unique link name '{link.Name}'." );

        // Render materials with unique name is added to the global
        // library as we instantiate the model.
        var newDefinedMaterials = link.Visuals.Where( visual => visual.Material != null &&
                                                                !visual.Material.IsReference &&
                                                                !localMaterials.Contains( visual.Material.Name ) &&
                                                                GetMaterial( visual.Material.Name ) == null ).Select( visual => visual.Material.Name ).ToArray();
        foreach ( var newDefinedMaterialName in newDefinedMaterials )
          localMaterials.Add( newDefinedMaterialName );

        var missingMaterialReferences = link.Visuals.Where( visual => visual.Material != null &&
                                                                      visual.Material.IsReference &&
                                                                      !localMaterials.Contains( visual.Material.Name ) &&
                                                                      GetMaterial( visual.Material.Name ) == null ).Select( visual => visual.Material ).ToArray();
        if ( missingMaterialReferences.Length > 0 ) {
          Debug.LogWarning( $"{warningBeginStr}: {link.Name} contains " +
                            $"{missingMaterialReferences.Length} missing material references:" );
          foreach ( var missingMaterialReference in missingMaterialReferences )
            Debug.LogWarning( $"    {Utils.GetLineInfo( missingMaterialReference.LineNumber )}: <material name = \"{missingMaterialReference.Name}\"/>" );
        }

        m_linkTable.Add( link.Name, links.Count );
        links.Add( link );

        RequiresResourceLoader = RequiresResourceLoader || FindRequiresResourceLoader( link );
      }
      m_links = links.ToArray();

      // Reading joints defined under the "robot" scope.
      var joints = new List<UJoint>();
      foreach ( var jointElement in root.Elements( "joint" ) ) {
        var joint = Instantiate<UJoint>( jointElement );

        if ( m_jointTable.ContainsKey( joint.Name ) )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Non-unique link name '{joint.Name}'." );
        if ( GetLink( joint.Parent ) == null )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Joint parent link with name {joint.Parent} doesn't exist." );
        var childLink = GetLink( joint.Child );
        if ( childLink == null )
          throw new UrdfIOException( $"{Utils.GetLineInfo( jointElement )}: Joint child link with name {joint.Child} doesn't exist." );

        m_jointTable.Add( joint.Name, joints.Count );
        joints.Add( joint );

        // Avoiding warning for "world" type of links, i.e., 'parentLink' of
        // this joint may have inertial == null.
        if ( childLink.Inertial == null && childLink.Collisions.Length == 0 )
          Debug.LogWarning( $"{warningBeginStr} [{Utils.GetLineInfo( childLink.LineNumber )}]: Intermediate link '{childLink.Name}' is defined without " +
                            $"<inertial> and <collision> which results in default mass (1) and default inertia diagonal (1, 1, 1)." );
      }
      m_joints = joints.ToArray();

      // Verifying "mimic" references in the joints.
      foreach ( var joint in Joints )
        if ( joint.Mimic.Enabled && GetJoint( joint.Mimic.Joint ) == null )
          throw new UrdfIOException( $"{Utils.GetLineInfo( joint.LineNumber )}: Mimic joint '{joint.Mimic.Joint}' isn't defined." );
    }

    private bool FindRequiresResourceLoader( Link link )
    {
      if ( link == null || link.IsWorld )
        return false;
      return link.Collisions.Any( collision => collision.Geometry.Type == Geometry.GeometryType.Mesh ) ||
             link.Visuals.Any( visual => visual.Geometry.Type == Geometry.GeometryType.Mesh ||
                                         ( visual.Material != null && !string.IsNullOrEmpty( visual.Material.Texture ) ) );
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

    private UnityEngine.Material GetRenderMaterial( string name )
    {
      if ( m_renderMaterialsTable.TryGetValue( name, out var renderMaterialIndex ) )
        return m_renderMaterials[ renderMaterialIndex ];
      return null;
    }

    private UnityEngine.Material CreateRenderMaterial( Material material )
    {
      var renderMaterial   = new UnityEngine.Material( Shader.Find( "Standard" ) );
      renderMaterial.name  = material.Name;
      renderMaterial.color = material.Color;

      if ( !string.IsNullOrEmpty( material.Texture ) ) {
        var texture = GetResource<Texture>( material.Texture, ResourceType.Texture );
        if ( texture == null )
          throw new UrdfIOException( $"Texture resource '{material.Texture}' is null." );
        renderMaterial.SetTexture( "_MainTex", texture );
      }

      m_onObjectCreate?.Invoke( material );

      return renderMaterial;
    }

    /// <summary>
    /// Finds referenced material or creates a new.
    /// </summary>
    /// <remarks>
    /// This method can return null if <paramref name="material"/> is null or
    /// if a material is referencing a material that doesn't exist.
    /// </remarks>
    /// <param name="material">Material element.</param>
    /// <returns>Referenced or newly created material, null if something is wrong.</returns>
    private UnityEngine.Material GetOrCreateRenderMaterial( Material material )
    {
      if ( material == null )
        return null;
      if ( material.IsReference )
        return GetRenderMaterial( material.Name );
      // Some seems to define materials in the first elements and references
      // them later in the file. Add the material to the library for later
      // references if this material has a unique name.
      var renderMaterial = CreateRenderMaterial( material );
      if ( !string.IsNullOrEmpty( material.Name ) && GetRenderMaterial( material.Name ) == null )
        AddGloballyDeclaredRenderMaterial( renderMaterial );
      return renderMaterial;
    }

    private void GenerateGloballyDeclaredRenderMaterials()
    {
      foreach ( var material in m_materials )
        AddGloballyDeclaredRenderMaterial( CreateRenderMaterial( material ) );
    }

    private void AddGloballyDeclaredRenderMaterial( UnityEngine.Material renderMaterial )
    {
      m_renderMaterialsTable.Add( renderMaterial.name, m_renderMaterials.Count );
      m_renderMaterials.Add( renderMaterial );
    }

    private GameObject OnElementGameObject( GameObject go, Element element )
    {
      if ( go != null && element != null )
        GetOrCreateComponent<ElementComponent>( go ).SetElement( element );
      return go;
    }

    private void SetTransform( Transform transform, Pose pose, bool patchColladaTransform )
    {
      if ( pose == null )
        return;

      // By default, Unity puts a transform on Collada models
      // when the model is loaded into a project. This "patch"
      // undo this transform in many cases. Assumes <up_axis>Z_UP</up_axis>
      // in the .dae.
      if ( patchColladaTransform ) {
        var xRotation = Quaternion.Euler( new Vector3( 90, 0, 0 ) );
        transform.SetPositionAndRotation( pose.Xyz.ToLeftHanded() + xRotation * transform.position,
                                          ( pose.Rpy.RadEulerToLeftHanded() * xRotation ) * transform.rotation );
      }
      else {
        transform.position = pose.Xyz.ToLeftHanded();
        transform.rotation = pose.Rpy.RadEulerToLeftHanded();
      }
    }

    private RigidBody CreateRigidBody( GameObject gameObject, Link link )
    {
      var rb = GetOrCreateComponent<RigidBody>( gameObject );
      GetOrCreateComponent<ElementComponent>( gameObject ).SetElement( link );
      if ( link.IsStatic ) {
        rb.MotionControl = agx.RigidBody.MotionControl.STATIC;
        return rb;
      }

      // Note: When <inertial> isn't defined the collision shapes defines
      //       the mass properties.

      // <inertial> defined with required <mass> and <inertia>. Create
      // a native rigid body with the given properties and read the
      // values back to our rigid body component.
      if ( link.Inertial != null ) {
        var native = new agx.RigidBody();
        native.getMassProperties().setMass( link.Inertial.Mass, false );
        // Inertia tensor is given in the inertia frame. The rotation of the
        // CM frame can't be applied to the CM frame so we transform the inertia
        // CM frame and rotate the game object.
        var rotationMatrix = link.Inertial.Rpy.RadEulerToRotationMatrix();
        var inertia3x3 = (agx.Matrix3x3)link.Inertial.Inertia;
        inertia3x3 = rotationMatrix.transpose().Multiply( inertia3x3 ).Multiply( rotationMatrix );
        native.getMassProperties().setInertiaTensor( new agx.SPDMatrix3x3( inertia3x3 ) );
        native.getCmFrame().setLocalTranslate( link.Inertial.Xyz.ToVec3() );

        rb.RestoreLocalDataFrom( native );
      }

      return rb;
    }

    private GameObject AddCollision( RigidBody rb, Collision collision )
    {
      var shapeGoName = string.IsNullOrEmpty( collision.Name ) ?
                          CreateName( rb.transform, $"_{collision.Geometry.Type.ToString()}" ) :
                          collision.Name;
      var shapeGo = GetOrCreateGameObject( shapeGoName );
      GetOrCreateComponent<ElementComponent>( shapeGo ).SetElement( collision );
      var patchColladaTransform = false;
      try {
        if ( collision.Geometry.Type == Geometry.GeometryType.Box ) {
          var box = GetOrCreateComponent<Collide.Box>( shapeGo );
          box.HalfExtents = 0.5f * collision.Geometry.FullExtents;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Cylinder ) {
          // <cylinder> in URDF have their cylinder axis along z but in
          // Unity/AGX Dynamics the cylinder axis is along y. We're adding
          // an additional child which transform the axis to be z.
          var cylinderY2Z = GetOrCreateGameObject( collision.Name + "_extra" );
          cylinderY2Z.transform.parent = shapeGo.transform;
          cylinderY2Z.transform.rotation = Quaternion.FromToRotation( Vector3.up, Vector3.forward );

          var cylinder = GetOrCreateComponent<Collide.Cylinder>( cylinderY2Z );
          cylinder.Radius = collision.Geometry.Radius;
          cylinder.Height = collision.Geometry.Length;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Sphere ) {
          var sphere = GetOrCreateComponent<Collide.Sphere>( shapeGo );
          sphere.Radius = collision.Geometry.Radius;
        }
        else if ( collision.Geometry.Type == Geometry.GeometryType.Mesh ) {
          patchColladaTransform = CreateCollisionMesh( collision, shapeGo );
        }
      }
      catch ( System.Exception ) {
        GameObject.DestroyImmediate( shapeGo );
        throw;
      }

      shapeGo.transform.parent = rb.transform;
      SetTransform( shapeGo.transform, collision, patchColladaTransform && Options.TransformCollada );

      return shapeGo;
    }

    private GameObject AddVisual( RigidBody rb, Visual visual )
    {
      GameObject instance = null;
      UnityEngine.Material renderMaterial = null;
      var patchColladaTransform = false;
      if ( visual.Geometry.Type == Geometry.GeometryType.Mesh ) {
        var meshResource = GetResource<GameObject>( visual.Geometry.Filename, ResourceType.VisualMesh );
        if ( meshResource == null )
          throw new UrdfIOException( $"Mesh resource '{visual.Geometry.Filename}' is null." );
        instance = GameObject.Instantiate<GameObject>( meshResource );
        if ( visual.Geometry.HasScale )
          instance.transform.localScale = visual.Geometry.Scale;
        // Overrides model if <material> is defined under <visual>.
        renderMaterial = GetOrCreateRenderMaterial( visual.Material );

        patchColladaTransform = Options.TransformCollada &&
                                visual.Geometry.Type == Geometry.GeometryType.Mesh &&
                                visual.Geometry.ResourceType == Geometry.MeshResourceType.Collada;
      }
      else {
        instance = InstantiateAndSizePrimitiveRenderer( visual );

        renderMaterial = GetOrCreateRenderMaterial( visual.Material ) ??
                         Rendering.ShapeVisual.DefaultMaterial;
      }

      if ( renderMaterial != null ) {
        var renderers = ( from renderer in instance.GetComponentsInChildren<MeshRenderer>()
                          select renderer ).ToArray();
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
        gameObjectToTransform = GetOrCreateGameObject( instance.name + "_extra" );
        instance.transform.parent = gameObjectToTransform.transform;
        instance.transform.rotation = Quaternion.FromToRotation( Vector3.up, Vector3.forward );
      }

      gameObjectToTransform.transform.parent = rb.transform;
      SetTransform( gameObjectToTransform.transform, visual, patchColladaTransform );
      GetOrCreateComponent<ElementComponent>( gameObjectToTransform ).SetElement( visual );

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
      var child = getGameObject( joint.Child );
      if ( parent == null )
        throw new UrdfIOException( $"Unable to find parent link '{joint.Parent}' in joint '{joint.Name}'." );
      if ( child == null )
        throw new UrdfIOException( $"Unable to find child link '{joint.Child}' in joint '{joint.Name}'." );
      var childTransform = parent.transform.localToWorldMatrix * Matrix4x4.TRS( joint.Xyz.ToLeftHanded(),
                                                                                joint.Rpy.RadEulerToLeftHanded(),
                                                                                Vector3.one );
      child.transform.parent = parent.transform;
      child.transform.position = childTransform.GetTranslate();
      child.transform.rotation = childTransform.GetRotation();

      GameObject constraintGameObject = null;
      if ( joint.Type != UJoint.JointType.Floating &&
           child.GetComponent<RigidBody>() != null &&
           parent.GetComponent<RigidBody>() != null ) {
        constraintGameObject = Factory.Create( joint.Type == UJoint.JointType.Revolute || joint.Type == UJoint.JointType.Continuous ?
                                                 ConstraintType.Hinge :
                                               joint.Type == UJoint.JointType.Prismatic ?
                                                 ConstraintType.Prismatic :
                                               joint.Type == UJoint.JointType.Fixed ?
                                                 ConstraintType.LockJoint :
                                               joint.Type == UJoint.JointType.Planar ?
                                                 ConstraintType.PlaneJoint :
                                                 ConstraintType.Unknown,
                                               Vector3.zero,
                                               joint.Axis.ToLeftHanded(),
                                               child.GetComponent<RigidBody>(),
                                               parent.GetComponent<RigidBody>() );

        var constraint = constraintGameObject.GetComponent<Constraint>();
        constraint.CollisionsState = Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;
        // Note: If this is a mimic joint we're just setting the values,
        //       the actual controllers are disabled.
        if ( joint.Limit.Enabled ) {
          if ( joint.Limit.RangeEnabled ) {
            var rangeController = constraint.GetController<RangeController>();
            if ( rangeController != null ) {
              rangeController.Enable = !joint.Mimic.Enabled;
              rangeController.Range = new RangeReal( joint.Limit.Lower, joint.Limit.Upper );
            }
          }

          if ( joint.Limit.Effort > 0.0f ) {
            var targetSpeedController = constraint.GetController<TargetSpeedController>();
            if ( targetSpeedController != null ) {
              targetSpeedController.Enable = !joint.Mimic.Enabled;
              targetSpeedController.ForceRange = new RangeReal( joint.Limit.Effort );
            }
          }
        }

        if ( joint.Dynamics.Enabled ) {
          if ( joint.Dynamics.Friction > 0 ) {
            var frictionController = constraint.GetController<FrictionController>();
            if ( frictionController != null ) {
              frictionController.Enable = true;
              frictionController.FrictionCoefficient = 0.0f;
              frictionController.MinimumStaticFrictionForceRange = new RangeReal( joint.Dynamics.Friction );
            }
          }
        }
      }
      else
        constraintGameObject = new GameObject( joint.name );

      constraintGameObject.name = joint.Name;
      constraintGameObject.transform.parent = parent.transform;
      GetOrCreateComponent<ElementComponent>( constraintGameObject ).SetElement( joint );

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

    private bool CreateCollisionMesh( Collision collision, GameObject shapeGo )
    {
      var meshResource = GetResource<GameObject>( collision.Geometry.Filename, ResourceType.CollisionMesh );
      if ( meshResource == null )
        throw new UrdfIOException( $"Mesh resource '{collision.Geometry.Filename}' is null." );

      if ( collision.Geometry.HasScale )
        shapeGo.transform.localScale = collision.Geometry.Scale;

      var isCollada = collision.Geometry.ResourceType == Geometry.MeshResourceType.Collada;
      // The Collada importer in Unity is scaling the models depending
      // on the units used.
      if ( isCollada )
        shapeGo.transform.localScale = Vector3.Scale( shapeGo.transform.localScale,
                                                      meshResource.transform.localScale );

      var filters = meshResource.GetComponentsInChildren<MeshFilter>();
      var handleSpecialCollada = filters.Length > 0 &&
                                 isCollada &&
                                 filters.Any( filter => filter.gameObject != meshResource );
      if ( handleSpecialCollada ) {
        // Special case where a Collada import contains game objects with
        // filter(s) that isn't on the meshResource. It could either be
        // a single child or multiple, in hierarchy. We're currently not
        // restoring that complete hierarchy, we're only taking the ones
        // that contains a mesh filter. That's why we use 'lossyScale' and
        // "global" position and rotation.
        for ( int i = 0; i < filters.Length; ++i ) {
          var extra = GetOrCreateGameObject( $"{collision.Name}_extra_{i + 1}" );
          extra.transform.parent = shapeGo.transform;
          extra.transform.localRotation = filters[ i ].transform.rotation;
          extra.transform.localPosition = filters[ i ].transform.position;
          extra.transform.localScale = filters[ i ].transform.lossyScale;

          var mesh = GetOrCreateComponent<Collide.Mesh>( extra );
          mesh.SetSourceObject( filters[ i ].sharedMesh );
        }
      }
      else {
        var sourceMeshes = meshResource.GetComponentsInChildren<MeshFilter>().Select( filter => filter.sharedMesh );
        if ( sourceMeshes.Count() == 0 )
          throw new UrdfIOException( $"Mesh resource '{collision.Geometry.Filename}' doesn't contain any meshes." );
        var mesh = GetOrCreateComponent<Collide.Mesh>( shapeGo );
        foreach ( var sourceMesh in sourceMeshes )
          mesh.AddSourceObject( sourceMesh );
      }

      return handleSpecialCollada;
    }

    private T GetResource<T>( string filename, ResourceType type )
      where T : Object
    {
      if ( m_resourceCache == null )
        m_resourceCache = new Dictionary<string, Object>();
      else if ( m_resourceCache.TryGetValue( filename, out var cachedResource ) )
        return cachedResource as T;

      var resource = m_resourceLoader?.Invoke( filename, type );
      if ( resource != null )
        m_resourceCache.Add( filename, resource );
      return resource as T;
    }

    private string CreateName( Transform parent, string postFix )
    {
      var name = parent.name + postFix;
      var finalName = name;
      var counter = 0;
      while ( parent.Find( finalName ) != null )
        finalName = $"{name} ({++counter})";

      return finalName;
    }

    [SerializeField]
    private bool m_requiresResourceLoader = false;

    [SerializeField]
    private Material[] m_materials = new Material[] { };

    [SerializeField]
    private Link[] m_links = new Link[] { };

    [SerializeField]
    private UJoint[] m_joints = new UJoint[] { };

    private Dictionary<string, int> m_materialTable = new Dictionary<string, int>();
    private Dictionary<string, int> m_linkTable = new Dictionary<string, int>();
    private Dictionary<string, int> m_jointTable = new Dictionary<string, int>();
    private Func<string, ResourceType, Object> m_resourceLoader = null;
    private Action<Object> m_onObjectCreate = null;
    private Dictionary<string, Object> m_resourceCache = null;
    private List<UnityEngine.Material> m_renderMaterials = new List<UnityEngine.Material>();
    private Dictionary<string, int> m_renderMaterialsTable = new Dictionary<string, int>();
  }
}
