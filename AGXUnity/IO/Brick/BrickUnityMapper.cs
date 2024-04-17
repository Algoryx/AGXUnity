using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Utils;
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

    GameObject CreateShape<UnityType, BrickType>( BrickType brick, Action<BrickType, UnityType> setup )
      where UnityType : Shape
      where BrickType : Charges.ContactGeometry
    {
      GameObject go = Factory.Create<UnityType>();
      setup( brick, go.GetComponent<UnityType>() );
      return go;
    }

    UnityEngine.Mesh AGXMeshToUnityMesh( agxCollide.Trimesh inMesh )
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

    GameObject mapContactGeometry( Charges.ContactGeometry geom )
    {
      GameObject go = geom switch
      {
        Charges.Box box => CreateShape<Box,Charges.Box>(box,(bbox,ubox) => ubox.HalfExtents =  bbox.size().ToVector3()/2),
        Charges.Cylinder cyl => CreateShape<Cylinder,Charges.Cylinder>(cyl,(bcyl,ucyl) =>
      {
        ucyl.Radius = (float)bcyl.radius();
        ucyl.Height = (float)bcyl.height();
      }),
        Charges.Sphere sphere => CreateShape<Sphere,Charges.Sphere>(sphere,(bsphere,usphere) => usphere.Radius = (float)bsphere.radius()),
        Charges.Capsule cap => CreateShape<Capsule,Charges.Capsule>(cap,(bcap,ucap) =>
      {
        ucap.Radius = (float)bcap.radius();
        ucap.Height = (float)bcap.height();
      }),
        Charges.ExternalTriMeshGeometry etm => MapExternalTriMesh(etm),
        _ => null
      };
      if ( go == null )
        return Utils.ReportUnimplemented<GameObject>( geom, Data.ErrorReporter );

      BrickObject.RegisterGameObject( geom.getName(), go );

      var visualGO = ShapeVisual.Create( go.GetComponent<Shape>() );
      var visual = visualGO.GetComponent<ShapeVisual>();
      visual.SetMaterial( VisualMaterial );
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

      foreach ( var geom in body.getValues<Charges.ContactGeometry>() )
        Utils.AddChild( rb, mapContactGeometry( geom ), Data.ErrorReporter, geom );

      Data.BodyCache[ body ] = rbComp;
      return rb;
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

      foreach ( var rb in system.kinematically_controlled() )
        Data.BodyCache[ rb ].MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
    }
  }
}