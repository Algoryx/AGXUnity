using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.IO
{
  public static class Utils
  {
    private const string m_refDirectory = "AGXUnity";
    private const string m_refScript = "RigidBody.cs";
    private static string m_relDataPathDir = string.Empty;

    /// <summary>
    /// Absolute Unity project directory without trailing /, i.e., add '/Assets/Foo' for
    /// directory Foo in the project default assets folder.
    /// </summary>
    public static string ProjectDirectory { get { return Application.dataPath.Remove( Application.dataPath.LastIndexOf( "/Assets" ), "/Assets".Length ); } }                                                         
    
    /// <summary>
    /// Directory of AGXUnity source code, i.e, package directory + /AGXUnity.
    /// </summary>
    public static string AGXUnitySourceDirectory { get { return AGXUnityPackageDirectory + "/AGXUnity"; } }

    /// <summary>
    /// Absolute directory of AGXUnity source code, i.e, full package directory + /AGXUnity.
    /// </summary>
    public static string AGXUnitySourceDirectoryFull { get { return ProjectDirectory + '/' + AGXUnitySourceDirectory; } }

    /// <summary>
    /// Directory of AGXUnityEditor source code, i.e, package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectory { get { return AGXUnityPackageDirectory + "/Editor/AGXUnityEditor"; } }

    /// <summary>
    /// Absolute directory of AGXUnityEditor source code, i.e, full package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectoryFull  { get { return AGXUnityPackageDirectory + '/' + AGXUnityEditorSourceDirectory; } }

    /// <summary>
    /// AGXUnity package directory relative Unity project, e.g., Assets/Foo if AGXUnity source
    /// is located in Assets/Foo/AGXUnity, AGXUnityEditor source in Assets/Foo/Editor/AGXUnityEditor
    /// and resources in Assets/Foo/Resources. 'Foo' is by default 'AGXUnity'.
    /// </summary>
    public static string AGXUnityPackageDirectory
    {
      get
      {
        var refScriptFullPath = Application.dataPath + '/' + m_relDataPathDir + '/' + m_refDirectory + '/' + m_refScript;
        if ( m_relDataPathDir != "" && File.Exists( refScriptFullPath ) )
          return "Assets/" + m_relDataPathDir;

        var results = Directory.GetFiles( Application.dataPath, m_refScript, SearchOption.AllDirectories );
        foreach ( var result in results ) {
          var file = new FileInfo( result );
          if ( file.Directory.Name == "AGXUnity" ) {
            m_relDataPathDir = MakeRelative( file.Directory.Parent.FullName, Application.dataPath ).Remove( 0, "Assets/".Length );
            break;
          }
        }

        Debug.Log( "NEW!" );

        return "Assets/" + m_relDataPathDir;
      }
    }

    /// <summary>
    /// Full path to the AGXUnity install directory.
    /// </summary>
    public static string AGXUnityPackageDirectoryFull { get { return ProjectDirectory + '/' + AGXUnityPackageDirectory; } }

    /// <summary>
    /// Makes relative path given complete path.
    /// </summary>
    /// <param name="complete">Complete path.</param>
    /// <param name="root">New root directory.</param>
    /// <returns>Path with <paramref name="root"/> as root.</returns>
    public static string MakeRelative( string complete, string root )
    {
      Uri completeUri = new Uri( complete );
      Uri rootUri = new Uri( root );
      Uri relUri = rootUri.MakeRelativeUri( completeUri );
      return relUri.ToString();
    }
  }
}
