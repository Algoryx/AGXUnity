using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using AGXUnity.Rendering;
using Tree = AGXUnityEditor.IO.InputAGXFileTree;
using Node = AGXUnityEditor.IO.InputAGXFileTreeNode;

namespace AGXUnityEditor.IO
{
  /// <summary>
  /// Load .agx/.aagx file to a prefab with the same name in the same directory.
  /// 1. TryLoad - loading file into native simulation (restoring file)
  /// 2. TryParse - parsing simulation creating the simulation tree
  /// 3. TryGenerate - generates game objects and assets given simulation tree
  /// 4. TryCreatePrefab - creates a prefab of the generated objects, the instance
  ///                      is destroyed when this object is disposed
  /// </summary>
  public class InputAGXFile : IDisposable
  {
    /// <summary>
    /// AGX file info.
    /// </summary>
    public AGXFileInfo FileInfo { get; private set; }

    /// <summary>
    /// Native simulation the file is loaded into.
    /// </summary>
    public agxSDK.Simulation Simulation { get; private set; }

    /// <summary>
    /// True when the prefab has been successfully created.
    /// </summary>
    public bool Successful { get; private set; }

    /// <summary>
    /// Construct given AGX file info.
    /// </summary>
    /// <param name="info">AGX file info.</param>
    public InputAGXFile( AGXFileInfo info )
    {
      if ( info == null )
        throw new ArgumentNullException( "info", "File info object is null." );
      else
        FileInfo = info;

      if ( !FileInfo.Exists )
        throw new FileNotFoundException( "File not found: " + FileInfo.FullName );

      if ( FileInfo.Type == AGXFileInfo.FileType.Unknown || FileInfo.Type == AGXFileInfo.FileType.AGXPrefab )
        throw new AGXUnity.Exception( "Unsupported file format: " + FileInfo.FullName );

      Successful = false;

      // Making sure AGX has been initialized before we create a simulation.
      var nativeHandler = NativeHandler.Instance;

      Simulation = new agxSDK.Simulation();
    }

    /// <summary>
    /// Trying to read AGX file. Throws if something goes wrong.
    /// </summary>
    public void TryLoad()
    {
      m_progressBar = new ProgressBar() { Title = "Creating prefab from: " + FileInfo.NameWithExtension };

      using ( m_progressBar.Progress( "Loading: " + FileInfo.NameWithExtension, 1 ) )
        if ( !agxIO.agxIOSWIG.readFile( FileInfo.FullName, Simulation ) )
          throw new AGXUnity.Exception( "Unable to load file:" + FileInfo.FullName );
    }

    /// <summary>
    /// Trying to parse the simulation, creating the simulation tree.
    /// Throws if something goes wrong.
    /// </summary>
    public void TryParse()
    {
      FileInfo.CreateInstance();

      using ( m_progressBar.Progress( "Parsing: " + FileInfo.NameWithExtension, 1 ) )
        m_tree.Parse( Simulation, FileInfo );
    }

    /// <summary>
    /// Trying to generate the objects given the simulation tree.
    /// Throws if something goes wrong.
    /// </summary>
    public ObjectDb.Statistics TryGenerate()
    {
      FileInfo.GetOrCreateDataDirectory();

      // Adding one for disabled collisions.
      int numSubProgresses = m_tree.Roots.Length + 1;
      using ( var subProgress = m_progressBar.Progress( "Generating: " + FileInfo.NameWithExtension, numSubProgresses ) ) {
        FileInfo.PrefabInstance.transform.position = Vector3.zero;
        FileInfo.PrefabInstance.transform.rotation = Quaternion.identity;
        var fileData = FileInfo.PrefabInstance.GetOrCreateComponent<AGXUnity.IO.RestoredAGXFile>();

        fileData.DataDirectoryId = FileInfo.DataDirectoryId;
        fileData.SolverSettings  = FileInfo.ObjectDb.GetOrCreateAsset( fileData.SolverSettings,
                                                                       FindName( "SolverSettings_" + FileInfo.Name, "SolverSettings" ),
                                                                       ss => ss.RestoreLocalDataFrom( Simulation ) );

        foreach ( var root in m_tree.Roots ) {
          subProgress.Tick( $"Generating: {(string.IsNullOrEmpty( root.Name ) ? root.Type.ToString().SplitCamelCase() : root.Name)}" );
          Generate( root );
          subProgress.Tack();
        }

        subProgress.Tick( "Disabled collisions" );
        var disabledCollisionsState = Simulation.getSpace().findDisabledCollisionsState();
        foreach ( var namePair in disabledCollisionsState.getDisabledNames() )
          fileData.AddDisabledPair( namePair.first, namePair.second );
        foreach ( var idPair in disabledCollisionsState.getDisabledIds() )
          fileData.AddDisabledPair( idPair.first.ToString(), idPair.second.ToString() );
        foreach ( var geometryPair in disabledCollisionsState.getDisabledGeometryPairs() ) {
          if ( !Tree.IsValid( geometryPair.first ) || !Tree.IsValid( geometryPair.second ) )
            continue;

          var geometry1Node = m_tree.GetNode( geometryPair.first.getUuid() );
          var geometry2Node = m_tree.GetNode( geometryPair.second.getUuid() );
          if ( geometry1Node == null || geometry2Node == null ) {
            Debug.LogWarning( "Unreferenced geometry in disabled collisions pair." );
            continue;
          }

          var geometry1Id = geometry2Node.Uuid.str();
          foreach ( var shapeNode in geometry1Node.GetChildren( Node.NodeType.Shape ) )
            shapeNode.GameObject.GetOrCreateComponent<CollisionGroups>().AddGroup( geometry1Id, false );
          var geometry2Id = geometry1Node.Uuid.str();
          foreach ( var shapeNode in geometry2Node.GetChildren( Node.NodeType.Shape ) )
            shapeNode.GameObject.GetOrCreateComponent<CollisionGroups>().AddGroup( geometry2Id, false );

          fileData.AddDisabledPair( geometry1Id, geometry2Id );
        }
        subProgress.Tack();
      }

      return FileInfo.ObjectDb.RemoveUnreferencedObjects( FileInfo.PrefabInstance );
    }

