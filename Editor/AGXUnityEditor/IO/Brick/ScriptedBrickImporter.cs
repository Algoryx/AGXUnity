using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using AGXUnity.IO.BrickIO;
using Brick;

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

    public override void OnImportAsset( AssetImportContext ctx )
    {
      Errors = new List<Error>();
      var icon = AssetDatabase.LoadAssetAtPath<Texture2D>( "Assets/Brick/brick-icon.png" );

      // Todo: Add support for dependecy-based reimport
      //foreach ( var dep in dependencies )
      //  ctx.DependsOnSourceAsset( dep );

      // Todo: Investigate why selecting config.brick files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity" ) || ctx.assetPath.EndsWith( "config.brick" ) )
        return;

      var go = BrickImporter.ImportBrickFile( ctx.assetPath, ReportErrors, data => ctx.AddObjectToAsset( "Default Material", data.VisualMaterial ) );

      ctx.AddObjectToAsset( "Root", go, icon );
      ctx.SetMainObject( go );
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