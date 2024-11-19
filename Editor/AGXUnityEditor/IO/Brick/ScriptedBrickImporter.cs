using AGXUnity.IO.BrickIO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AGXUnityEditor.IO.BrickIO
{
  [ScriptedImporter( 0, ".brick" )]
  public class ScriptedBrickImporter : ScriptedImporter
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

    public override void OnImportAsset( AssetImportContext ctx )
    {
      Errors = new List<Error>();
      ImportTime = 0;
      var iconPath =  IO.Utils.AGXUnityEditorDirectory + "/Icons/Logos/brick-icon.png";
      var icon = AssetDatabase.LoadAssetAtPath<Texture2D>( iconPath );

      // TODO: Investigate why selecting config.brick files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity" ) || ctx.assetPath.EndsWith( "config.brick" ) )
        SkipImport = true;

      if ( SkipImport ) {
        ctx.AddObjectToAsset( "Root", new GameObject(), icon );
        return;
      }


      // TODO: Add support for dependecy-based reimport
      //foreach ( var dep in dependencies )
      //  ctx.DependsOnSourceAsset( dep );


      var start = DateTime.Now;
      var importer = new BrickImporter();
      importer.ErrorReporter = ReportErrors;
      importer.Options = new MapperOptions( HideImportedMeshes, HideImportedVisualMaterials, IgnoreDisabledMeshes );
      importer.SuccessCallback = data => OnSuccess( ctx, data );

      try {
        var go = importer.ImportBrickFile( ctx.assetPath );
        var end = DateTime.Now;

        ctx.AddObjectToAsset( "Root", go, icon );
        ctx.SetMainObject( go );
        ImportTime = (float)( end - start ).TotalSeconds;
      }
      catch ( Exception e ) {
        Debug.LogError( "Failed importing file" );
      }
    }

    public void OnSuccess( AssetImportContext ctx, MapperData data )
    {
      ctx.AddObjectToAsset( "Default Material", data.VisualMaterial );
      foreach ( var mesh in data.CacheMappedMeshes )
        ctx.AddObjectToAsset( mesh.name, mesh );
      foreach ( var mat in data.CacheMappedMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
      ctx.AddObjectToAsset( data.DefaultMaterial.name, data.DefaultMaterial );
      foreach ( var mat in data.MaterialCache.Values )
        if ( mat != data.DefaultMaterial )
          ctx.AddObjectToAsset( mat.name, mat );
      foreach ( var mat in data.ContactMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
      foreach ( var props in data.CacheMappedTrackProperties )
        ctx.AddObjectToAsset( props.name, props );
      foreach ( var props in data.CacheMappedTrackInternalMergeProperties )
        ctx.AddObjectToAsset( props.name, props );
    }

    public void ReportErrors( openplx.Error error )
    {
      var ef = new UnityBrickErrorFormatter();
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