    /// <summary>
    /// Trying to create and save a prefab given the generated object(s).
    /// Throws if something goes wrong.
    /// </summary>
    /// <returns>Prefab parent.</returns>
    public GameObject TryCreatePrefab()
    {
      using ( m_progressBar.Progress( "Creating prefab and saving assets.", 1 ) ) {
        var prefab = FileInfo.SavePrefab();
        if ( prefab == null )
          throw new AGXUnity.Exception( "Unable to create prefab: " + FileInfo.PrefabPath );

        FileInfo.Save();

        Successful = true;

        return prefab;
      }
    }

    /// <summary>
    /// Disposes the native simulation and destroys any created instances
    /// that hasn't been saved as assets.
    /// </summary>
    public void Dispose()
    {
      if ( Simulation != null )
        Simulation.Dispose();
      Simulation = null;

      if ( FileInfo.PrefabInstance != null )
        GameObject.DestroyImmediate( FileInfo.PrefabInstance );

      if ( m_progressBar != null )
        m_progressBar.Dispose();
    }

    private void Generate( Node node )
    {
      if ( node == null )
        return;

      switch ( node.Type ) {
        case Node.NodeType.Assembly:
          agx.Frame frame      = m_tree.GetAssembly( node.Uuid );
          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( "", node.Type.ToString() );

          node.GameObject.transform.position = frame.getTranslate().ToHandedVector3();
          node.GameObject.transform.rotation = frame.getRotate().ToHandedQuaternion();

          node.GameObject.GetOrCreateComponent<Assembly>();

          break;
        case Node.NodeType.RigidBody:
          agx.RigidBody nativeRb = m_tree.GetRigidBody( node.Uuid );
          node.GameObject        = GetOrCreateGameObject( node );
          node.GameObject.name   = FindName( nativeRb.getName(), node.Type.ToString() );


          node.GameObject.transform.position = nativeRb.getPosition().ToHandedVector3();
          node.GameObject.transform.rotation = nativeRb.getRotation().ToHandedQuaternion();

          node.GameObject.GetOrCreateComponent<RigidBody>().RestoreLocalDataFrom( nativeRb );          

          break;
        case Node.NodeType.Geometry:
          // Ignoring geometries - handling Shape == Geometry.
          // The shapes are children to this node.
          break;
        case Node.NodeType.Shape:
          var nativeGeometry  = m_tree.GetGeometry( node.Parent.Uuid );
          var nativeShape     = m_tree.GetShape( node.Uuid );
          var nativeShapeType = (agxCollide.Shape.Type)nativeShape.getType();

          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( nativeGeometry.getName() +
                                           "_" +
                                           nativeShapeType.ToString().ToLower().FirstCharToUpperCase(),
                                           node.Type.ToString() );

          node.GameObject.transform.position = nativeShape.getTransform().getTranslate().ToHandedVector3();
          node.GameObject.transform.rotation = nativeShape.getTransform().getRotate().ToHandedQuaternion();

          if ( !CreateShape( node ) )
            GameObject.DestroyImmediate( node.GameObject );

          break;
        case Node.NodeType.Material:
          // Ignoring materials - the referenced materials should have been restored
          // by now using RestoreShapeMaterial.
          break;
        case Node.NodeType.ContactMaterial:
          var materialNodes = node.GetReferences( Node.NodeType.Material );
          Func<string> contactMaterialMaterialNames = () =>
          {
            return m_tree.GetMaterial( materialNodes[ 0 ].Uuid ).getName() + " <-> " +
                   m_tree.GetMaterial( materialNodes[ 1 ].Uuid ).getName();
          };
          if ( materialNodes.Length == 0 ) {
            Debug.LogWarning( "No materials referenced to ContactMaterial node - ignoring contact material." );
            break;
          }
          else if ( materialNodes.Length > 2 ) {
            Debug.LogWarning( "More than two materials referenced to contact material - first two will be used: " +
                              contactMaterialMaterialNames() );
          }

          var materials = new ShapeMaterial[ 2 ]
          {
            materialNodes[ 0 ].Asset as ShapeMaterial,
            materialNodes.Length > 1 ?
              materialNodes[ 1 ].Asset as ShapeMaterial :
              materialNodes[ 0 ].Asset as ShapeMaterial
          };

          var nativeContactMaterial = m_tree.GetContactMaterial( node.Uuid );
          var nativeFrictionModel = nativeContactMaterial.getFrictionModel();
          if ( materials[ 0 ] == null || materials[ 1 ] == null ) {
            Debug.LogWarning( $"Unreferenced ShapeMaterial(s) in ContactMaterial: {contactMaterialMaterialNames()} - ignoring contact material." );
            break;
          }
          Predicate<ContactMaterial> materialsMatcher = cm => ( cm.Material1 == materials[ 0 ] && cm.Material2 == materials[ 1 ] ) ||
                                                              ( cm.Material2 == materials[ 0 ] && cm.Material1 == materials[ 1 ] );
          var contactMaterial = FileInfo.ObjectDb.GetOrCreateAsset( materialsMatcher,
                                                                    FindName( $"ContactMaterial_{materials[ 0 ].name}_{materials[ 1 ].name}",
                                                                              node.Type.ToString() ),
                                                                    cm =>
                                                                    {
                                                                      cm.Material1 = materials[ 0 ];
                                                                      cm.Material2 = materials[ 1 ];
                                                                      cm.RestoreLocalDataFrom( nativeContactMaterial );
                                                                    } );
          if ( nativeFrictionModel != null ) {
            var externalFrictionModel = contactMaterial.FrictionModel != null &&
                                        !FileInfo.ObjectDb.ContainsAsset( contactMaterial.FrictionModel );
            // The user has assigned a friction model, not located in our data directory,
            // to this contact material. We approve this change (with a warning) by not
            // assigning the friction model from the model.
            if ( externalFrictionModel ) {
              Debug.LogWarning( $"Friction Model {contactMaterial.FrictionModel.name} is external from the re-imported model. " +
                                "No changes will be made.", contactMaterial );
            }
            else {
              contactMaterial.FrictionModel = FileInfo.ObjectDb.GetOrCreateAsset( contactMaterial.FrictionModel,
                                                                                  $"FrictionModel_{contactMaterial.name}",
                                                                                  fm => fm.RestoreLocalDataFrom( nativeFrictionModel ) );
            }
          }

          node.Asset = contactMaterial;

          break;
        case Node.NodeType.Constraint:
          var nativeConstraint = m_tree.GetConstraint( node.Uuid );

          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( nativeConstraint.getName(),
                                           "AGXUnity." + Constraint.FindType( nativeConstraint ) );

          if ( !CreateConstraint( node ) )
            GameObject.DestroyImmediate( node.GameObject );

          break;
        case Node.NodeType.Wire:
          var nativeWire = m_tree.GetWire( node.Uuid );

          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( nativeWire.getName(), "AGXUnity.Wire" );

          if ( !CreateWire( node ) )
            GameObject.DestroyImmediate( node.GameObject );

          break;
        case Node.NodeType.Cable:
          var nativeCable = m_tree.GetCable( node.Uuid );

          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( nativeCable.getName(), "AGXUnity.Cable" );

          if ( !CreateCable( node ) )
            GameObject.DestroyImmediate( node.GameObject );

          break;
        case Node.NodeType.ObserverFrame:
          var nativeObserverFrame = m_tree.GetObserverFrame( node.Uuid );

          node.GameObject      = GetOrCreateGameObject( node );
          node.GameObject.name = FindName( nativeObserverFrame.getName(), "AGXUnity.ObserverFrame" );

          var rbNode = node.GetReferences( Node.NodeType.RigidBody ).FirstOrDefault();
          node.GameObject.GetOrCreateComponent<ObserverFrame>().RestoreLocalDataFrom( nativeObserverFrame,
                                                                                      rbNode != null ? rbNode.GameObject : null );

          break;
      }

      foreach ( var child in node.Children )
        Generate( child );
    }

