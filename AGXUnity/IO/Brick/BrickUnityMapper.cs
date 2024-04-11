using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using System;
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

    private InteractionMapper MateMapper { get; set; }
    private TrackMapper TrackMapper { get; set; }

    public BrickUnityMapper()
    {
      Data.VisualMaterial = ShapeVisual.CreateDefaultMaterial();
      Data.VisualMaterial.hideFlags = HideFlags.HideInHierarchy;
      Data.ErrorReporter = new Brick.ErrorReporter();

      MateMapper = new InteractionMapper( Data );
      TrackMapper = new TrackMapper( Data );
    }

    public GameObject MapObject( Object obj, GameObject rootNode )
    {
      Data.RootNode = rootNode;
      if ( obj is Brick.Physics3D.System system )
        Utils.AddChild( RootNode, mapSystem( system ), Data.ErrorReporter, system );

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
        _ => null
      };
      if ( go == null )
        return Utils.ReportUnimplemented<GameObject>( geom, Data.ErrorReporter );

      BrickObject.RegisterGameObject( geom.getName(), go );

      ShapeVisual.Create( go.GetComponent<Shape>() ).GetComponent<ShapeVisual>().SetMaterial( VisualMaterial );
      Utils.mapLocalTransform( go.transform, geom.local_transform() );

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

      // TODO: Map Physics1D RotationalBodies

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
        if(!Utils.IsRuntimeMapped( interaction ) )
          Utils.AddChild( s, MateMapper.MapInteraction( interaction, system ), Data.ErrorReporter, interaction );

      foreach ( var rb in system.kinematically_controlled() )
        Data.BodyCache[ rb ].MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
    }
  }
}