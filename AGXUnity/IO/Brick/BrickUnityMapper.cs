using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using Brick.Simulation;
using System;
using System.Linq;
using UnityEngine;

using Bodies = Brick.Physics3D.Bodies;
using Charges = Brick.Physics3D.Charges;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  public class BrickUnityMapper
  {
    public MapperData Data { get; } = new MapperData();

    public GameObject RootNode => Data.RootNode;
    public Material VisualMaterial => Data.VisualMaterial;

    private InteractionMapper InteractionMapper { get; set; }
    private TrackMapper TrackMapper { get; set; }

    public BrickUnityMapper()
    {
      Data.VisualMaterial = ShapeVisual.CreateDefaultMaterial();
      Data.VisualMaterial.hideFlags = HideFlags.HideInHierarchy;
      Data.ErrorReporter = new Brick.ErrorReporter();

      InteractionMapper = new InteractionMapper( Data );
      TrackMapper = new TrackMapper( Data );
    }

    public GameObject MapObject( Object obj, GameObject rootNode )
    {
      Data.RootNode = rootNode;
      Data.PrefabLocalData = rootNode.AddComponent<SavedPrefabLocalData>();
      if ( obj is Brick.Physics3D.System system )
        Utils.AddChild( RootNode, mapSystem( system ), Data.ErrorReporter, system );
      else if ( obj is Bodies.RigidBody body )
        Utils.AddChild( RootNode, mapBody( body ), Data.ErrorReporter, body );

      var signals = Data.RootNode.AddComponent<BrickSignals>();
      mapSignals( obj, signals );

      return RootNode;
    }

    private void mapSignals( Object obj, BrickSignals signals )
    {
      foreach ( var subsystem in obj.getValues<Brick.Physics3D.System>() )
        mapSignals( subsystem, signals );

      foreach ( var output in obj.getValues<Brick.Physics.Signals.Output>() )
        signals.RegisterSignal( output.getName() );

      foreach ( var input in obj.getValues<Brick.Physics.Signals.Input>() )
        signals.RegisterSignal( input.getName() );

    }

    GameObject mapCachedVisual( agxCollide.Shape shape, agx.AffineMatrix4x4 transform, Brick.Visuals.Geometries.Geometry visual )
    {
      GameObject go = new GameObject();

      var rd      = shape.getRenderData();
      var meshes  = MeshSplitter.Split( rd.getVertexArray(),
                                        rd.getIndexArray(),
                                        v => v.ToHandedVector3()).Meshes;

      var filter = go.AddComponent<MeshFilter>();
      var renderer = go.AddComponent<MeshRenderer>();

      meshes[ 0 ].name = visual.getName();
      filter.mesh = meshes[ 0 ];
      Data.CacheMappedMeshes.Add( meshes[ 0 ] );

      return go;
    }

    //GameObject mapVisuaExternallTrimesh( Brick.Visuals.Geometries.ExternalTriMeshGeometry mesh )
    //{
    //  var go = new GameObject();
    //  var filter = go.AddComponent<MeshFilter>();
    //  var renderer = go.AddComponent<MeshRenderer>();

    //  var meshes        = MeshSplitter.Split( collisionData.getVertices(),
    //                                          collisionData.getIndices(),
    //                                          v => v.ToHandedVector3()).Meshes;
    //}

    GameObject mapVisualGeometry( Brick.Visuals.Geometries.Geometry visual )
    {
      GameObject go = null;
      var uuid_annots = visual.getType().findAnnotations("uuid");
      foreach ( var uuid_annot in uuid_annots ) {
        if ( uuid_annot.isString() ) {
          var uuid = uuid_annot.asString();
          var shape = Data.AgxCache.readCollisionShapeAndTransformCS( uuid );
          if ( shape != null ) {
            go = mapCachedVisual( shape.first, shape.second, visual );

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

    UnityEngine.Mesh AGXMeshToUnityMesh( agxCollide.Mesh inMesh )
    {
      var outMesh = new UnityEngine.Mesh();
      var md = inMesh.getMeshData();
      outMesh.vertices = md.getVertices().Select( v => v.ToHandedVector3() ).ToArray();
      outMesh.SetIndices( md.getIndices().Select( i => (int)i ).ToArray(), MeshTopology.Triangles, 0 );
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

    GameObject mapCachedShape( agxCollide.Shape shape, Charges.ContactGeometry geom )
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

        var sourceObjects = mesh.SourceObjects;
        var meshes        = MeshSplitter.Split( collisionData.getVertices(),
                                                collisionData.getIndices(),
                                                v => v.ToHandedVector3()).Meshes;
        foreach ( var meshSource in meshes ) {
          meshSource.name = geom.getName();
          Data.CacheMappedMeshes.Add( meshSource );
          mesh.AddSourceObject( meshSource );
        }
      }
      else {
        Debug.LogWarning( "Unsupported shape type: " + type );
        return null;
      }

      return go;
    }
    
    GameObject mapContactGeometry( Charges.ContactGeometry geom, bool addVisuals )
    {
      GameObject go = null;
      var uuid_annots = geom.getType().findAnnotations("uuid");
      foreach ( var uuid_annot in uuid_annots ) {
        if ( uuid_annot.isString() ) {
          var uuid = uuid_annot.asString();
          var shape = Data.AgxCache.readCollisionShapeCS( uuid );
          if ( shape != null ) {
            go = mapCachedShape( shape, geom );

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

      Utils.mapLocalTransform( go.transform, geom.local_transform() );
      var shapeComp = go.GetComponent<Shape>();
      shapeComp.CollisionsEnabled = geom.enable_collisions();
      Data.GeometryCache[ geom ] = shapeComp;
      return go;
    }

    GameObject mapBody( Bodies.RigidBody body )
    {
      GameObject rb = Factory.Create<RigidBody>();
      BrickObject.RegisterGameObject( body.getName(), rb );
      var rbComp = rb.GetComponent<RigidBody>();
      Utils.mapLocalTransform( rb.transform, body.kinematics().local_transform() );

      rbComp.MassProperties.Mass.UseDefault = false;
      rbComp.MassProperties.Mass.UserValue = (float)body.inertia().mass();

      bool hasVisuals = false;
      foreach ( var visual in body.getValues<Brick.Visuals.Geometries.Geometry>() ) {
        hasVisuals = true;
        Utils.AddChild( rb, mapVisualGeometry( visual ), Data.ErrorReporter, visual );
      }

      foreach ( var geom in body.getValues<Charges.ContactGeometry>() )
        Utils.AddChild( rb, mapContactGeometry( geom, !hasVisuals ), Data.ErrorReporter, geom );

      foreach ( var mc in body.getValues<Charges.MateConnector>() ) {
        if ( mc.getType().getName() == "ExternalConnector" ) {
          var mapped = InteractionMapper.MapExternalMateConnector( mc );
          mapped.GetComponent<ExternalConnector>().ConnectorFrame.SetParent( rb );
          Utils.AddChild( rb, mapped, Data.ErrorReporter, mc );
        }
      }

      Data.BodyCache[ body ] = rbComp;
      return rb;
    }

    void mapSystemToCollisionGroup( Brick.Physics3D.System system, Brick.Simulation.CollisionGroup collision_group )
    {
      if ( Data.SystemCache.ContainsKey( system ) ) {
        var sysGO = Data.SystemCache[ system ];
        var cg = sysGO.GetOrCreateComponent<AGXUnity.CollisionGroups>();
        cg.AddGroup( collision_group.getName(), true );
      }
    }

    void mapBodyToCollisionGroup( Bodies.Body body, CollisionGroup collision_group )
    {
      if ( Data.BodyCache.ContainsKey( body ) ) {
        var rb = Data.BodyCache[body];
        var cg = rb.gameObject.GetOrCreateComponent<AGXUnity.CollisionGroups>();
        cg.AddGroup( collision_group.getName(), true );
      }
    }

    void mapGeometryToCollisionGroup( Charges.ContactGeometry geometry, CollisionGroup collision_group )
    {
      if ( Data.GeometryCache.ContainsKey( geometry ) ) {
        var shape = Data.GeometryCache[geometry];
        var cg = shape.gameObject.GetOrCreateComponent<AGXUnity.CollisionGroups>();
        cg.AddGroup( collision_group.getName(), false );
      }
    }

    void mapCollisionGroup( CollisionGroup collision_group )
    {
      foreach ( var system in collision_group.systems() )
        if ( system is Brick.Physics3D.System system3d )
          mapSystemToCollisionGroup( system3d, collision_group );

      foreach ( var body in collision_group.bodies() )
        if ( body is Bodies.Body body3d )
          mapBodyToCollisionGroup( body3d, collision_group );

      foreach ( var geometry in collision_group.geometries() )
        if ( geometry is Charges.ContactGeometry geometry3d )
          mapGeometryToCollisionGroup( geometry3d, collision_group );
    }

    void mapDisabledPair(DisableCollisionPair pair )
    {
      Data.PrefabLocalData.AddDisabledPair( pair.group1().getName(), pair.group2().getName() );
    }

    GameObject mapSystem( Brick.Physics3D.System system )
    {
      var s = mapSystemPass1( system );
      mapSystemPass2( system );
      mapSystemPass3( system );
      mapSystemPass4( system );

      return s;
    }

    GameObject mapSystemPass1( Brick.Physics3D.System system )
    {
      GameObject s = BrickObject.CreateGameObject(system.getName());
      Utils.mapLocalTransform( s.transform, system.local_transform() );

      Data.SystemCache[ system ] = s;
      Data.FrameCache[ system ] = s;

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        Utils.AddChild( s, mapSystemPass1( subSystem ), Data.ErrorReporter, subSystem );

      // TODO: Map materials

      return s;
    }

    void mapSystemPass2( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        mapSystemPass2( subSystem );

      // Physics1D RotationalBodies are mapped at runtime by the RuntimeMapper

      foreach ( var body in system.getValues<Bodies.RigidBody>() )
        Utils.AddChild( s, mapBody( body ), Data.ErrorReporter, body );

      // TODO: Map terrains
    }

    void mapSystemPass3( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        mapSystemPass3( subSystem );

      foreach ( var trackSystem in system.getValues<Brick.Vehicles.Tracks.System>() )
        TrackMapper.MapTrackSystem( trackSystem );
    }

    void mapSystemPass4( Brick.Physics3D.System system )
    {
      var s = Data.SystemCache[system];

      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        mapSystemPass4( subSystem );

      foreach ( var interaction in system.getValues<Brick.Physics.Interactions.Interaction>() )
        if ( !Utils.IsRuntimeMapped( interaction ) )
          Utils.AddChild( s, InteractionMapper.MapInteraction( interaction, system ), Data.ErrorReporter, interaction );

      // Physics1D and Drivetrain interactions are mapped at runtime by the RuntimeMapper

      foreach ( var collision_group in system.getValues<CollisionGroup>() )
        mapCollisionGroup( collision_group );


      foreach ( var rb in system.kinematically_controlled() )
        Data.BodyCache[ rb ].MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      foreach (var disabledPair in system.getValues<Brick.Simulation.DisableCollisionPair>() )
        mapDisabledPair( disabledPair );
    }
  }
}