    private GameObject GetOrCreateGameObject( Node node )
    {
      if ( node.GameObject != null ) {
        FileInfo.ObjectDb.Ref( node.Uuid );
        return node.GameObject;
      }

      node.GameObject = FileInfo.ObjectDb.GetOrCreateGameObject( node.Uuid );

      // Is it safe to exit if the node has a parent?
      // I.e., the node has been read from an existing prefab.
      if ( FileInfo.PrefabInstance.HasChild( node.GameObject ) )
        return node.GameObject;

      // Passing parents with null game objects - e.g., shapes
      // has geometry as parent but we're not creating objects
      // for geometries.
      Node localParent = node.Parent;
      while ( localParent != null && localParent.GameObject == null )
        localParent = localParent.Parent;

      if ( localParent != null )
        localParent.GameObject.AddChild( node.GameObject );
      else
        FileInfo.PrefabInstance.AddChild( node.GameObject );

      return node.GameObject;
    }

    private bool CreateShape( Node node )
    {
      var nativeGeometry  = m_tree.GetGeometry( node.Parent.Uuid );
      var nativeShape     = m_tree.GetShape( node.Uuid );
      var nativeShapeType = (agxCollide.Shape.Type)nativeShape.getType();

      if ( nativeShapeType == agxCollide.Shape.Type.BOX ) {
        node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Box>().HalfExtents = nativeShape.asBox().getHalfExtents().ToVector3();
      }
      else if ( nativeShapeType == agxCollide.Shape.Type.CYLINDER ) {
        var cylinder    = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Cylinder>();
        cylinder.Radius = Convert.ToSingle( nativeShape.asCylinder().getRadius() );
        cylinder.Height = Convert.ToSingle( nativeShape.asCylinder().getHeight() );
      }
      else if ( nativeShapeType == agxCollide.Shape.Type.HOLLOW_CYLINDER ) {
        var hollowCylinder       = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.HollowCylinder>();
        hollowCylinder.Thickness = Convert.ToSingle( nativeShape.asHollowCylinder().getThickness() );
        hollowCylinder.Radius    = Convert.ToSingle( nativeShape.asHollowCylinder().getOuterRadius() );
        hollowCylinder.Height    = Convert.ToSingle( nativeShape.asHollowCylinder().getHeight() );
      }
      else if (nativeShapeType == agxCollide.Shape.Type.CONE)
      {
        var cone          = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Cone>();
        cone.BottomRadius = Convert.ToSingle(nativeShape.asCone().getBottomRadius());
        cone.TopRadius    = Convert.ToSingle(nativeShape.asCone().getTopRadius());
        cone.Height       = Convert.ToSingle(nativeShape.asCone().getHeight());
      }
      else if (nativeShapeType == agxCollide.Shape.Type.HOLLOW_CONE)
      {
        var hollowCone          = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.HollowCone>();
        hollowCone.Thickness    = Convert.ToSingle(nativeShape.asHollowCone().getThickness());
        hollowCone.BottomRadius = Convert.ToSingle(nativeShape.asHollowCone().getBottomOuterRadius());
        hollowCone.TopRadius    = Convert.ToSingle(nativeShape.asHollowCone().getTopOuterRadius());
        hollowCone.Height       = Convert.ToSingle(nativeShape.asHollowCone().getHeight());
      }
      else if ( nativeShapeType == agxCollide.Shape.Type.CAPSULE ) {
        var capsule    = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Capsule>();
        capsule.Radius = Convert.ToSingle( nativeShape.asCapsule().getRadius() );
        capsule.Height = Convert.ToSingle( nativeShape.asCapsule().getHeight() );
      }
      else if ( nativeShapeType == agxCollide.Shape.Type.SPHERE ) {
        var sphere    = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Sphere>();
        sphere.Radius = Convert.ToSingle( nativeShape.asSphere().getRadius() );
      }
      else if ( nativeShapeType == agxCollide.Shape.Type.CONVEX ||
                nativeShapeType == agxCollide.Shape.Type.TRIMESH ||
                nativeShapeType == agxCollide.Shape.Type.HEIGHT_FIELD ) {
        var mesh          = node.GameObject.GetOrCreateComponent<AGXUnity.Collide.Mesh>();
        var collisionData = nativeShape.asMesh().getMeshData();
        var nativeToWorld = nativeShape.getTransform();
        var meshToLocal   = mesh.transform.worldToLocalMatrix;

        var sourceObjects = mesh.SourceObjects;
        var meshes        = MeshSplitter.Split( collisionData.getVertices(),
                                                collisionData.getIndices(),
                                                v => meshToLocal.MultiplyPoint3x4( nativeToWorld.preMult( v ).ToHandedVector3() ),
                                                UInt16.MaxValue ).Meshes;

        // Clearing previous sources.
        mesh.SetSourceObject( null );
        for ( int i = 0; i < meshes.Length; ++i ) {
          var source = FileInfo.ObjectDb.GetOrCreateAsset( i < sourceObjects.Length ? sourceObjects[ i ] : null,
                                                           $"Mesh_{mesh.name}{(meshes.Length > 1 ? $"_{i}" : "")}",
                                                           m =>
                                                           {
                                                             m.Clear();
                                                             EditorUtility.CopySerialized( meshes[ i ], m );
                                                           } );
          mesh.AddSourceObject( source );
        }
      }
      else {
        Debug.LogWarning( "Unsupported shape type: " + nativeShapeType );
        return false;
      }

      var shape = node.GameObject.GetComponent<AGXUnity.Collide.Shape>();

      shape.gameObject.SetActive( nativeGeometry.isEnabled() );
      shape.IsSensor = nativeGeometry.isSensor();

      shape.Material = RestoreShapeMaterial( shape.Material,
                                             nativeGeometry.getMaterial(),
                                             shape );

      shape.CollisionsEnabled = nativeGeometry.getEnableCollisions();

      // Groups referenced in geometry node.
      var groups = node.Parent.GetReferences( Node.NodeType.GroupId );
      if ( groups.Length > 0 ) {
        var groupsComponent = shape.gameObject.GetOrCreateComponent<CollisionGroups>();
        foreach ( var group in groups )
          if ( group.Object is string )
            groupsComponent.AddGroup( group.Object as string, false );
      }

      CreateRenderData( node, shape );

      return true;
    }

