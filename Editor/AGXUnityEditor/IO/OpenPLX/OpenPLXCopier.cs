using AGXUnity.IO.OpenPLX;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace AGXUnityEditor.IO.OpenPLX
{
  public static class OpenPLXCopier
  {
    /// <summary>
    /// Since OpenPLX objects require their corresponding source files to be present at runtime to load, 
    /// the files need to be copied when the application is built. For simplicity, we currently assume in <see cref="OpenPLXRoot"/>
    /// that the path of the source file relative to the asset root folder is mirrored in the build directory.
    /// Additionally, dependencies of OpenPLX files need to be imported as these are also required to load the OpenPLX file.
    /// </summary>
    [PostProcessBuild( 16 )]
    public static void OnPostProcessBuild( BuildTarget target, string targetPathFilename )
    {
      var targetExecutableFileInfo = new FileInfo( targetPathFilename );
      if ( !targetExecutableFileInfo.Exists ) {
        Debug.LogWarning( "Target executable doesn't exist: " + targetPathFilename );
        return;
      }

      var targetDataPath =  targetExecutableFileInfo.Directory.FullName +
                          Path.DirectorySeparatorChar +
                          Path.GetFileNameWithoutExtension( targetExecutableFileInfo.Name ) +
                          "_Data";

      var openPLXTarDir = $"{targetDataPath}{Path.DirectorySeparatorChar}OpenPLX";

      var projectPath = Directory.GetParent(Application.dataPath).ToString();

      var copiedDeps = new HashSet<string>();

      var openPLXAssets = AssetDatabase.FindAssets( "glob:\"*.openplx\"" );
      var num = 0;
      foreach ( var asset in openPLXAssets ) {
        var src = AssetDatabase.GUIDToAssetPath( asset );
        if ( src.StartsWith( "Assets/" ) ) {
          CopyAsset( src, openPLXTarDir );
          num++;

          // TODO: this path should use the importer properties
          if ( src.StartsWith( "Assets/AGXUnity/OpenPLX" ) || src.EndsWith( "config.openplx" ) )
            continue;

          var importer = (ScriptedOpenPLXImporter)AssetImporter.GetAtPath(src);
          if ( importer == null ) {
            Debug.LogWarning( $"No importer found for asset at path '{src}'." );
            continue;
          }

          string[] dependencies = importer.Dependencies;

          // If no dependencies are found, we assume that the importer has not yet run and does not have any cached dependencies
          // TODO: This should be changed once the OpenPLX API is changed to more easily check for dependencies
          if ( dependencies.Length == 0 && !importer.SkipImport )
            dependencies = OpenPLXImporter.FindDependencies( src, importer.ImportedModel == "Default" ? null : importer.ImportedModel );

          if ( importer.Errors.Count != 0 || dependencies == null ) {
            Debug.LogWarning( $"There were error importing openplx file '{src}', build might not include all of its dependencies." );
            continue;
          }

          foreach ( var dep in dependencies ) {
            if ( dep.EndsWith( ".openplx" ) )
              continue;
            var relative = Path.GetRelativePath( projectPath, dep );
            if ( !copiedDeps.Contains( relative ) ) {
              CopyAsset( relative, openPLXTarDir );
              copiedDeps.Add( relative );
            }
          }
        }
        else {
          Debug.LogError( $"OpenPLX asset '{src}' is not in Assets directory" );
          return;
        }
      }
      Debug.Log( $"Copied {num} OpenPLX source files to '{openPLXTarDir}'" );
      if ( copiedDeps.Count > 0 )
        Debug.Log( $"Copied {copiedDeps.Count} OpenPLX dependencies to '{openPLXTarDir}'" );
    }

    public static void CopyAsset( string assetPath, string targetDir )
    {
      var tar = $"{targetDir}/{assetPath.Substring(7)}";

      var tarDir = Path.GetDirectoryName( tar );
      if ( !File.Exists( tarDir ) )
        Directory.CreateDirectory( tarDir );

      File.Copy( assetPath, tar, true );
    }
  }
}
