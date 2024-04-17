using Brick;
using Brick.Core.Api;
using Brick.Robotics.Robots;
using System;
using System.Linq;
using UnityEngine;

using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  public static class BrickImporter
  {
    public static string BrickDir => Application.dataPath + ( Application.isEditor ? "" : "/Brick" );

    public static GameObject ImportBrickFile( string path, Action<MapperData> onSuccess = null )
    {
      return ImportBrickFile( path, ReportToConsole, onSuccess );
    }

    public static GameObject ImportBrickFile( string path, Action<Error> errorReporter, Action<MapperData> onSuccess = null )
    {
      var go = new GameObject( "Brick Root" );
      var root = go.AddComponent<BrickRoot>();

      root.BrickAssetPath = path;

      var loadedModel = ParseBrickSource(root.BrickFile, errorReporter);

      if ( loadedModel != null ) {
        var mapper = new BrickUnityMapper();
        mapper.MapObject( loadedModel, root.gameObject );
        foreach (var error in mapper.Data.ErrorReporter.getErrors() )
          errorReporter( error );
        onSuccess?.Invoke( mapper.Data );
      }
      else
        Debug.LogError( $"There were errors importing the brick file '{path}'" );

      return go;
    }

    private static BrickContext CreateContext()
    {
      std.StringVector bundle_paths = new std.StringVector { BrickDir + "/AGXUnity/Brick" };

      var context = new BrickContext( bundle_paths );

      DriveTrainSwig.DriveTrain_register_factories_cs( context );
      MathSwig.Math_register_factories_cs( context );
      Physics1DSwig.Physics1D_register_factories_cs( context );
      Physics3DSwig.Physics3D_register_factories_cs( context );
      PhysicsSwig.Physics_register_factories_cs( context );
      RoboticsSwig.Robotics_register_factories_cs( context );
      SimulationSwig.Simulation_register_factories_cs( context );
      TerrainSwig.Terrain_register_factories_cs( context );
      UrdfSwig.Urdf_register_factories_cs( context );
      VehiclesSwig.Vehicles_register_factories_cs( context );
      VisualsSwig.Visuals_register_factories_cs( context );
      UrdfSwig.Urdf_register_factories_cs( context );

      AgxBrick.register_plugins( context );

      return context;
    }

    public static BrickContext s_context;

    public static Object ParseBrickSource( string source, Action<Error> errorReporter )
    {
      var context = CreateContext();
      s_context = context;

      var loadedObj = CoreSwig.loadModelFromFile( source, null, context );

      if ( context.hasErrors() ) {
        loadedObj = null;
        foreach ( var error in context.getErrors() )
          errorReporter( error );
      }

      return loadedObj;
    }

    public static string[] FindDependencies( string source )
    {
      var context = CreateContext();
      var ic = BrickContextInternal.fromContext( context );
      var doc = ic.parseFile( source );

      var imports = doc.findImports();
      return imports.Select( imp => imp.getPath().Replace( "\\", "/" ) ).ToArray();

    }

    private static void ReportToConsole( Error error )
    {
      var ef = new ErrorFormatter();
      Debug.LogError( ef.format( error ) );
    }
  }
}