    private bool CreateRenderData( Node node, AGXUnity.Collide.Shape shape )
    {
      if ( shape == null )
        return false;

      var nativeShape      = m_tree.GetShape( node.Uuid );
      var nativeRenderData = nativeShape.getRenderData();
      var shapeVisual      = ShapeVisual.Find( shape );
      if ( nativeRenderData == null || !nativeRenderData.getShouldRender() ) {
        if ( shapeVisual != null ) {
          UnityEngine.Object.DestroyImmediate( shapeVisual.gameObject );
          EditorUtility.SetDirty( FileInfo.PrefabInstance );
        }
        return false;
      }

      var currentMeshes = shapeVisual == null ?
                            new Mesh[] {} :
                            ( from proxy in shapeVisual.GetComponentsInChildren<OnSelectionProxy>()
                              where proxy.Component == shape &&
                                    proxy.GetComponent<MeshFilter>() != null &&
                                    proxy.GetComponent<MeshFilter>().sharedMesh != null
                              select proxy.GetComponent<MeshFilter>().sharedMesh ).ToArray();
      var currentRenderers = shapeVisual == null ?
                               new MeshRenderer[] { } :
                               ( from proxy in shapeVisual.GetComponentsInChildren<OnSelectionProxy>()
                                 where proxy.Component == shape &&
                                       proxy.GetComponent<MeshRenderer>() != null
                                 select proxy.GetComponent<MeshRenderer>() ).ToArray();
      var currentMaterials = ( from renderer in currentRenderers select renderer.sharedMaterial ).ToArray();

      var nativeGeometry = m_tree.GetGeometry( node.Parent.Uuid );
      var toWorld        = nativeGeometry.getTransform();
      var toLocal        = shape.transform.worldToLocalMatrix;
      Mesh[] meshes      = MeshSplitter.Split( nativeRenderData.getVertexArray(),
                                               nativeRenderData.getIndexArray(),
                                               nativeRenderData.getTexCoordArray(),
                                               v => toLocal.MultiplyPoint3x4( toWorld.preMult( v ).ToHandedVector3() ) ).Meshes;

      // Initial import with currentMeshes.Length == 0 and shape mesh source
      // matches render data - use shape mesh sources instead.
      if ( shape is AGXUnity.Collide.Mesh &&
           currentMeshes.Length == 0 &&
           meshes.Length == ( shape as AGXUnity.Collide.Mesh ).SourceObjects.Length ) {
        var shapeMesh = shape as AGXUnity.Collide.Mesh;
        var matching = true;
        for ( int i = 0; matching && i < meshes.Length; ++i ) {
          matching = meshes[ i ].vertexCount == shapeMesh.SourceObjects[ i ].vertexCount;
          if ( !matching )
            continue;
          var v1 = meshes[ i ].vertices;
          var v2 = shapeMesh.SourceObjects[ i ].vertices;
          for ( int j = 0; matching && j < v1.Length; ++j )
            matching = AGXUnity.Utils.Math.Approximately( v1[ j ], v2[ j ], 1.0E-5f );
        }
        if ( matching ) {
          currentMeshes = new Mesh[ meshes.Length ];
          for ( int i = 0; i < meshes.Length; ++i ) {
            shapeMesh.SourceObjects[ i ].SetUVs( 0, meshes[ i ].uv.ToList() );
            currentMeshes[ i ] = meshes[ i ] = shapeMesh.SourceObjects[ i ];
          }
        }
      }

      var materialName = string.IsNullOrEmpty( nativeRenderData.getRenderMaterial().getName() ) ?
                           $"{shape.name}_Visual_Material" :
                           nativeRenderData.getRenderMaterial().getName();


      // No structural changes from previous read visuals.
      if ( shapeVisual != null &&
           currentMeshes.Length == meshes.Length &&
           currentRenderers.Length == meshes.Length ) {
        for ( int i = 0; i < meshes.Length; ++i ) {
          // Meshes and materials are already referenced so we don't have to
          // assign them again to the ShapeVisuals.
          FileInfo.ObjectDb.GetOrCreateAsset( currentMeshes[ i ],
                                              $"{shape.name}_Visual_Mesh_{i}",
                                              m =>
                                              {
                                                m.Clear();
                                                EditorUtility.CopySerialized( meshes[ i ], m );
                                              } );
          var material = RestoreRenderMaterial( currentMaterials[ i ],
                                                materialName,
                                                nativeRenderData.getRenderMaterial(),
                                                shape );
          currentRenderers[ i ].sharedMaterial = material;
          EditorUtility.SetDirty( shapeVisual );
        }
      }
      else {
        if ( shapeVisual != null ) {
          UnityEngine.Object.DestroyImmediate( shapeVisual.gameObject, true );
          EditorUtility.SetDirty( FileInfo.PrefabInstance );
          shapeVisual = null;
        }

        var material = RestoreRenderMaterial( currentMaterials.Length > 0 ?
                                                currentMaterials[ 0 ] :
                                                null,
                                              materialName,
                                              nativeRenderData.getRenderMaterial(),
                                              shape );
        for ( int i = 0; i < meshes.Length; ++i ) {
          meshes[ i ] = FileInfo.ObjectDb.GetOrCreateAsset( i < currentMeshes.Length ? currentMeshes[ i ] : null,
                                                            $"{shape.name}_Visual_Mesh_{i}",
                                                            m =>
                                                            {
                                                              m.Clear();
                                                              EditorUtility.CopySerialized( meshes[ i ], m );
                                                            } );
        }

        ShapeVisual.CreateRenderData( shape, meshes, material );
      }

      return true;
    }

