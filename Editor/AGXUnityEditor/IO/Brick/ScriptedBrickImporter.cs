using AGXUnity.IO.BrickIO;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AGXUnityEditor.IO.BrickIO
{
  [ScriptedImporter( 0, ".brick" )]
  [Icon( "Assets/Brick/brick-icon.png" )]
  public class ScriptedBrickImporter : ScriptedImporter
  {
    [Serializable]
    public struct Error
    {
      public string message;
      public string Location => $"{line}:{column}";
      public int line;
      public int column;
    }

    [SerializeField]
    public List<Error> Errors;

    [field: SerializeField]
    public float ImportTime { get; set; }

    public bool HideImportedMeshes = true;
    public bool HideImportedVisualMaterials = false;

    public override void OnImportAsset( AssetImportContext ctx )
    {
      Errors = new List<Error>();
      var icon = AssetDatabase.LoadAssetAtPath<Texture2D>( "Assets/Brick/brick-icon.png" );

      // TODO: Add support for dependecy-based reimport
      //foreach ( var dep in dependencies )
      //  ctx.DependsOnSourceAsset( dep );

      ImportTime = 0;

      // TODO: Investigate why selecting config.brick files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity" ) || ctx.assetPath.EndsWith( "config.brick" ) )
        return;

      var start = DateTime.Now;
      var go = BrickImporter.ImportBrickFile( ctx.assetPath,
                                              ReportErrors,
                                              new MapperOptions(HideImportedMeshes,HideImportedVisualMaterials),
                                              data => OnSuccess(ctx,data) );
      var end = DateTime.Now;
      ImportTime = (float)( end - start ).TotalSeconds;

      ctx.AddObjectToAsset( "Root", go, icon );
      ctx.SetMainObject( go );
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
    }

    public void ReportErrors( Brick.Error error )
    {
      var ef = new UnityBrickErrorFormatter();
      Errors.Add( new Error
      {
        message = new string( ef.format( error ).SkipWhile( c => c != ' ' ).Skip( 1 ).ToArray() ),
        line    = (int)error.getLine(),
        column  = (int)error.getColumn()
      } );
    }
  }
}