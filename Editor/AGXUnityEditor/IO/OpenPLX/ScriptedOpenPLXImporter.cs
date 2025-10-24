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
    private List<Error> m_errors;
    [SerializeField]
    private List<string> m_dependencies;
    [SerializeField]
    private List<string> m_declaredModels;

    public Error[] Errors => m_errors.ToArray();
    public string[] Dependencies => m_dependencies.ToArray();
    public string[] DeclaredModels => m_declaredModels.ToArray();

    [field: SerializeField]
    public float ImportTime { get; private set; }

    [SerializeField]
    [Tooltip("By default, the last model declared in an imported OpenPLX-file will be the one mapped to AGXUnity. This option allows this behaviour to be overridden to instead import any model declared at the root level of the file. Note that the options provided are not guaranteed to be importable or complete.")]
    public string ImportedModel;
    [Tooltip("Skips this OpenPLX file when importing assets. Other files using models defined in this OpenPLX file are not skipped by extension.")]
    public bool SkipImport = false;
    [Tooltip("When importing large models there might be a large amount of meshes being imported which can make it hard to navigate the subassets of the imported OpenPLX file. This option hides the imported meshes in the subasset view.")]
    public bool HideImportedMeshes = true;
    [Tooltip("When importing large models there might be a large amount of materials being imported which can make it hard to navigate the subassets of the imported OpenPLX file. This option hides the imported materials in the subasset view.")]
    public bool HideImportedVisualMaterials = false;
    [Tooltip("When importing OpenPLX files that in turn import .agx archives, the visual meshes are sometimes attached to a disabled collision mesh with the same source mesh. When enabled, this option skips the import of these disabled meshes when mapping to AGXUnity objects.")]
    public bool IgnoreDisabledMeshes = false;
    [Tooltip("Since AGX use Z-up by default, many OpenPLX models might be built using Z-Up. This option applies a rotation to move the model Z-axis to the Unity Y-axis.")]
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
      m_errors = new List<Error>();
      m_dependencies = new List<string>();
      ImportTime = 0;

      // TODO: Investigate why selecting config.openplx files in the project view crashes unity
      if ( ctx.assetPath.StartsWith( "Assets/AGXUnity/OpenPLX" ) || ctx.assetPath.EndsWith( "config.openplx" ) )
        SkipImport = true;

      if ( SkipImport ) {
        ctx.AddObjectToAsset( "Root", new GameObject(), Icon );
        return;
      }
      m_declaredModels = OpenPLXImporter.FindDeclaredModels( ctx.assetPath );
      if ( ImportedModel == null
        || ( ImportedModel != "Default" && !m_declaredModels.Contains( ImportedModel ) ) )
        ImportedModel = "Default";

      var start = DateTime.Now;
      var importer = new OpenPLXImporter();
      importer.ErrorReporter = ReportErrors;
      importer.Options = new MapperOptions( HideImportedMeshes, HideImportedVisualMaterials, IgnoreDisabledMeshes, RotateUp );
      importer.SuccessCallback = data => OnSuccess( ctx, data );
      importer.ErrorCallback = () => {
        if ( !m_nonImportable )
          Debug.LogError( $"There were errors importing the OpenPLX file '{ctx.assetPath}'" );
      };

      var modelName = ImportedModel != "Default" ? ImportedModel : null;
      var go = importer.ImportOpenPLXFile( ctx.assetPath, modelName );
      var end = DateTime.Now;
      ImportTime = (float)( end - start ).TotalSeconds;

      if ( m_errors.Count > 0 ) {
        if ( m_nonImportable )
          SkipImport = true;
      }
      else
        ctx.SetMainObject( go );

      EditorUtility.SetDirty( this );
    }

    public void OnSuccess( AssetImportContext ctx, MapperData data )
    {
      foreach ( var doc in data.RegisteredDocuments ) {
        if ( doc == null || doc.Length == 0 )
          continue;
        var relative = System.IO.Path.GetRelativePath( Application.dataPath, doc );
        var normalized = "Assets/" + relative.Replace('\\','/');

        m_dependencies.Add( normalized );
        ctx.DependsOnSourceAsset( normalized );
      }
      m_dependencies.Sort();

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
      m_errors.Add( new Error
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