    private bool CreateConstraint( Node node )
    {
      var nativeConstraint = m_tree.GetConstraint( node.Uuid );
      if ( nativeConstraint == null ) {
        Debug.LogWarning( "Unable to find native constraint instance with name: " +
                          node.GameObject.name +
                          " (UUID: " + node.Uuid.str() + ")" );
        return false;
      }

      var bodyNodes = node.GetReferences( Node.NodeType.RigidBody );
      if ( bodyNodes.Length < 1 || bodyNodes.Length > 2 ) {
        Debug.LogWarning( "Unsupported number of body references to constraint with name: " +
                          node.GameObject.name +
                          " (#bodies: " + bodyNodes.Length + ")" );
        return false;
      }

      var constraintType = Constraint.FindType( nativeConstraint );
      if ( constraintType == ConstraintType.Unknown ) {
        Debug.LogWarning( "Unknown/unsupported constraint type of constraint with name: " +
                          node.GameObject.name +
                          " (UUID: " + node.Uuid.str() + ")" );
        return false;
      }

      Constraint constraint = node.GameObject.GetOrCreateComponent<Constraint>();

      // Is the constraint enabled/active? 
      // Somewhat strange though: Why is not only the Constraint component disabled?
      // For RigidBody component, only that component is disabled, but the below code disables the whole constraint GameObject.
      // Works for now
      constraint.gameObject.SetActive(nativeConstraint.isEnabled());

      constraint.SetType( constraintType, true );

      try {
        constraint.TryAddElementaryConstraints( nativeConstraint );
        constraint.VerifyImplementation();
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        return false;
      }

      // Scaling damping to our (sigh) hard coded time step.
      float fixedStepTime = Time.fixedDeltaTime;
      float readTimeStep  = Convert.ToSingle( Simulation.getTimeStep() );
      float timeStepRatio = fixedStepTime / readTimeStep;
      if ( !AGXUnity.Utils.Math.Approximately( timeStepRatio, 1.0f ) ) {
        foreach ( var ec in constraint.ElementaryConstraints ) {
          foreach ( var rowData in ec.RowData ) {
            if ( rowData.Compliance < -float.Epsilon ) {
              Debug.LogWarning( "Constraint: " + constraint.name +
                                " (ec name: " + rowData.ElementaryConstraint.NativeName + ")," +
                                " has too low compliance: " + rowData.Compliance + ". Setting to zero." );
              rowData.Compliance = 0.0f;
            }
            else if ( rowData.Compliance > float.MaxValue ) {
              Debug.LogWarning( "Constraint: " + constraint.name +
                                " (ec name: " + rowData.ElementaryConstraint.NativeName + ")," +
                                " has too high compliance: " + rowData.Compliance + ". Setting to a large value." );
              rowData.Compliance = 0.5f * float.MaxValue;
            }
            rowData.Damping *= timeStepRatio;
          }
        }
      }

      constraint.AttachmentPair.ReferenceFrame.SetParent( bodyNodes[ 0 ].GameObject );
      constraint.AttachmentPair.ReferenceFrame.LocalPosition = nativeConstraint.getAttachment( 0ul ).getFrame().getLocalTranslate().ToHandedVector3();
      constraint.AttachmentPair.ReferenceFrame.LocalRotation = nativeConstraint.getAttachment( 0ul ).getFrame().getLocalRotate().ToHandedQuaternion();

      if ( bodyNodes.Length > 1 )
        constraint.AttachmentPair.ConnectedFrame.SetParent( bodyNodes[ 1 ].GameObject );
      else
        constraint.AttachmentPair.ConnectedFrame.SetParent( FileInfo.PrefabInstance );

      constraint.AttachmentPair.ConnectedFrame.LocalPosition = nativeConstraint.getAttachment( 1ul ).getFrame().getLocalTranslate().ToHandedVector3();
      constraint.AttachmentPair.ConnectedFrame.LocalRotation = nativeConstraint.getAttachment( 1ul ).getFrame().getLocalRotate().ToHandedQuaternion();

      constraint.AttachmentPair.Synchronized = constraintType != ConstraintType.DistanceJoint;

      return true;
    }

