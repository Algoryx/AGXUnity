using AGXUnity.IO.BrickIO;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace AGXUnityEditor.IO.BrickIO
{
  public static class BrickCopier
  {
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

      var brickTarDir = $"{targetDataPath}{Path.DirectorySeparatorChar}Brick";

      var projectPath = Directory.GetParent(Application.dataPath).ToString();

      var copiedDeps = new HashSet<string>();

      var brickAssets = AssetDatabase.FindAssets( "glob:\"*.brick\"" );
      var num = 0;
      foreach ( var asset in brickAssets ) {
        var src = AssetDatabase.GUIDToAssetPath( asset );
        if ( src.StartsWith( "Assets/" ) ) {
          CopyAsset( src, brickTarDir );
          num++;

          if ( src.StartsWith( "Assets/AGXUnity" ) )
            continue;
          var deps = BrickImporter.FindDependencies( src );
          foreach ( var dep in deps ) {
            var relative = Path.GetRelativePath( projectPath, dep );
            if ( !copiedDeps.Contains( relative ) ) {
              CopyAsset( relative, brickTarDir );
              copiedDeps.Add( relative );
            }
          }
        }
        else {
          Debug.LogError( $"Brick asset '{src}' is not in Assets directory" );
          return;
        }
      }
      Debug.Log( $"Copied {num} brick source files to '{brickTarDir}'" );
      if(copiedDeps.Count > 0)
        Debug.Log( $"Copied {copiedDeps.Count} brick dependencies to '{brickTarDir}'" );
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