using AGXUnity.IO.OpenPLX;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AGXUnityEditor.IO.OpenPLX
{
  [ScriptedImporter( 0, ".openplx" )]
  public class ScriptedOpenPLXImporter : ScriptedImporter
  {
    [Serializable]
    public struct Error
    {
      public string raw;
      public string message;
      public string document;
      public string Location => $"{line}:{column}";
      public int line;
      public int column;
    }

    [SerializeField]
    public List<Error> Errors;

    [field: SerializeField]
    public float ImportTime { get; private set; }

    public bool SkipImport = false;
    public bool HideImportedMeshes = true;
    public bool HideImportedVisualMaterials = false;
    public bool IgnoreDisabledMeshes = false;
    public bool RotateUp = true;

    public override void OnImportAsset( AssetImportContext ctx )
    {
      Errors = new List<Error>();
      ImportTime = 0;
      var iconPath =  IO.Utils.AGXUnityEditorDirectory + "/Icons/Logos/openplx-icon.png";
      var icon = AssetDatabase.LoadAssetAtPath<Texture2D>( iconPath );

      // TODO: Investigate why selecting config.openplx files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity" ) || ctx.assetPath.EndsWith( "config.openplx" ) )
        SkipImport = true;

      if ( SkipImport ) {
        ctx.AddObjectToAsset( "Root", new GameObject(), icon );
        return;
      }

      // TODO: Add support for dependecy-based reimport
      //foreach ( var dep in dependencies )
      //  ctx.DependsOnSourceAsset( dep );

      var start = DateTime.Now;
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportErrors;
      importer.Options = new MapperOptions( HideImportedMeshes, HideImportedVisualMaterials, IgnoreDisabledMeshes, RotateUp );
      importer.SuccessCallback = data => OnSuccess( ctx, data );

      try {
        var go = importer.ImportOpenPLXFile( ctx.assetPath );
        var end = DateTime.Now;

        ctx.AddObjectToAsset( "Root", go, icon );
        ctx.SetMainObject( go );
        ImportTime = (float)( end - start ).TotalSeconds;
      }
      catch ( Exception e ) {
        Debug.LogError( "Failed importing file" );
        Debug.LogException( e );
      }
    }

    public void OnSuccess( AssetImportContext ctx, MapperData data )
    {
      ctx.AddObjectToAsset( "Default Material", data.VisualMaterial );
      foreach ( var mesh in data.MappedMeshes )
        ctx.AddObjectToAsset( mesh.name, mesh );
      foreach ( var mat in data.MappedMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
      ctx.AddObjectToAsset( data.DefaultMaterial.name, data.DefaultMaterial );
      foreach ( var mat in data.MaterialCache.Values )
        if ( mat != data.DefaultMaterial )
          ctx.AddObjectToAsset( mat.name, mat );
      foreach ( var mat in data.MappedContactMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
      foreach ( var fric in data.MappedFrictionModels )
        ctx.AddObjectToAsset( fric.name, fric );
      foreach ( var props in data.MappedTrackProperties )
        ctx.AddObjectToAsset( props.name, props );
      foreach ( var props in data.MappedTrackInternalMergeProperties )
        ctx.AddObjectToAsset( props.name, props );
    }

    public void ReportErrors( openplx.Error error )
    {
      var ef = new UnityOpenPLXErrorFormatter();
      var raw = ef.format( error );
      var m = Regex.Match( raw, ".+:\\d+:\\d+ (.+)" );
      Errors.Add( new Error
      {
        raw       = raw,
        message   = m.Groups[ 1 ].Value,
        document  = error.getSourceId(),
        line      = (int)error.getLine(),
        column    = (int)error.getColumn()
      } );
    }
  }
}