    private bool CreateWire( Node node )
    {
      var nativeWire = m_tree.GetWire( node.Uuid );
      if ( nativeWire == null ) {
        Debug.LogWarning( "Unable to find native instance of wire: " + node.GameObject.name +
                          " (UUID: " + node.Uuid.str() + ")" );
        return false;
      }

      Func<agx.RigidBody, GameObject> findRigidBody = ( nativeRb ) =>
      {
        if ( nativeRb == null )
          return FileInfo.PrefabInstance;

        // Do not reference lumped nodes!
        if ( agxWire.Wire.isLumpedNode( nativeRb ) )
          return FileInfo.PrefabInstance;

        Node rbNode = m_tree.GetNode( nativeRb.getUuid() );
        if ( rbNode == null ) {
          Debug.LogWarning( "Unable to find reference rigid body: " + nativeRb.getName() + " (UUID: " + nativeRb.getUuid().str() + ")" );
          return FileInfo.PrefabInstance;
        }
        if ( rbNode.GameObject == null ) {
          Debug.LogWarning( "Referenced native rigid body hasn't a game object: " + nativeRb.getName() + " (UUID: " + rbNode.Uuid.str() + ")" );
          return FileInfo.PrefabInstance;
        }

        return rbNode.GameObject;
      };

      var wire  = node.GameObject.GetOrCreateComponent<Wire>();
      var route = wire.Route;

      route.Clear();

      node.GameObject.GetOrCreateComponent<WireRenderer>();

      wire.RestoreLocalDataFrom( nativeWire );

      var nativeIt         = nativeWire.getRenderBeginIterator();
      var nativeEndIt      = nativeWire.getRenderEndIterator();
      var nativeBeginWinch = nativeWire.getWinchController( 0u );
      var nativeEndWinch   = nativeWire.getWinchController( 1u );

      if ( nativeBeginWinch != null ) {
        route.Add( nativeBeginWinch,
                   findRigidBody( nativeBeginWinch.getRigidBody() ) );
      }
      // Connecting nodes will show up in render iterators.
      else if ( nativeIt.get().getNodeType() != agxWire.WireNode.Type.CONNECTING && nativeWire.getFirstNode().getNodeType() == agxWire.WireNode.Type.BODY_FIXED )
        route.Add( nativeWire.getFirstNode(), findRigidBody( nativeWire.getFirstNode().getRigidBody() ) );

      while ( !nativeIt.EqualWith( nativeEndIt ) ) {
        var nativeNode = nativeIt.get();

        // Handing ContactNode and ShapeContactNode parenting.
        GameObject nodeParent = null;
        if ( nativeNode.getType() == agxWire.WireNode.Type.CONTACT ) {
          var nativeGeometry = nativeNode.getAsContact().getGeometry();
          var geometryNode = m_tree.GetNode( nativeGeometry.getUuid() );
          if ( geometryNode != null && geometryNode.GetChildren( Node.NodeType.Shape ).Length > 0 )
            nodeParent = geometryNode.GetChildren( Node.NodeType.Shape )[ 0 ].GameObject;
        }
        else if ( nativeNode.getType() == agxWire.WireNode.Type.SHAPE_CONTACT ) {
          var nativeShape = nativeNode.getAsShapeContact().getShape();
          var shapeNode = m_tree.GetNode( nativeShape.getUuid() );
          if ( shapeNode != null )
            nodeParent = shapeNode.GameObject;
        }

        if ( nodeParent == null )
          nodeParent = findRigidBody( nativeNode.getRigidBody() );

        route.Add( nativeNode, nodeParent );
        nativeIt.inc();
      }

      // Remove last node if we should have a winch or a body fixed node there.
      if ( route.Last().Type == Wire.NodeType.FreeNode && nativeWire.getLastNode().getNodeType() == agxWire.WireNode.Type.BODY_FIXED )
        route.Remove( route.Last() );

      if ( nativeEndWinch != null ) {
        route.Add( nativeEndWinch,
                   findRigidBody( nativeEndWinch.getRigidBody() ) );
      }
      else if ( nativeIt.prev().get().getNodeType() != agxWire.WireNode.Type.CONNECTING && nativeWire.getLastNode().getNodeType() == agxWire.WireNode.Type.BODY_FIXED )
        route.Add( nativeWire.getLastNode(), findRigidBody( nativeWire.getLastNode().getRigidBody() ) );

      wire.Material = RestoreShapeMaterial( wire.Material,
                                            nativeWire.getMaterial(),
                                            wire );

      wire.GetComponent<WireRenderer>().InitializeRenderer();
      // Reset to assign default material.
      wire.GetComponent<WireRenderer>().Material = null;

      // Adding collision group from restored instance since the disabled pair
      // will be read from Space (wire.setEnableCollisions( foo, false ) will
      // work out of the box).
      var collisionGroups = wire.gameObject.GetOrCreateComponent<CollisionGroups>();
      collisionGroups.AddGroup( nativeWire.getGeometryController().getDisabledCollisionsGroupId().ToString(), false );
      foreach ( var id in nativeWire.getGeometryController().getGroupIds() )
        collisionGroups.AddGroup( id.ToString(), false );

      return true;
    }

