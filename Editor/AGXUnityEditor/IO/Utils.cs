using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

using Object = UnityEngine.Object;

namespace AGXUnityEditor.IO
{
  public static partial class Utils
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
    /// AGXUnity resources directory, i.e, package directory + /Resources.
    /// </summary>
    public static string AGXUnityResourceDirectory { get { return AGXUnityPackageDirectory + "/Resources"; } }

    /// <summary>
    /// Absolute directory of AGXUnity resources, i.e., package directory full + /Resources.
    /// </summary>
    public static string AGXUnityResourceDirectoryFull { get { return ProjectDirectory + '/' + AGXUnityResourceDirectory; } }

    /// <summary>
    /// Native plugin directory, i.e., package directory + /Plugins/x86_64.
    /// </summary>
    public static string AGXUnityPluginDirectory { get { return AGXUnityPackageDirectory + "/Plugins/x86_64"; } }

    /// <summary>
    /// Native plugin directory, i.e., package directory full + /Plugins/x86_64.
    /// </summary>
    public static string AGXUnityPluginDirectoryFull { get { return ProjectDirectory + '/' + AGXUnityPluginDirectory; } }

    /// <summary>
    /// AGXUnity package editor directory, i.e., package directory + /Editor.
    /// </summary>
    public static string AGXUnityEditorDirectory { get { return AGXUnityPackageDirectory + "/Editor"; } }

    /// <summary>
    /// Absolute directory of AGXUnity editor, i.e. package directory full + /Editor.
    /// </summary>
    public static string AGXUnityEditorDirectoryFull { get { return ProjectDirectory + '/' + AGXUnityEditorDirectory; } }

    /// <summary>
    /// Directory of AGXUnityEditor source code, i.e, package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectory { get { return AGXUnityPackageDirectory + "/Editor/AGXUnityEditor"; } }

    /// <summary>
    /// Absolute directory of AGXUnityEditor source code, i.e, full package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectoryFull  { get { return ProjectDirectory + '/' + AGXUnityEditorSourceDirectory; } }

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

        return "Assets/" + m_relDataPathDir;
      }
    }

    /// <summary>
    /// Full path to the AGXUnity install directory.
    /// </summary>
    public static string AGXUnityPackageDirectoryFull { get { return ProjectDirectory + '/' + AGXUnityPackageDirectory; } }

    public static void VerifyDirectories()
    {
      Predicate<string> isRelDirectory = name => name.Contains( "AGXUnity" ) &&
                                                 name.Contains( "Directory" ) &&
                                                !name.Contains( "Full" );
      var directories = ( from propertyInfo
                          in typeof( Utils ).GetProperties( BindingFlags.Public | BindingFlags.Static )
                          where isRelDirectory( propertyInfo.Name )
                          select (string)propertyInfo.GetGetMethod().Invoke( null, new object[] { } ) ).ToArray();
      foreach ( var dir in directories ) {
        if ( !AssetDatabase.IsValidFolder( dir ) )
          Debug.LogWarning( "Missing AGXUnity directory: " + dir );
      }
    }

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
      return Uri.UnescapeDataString( relUri.ToString() );
    }

    public static T[] FindAssetsOfType<T>( string directory = "" )
      where T : Object
    {
      // FindAssets will return same GUID for all grouped assets (AddObjectToAsset), so
      // if we have 17 ContactMaterial assets in a group FindAsset will return an array
      // of 17 where all entries are identical.
      var type = typeof( T );
      var typeName = string.Empty;
      if ( type.Namespace != null && type.Namespace.StartsWith( "UnityEngine" ) )
        typeName = type.Name;
      else
        typeName = type.FullName;
      var guids = string.IsNullOrEmpty( directory ) ?
                    AssetDatabase.FindAssets( "t:" + typeName ).Distinct() :
                    AssetDatabase.FindAssets( "t:" + typeName, new string[] { directory } ).Distinct();
      return ( from guid
               in guids
               from obj
               in AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath( guid ) )
               select obj as T ).ToArray();
    }
  }
}
