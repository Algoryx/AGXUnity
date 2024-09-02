using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using Brick.Simulation;
using System;
using UnityEngine;

using Bodies = Brick.Physics3D.Bodies;
using Charges = Brick.Physics3D.Charges;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  public struct MapperOptions
  {
    public MapperOptions( bool hideMeshes = false, bool hideVisuals = false, bool ignoreDisabledMeshes = false )
    {
      HideMeshesInHierarchy = hideMeshes;
      HideVisualMaterialsInHierarchy = hideVisuals;
      IgnoreDisabledMeshes = ignoreDisabledMeshes;
    }

    public bool HideMeshesInHierarchy;
    public bool HideVisualMaterialsInHierarchy;
    public bool IgnoreDisabledMeshes;
  }

  public class BrickUnityMapper
  {
    public MapperData Data { get; } = new MapperData();

    public GameObject RootNode => Data.RootNode;
    public Material VisualMaterial => Data.VisualMaterial;

    private InteractionMapper InteractionMapper { get; set; }
    private TrackMapper TrackMapper { get; set; }

    MapperOptions Options;

    public BrickUnityMapper( MapperOptions options = new MapperOptions() )
    {
      Data.VisualMaterial = ShapeVisual.CreateDefaultMaterial();
      Data.VisualMaterial.hideFlags = HideFlags.HideInHierarchy;
      Data.ErrorReporter = new Brick.ErrorReporter();
      Data.DefaultMaterial = ShapeMaterial.CreateInstance<ShapeMaterial>();
      Data.DefaultMaterial.Density = 1000;
      Data.DefaultMaterial.name = "Default";

      Options = options;

      InteractionMapper = new InteractionMapper( Data );
      TrackMapper = new TrackMapper( Data );
    }

    public GameObject MapObject( Object obj, GameObject rootNode )
    {
      Data.RootNode = rootNode;
      Data.PrefabLocalData = rootNode.AddComponent<SavedPrefabLocalData>();
      if ( obj is Brick.Physics3D.System system )
        Utils.AddChild( RootNode, MapSystem( system ), Data.ErrorReporter, system );
      else if ( obj is Bodies.RigidBody body )
        Utils.AddChild( RootNode, MapBody( body ), Data.ErrorReporter, body );

      var signals = Data.RootNode.AddComponent<BrickSignals>();
      MapSignals( obj, signals );

      return RootNode;
    }

    private void MapSignals( Object obj, BrickSignals signals )
    {
      foreach ( var subsystem in obj.getValues<Brick.Physics3D.System>() )
        MapSignals( subsystem, signals );

      foreach ( var output in obj.getValues<Brick.Physics.Signals.Output>() )
        signals.RegisterSignal( output.getName() );

      foreach ( var input in obj.getValues<Brick.Physics.Signals.Input>() )
        signals.RegisterSignal( input.getName() );

    }

    Tuple<GameObject, bool> MapCachedVisual( agxCollide.Shape shape, agx.AffineMatrix4x4 transform, Brick.Visuals.Geometries.Geometry visual )
    {
      GameObject go = new GameObject();

      var rd      = shape.getRenderData();

      var filter = go.AddComponent<MeshFilter>();
      var renderer = go.AddComponent<MeshRenderer>();

      renderer.enabled = rd.getShouldRender();

      // TODO: Should these be cached? Can they?
      var mesh = AGXMeshToUnityMesh(rd.getVertexArray(),rd.getIndexArray());
      if ( Options.HideMeshesInHierarchy )
        mesh.hideFlags = HideFlags.HideInHierarchy;
      mesh.name = visual.getName();
      Data.CacheMappedMeshes.Add( mesh );
      filter.mesh = mesh;

      var rm = rd.getRenderMaterial();
      if ( rm != null ) {
        if ( !Data.MappedRenderMaterialCache.TryGetValue( rm.getHash(), out Material mat ) ) {
          mat = new Material( Shader.Find( "Standard" ) );
          mat.RestoreLocalDataFrom( rm );
          if ( rm.getName() != "" )
            mat.name = rm.getName();
          else
            mat.name = rm.getHash().ToString();
          if ( Options.HideVisualMaterialsInHierarchy )
            mat.hideFlags = HideFlags.HideInHierarchy;
          Data.MappedRenderMaterialCache[ rm.getHash() ] = mat;
          Data.CacheMappedMaterials.Add( mat );
        }

        renderer.material = mat;
        return Tuple.Create( go, true );
      }

      return Tuple.Create( go, false );
    }

    GameObject MapVisualGeometry( Brick.Visuals.Geometries.Geometry visual )
    {
      GameObject go = null;
      bool cachedMat = false;
      var uuid_annots = visual.findAnnotations("uuid");
      foreach ( var uuid_annot in uuid_annots ) {
        if ( uuid_annot.isString() ) {
          var uuid = uuid_annot.asString();
          var shape = Data.AgxCache.readCollisionShapeAndTransformCS( uuid );
          if ( shape != null ) {
            (go, cachedMat) = MapCachedVisual( shape.first, shape.second, visual );

            if ( go == null ) {
              Debug.Log( "uh oh" );
              return null;
            }
          }
        }
      }

      if ( go == null ) {
        go = visual switch
        {
          Brick.Visuals.Geometries.Box box => GameObject.CreatePrimitive( PrimitiveType.Cube ),
          Brick.Visuals.Geometries.Cylinder cyl => GameObject.CreatePrimitive( PrimitiveType.Cylinder ),
          Brick.Visuals.Geometries.ExternalTriMeshGeometry etmg => null,
          Brick.Visuals.Geometries.Sphere sphere => GameObject.CreatePrimitive( PrimitiveType.Sphere ),
          _ => null
        };

        switch ( visual ) {
          case Brick.Visuals.Geometries.Box box:
            go.transform.localScale = box.size().ToVector3();
            break;
          case Brick.Visuals.Geometries.Cylinder cyl:
            go.transform.localScale = new Vector3( (float)cyl.radius(), (float)cyl.height(), (float)cyl.radius() );
            break;
          case Brick.Visuals.Geometries.Sphere sphere:
            go.transform.localScale = Vector3.one * (float)sphere.radius();
            break;
          default:
            break;
        };
      }

      if ( go == null )
        return Utils.ReportUnimplemented<GameObject>( visual, Data.ErrorReporter );

      BrickObject.RegisterGameObject( visual.getName(), go );
      Utils.mapLocalTransform( go.transform, visual.local_transform() );
      if ( !cachedMat )
        go.GetComponent<MeshRenderer>().material = Data.VisualMaterial;

      return go;
    }

    GameObject CreateShape<UnityType, BrickType>( BrickType brick, Action<BrickType, UnityType> setup )
      where UnityType : Shape
      where BrickType : Charges.ContactGeometry
    {
      GameObject go = Factory.Create<UnityType>();
      setup( brick, go.GetComponent<UnityType>() );
      return go;
    }

    UnityEngine.Mesh AGXMeshToUnityMesh( agx.Vec3Vector vertices, agx.UInt32Vector indices )
    {
      var outMesh = new UnityEngine.Mesh();
      Vector3[] uVertices = new Vector3[vertices.Count];
      for ( int i = 0; i < vertices.Count; i++ )
        uVertices[ i ].Set( (float)-vertices[ i ].x, (float)vertices[ i ].y, (float)vertices[ i ].z );
      outMesh.vertices = uVertices;
      if ( vertices.Count > UInt16.MaxValue )
        outMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

      int[] uIndices = new int[indices.Count];
      for ( int i = 0; i < indices.Count; i += 3 ) {
        uIndices[ i ]     = (int)indices[ i ];
        uIndices[ i + 1 ] = (int)indices[ i + 2 ];
        uIndices[ i + 2 ] = (int)indices[ i + 1 ];
      }
      outMesh.SetIndices( uIndices, MeshTopology.Triangles, 0 );
      outMesh.RecalculateBounds();
      outMesh.RecalculateNormals();
      return outMesh;
    }

    GameObject MapExternalTriMesh( Charges.ExternalTriMeshGeometry objGeom )
    {
      //std::filesystem::path source_id = m_source_id;
      string path = objGeom.path();
      Debug.Log( $"External obj file path: {path}" );

      GameObject go = Factory.Create<AGXUnity.Collide.Mesh>();
      var mesh = go.GetComponent<AGXUnity.Collide.Mesh>();

      if ( !System.IO.Path.IsPathFullyQualified( path ) ) {
        // TODO: Error reporting
        //var name = Brick::Internal::split(obj_geometry.getName(), ".").back();
        //var member = obj_geometry.getOwner()->getType()->findFirstMember(name);
        //var token = member->isVarDeclaration() ? member->asVarDeclaration()->getNameToken() : member->asVarAssignment()->getTargetSegments().back();
        //m_error_reporter->reportError( StringParameterError::create( AGXBrickError::PathNotAbsolute, token.line, token.column, m_source_id, obj_geometry.path() ) );
        //geometry = new agxCollide::Geometry();
      }
      else {
#if UNITY_EDITOR
        var assetPath = "Assets/" + System.IO.Path.GetRelativePath(Application.dataPath,path).Replace('\\','/');

        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>( assetPath );
        UnityEditor.SerializedObject s = new UnityEditor.SerializedObject(source);

        var readable = s.FindProperty( "m_IsReadable" );
        if ( !readable.boolValue ) {
          Debug.LogWarning( $"Mesh at path '{assetPath}' was not readable. Marking the mesh as readable..." );
          readable.boolValue = true;
          s.ApplyModifiedProperties();
        }

        mesh.AddSourceObject( source );
        mesh.transform.localScale = objGeom.scale().ToVector3();
#else
        var source = agxUtil.agxUtilSWIG.createTrimesh(path, (uint)agxCollide.Trimesh.TrimeshOptionsFlags.REMOVE_DUPLICATE_VERTICES, new agx.Matrix3x3(objGeom.scale().ToVec3()));
        mesh.AddSourceObject( AGXMeshToUnityMesh( source ) );
#endif
        //agxCollide::ShapeRef mesh = agxUtil::TrimeshReaderWriter::createTrimesh(path.string(), agxCollide::Trimesh::REMOVE_DUPLICATE_VERTICES, agx::Matrix3x3(mapVec3(obj_geometry.scale())));
        //if ( mesh == nullptr ) {
        //  auto name = Brick::Internal::split(obj_geometry.getName(), ".").back();
        //  auto member = obj_geometry.getOwner()->getType()->findFirstMember(name);
        //  auto token = member->isVarDeclaration() ? member->asVarDeclaration()->getNameToken() : member->asVarAssignment()->getTargetSegments().back();
        //  m_error_reporter->reportError( Error::create( AGXBrickError::InvalidObjFile, token.line, token.column, m_source_id ) );
        //  geometry = new agxCollide::Geometry();
        //}
        //else {
        //  geometry = new agxCollide::Geometry( mesh );
        //}
      }

      return go;
    }

    private T CreateShapeHelper<T>( ref GameObject go )
      where T : Shape
    {
      go = Factory.Create<T>();
      return go.GetComponent<T>();
    }

    GameObject MapCachedShape( agxCollide.Shape shape, Charges.ContactGeometry geom )
    {
      var type = (agxCollide.Shape.Type)shape.getType();
      GameObject go = null;

      if ( type == agxCollide.Shape.Type.BOX ) {
        var box = CreateShapeHelper<Box>(ref go);
        box.HalfExtents = shape.asBox().getHalfExtents().ToVector3();
      }
      else if ( type == agxCollide.Shape.Type.CYLINDER ) {
        var cylinder    = CreateShapeHelper<Cylinder>(ref go);
        cylinder.Radius = Convert.ToSingle( shape.asCylinder().getRadius() );
        cylinder.Height = Convert.ToSingle( shape.asCylinder().getHeight() );
      }
      else if ( type == agxCollide.Shape.Type.HOLLOW_CYLINDER ) {
        var hollowCylinder       = CreateShapeHelper<HollowCylinder>( ref go );
        hollowCylinder.Thickness = Convert.ToSingle( shape.asHollowCylinder().getThickness() );
        hollowCylinder.Radius    = Convert.ToSingle( shape.asHollowCylinder().getOuterRadius() );
        hollowCylinder.Height    = Convert.ToSingle( shape.asHollowCylinder().getHeight() );
      }
      else if ( type == agxCollide.Shape.Type.CONE ) {
        var cone          = CreateShapeHelper < Cone >(ref go);
        cone.BottomRadius = Convert.ToSingle( shape.asCone().getBottomRadius() );
        cone.TopRadius    = Convert.ToSingle( shape.asCone().getTopRadius() );
        cone.Height       = Convert.ToSingle( shape.asCone().getHeight() );
      }
      else if ( type == agxCollide.Shape.Type.HOLLOW_CONE ) {
        var hollowCone          = CreateShapeHelper < HollowCone >(ref go);
        hollowCone.Thickness    = Convert.ToSingle( shape.asHollowCone().getThickness() );
        hollowCone.BottomRadius = Convert.ToSingle( shape.asHollowCone().getBottomOuterRadius() );
        hollowCone.TopRadius    = Convert.ToSingle( shape.asHollowCone().getTopOuterRadius() );
        hollowCone.Height       = Convert.ToSingle( shape.asHollowCone().getHeight() );
      }
      else if ( type == agxCollide.Shape.Type.CAPSULE ) {
        var capsule    = CreateShapeHelper < Capsule >(ref go);
        capsule.Radius = Convert.ToSingle( shape.asCapsule().getRadius() );
        capsule.Height = Convert.ToSingle( shape.asCapsule().getHeight() );
      }
      else if ( type == agxCollide.Shape.Type.SPHERE ) {
        var sphere    = CreateShapeHelper < Sphere >(ref go);
        sphere.Radius = Convert.ToSingle( shape.asSphere().getRadius() );
      }
      else if ( type == agxCollide.Shape.Type.CONVEX ||
                type == agxCollide.Shape.Type.TRIMESH ||
                type == agxCollide.Shape.Type.HEIGHT_FIELD ) {
        var mesh          = CreateShapeHelper < Collide.Mesh >(ref go);
        var collisionData = shape.asMesh().getMeshData();
        var nativeToWorld = shape.getTransform();
        var meshToLocal   = mesh.transform.worldToLocalMatrix;

        var meshSource = AGXMeshToUnityMesh( collisionData.getVertices(), collisionData.getIndices());
        meshSource.name = geom.getName();
        if ( Options.HideMeshesInHierarchy )
          meshSource.hideFlags    = HideFlags.HideInHierarchy;
        Data.CacheMappedMeshes.Add( meshSource );
        mesh.AddSourceObject( meshSource );

        //var meshes        = MeshSplitter.Split( collisionData.getVertices(),
        //                                        collisionData.getIndices(),
        //                                        v => v.ToHandedVector3()).Meshes;
        //foreach ( var meshSource in meshes ) {
        //  meshSource.name = geom.getName();
        //  Data.CacheMappedMeshes.Add( meshSource );
        //  mesh.AddSourceObject( meshSource );
        //}
      }
      else {
        Debug.LogWarning( "Unsupported shape type: " + type );
        return null;
      }

      return go;
    }

    private bool ShapeIsMeshType( agxCollide.Shape shape )
    {
      var type = (agxCollide.Shape.Type)shape.getType();
      return type == agxCollide.Shape.Type.CONVEX ||
             type == agxCollide.Shape.Type.TRIMESH ||
             type == agxCollide.Shape.Type.HEIGHT_FIELD;
    }

    GameObject MapContactGeometry( Charges.ContactGeometry geom, bool addVisuals )
    {
      GameObject go = null;
      var uuid_annots = geom.findAnnotations("uuid");
      foreach ( var uuid_annot in uuid_annots ) {
        if ( uuid_annot.isString() ) {
          var uuid = uuid_annot.asString();
          var shape = Data.AgxCache.readCollisionShapeCS( uuid );
          if ( shape != null ) {
            if ( Options.IgnoreDisabledMeshes && !geom.enable_collisions() && ShapeIsMeshType( shape ) )
              return null;

            go = MapCachedShape( shape, geom );

            if ( go == null ) {
              Debug.Log( "uh oh" );
              return null;
            }
          }
        }
      }

      if ( go == null ) {
        go = geom switch
        {
          Charges.Box box => CreateShape<Box, Charges.Box>( box, ( bbox, ubox ) => ubox.HalfExtents =  bbox.size().ToVector3()/2 ),
          Charges.Cylinder cyl => CreateShape<Cylinder, Charges.Cylinder>( cyl, ( bcyl, ucyl ) =>
        {
          ucyl.Radius = (float)bcyl.radius();
          ucyl.Height = (float)bcyl.height();
        } ),
          Charges.Sphere sphere => CreateShape<Sphere, Charges.Sphere>( sphere, ( bsphere, usphere ) => usphere.Radius = (float)bsphere.radius() ),
          Charges.Capsule cap => CreateShape<Capsule, Charges.Capsule>( cap, ( bcap, ucap ) =>
        {
          ucap.Radius = (float)bcap.radius();
          ucap.Height = (float)bcap.height();
        } ),
          Charges.ExternalTriMeshGeometry etm => MapExternalTriMesh( etm ),
          _ => null
        };
      }

      if ( go == null )
        return Utils.ReportUnimplemented<GameObject>( geom, Data.ErrorReporter );

      BrickObject.RegisterGameObject( geom.getName(), go );

      if ( addVisuals ) {
        var visualGO = ShapeVisual.Create( go.GetComponent<Shape>() );
        var visual = visualGO.GetComponent<ShapeVisual>();
        visual.SetMaterial( VisualMaterial );
      }

      Utils.MapLocalTransform( go.transform, geom.local_transform() );
      var shapeComp = go.GetComponent<Shape>();
      shapeComp.CollisionsEnabled = geom.enable_collisions();
      // TODO: Replace this if this property is added to Brick.
      // For now, assume that we dont want automatic calculation for disabled geometries
      //shapeComp.EnableMassProperties = geom.enable_collisions();

      // TODO: This does not properly check whether it is the default material
      if ( geom.material().getName() != "Physics.Charges.Material" )
        if ( Data.MaterialCache.TryGetValue( geom.material(), out ShapeMaterial sm ) )
          shapeComp.Material = sm;

      Data.GeometryCache[ geom ] = shapeComp;
      return go;
    }

    bool InertiaTensorIsSet( Brick.Math.Matrix3x3 inertia_tensor )
    {
      return inertia_tensor.e00() != 0.0
          || inertia_tensor.e11() != 0.0
          || inertia_tensor.e22() != 0.0;
    }

    bool MapMassProperties( MassProperties mp, Bodies.Inertia inertia, Brick.Physics3D.Transform cm )
    {
      if ( inertia.mass() > 0.0 )
        mp.Mass.UserValue = (float)inertia.mass();

      else if ( inertia.mass() < 0.0 ) {
        // TODO: Error Reporting
        //auto token = inertia.getOwner()->getType()->getNameToken();
        //m_error_reporter->reportError( Error::create( AGXBrickError::NegativeMass, token.line, token.column, m_source_id ) );
        return false;
      }

      var cm_transform_is_set =
           !cm.position().isDefault("x")
        || !cm.position().isDefault("y")
        || !cm.position().isDefault("z")
        || !cm.rotation().isDefault("x")
        || !cm.rotation().isDefault("y")
        || !cm.rotation().isDefault("z")
        || !cm.rotation().isDefault("w");

      if ( cm_transform_is_set ) {
        mp.CenterOfMassOffset.UserValue = cm.position().ToHandedVector3();
        if ( !cm.rotation().isDefault( "x" )
          || !cm.rotation().isDefault( "y" )
          || !cm.rotation().isDefault( "z" )
          || !cm.rotation().isDefault( "w" ) )
          // TODO: Proper warning passed to importer
          Debug.LogWarning( "AGXUnity does not support rotated Center of mass frames" );
      }

      var inertia_tensor = inertia.tensor();
      var inertia_tensor_is_set = InertiaTensorIsSet(inertia_tensor);
      if ( inertia_tensor_is_set ) {
        mp.InertiaDiagonal.UserValue = new Vector3( (float)inertia_tensor.e00(), (float)inertia_tensor.e11(), (float)inertia_tensor.e22() );
        mp.InertiaOffDiagonal.UserValue = new Vector3( (float)inertia_tensor.e01(), (float)inertia_tensor.e02(), (float)inertia_tensor.e12() );
      }

      mp.Mass.UseDefault = inertia.mass() <= 0.0;
      mp.InertiaDiagonal.UseDefault = !inertia_tensor_is_set;
      mp.InertiaOffDiagonal.UseDefault = !inertia_tensor_is_set;

      mp.CenterOfMassOffset.UseDefault = !cm_transform_is_set;

      return true;
    }

    GameObject MapBody( Bodies.RigidBody body )
    {
      GameObject rb = Factory.Create<RigidBody>();
      BrickObject.RegisterGameObject( body.getName(), rb );
      var rbComp = rb.GetComponent<RigidBody>();
      Utils.MapLocalTransform( rb.transform, body.kinematics().local_transform() );

      MapMassProperties( rbComp.MassProperties, body.inertia(), body.kinematics().local_cm_transform() );

      bool hasVisuals = false;
      foreach ( var visual in body.getValues<Brick.Visuals.Geometries.Geometry>() ) {
        hasVisuals = true;
        Utils.AddChild( rb, MapVisualGeometry( visual ), Data.ErrorReporter, visual );
      }

      foreach ( var geom in body.getValues<Charges.ContactGeometry>() )
        Utils.AddChild( rb, MapContactGeometry( geom, !hasVisuals ), Data.ErrorReporter, geom );

      Data.BodyCache[ body ] = rbComp;
      return rb;
    }

    ShapeMaterial MapMaterial( Brick.Physics.Charges.Material material )
    {
      var sm = ShapeMaterial.CreateInstance<ShapeMaterial>();
      sm.name = material.getName();

      sm.Density = (float)material.density();
      // TODO: AGXUnity does not expose Young's modulus in ShapeMaterial

      return sm;
    }

    void MapShovel( Brick.Terrain.Shovel shovel )
    {
      var body = Data.BodyCache[shovel.body()];
      var mapped = body.gameObject.AddComponent<DeformableTerrainShovel>();
      mapped.TopEdge = Line.Create( body.gameObject, shovel.top_edge().start().ToHandedVector3(), shovel.top_edge().end().ToHandedVector3() );
      mapped.CuttingEdge = Line.Create( body.gameObject, shovel.cutting_edge().start().ToHandedVector3(), shovel.cutting_edge().end().ToHandedVector3() );
      mapped.CuttingDirection = Line.Create( body.gameObject, Vector3.zero, shovel.cutting_direction().ToHandedVector3() );
      mapped.CuttingDirection.Start.LocalRotation = Quaternion.FromToRotation( Vector3.up, shovel.cutting_direction().ToHandedVector3() );
      mapped.AutoAddToTerrains = true;
    }

    void MapSystemToCollisionGroup( Brick.Physics3D.System system, CollisionGroup collision_group )
    {
      if ( Data.SystemCache.ContainsKey( system ) ) {
        var sysGO = Data.SystemCache[ system ];
        var cg = sysGO.GetOrCreateComponent<CollisionGroups>();
        cg.AddGroup( collision_group.getName(), true );
      }
    }

    void MapBodyToCollisionGroup( Bodies.Body body, CollisionGroup collision_group )
    {
      if ( Data.BodyCache.ContainsKey( body ) ) {
        var rb = Data.BodyCache[body];
        var cg = rb.gameObject.GetOrCreateComponent<CollisionGroups>();
        cg.AddGroup( collision_group.getName(), true );
      }
    }

    void MapGeometryToCollisionGroup( Charges.ContactGeometry geometry, CollisionGroup collision_group )
    {
      if ( Data.GeometryCache.ContainsKey( geometry ) ) {
        var shape = Data.GeometryCache[geometry];
        var cg = shape.gameObject.GetOrCreateComponent<CollisionGroups>();
        cg.AddGroup( collision_group.getName(), false );
      }
    }

    void MapCollisionGroup( CollisionGroup collision_group )
    {
      foreach ( var system in collision_group.systems() )
        if ( system is Brick.Physics3D.System system3d )
          MapSystemToCollisionGroup( system3d, collision_group );

      foreach ( var body in collision_group.bodies() )
        if ( body is Bodies.Body body3d )
          MapBodyToCollisionGroup( body3d, collision_group );

      foreach ( var geometry in collision_group.geometries() )
        if ( geometry is Charges.ContactGeometry geometry3d )
          MapGeometryToCollisionGroup( geometry3d, collision_group );
    }

    void MapDisabledPair( DisableCollisionPair pair )
    {
      Data.PrefabLocalData.AddDisabledPair( pair.group1().getName(), pair.group2().getName() );
    }

    GameObject MapSystem( Brick.Physics3D.System system )
    {
      var s = MapSystemPass1( system );
      MapSystemPass2( system );
      MapSystemPass3( system );
      MapSystemPass4( system );

      return s;
    }

    GameObject MapSystemPass1( Brick.Physics3D.System system )
    {
      GameObject s = BrickObject.CreateGameObject(system.getName());
      Utils.MapLocalTransform( s.transform, system.local_transform() );

      Data.SystemCache[ system ] = s;
      var dummyRB = Factory.Create<RigidBody>();
      dummyRB.transform.SetParent( s.transform, false );
      dummyRB.GetComponent<RigidBody>().MotionControl = agx.RigidBody.MotionControl.STATIC;
      dummyRB.name = "System Dummy RB";
      Data.FrameCache[ system ] = dummyRB;

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        Utils.AddChild( s, MapSystemPass1( subSystem ), Data.ErrorReporter, subSystem );

      foreach ( var body in system.getValues<Bodies.RigidBody>() ) {
        foreach ( var geometry in body.getValues<Charges.ContactGeometry>() ) {
          if ( geometry.material().getType().getNameWithNamespace( "." ) != "Physics.Charges.Material" || !geometry.material().isDefault( "density" ) ) {
            if ( !Data.MaterialCache.ContainsKey( geometry.material() ) )
              Data.MaterialCache[ geometry.material() ] = MapMaterial( geometry.material() );
          }
          else
            Data.MaterialCache[ geometry.material() ] = Data.DefaultMaterial;
        }
      }
      foreach ( var trackSystem in system.getValues<Brick.Vehicles.Tracks.System>() ) {
        if ( trackSystem.belt().link_description() is Brick.Vehicles.Tracks.BoxLinkDescription desc ) {
          var mat = desc.contact_geometry().material();
          if ( !Data.MaterialCache.ContainsKey( mat ) ) {
            Data.MaterialCache[ mat ] = MapMaterial( mat );
          }
        }
      }

      return s;
    }

    void MapSystemPass2( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        MapSystemPass2( subSystem );

      // Physics1D RotationalBodies are mapped at runtime by the RuntimeMapper

      foreach ( var body in system.getValues<Bodies.RigidBody>() ) {
        if ( !Data.BodyCache.ContainsKey( body ) )
          Utils.AddChild( s, MapBody( body ), Data.ErrorReporter, body );
      }

      // TODO: Map terrains
    }

    void MapSystemPass3( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        MapSystemPass3( subSystem );

      foreach ( var trackSystem in system.getValues<Brick.Vehicles.Tracks.System>() )
        TrackMapper.MapTrackSystem( trackSystem );

      foreach ( var mateConnector in system.getValues<Brick.Physics3D.Charges.MateConnector>() )
        InteractionMapper.MapMateConnectorInitial( mateConnector, s );

      foreach ( var body in system.getValues<Brick.Physics3D.Bodies.RigidBody>() )
        foreach ( var mateConnector in body.getValues<Brick.Physics3D.Charges.MateConnector>() )
          InteractionMapper.MapMateConnectorInitial( mateConnector, Data.BodyCache[ body ].gameObject );
    }

    void MapSystemPass4( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        MapSystemPass4( subSystem );

      foreach ( var interaction in system.getValues<Brick.Physics.Interactions.Interaction>() )
        if ( !Utils.IsRuntimeMapped( interaction ) )
          Utils.AddChild( s, InteractionMapper.MapInteraction( interaction, system ), Data.ErrorReporter, interaction );

      foreach ( var contactModel in system.getValues<Brick.Physics.Interactions.SurfaceContact.Model>() )
        InteractionMapper.MapContactModel( contactModel );

      foreach ( var shovel in system.getValues<Brick.Terrain.Shovel>() )
        MapShovel( shovel );

      // Physics1D and Drivetrain interactions are mapped at runtime by the RuntimeMapper

      foreach ( var collision_group in system.getValues<CollisionGroup>() )
        MapCollisionGroup( collision_group );


      foreach ( var rb in system.kinematically_controlled() )
        Data.BodyCache[ rb ].MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      foreach ( var disabledPair in system.getValues<Brick.Simulation.DisableCollisionPair>() )
        MapDisabledPair( disabledPair );
    }
  }
}