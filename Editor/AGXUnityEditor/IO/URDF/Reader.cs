using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.IO.URDF
{
  public static class Reader
  {
    /// <summary>
    /// Reads and instantiates URDF file at given <paramref name="urdfFilePath"/>. If
    /// <paramref name="resourceLoader"/> is null, the default resource loader is used.
    /// In case of errors, the error is printed in the Console and null is returned.
    /// </summary>
    /// <see cref="CreateDefaultResourceLoader(string)"/>
    /// <param name="urdfFilePath">Path and filename to the URDF file to instantiate.</param>
    /// <param name="resourceLoader">Resource loader callback - default is used if null.</param>
    /// <param name="silent">True to print progress into the Console. Default: true, silent.</param>
    /// <returns>Game object instance if successful, otherwise null.</returns>
    public static GameObject Instantiate( string urdfFilePath,
                                          Func<string, GameObject> resourceLoader = null,
                                          bool silent = true )
    {
      GameObject instance = null;
      agx.Timer timer = null;
      var pColor = InspectorGUISkin.BrandColorBlue;
      try {
        if ( !silent ) {
          Debug.Log( $"Reading URDF: '{AddColor( urdfFilePath, pColor )}'..." );
          timer = new agx.Timer( true );
        }

        // Reading model, will throw if anything goes wrong.
        var model = AGXUnity.IO.URDF.Model.Read( urdfFilePath );

        if ( !silent )
          Debug.Log( $"Successfully read " +
                     $"{AddColor( $"{model.Links.Count()} links", pColor )}, " +
                     $"{AddColor( $"{model.Joints.Count()} joints", pColor )} and " +
                     $"{AddColor( $"{model.Materials.Count()} materials", pColor )} in " +
                     $"{AddColor( timer.getTime().ToString("0.00"), pColor )} ms." );

        // Open "select folder" panel if the model requires resources
        // and no resource loader callback is provided.
        var dataDirectory = string.Empty;
        if ( resourceLoader == null && model.RequiresResourceLoader ) {
          dataDirectory = EditorUtility.OpenFolderPanel( $"Select '{urdfFilePath}' root data folder.",
                                                         System.IO.Path.GetDirectoryName( urdfFilePath ).Replace( '\\', '/' ),
                                                         "" );
          if ( !string.IsNullOrEmpty( dataDirectory ) )
            dataDirectory = Utils.MakeRelative( dataDirectory, Application.dataPath );
          if ( !AssetDatabase.IsValidFolder( dataDirectory ) ) {
            Debug.LogWarning( $"Unable to find given '{dataDirectory}' in the project Assets folder." );
            dataDirectory = string.Empty;
          }
          else if ( !silent )
            Debug.Log( $"Using data directory: '{AddColor( dataDirectory, pColor )}'." );
        }

        if ( !silent )
          timer.reset( true );

        // Instantiating the model, will throw if anything goes wrong.
        using ( new Utils.UndoCollapseBlock( $"Instantiating model {model.Name}" ) )
          instance = model.Instantiate( resourceLoader ?? CreateDefaultResourceLoader( dataDirectory ),
                                        obj => Undo.RegisterCreatedObjectUndo( obj, $"Created: {obj.name}" ) );

        if ( !silent )
          Debug.Log( $"Successfully instantiated URDF robot: '{AddColor( instance.name, pColor )}' in " +
                     $"{AddColor( timer.getTime().ToString( "0.00" ), pColor )} ms." );
      }
      catch ( System.Xml.XmlException e ) {
        Debug.LogException( e );
        return null;
      }

      return instance;
    }

    /// <summary>
    /// Reads and instantiates all URDF files at given <paramref name="urdfFilePaths"/>. If
    /// <paramref name="resourceLoader"/> is null, the default resource loader is used.
    /// In case of errors, the error is printed in the Console.
    /// </summary>
    /// <see cref="CreateDefaultResourceLoader(string)"/>
    /// <param name="urdfFilePaths">Array of URDF files to instantiate.</param>
    /// <param name="resourceLoader">Resource loader callback - default is used if null.</param>
    /// <param name="silent">True to print progress into the Console. Default: true, silent.</param>
    /// <returns></returns>
    public static GameObject[] Instantiate( string[] urdfFilePaths,
                                            Func<string, GameObject> resourceLoader = null,
                                            bool silent = true )
    {
      using ( new Utils.UndoCollapseBlock( $"Instantiating {urdfFilePaths.Length} models" ) )
        return ( from urdfFilePath in urdfFilePaths
                 let go = Instantiate( urdfFilePath, resourceLoader, silent )
                 where go != null
                 select go ).ToArray();
    }

    /// <summary>
    /// Creates a resource loader if none is provided. This resource loader replaces
    /// "package:/" with <paramref name="dataDirectory"/> and <paramref name="dataDirectory"/>
    /// must be relative to the project (i.e., start with Assets). When Collada is
    /// a resource any rotation of the loaded game objects are removed.
    /// </summary>
    /// <remarks>
    /// If a Collada resource is missing, this loader checks if there's a .obj file with
    /// the same name.
    /// </remarks>
    /// <param name="dataDirectory">Root data directory.</param>
    /// <returns>Resource loader function.</returns>
    public static Func<string, GameObject> CreateDefaultResourceLoader( string dataDirectory )
    {
      if ( string.IsNullOrEmpty( dataDirectory ) )
        return null;

      dataDirectory = dataDirectory.Replace( '\\', '/' );
      if ( !dataDirectory.StartsWith( "Assets/" ) ) {
        Debug.LogWarning( $"The data directory ('{dataDirectory}') must be relative to the project." );
      }

      return AGXUnity.IO.URDF.Model.CreateDefaultResourceLoader( dataDirectory,
                                                                 resourceFileName =>
                                                                   AssetDatabase.LoadAssetAtPath<GameObject>( resourceFileName ) );
    }

    /// <summary>
    /// Finds selected URDF files.
    /// </summary>
    /// <param name="warnAboutNonUrdfFiles">True to print warning in the Console of non-supported file types. Default: false.</param>
    /// <returns>Array of selected URDF files relative to the project.</returns>
    public static string[] GetSelectedUrdfFiles( bool warnAboutNonUrdfFiles = false )
    {
      var urdfFilePaths = new List<string>();
      var nonUrdfFilePaths = new List<string>();
      foreach ( var obj in Selection.objects ) {
        var assetPath = AssetDatabase.GetAssetPath( obj );
        if ( string.IsNullOrEmpty( assetPath ) )
          continue;
        if ( !assetPath.ToLower().EndsWith( ".urdf" ) ) {
          nonUrdfFilePaths.Add( assetPath );
          continue;
        }
        urdfFilePaths.Add( assetPath );
      }

      if ( warnAboutNonUrdfFiles )
        foreach ( var nonUrdfFilePath in nonUrdfFilePaths )
          Debug.LogWarning( $"{AddColor( nonUrdfFilePath, Color.red )} isn't a supported URDF file." );

      return urdfFilePaths.ToArray();
    }

    private static string AddColor( string str, Color color )
    {
      return AGXUnity.Utils.GUI.AddColorTag( str, color );
    }
  }
}
