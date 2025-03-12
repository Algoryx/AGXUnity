using openplx;
using openplx.Core.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  public class OpenPLXImporter
  {
    public static string OpenPLXDir => Application.dataPath + ( Application.isEditor ? "" : "/OpenPLX" );

    public static GameObject ImportOpenPLXFile( string path, MapperOptions options = new MapperOptions(), Action<MapperData> onSuccess = null )
    {
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportToConsole;
      importer.SuccessCallback = onSuccess;
      importer.Options = options;
      return importer.ImportOpenPLXFile( path );
    }

    public Action<Error> ErrorReporter { get; set; } = null;
    public Action<MapperData> SuccessCallback { get; set; } = null;
    public MapperOptions Options { get; set; }
    public GameObject ImportOpenPLXFile( string path )
    {
      var go = new GameObject( System.IO.Path.GetFileNameWithoutExtension(path) );
      var root = go.AddComponent<OpenPLXRoot>();

      root.OpenPLXAssetPath = path;

      var mapper = new OpenPLXUnityMapper(Options);
      var loadedModel = ParseOpenPLXSource(root.OpenPLXFile, mapper.Data.AgxCache);

      if ( loadedModel != null ) {
        mapper.MapObject( loadedModel, root.gameObject );
        foreach ( var error in mapper.Data.ErrorReporter.getErrors() )
          ErrorReporter?.Invoke( error );
        SuccessCallback?.Invoke( mapper.Data );

        if ( Options.RotateUp )
          root.transform.rotation = Quaternion.FromToRotation( Vector3.forward, Vector3.up );
      }
      else
        Debug.LogError( $"There were errors importing the OpenPLX file '{path}'" );

      return go;
    }

    private OpenPlxContext CreateContext( agxopenplx.AgxCache cache = null )
    {
      std.StringVector bundle_paths = new std.StringVector { OpenPLXDir + "/AGXUnity/OpenPLX" };
      foreach ( var additional in OpenPLXSettings.Instance.AdditionalBundleDirs ) {
        if ( String.IsNullOrEmpty( additional ) )
          continue;
        if ( !System.IO.Directory.Exists( additional ) ) {
          Debug.LogWarning( $"Specified bundle directory '{additional}' does not exist, skipping..." );
          continue;
        }

        bundle_paths.Add( additional );
      }

      var context = new OpenPlxContext( bundle_paths );

      MathSWIG.Math_register_factories_cs( context );
      PhysicsSWIG.Physics_register_factories_cs( context );
      Physics1DSWIG.Physics1D_register_factories_cs( context );
      Physics3DSWIG.Physics3D_register_factories_cs( context );
      DriveTrainSWIG.DriveTrain_register_factories_cs( context );
      RoboticsSWIG.Robotics_register_factories_cs( context );
      SimulationSWIG.Simulation_register_factories_cs( context );
      VehiclesSWIG.Vehicles_register_factories_cs( context );
      TerrainSWIG.Terrain_register_factories_cs( context );
      VisualsSWIG.Visuals_register_factories_cs( context );
      UrdfSWIG.Urdf_register_factories_cs( context );


      agxOpenPLXSWIG.register_plugins( context, cache );

      return context;
    }

    public Object ParseOpenPLXSource( string source, agxopenplx.AgxCache agxCache = null )
    {
      var context = CreateContext(agxCache);
      Object loadedObj = CoreSWIG.loadModelFromFile( source, null, context );

      if ( context.hasErrors() ) {
        loadedObj = null;
        if ( ErrorReporter != null )
          foreach ( var error in context.getErrors() )
            ErrorReporter( error );
      }

      return loadedObj;
    }

    public static string[] FindDependencies( string source )
    {
      // TODO: Explicit dependency check is slow and crashes on build
      //var context = CreateContext();
      //var ic = OpenPLXContextInternal.fromContext( context );
      //var doc = ic.parseFile( source );

      //var imports = doc.findImports();
      //return imports.Select( imp => imp.getPath().Replace( "\\", "/" ) ).ToArray();


      // TODO: This does not currently handle nested imports handled by plugins. 
      // i.e. if an imported URDF file in turn import an OBJ file.
      List<string> dependencyExtensions = new List<string>() { "obj" };

      string extRegex = "";
      foreach ( var ext in dependencyExtensions )
        extRegex += "|" + ext;
      extRegex = extRegex[ 1.. ];

      // Check for instances where a string is preceeded by an "import" statement or where the string ends with a known extension supported path.
      string depRegex = $"(?:import\\s+(@?\".*?\"))|(?:(@?\".*?\\.(?:{extRegex})\"))";

      var reg = new Regex(depRegex);

      var relativeDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(source)) + "/";
      var openPLX = System.IO.File.ReadAllLines( source );
      return openPLX
        .SelectMany( l => reg.Matches( l ) )
        .SelectMany( m => m.Groups.Skip( 1 ) )
        .Where( g => g.Success )
        .Select( g => g.Value )
        .Select( i => i.StartsWith( '@' ) ? relativeDir + i[ 2..( i.Length - 1 ) ] : i[ 1..( i.Length - 1 ) ] )
        .Select( i => i.Replace( '\\', '/' ) )
        .Distinct()
        .ToArray();
    }

    private static void ReportToConsole( Error error )
    {
      var ef = new ErrorFormatter();
      Debug.LogError( ef.format( error ) );
    }
  }
}
