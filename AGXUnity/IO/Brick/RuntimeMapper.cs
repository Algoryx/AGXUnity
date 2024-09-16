using System.Collections.Generic;
using UnityEngine;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  public class RuntimeMapper
  {
    public class Data
    {
      public BrickRoot Root { get; set; }
      public agxPowerLine.PowerLine PowerLine { get; set; }
      public Dictionary<Object, agxPowerLine.Unit> UnitCache { get; set; } = new Dictionary<Object, agxPowerLine.Unit>();
      public Dictionary<string, object> RuntimeMap { get; set; } = new Dictionary<string, object>();
    }

    private DrivetrainMapper DrivetrainMapper { get; set; }

    public Data MapperData = new Data();

    public void PerformRuntimeMapping( BrickRoot brickRoot )
    {
      MapperData.Root = brickRoot;
      if ( MapperData.Root.Native == null ) {
        Debug.LogError( $"Runtime mapping of brick object '{brickRoot.name}' failed: Object is not initialized" );
        return;
      }
      MapperData.PowerLine = new agxPowerLine.PowerLine();
      DrivetrainMapper = new DrivetrainMapper( MapperData );
      MapObject( MapperData.Root.Native );
      Simulation.Instance.Native.add( MapperData.PowerLine );
    }

    private void MapObject( Object obj )
    {
      if ( obj is Brick.Physics3D.System system )
        MapSystem( system );
    }

    private void MapSystem( Brick.Physics3D.System system )
    {
      //MapSystemPass1( system );
      MapSystemPass2( system );
      //MapSystemPass3( system );
      MapSystemPass4( system );
    }

    void MapSystemPass2( Brick.Physics3D.System system )
    {
      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        MapSystemPass2( subSystem );

      foreach ( var rotBod in system.getValues<Brick.Physics1D.Bodies.RotationalBody>() ) {
        var mapped = DrivetrainMapper.MapRotationalBody( rotBod );
        MapperData.UnitCache[ rotBod ] = mapped;
        MapperData.RuntimeMap[ rotBod.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }
    }

    void MapSystemPass4( Brick.Physics3D.System system )
    {
      foreach ( var subSystem in system.getValues<Brick.Physics3D.System>() )
        MapSystemPass4( subSystem );

      foreach ( var gear in system.getValues<Brick.DriveTrain.Gear>() ) {
        var mapped = DrivetrainMapper.MapGear( gear );
        MapperData.RuntimeMap[ gear.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }

      foreach ( var gearbox in system.getValues<Brick.DriveTrain.GearBox>() ) {
        var mapped = DrivetrainMapper.MapGearBox( gearbox );
        MapperData.RuntimeMap[ gearbox.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }

      foreach ( var diff in system.getValues<Brick.DriveTrain.Differential>() ) {
        var mapped = DrivetrainMapper.MapDifferential( diff );
        MapperData.RuntimeMap[ diff.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }

      foreach ( var tc in system.getValues<Brick.DriveTrain.EmpiricalTorqueConverter>()) {
        var mapped = DrivetrainMapper.MapTorqueConverter( tc );
        MapperData.RuntimeMap[ tc.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }

      foreach ( var engine in system.getValues<Brick.DriveTrain.CombustionEngine>()) {
        var mapped = DrivetrainMapper.MapCombustionEngine(engine);
        MapperData.RuntimeMap[ engine.getName() ] = mapped;
        MapperData.PowerLine.add( mapped );
      }

      foreach ( var actuator in system.getValues<Brick.DriveTrain.Actuator>() )
        DrivetrainMapper.MapActuator( actuator );

      foreach ( var rvm in system.getValues<Brick.Physics1D.Interactions.RotationalVelocityMotor>() ) {
        var mapped = DrivetrainMapper.Map1dRotationalVelocityMotor( rvm );
        MapperData.RuntimeMap[ rvm.getName() ] = mapped;
        Simulation.Instance.Native.add( mapped );
      }
    }
  }
}