    private bool CreateCable( Node node )
    {
      var nativeCable = m_tree.GetCable( node.Uuid );
      if ( nativeCable == null ) {
        Debug.LogWarning( "Unable to find native instance of cable: " + node.GameObject.name +
                          " (UUID: " + node.Uuid.str() + ")" );
        return false;
      }

      var cable = node.GameObject.GetOrCreateComponent<Cable>();
      var route = cable.Route;

      route.Clear();

      node.GameObject.GetOrCreateComponent<CableRenderer>();

      cable.RestoreLocalDataFrom( nativeCable );
      cable.RouteAlgorithm = Cable.RouteType.Identity;

      var externalCableProperties = cable.Properties != null &&
                                    !FileInfo.ObjectDb.ContainsAsset( cable.Properties );
      // The user has assigned a cable properties, not located in our data directory,
      // to this cable. We approve this change (with a warning) by not assigning the
      // cable properties from the model.
      if ( externalCableProperties ) {
        Debug.LogWarning( $"Friction Model {cable.Properties.name} is external from the re-imported model. " +
                          "No changes will be made.", cable );
      }
      else {
        cable.Properties = FileInfo.ObjectDb.GetOrCreateAsset( cable.Properties,
                                                             $"{cable.name}_properties",
                                                             p => p.RestoreLocalDataFrom( nativeCable.getCableProperties(),
                                                                                          nativeCable.getCablePlasticity() ) );
      }

      for ( var it = nativeCable.getSegments().begin(); !it.EqualWith( nativeCable.getSegments().end() ); it.inc() ) {
        var segment = it.get();
        route.Add( segment, attachment =>
                            {
                              if ( attachment == null || attachment.getRigidBody() == null )
                                return FileInfo.PrefabInstance;
                              var rbNode = m_tree.GetNode( attachment.getRigidBody().getUuid() );
                              if ( rbNode == null ) {
                                Debug.LogWarning( "Unable to find rigid body in cable attachment." );
                                return FileInfo.PrefabInstance;
                              }
                              return rbNode.GameObject;
                            } );
      }

      cable.Material = RestoreShapeMaterial( cable.Material,
                                             nativeCable.getMaterial(),
                                             cable );

      cable.GetComponent<CableRenderer>().InitializeRenderer();
      cable.GetComponent<CableRenderer>().Material = null;

      // Adding collision group from restored instance since the disabled pair
      // will be read from Space (cable.setEnableCollisions( foo, false ) will
      // work out of the box).
      var collisionGroups = cable.gameObject.GetOrCreateComponent<CollisionGroups>();
      collisionGroups.AddGroup( nativeCable.getUniqueId().ToString(), false );
      var referencedGroups = node.GetReferences( Node.NodeType.GroupId );
      foreach ( var group in referencedGroups )
        if ( group.Object is string )
          collisionGroups.AddGroup( group.Object as string, false );

      return true;
    }

    private ShapeMaterial RestoreShapeMaterial( ShapeMaterial currentShapeMaterial,
                                                agx.Material material,
                                                UnityEngine.Object context )
    {
      if ( material == null )
        return null;

      // Re-import: The user has assigned a ShapeMaterial that isn't in our
      //            data directory. We approve this modification by not reading
      //            data from 'material' and returning the current assigned material.
      if ( currentShapeMaterial != null && !FileInfo.ObjectDb.ContainsAsset( currentShapeMaterial ) ) {
        Debug.LogWarning( $"Shape Material {currentShapeMaterial.name} is external from the re-imported model. " +
                          "No changes will be made.", context );
        return currentShapeMaterial;
      }

      var materialNode = m_tree.GetNode( material.getUuid() );
      // Re-import: The user may have assigned a ShapeMaterial that is in our data
      //            directory but doesn't match the material in the model. This
      //            change will go without warning because we don't know how
      //            ShapeMaterial assets maps to agx.Material instances. I.e.,
      //            there's no UUID for ShapeMaterial.
      //
      //            If the user desires to have a different material on this object
      //            the user should assign a ShapeMaterial from a directory different
      //            from our data directory (caught in the if-statement above),
      //            resulting in currentShapeMaterial to be used instead.
      return FileInfo.ObjectDb.GetOrCreateAsset( materialNode.Asset as ShapeMaterial ??
                                                   currentShapeMaterial, // Important during re-import because materialNode.Asset is null.
                                                 FindName( material.getName(),
                                                           materialNode.Type.ToString() ),
                                                 m =>
                                                 {
                                                   m.RestoreLocalDataFrom( material );
                                                   // If node.Asset == null, assigning it here
                                                   // in first ref to the material.
                                                   materialNode.Asset = m;
                                                 } );
    }

