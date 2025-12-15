using AGXUnity.Utils;
using openplx;
using openplx.Core.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public static T ImportOpenPLXFile<T>( string path, MapperOptions options = new MapperOptions(), Action<MapperData> onSuccess = null, string model = null ) where T : UnityEngine.Object
    {
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportToConsole;
      importer.SuccessCallback = onSuccess;
      importer.Options = options;
      return importer.ImportOpenPLXFile<T>( path, model );
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

      if ( loadedModel != null && loadedModel.getType().isTrait() ) {
        ErrorReporter?.Invoke( new TraitNotImportableError( loadedModel ) );
        loadedModel = null;
      }


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

    private static OpenPlxContext CreateContext( agxopenplx.AgxCache cache = null )
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

      agxOpenPLXSWIG.register_bundles( context );
      agxOpenPLXSWIG.register_plugins( context, cache );

      return context;
    }

    public Object ParseOpenPLXSource( string source, string model = null, MapperData data = null )
    {
      var context = CreateContext(data?.AgxCache);
      Object loadedObj = CoreSWIG.loadModelFromFile( source, model, context );
      var objs = context.getRegisteredObjects();

      if ( context.hasErrors() ) {
        loadedObj = null;
        if ( ErrorReporter != null )
          foreach ( var error in context.getErrors() )
            ErrorReporter( error );
      }
      else if ( data != null ) {
        foreach ( var dep in FindDependencies( loadedObj, context ) )
          data.RegisteredDocuments.Add( dep );
      }

      return loadedObj;
    }

    /// <summary>
    /// Attempts to find the dependencies required when loading a given OpenPLX-model from the provided file.
    /// Note that this method will load the OpenPLX file provided to analyze it and can thus take some time to finish.
    /// It is not recommended to use this method regularly during runtime, and when possible, caching of the result should be used.
    /// </summary>
    /// <param name="source">The source file to find the dependencies of</param>
    /// <param name="model">The model to load from the OpenPLX-file or null to use the default</param>
    /// <returns></returns>
    public static string[] FindDependencies( string source, string model = null )
    {
      var context = CreateContext();
      var obj = CoreSWIG.loadModelFromFile( source, model, context );
      if ( obj == null )
        return null;
      return FindDependencies( obj, context );
    }

    private static string[] FindDependencies( Object obj, OpenPlxContext context )
    {
      HashSet<string> dependencies = new HashSet<string>();
      // TODO: This currently returns all openplx files in all bundles which can cause circular import dependencies.
      Action<string> addIfValid = (string path) => {
        if(path != null && path.Length > 0 && path != obj.getType().getOwningDocument().getSourceId())
          dependencies.Add( path );
      };

      // Find Imports
      var imports = obj.getType().getOwningDocument().findImports();
      foreach ( var import in imports )
        addIfValid( import.getPath() );

      // Find references to paths in members
      var values = new std.OuterSymbolSet();
      obj.getType().getOuterTreeRoot().collectValues( values );
      foreach ( var val in values ) {
        var boundValue = val.getBoundValue();
        if ( boundValue == null )
          continue;
        if ( boundValue.isConstant() && boundValue.asConstant().getToken().type == TokenType.String ) {
          var path = boundValue.asConstant().getToken().lexeme.Trim('\"');
          if ( System.IO.File.Exists( path ) )
            addIfValid( path );
        }
      }

      // Find bundle dependencies and add all files to dependencies
      var ic = OpenPlxContextInternal.fromContext( context );
      foreach ( var bundle in ic.analysisContext().getBundles() ) {
        var config = bundle.documents[ 0 ].getBundleConfig();
        addIfValid( config.config_file_path );
        foreach ( var file in config.openplx_files )
          addIfValid( file );
      }

      return dependencies.ToArray();
    }

    private static void ReportToConsole( Error error )
    {
      Debug.LogError( error.getMessage( true ) );
    }
  }
}
