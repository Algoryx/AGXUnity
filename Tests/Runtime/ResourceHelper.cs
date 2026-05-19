using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif

public static class ResourceHelper
{
  private static string[] s_trackedResources =
  {
    "csv_lidar_pattern.csv"
  };

  private static string GetEditorResourcePath( [CallerFilePath] string path = "" ) => Path.Combine( Path.GetDirectoryName( path ), "Test Resources" );
  private static string GetRuntimeResourcePath() => Path.Combine( Application.dataPath, "Test Resources" );
  private static string ResourcePath => Application.isEditor ? GetEditorResourcePath() : GetRuntimeResourcePath();

#if UNITY_EDITOR
  [PostProcessBuild( 16 )]
  public static void OnPostProcessBuild( BuildTarget target, string targetPathFilename )
  {
    var targetExecutableFileInfo = new FileInfo( targetPathFilename );
    var targetDataPath =  targetExecutableFileInfo.Directory.FullName +
                          Path.DirectorySeparatorChar +
                          Path.GetFileNameWithoutExtension( targetExecutableFileInfo.Name ) +
                          "_Data";

    targetDataPath = Path.Combine( targetDataPath, "Test Resources" );
    Debug.Log( $"Copying test resources to '{targetDataPath}'" );

    Directory.CreateDirectory( targetDataPath );

    foreach ( var f in s_trackedResources )
      File.Copy( Path.Combine( ResourcePath, f ), Path.Combine( targetDataPath, f ) );
  }
#endif

  public static string GetTestResource( string identifier )
  {
    Debug.Assert( s_trackedResources.Contains( identifier ), $"File '{identifier}' is not a tracked test resource" );

    return Path.Combine( ResourcePath, identifier );
  }
}
