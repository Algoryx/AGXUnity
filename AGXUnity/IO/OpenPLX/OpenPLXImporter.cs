using AGXUnity.Utils;
using openplx;
using openplx.Core.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  public class OpenPLXImporter
  {
    public static string[] BundleDirOverrides { get; set; } = null;
    public static string OpenPLXRoot => Application.dataPath + ( Application.isEditor ? "" : "/OpenPLX" );

    public static string[] OpenPLXBundlesDirs
    {
      get
      {
        if ( BundleDirOverrides != null )
          return BundleDirOverrides;
        return new string[] { OpenPLXRoot + "/AGXUnity/OpenPLX" };
      }
    }

    public static string TransformOpenPLXPath( string path ) => System.IO.Path.IsPathRooted( path ) ? path : path.Replace( "Assets/", OpenPLXRoot + "/" );

    public static List<string> FindDeclaredModels( string path )
    {
      var transformed = TransformOpenPLXPath(path);
      if ( !System.IO.File.Exists( path ) )
        throw new FileNotFoundException( "Cannot find the specified OpenPLX-file", path );
      var lines = System.IO.File.ReadAllLines(transformed);
      var result = new List<string>();
      foreach ( var line in lines ) {
        if ( line.Length == 0
          || !System.Char.IsLetter( line, 0 )
          || line.StartsWith( "trait " )
          || line.StartsWith( "import " ) )
          continue;

        var src = line;
        if ( src.StartsWith( "const " ) )
          src = src[ 6.. ];

        result.Add( src.SplitSpace()[ 0 ] );
      }
      return result;
    }

    public static T ImportOpenPLXFile<T>( string path, MapperOptions options = new MapperOptions(), Action<MapperData> onSuccess = null ) where T : UnityEngine.Object
    {
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportToConsole;
      importer.SuccessCallback = onSuccess;
      importer.Options = options;
      return importer.ImportOpenPLXFile<T>( path );
    }

    public Action<Error> ErrorReporter { get; set; } = null;
    public Action<MapperData> SuccessCallback { get; set; } = null;
    public Action ErrorCallback { get; set; } = null;
    public MapperOptions Options { get; set; }
    public T ImportOpenPLXFile<T>( string path, string model = null ) where T : UnityEngine.Object
    {
      var obj = ImportOpenPLXFile(path, model);

      if ( obj is not T casted ) {
        ErrorReporter?.Invoke( new IncompatibleImportTypeError( path ) );
        return null;
      }

      return casted;
    }

    public UnityEngine.Object ImportOpenPLXFile( string path, string model = null )
    {
      var mapper = new OpenPLXUnityMapper(Options);
      var transformed = TransformOpenPLXPath(path);
      Object loadedModel = null;
      if ( System.IO.File.Exists( transformed ) )
        loadedModel = ParseOpenPLXSource( transformed, model, mapper.Data );
      else
        ErrorReporter?.Invoke( new FileDoesNotExistError( transformed ) );

      UnityEngine.Object importedObject = null;
      if ( loadedModel != null ) {
        importedObject = mapper.MapObject( loadedModel, path );
        if ( importedObject == null || mapper.Data.ErrorReporter.getErrorCount() > 0 ) {
          foreach ( var error in mapper.Data.ErrorReporter.getErrors() )
            ErrorReporter?.Invoke( error );
          ErrorCallback?.Invoke();
        }
        else {
          if ( importedObject is GameObject go ) {
            var root = go.GetComponent<OpenPLX.OpenPLXRoot>();
            root.OpenPLXAssetPath = path;
            root.OpenPLXModelName = model;
          }
          SuccessCallback?.Invoke( mapper.Data );
        }
      }
      else
        ErrorCallback?.Invoke();

      return importedObject;
    }

    private OpenPlxContext CreateContext( agxopenplx.AgxCache cache = null )
    {
      std.StringVector bundle_paths = new std.StringVector(OpenPLXBundlesDirs);
      foreach ( var additional in OpenPLXSettings.Instance.AdditionalBundleDirs ) {
        var transformed = TransformOpenPLXPath( additional );
        if ( String.IsNullOrEmpty( transformed ) )
          continue;
        if ( !System.IO.Directory.Exists( transformed ) ) {
          Debug.LogWarning( $"Specified bundle directory '{transformed}' does not exist, skipping..." );
          continue;
        }

        bundle_paths.Add( transformed );
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

    public Object ParseOpenPLXSource( string source, string model = null, MapperData data = null )
    {
      var context = CreateContext(data?.AgxCache);
      Object loadedObj = CoreSWIG.loadModelFromFile( source, model, context );
      var objs = context.getRegisteredObjects();
      if ( data != null ) {
        foreach ( var obj in objs ) {
          var doc = obj?.getType()?.getOwningDocument()?.getSourceId();
          if ( doc != null )
            data.RegisteredDocuments.Add( doc );
        }
      }

      if ( context.hasErrors() ) {
        loadedObj = null;
        if ( ErrorReporter != null )
          foreach ( var error in context.getErrors() )
            ErrorReporter( error );
      }

      return loadedObj;
    }

    public static string[] FindExplicitDependencies( string source )
    {
      // TODO: Explicit dependency check is slow and crashes on build
      //var context = CreateContext();
      //var ic = OpenPLXContextInternal.fromContext( context );
      //var doc = ic.parseFile( source );

      //var imports = doc.findImports();
      //return imports.Select( imp => imp.getPath().Replace( "\\", "/" ) ).ToArray();


      // TODO: This does not currently handle nested imports handled by plugins. 
      // i.e. if an imported URDF file in turn import an OBJ file.
      List<string> dependencyExtensions = new List<string>() { "obj", "png", "jpg" };

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
      Debug.LogError( error.getMessage( true ) );
    }
  }
}
