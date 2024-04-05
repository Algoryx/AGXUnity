using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using AGXUnity.IO.BrickIO;

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
      var icon = AssetDatabase.LoadAssetAtPath<Texture2D>( "Assets/Brick/brick-icon.png" );

      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity" ) )
        return;

      var go = BrickImporter.ImportBrickFile( ctx.assetPath, ReportErrors, data => ctx.AddObjectToAsset( "Default Material", data.VisualMaterial ) );

      ctx.AddObjectToAsset( "Root", go, icon );
      ctx.SetMainObject( go );
    }

    public Brick.Core.Object ReportErrors( Brick.Core.Object obj, Brick.Core.Api.BrickContext context )
    {
      var ef = new Brick.ErrorFormatter();
      foreach ( var error in context.getErrors() )
        Errors.Add( new Error
        {
          message = new string( ef.format( error ).SkipWhile( c => c != ' ' ).Skip( 1 ).ToArray() ),
          line    = (int)error.getLine(),
          column  = (int)error.getColumn()
        } );
      if ( context.hasErrors() )
        return null;
      return obj;
    }
  }
}