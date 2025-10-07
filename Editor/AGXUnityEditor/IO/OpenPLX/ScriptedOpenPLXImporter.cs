using AGXUnity.IO.OpenPLX;
using System;
using System.Collections.Generic;
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
    public List<string> Dependencies;

    [field: SerializeField]
    public float ImportTime { get; private set; }

    public bool SkipImport = false;
    public bool HideImportedMeshes = true;
    public bool HideImportedVisualMaterials = false;
    public bool IgnoreDisabledMeshes = false;
    public bool RotateUp = true;

    private bool m_nonImportable = false;

    private string IconPath => IO.Utils.AGXUnityEditorDirectory + "/Icons/Logos/openplx-icon.png";

    private Texture2D m_icon = null;
    private Texture2D Icon
    {
      get
      {
        if ( m_icon == null )
          m_icon = AssetDatabase.LoadAssetAtPath<Texture2D>( IconPath );

        return m_icon;
      }
    }

    public override void OnImportAsset( AssetImportContext ctx )
    {
      Errors = new List<Error>();
      Dependencies = new List<string>();
      ImportTime = 0;

      // TODO: Investigate why selecting config.openplx files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity/OpenPLX" ) || ctx.assetPath.EndsWith( "config.openplx" ) )
        SkipImport = true;

      if ( SkipImport ) {
        ctx.AddObjectToAsset( "Root", new GameObject(), Icon );
        return;
      }
      var start = DateTime.Now;
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportErrors;
      importer.Options = new MapperOptions( HideImportedMeshes, HideImportedVisualMaterials, IgnoreDisabledMeshes, RotateUp );
      importer.SuccessCallback = data => OnSuccess( ctx, data );
      importer.ErrorCallback = () => {
        if ( !m_nonImportable )
          Debug.LogError( $"There were errors importing the OpenPLX file '{ctx.assetPath}'" );
      };

      var go = importer.ImportOpenPLXFile( ctx.assetPath );
      var end = DateTime.Now;
      ImportTime = (float)( end - start ).TotalSeconds;

      if ( Errors.Count > 0 ) {
        if ( m_nonImportable )
          SkipImport = true;
      }
      else
        ctx.SetMainObject( go );
    }

    public void OnSuccess( AssetImportContext ctx, MapperData data )
    {
      foreach ( var doc in data.RegisteredDocuments ) {
        var relative = System.IO.Path.GetRelativePath( Application.dataPath, doc );
        var normalized = "Assets/" + relative.Replace('\\','/');

        Dependencies.Add( normalized );
        ctx.DependsOnSourceAsset( normalized );
      }
      Dependencies.Sort();

      if ( data.RootNode )
        ctx.AddObjectToAsset( "Root", data.RootNode, Icon );
      if ( data.HasDefaultVisualMaterial )
        ctx.AddObjectToAsset( "Default Material", data.DefaultVisualMaterial );
      foreach ( var mesh in data.MappedMeshes )
        ctx.AddObjectToAsset( mesh.name, mesh );
      foreach ( var mat in data.MappedMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
      if ( data.HasDefaultMaterial )
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
      foreach ( var mat in data.MappedTerrainMaterials )
        ctx.AddObjectToAsset( mat.name, mat );
    }

    public void ReportErrors( openplx.Error error )
    {
      if ( error.getErrorCode() != CoreSWIG.ModelDeclarationNotFound )
        m_nonImportable = false;
      Errors.Add( new Error
      {
        raw       = error.getMessage( true ),
        message   = error.getMessage( false ).Replace( "\\", "/" ),
        document  = error.getSourceId(),
        line      = (int)error.getLine(),
        column    = (int)error.getColumn()
      } );
    }
  }
}
