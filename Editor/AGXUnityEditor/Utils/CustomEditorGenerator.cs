using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using Assembly = System.Reflection.Assembly;

namespace AGXUnityEditor.Utils
{
  public static class CustomEditorGenerator
  {
    public static string Path { get { return IO.Utils.AGXUnityEditorDirectoryFull + "/CustomEditors/"; } }

    public static void Generate( Type type, bool refreshAssetDatabase = true )
    {
      GenerateEditor( type, Path );

      if ( refreshAssetDatabase )
        AssetDatabase.Refresh();
    }

    public static void Generate()
    {
      var types = GetAGXUnityTypes();
      foreach ( var type in types )
        Generate( type, false );

      AssetDatabase.Refresh();
    }

    public static Assembly GetAGXUnityAssembly()
    {
      return Assembly.Load( "Assembly-CSharp" );
    }

    public static Type[] GetAGXUnityTypes()
    {
      var assembly = GetAGXUnityAssembly();
      if ( assembly == null ) {
        Debug.LogWarning( "Updating custom editors failed - unable to load Assembly-CSharp.dll." );
        return new Type[] { };
      }

      return ( from type in assembly.GetTypes() where IsMatch( type ) select type ).ToArray();
    }

    public static string[] GetEditorFiles()
    {
      return Directory.GetFiles( Path, "*Editor.cs", SearchOption.TopDirectoryOnly );
    }

    public static System.Collections.Generic.IEnumerable<FileInfo> GetEditorFileInfos()
    {
      var files = GetEditorFiles();
      foreach ( var file in files )
        yield return new FileInfo( file );
    }

    public static void Synchronize()
    {
      // Newer versions replaces '.' with '+' so if our files
      // doesn't contains any '+' we regenerate all.
      {
        bool regenerate = false;
        foreach ( var info in GetEditorFileInfos() ) {
          if ( info.Name.StartsWith( "AGXUnity" ) && !info.Name.Contains( '+' ) ) {
            regenerate = true;
            break;
          }
        }

        if ( regenerate ) {
          Debug.Log( "Wrong version of custom editor files. Regenerating." );
          foreach ( var info in GetEditorFileInfos() )
            DeleteFile( info );
        }
      }

      bool assetDatabaseDirty = false;

      // Removing editors which classes has been removed.
      {
        var assembly = GetAGXUnityAssembly();
        foreach ( var info in GetEditorFileInfos() ) {
          string className = GetClassName( info.Name );
          Type type = assembly.GetType( className, false );
          if ( !IsMatch( type ) ) {
            Debug.Log( "Mismatching editor for class: " + className + ", removing custom editor." );
            DeleteFile( info );
            assetDatabaseDirty = true;
          }
        }
      }

      // Generating missing editors.
      {
        var types = GetAGXUnityTypes();
        foreach ( var type in types ) {
          FileInfo info = new FileInfo( GetFilename( type, true ) );
          if ( !info.Exists ) {
            Debug.Log( "Custom editor for class " + type.ToString() + " is missing. Generating." );
            GenerateEditor( type, Path );
            assetDatabaseDirty = true;
          }
        }
      }

      if ( assetDatabaseDirty )
        AssetDatabase.Refresh();
    }

    private static void DeleteFile( FileInfo info )
    {
      FileInfo meta = new FileInfo( info.Name + ".meta" );
      info.Delete();
      if ( meta.Exists )
        meta.Delete();
    }

    private static bool IsMatch( Type type )
    {
      return type != null &&
             !type.IsAbstract &&
             !type.ContainsGenericParameters &&
            ( type.IsSubclassOf( typeof( ScriptComponent ) ) ||
              type.IsSubclassOf( typeof( ScriptAsset ) ) ) &&
              type.GetCustomAttributes( typeof( DoNotGenerateCustomEditor ), false ).Length == 0;
    }

    private static string GetClassName( Type type )
    {
      return type.ToString().Replace( ".", string.Empty );
    }

    private static string GetTypeFilename( Type type )
    {
      return type.ToString().Replace( ".", "+" );
    }

    private static string GetClassName( string filename )
    {
      return filename.Replace( "Editor.cs", string.Empty ).Replace( '+', '.' );
    }

    private static string GetFilename( Type type, bool includePath )
    {
      string path = includePath ? Path : string.Empty;
      return path + GetTypeFilename( type ) + "Editor.cs";
    }

    private static void GenerateEditor( Type type, string path )
    {
      string classAndFilename = GetClassName( type );
      string csFileContent = @"
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( " + type.ToString() + @" ) )]
  public class " + classAndFilename + @"Editor : BaseEditor<" + type.ToString() + @">
  { }
}";
      File.WriteAllText( path + GetFilename( type, false ), csFileContent );
    }
  }
}