    private static void RestoreLocalDataFrom( Material thisMaterial, agxCollide.RenderMaterial nativeMaterial )
    {
      if ( nativeMaterial.hasDiffuseColor() ) {
        var color = nativeMaterial.getDiffuseColor().ToColor();
        color.a = 1.0f - nativeMaterial.getTransparency();

        thisMaterial.SetVector( "_Color", color );
      }
      if ( nativeMaterial.hasEmissiveColor() )
        thisMaterial.SetVector( "_EmissionColor", nativeMaterial.getEmissiveColor().ToColor() );

      thisMaterial.SetFloat( "_Metallic", 0.3f );
      thisMaterial.SetFloat( "_Glossiness", 0.8f );

      if ( nativeMaterial.getTransparency() > 0.0f )
        thisMaterial.SetBlendMode( BlendMode.Transparent );
    }

    private Dictionary<uint, Material> m_materialLibrary = new Dictionary<uint, Material>();

    private Material MaterialFactory( agxCollide.RenderMaterial nativeMaterial )
    {
      var material = GetMaterial( nativeMaterial );
      if ( material == null ) {
        material = new Material( Shader.Find( "Standard" ) ?? Shader.Find( "Diffuse" ) );
        UpdateMaterialLibrary( material, nativeMaterial );
      }

      return material;
    }

    private Material GetMaterial( agxCollide.RenderMaterial nativeMaterial )
    {
      Material material = null;
      m_materialLibrary.TryGetValue( nativeMaterial.getHash(), out material );
      return material;
    }

    private Material UpdateMaterialLibrary( Material material,
                                            agxCollide.RenderMaterial nativeMaterial )
    {
      // Material read from a model (re-import) or is newly created, map
      // instance to the native material hash.
      if ( material != null && GetMaterial( nativeMaterial ) == null )
        m_materialLibrary.Add( nativeMaterial.getHash(), material );
      return GetMaterial( nativeMaterial );
    }

    private Material RestoreRenderMaterial( Material material,
                                            string name,
                                            agxCollide.RenderMaterial nativeMaterial,
                                            UnityEngine.Object context )
    {
      // The user is referencing a material that isn't in our data directory.
      // We should not couple this material to our native hash when it could
      // result in other instances referencing materials in our directory to
      // get this material instead.
      if ( material != null && !FileInfo.ObjectDb.ContainsAsset( material ) ) {
        Debug.LogWarning( $"Visual material {material.name} is external from the re-imported model. " +
                          "No changes will be made.", context );
        return material;
      }

      return FileInfo.ObjectDb.GetOrCreateMaterial( UpdateMaterialLibrary( material, nativeMaterial ),
                                                    name,
                                                    m => RestoreLocalDataFrom( m, nativeMaterial ),
                                                    () => MaterialFactory( nativeMaterial ) );
    }

    private string FindName( string name, string typeName )
    {
      if ( name == "" )
        name = typeName;

      string result = name;
      int counter = 1;
      while ( m_names.Contains( result ) )
        result = name + " (" + ( counter++ ) + ")";

      m_names.Add( result );

      return result;
    }

    internal class SubProgress : IDisposable
    {
      public SubProgress( ProgressBar progressBar, ProgressBar.StateType target, string name, int numSubSteps )
      {
        m_progressBar = progressBar;
        m_target = target;
        m_numSubSteps = numSubSteps;

        ShowProgress( name );
      }

      public void Dispose()
      {
        m_progressBar.Progress();
      }

      public void Tick( string name )
      {
        ShowProgress( name );
      }

      public void Tack()
      {
        ++m_counter;
      }

      private void ShowProgress( string name )
      {
        EditorUtility.DisplayProgressBar( m_progressBar.Title,
                                          name,
                                          m_progressBar.GetSubProgressStart( m_target - 1 ) + ( (float)m_counter / m_numSubSteps ) * m_progressBar.GetSubProgressDelta( m_target ) );
      }

      private ProgressBar m_progressBar = null;
      private ProgressBar.StateType m_target = ProgressBar.StateType.Initial;
      private int m_numSubSteps = 1;
      private int m_counter = 0;
    }

    internal class ProgressBar : IDisposable
    {
      public enum StateType
      {
        Initial,
        ReadingFile,
        CreatingSimulationTree,
        GeneratingObjects,
        CreatingPrefab,
        Done
      }

      public string Title = "";

      public SubProgress Progress( string name, int numSubSteps )
      {
        return new SubProgress( this, m_state + 1, name, numSubSteps );
      }

      public void Progress()
      {
        if ( m_state == StateType.Done )
          return;

        m_state = m_state + 1;
      }

      public void Dispose()
      {
        EditorUtility.ClearProgressBar();
      }

      public float GetSubProgressStart( StateType start )
      {
        return m_subProgress[ (int)start ];
      }

      public float GetSubProgressDelta( StateType target )
      {
        if ( target == StateType.Initial )
          return 0.0f;
        return m_subProgress[ (int)target ] - m_subProgress[ (int)( target - 1 ) ];
      }

      private StateType m_state = StateType.Initial;
      //                                            Init, Read, Create, Gen, Prefab, Done
      private float[] m_subProgress = new float[] { 0.0f, 0.33f, 0.4f, 0.95f, 1.0f, 1.0f };
    }

    private Tree m_tree = new Tree();
    private HashSet<string> m_names = new HashSet<string>();
    private ProgressBar m_progressBar = null;
  }
}
