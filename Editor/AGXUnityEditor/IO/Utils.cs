using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

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
    public static string ProjectDirectory
    {
      get
      {
        return Application.dataPath.Remove( Application.dataPath.LastIndexOf( "/Assets" ),
                                            "/Assets".Length );
      }
    }

    /// <summary>
    /// Directory of AGXUnity source code, i.e, package directory + /AGXUnity.
    /// </summary>
    public static string AGXUnitySourceDirectory
    {
      get
      {
        return AGXUnityPackageDirectory + "/AGXUnity";
      }
    }

    /// <summary>
    /// Absolute directory of AGXUnity source code, i.e, full package directory + /AGXUnity.
    /// </summary>
    public static string AGXUnitySourceDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnitySourceDirectory; }
    }

    /// <summary>
    /// AGXUnity resources directory, i.e, package directory + /Resources.
    /// </summary>
    public static string AGXUnityResourceDirectory
    {
      get { return AGXUnityPackageDirectory + "/Resources"; }
    }

    /// <summary>
    /// Absolute directory of AGXUnity resources, i.e., package directory full + /Resources.
    /// </summary>
    public static string AGXUnityResourceDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnityResourceDirectory; }
    }

    /// <summary>
    /// Native plugin directory, i.e., package directory + /Plugins/x86_64.
    /// </summary>
    public static string AGXUnityPluginDirectory
    {
      get { return AGXUnityPackageDirectory + "/Plugins/x86_64"; }
    }

    /// <summary>
    /// Native plugin directory, i.e., package directory full + /Plugins/x86_64.
    /// </summary>
    public static string AGXUnityPluginDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnityPluginDirectory; }
    }

    /// <summary>
    /// AGXUnity package editor directory, i.e., package directory + /Editor.
    /// </summary>
    public static string AGXUnityEditorDirectory
    {
      get { return AGXUnityPackageDirectory + "/Editor"; }
    }

    /// <summary>
    /// Absolute directory of AGXUnity editor, i.e. package directory full + /Editor.
    /// </summary>
    public static string AGXUnityEditorDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnityEditorDirectory; }
    }

    /// <summary>
    /// Directory of AGXUnityEditor source code, i.e, package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectory
    {
      get { return AGXUnityPackageDirectory + "/Editor/AGXUnityEditor"; }
    }

    /// <summary>
    /// Absolute directory of AGXUnityEditor source code, i.e, full package directory + /Editor/AGXUnityEditor.
    /// </summary>
    public static string AGXUnityEditorSourceDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnityEditorSourceDirectory; }
    }

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
    public static string AGXUnityPackageDirectoryFull
    {
      get { return ProjectDirectory + '/' + AGXUnityPackageDirectory; }
    }

    /// <summary>
    /// True if AGX Dynamics is assumed to be installed in the project,
    /// i.e., Unity hasn't been started with AGX Dynamics in path.
    /// </summary>
    public static bool AGXDynamicsInstalledInProject
    {
      get
      {
        var di = new DirectoryInfo( AGXUnityPluginDirectoryFull );
        if ( !di.Exists )
          throw new AGXUnity.Exception( "Unable to find AGXUnity plugins directory: " + di.FullName );

        var libExtension =
#if UNITY_EDITOR_LINUX
          ".so";
#else
          ".dll";
#endif
        var libsToFind = new string[] { "agxCore", "agxPhysics", "agxSabre" };
        int numFound = 0;
        foreach ( var file in di.EnumerateFiles( $"*{libExtension}" ) ) {
          if ( Array.FindIndex( libsToFind, name => $"{name}{libExtension}" == file.Name ||
                                                    $"lib{name}{libExtension}" == file.Name ) >= 0 )
            ++numFound;
        }

        return numFound == libsToFind.Length;
      }
    }

    /// <summary>
    /// Verifies so that each property (returning a directory path) above
    /// with property name containing AGXUnity and Directory is returning
    /// a path to a valid folder.  If the folder doesn't exist, a warning
    /// is issued.
    /// </summary>
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
      return complete.MakeRelative( root );
    }

    /// <summary>
    /// Checks whether <paramref name="folder"/> is a folder in the
    /// current project, i.e., including the parent folder containing
    /// the "Assets" folder.
    /// </summary>
    /// <param name="folder">Folder to check.</param>
    /// <returns>True if part of the project, otherwise false.</returns>
    public static bool IsValidProjectFolder( string folder )
    {
      if ( !Directory.Exists( folder ) )
        return false;

      // Located somewhere under the Assets folder.
      if ( AssetDatabase.IsValidFolder( folder ) )
        return true;

      var projectFolderFullName = new DirectoryInfo( Application.dataPath ).Parent.FullName;
      var folderFullName = new DirectoryInfo( folder ).FullName;
      return folderFullName.StartsWith( projectFolderFullName );
    }

    /// <summary>
    /// Finds assets of given type <typeparamref name="T"/> inside the given
    /// <paramref name="directory"/>.
    /// </summary>
    /// <typeparam name="T">Asset type.</typeparam>
    /// <param name="directory">Directory relative to the projects folder.</param>
    /// <returns>Array of assets of given type in the given directory.</returns>
    public static T[] FindAssetsOfType<T>( string directory )
      where T : Object
    {
      return ( from obj in FindAssetsOfType( directory, typeof( T ) )
               where obj is T
               select obj as T ).ToArray();
    }

    /// <summary>
    /// Finds assets of given type <paramref name="type"/> inside the given
    /// <paramref name="directory"/>.
    /// </summary>
    /// <typeparam name="T">Asset type.</typeparam>
    /// <param name="directory">Directory relative to the projects folder.</param>
    /// <param name="type">Asset type.</param>
    /// <returns>Array of assets of given type in the given directory.</returns>
    public static Object[] FindAssetsOfType( string directory, Type type )
    {
      // FindAssets will return same GUID for all grouped assets (AddObjectToAsset), so
      // if we have 17 ContactMaterial assets in a group FindAsset will return an array
      // of 17 where all entries are identical.
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
               select obj ).ToArray();
    }

    /// <summary>
    /// Finds selected assets with extension <paramref name="fileExtension"/>.
    /// </summary>
    /// <param name="fileExtension">File extension, e.g., ".txt"."</param>
    /// <param name="warnAboutDifferentExtensions">True to warn about assets that doesn't match <paramref name="fileExtension"/>.</param>
    /// <returns>Array of asset paths to selected assets of given <paramref name="fileExtension"/> extension.</returns>
    public static string[] GetSelectedFiles( string fileExtension,
                                             bool warnAboutDifferentExtensions = false )
    {
      if ( string.IsNullOrEmpty( fileExtension ) )
        return new string[] { };
      if ( fileExtension[ 0 ] != '.' )
        fileExtension = '.' + fileExtension;

      var filesWithCorrectExtension = new List<string>();
      var filesWithWrongExtension = new List<string>();
      foreach ( var guid in Selection.assetGUIDs ) {
        var assetPath = AssetDatabase.GUIDToAssetPath( guid );
        if ( string.IsNullOrEmpty( assetPath ) )
          continue;
        if ( Path.GetExtension( assetPath ).ToLower() == fileExtension.ToLower() )
          filesWithCorrectExtension.Add( assetPath );
        else if ( warnAboutDifferentExtensions )
          filesWithWrongExtension.Add( assetPath );
      }

      foreach ( var fileWithWrongExtension in filesWithWrongExtension )
        Debug.LogWarning( $"File extension of {AGXUnity.Utils.GUI.AddColorTag( fileWithWrongExtension, Color.red )} " +
                          $"doesn't match the given {fileExtension} extension." );

      return filesWithCorrectExtension.ToArray();
    }
  }
}
