using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.IO
{
  public class AGXFileInfo
  {
    /// <summary>
    /// Supported file type.
    /// </summary>
    public enum FileType
    {
      Unknown,
      AGXBinary,
      AGXAscii,
      AGXPrefab
    }

    /// <summary>
    /// Returns true if file is an existing AGX file.
    /// </summary>
    /// <param name="info">File info.</param>
    /// <returns>True if file exists and is an AGX file.</returns>
    public static bool IsExistingAGXFile( FileInfo info )
    {
      return info != null && info.Exists && ( info.Extension == ".agx" || info.Extension == ".aagx" );
    }

    /// <summary>
    /// Asset extension given instance.
    ///   - Material:  ".mat"
    ///   - CubeMap:   ".cubemap"
    ///   - Animation: ".anim"
    /// </summary>
    /// <param name="asset">Asset instance.</param>
    /// <returns>File extension including period.</returns>
    public static string FindAssetExtension( UnityEngine.Object asset )
    {
      return asset is Material ?
               ".mat" :
             asset is Cubemap ?
               ".cubemap" :
             asset is Animation ?
               ".anim" :
               ".asset";
    }

    /// <summary>
    /// Returns true if file is an existing prefab with corresponding .agx/.aagx file.
    /// </summary>
    /// <param name="info">File info.</param>
    /// <returns>True if the .prefab file is has a corresponding .agx/.aagx file.</returns>
    public bool IsExistingAGXPrefab( FileInfo info )
    {
      if ( info == null || !info.Exists || PrefabInstance == null )
        return false;

      var restoredFileInfo = PrefabInstance.GetComponent<AGXUnity.IO.RestoredAGXFile>();
      return restoredFileInfo != null &&
             AssetDatabase.IsValidFolder( AssetDatabase.GUIDToAssetPath( restoredFileInfo.DataDirectoryId ) );
    }

    /// <summary>
    /// Find file type given file info.
    /// </summary>
    /// <param name="info">File info.</param>
    /// <returns>File type.</returns>
    public FileType FindType( FileInfo info )
    {
      if ( info == null )
        return FileType.Unknown;

      return info.Extension == ".agx" ?
               FileType.AGXBinary :
             info.Extension == ".aagx" ?
               FileType.AGXAscii :
             IsExistingAGXPrefab( info ) ?
               FileType.AGXPrefab :
               FileType.Unknown;
    }
    
    /// <summary>
    /// True if valid path was given.
    /// </summary>
    public bool IsValid { get { return m_fileInfo != null; } }

    /// <summary>
    /// Name of the file - without extension.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Full name including absolute path.
    /// </summary>
    public string FullName { get { return m_fileInfo.FullName; } }

    /// <summary>
    /// Name of file including extension.
    /// </summary>
    public string NameWithExtension { get { return m_fileInfo.Name; } }

    /// <summary>
    /// Root directory - where the file is located.
    /// </summary>
    public string RootDirectory { get; private set; }

    /// <summary>
    /// File data directory path.
    /// </summary>
    public string DataDirectory { get; private set; }

    /// <summary>
    /// GUID of the data directory.
    /// </summary>
    public string DataDirectoryId { get { return AssetDatabase.AssetPathToGUID( DataDirectory ); } }

    /// <summary>
    /// Prefab name including (relative) path.
    /// </summary>
    public string PrefabPath { get { return RootDirectory + "/" + Name + ".prefab"; } }

    /// <summary>
    /// File type.
    /// </summary>
    public FileType Type { get; private set; }

    /// <summary>
    /// True if file exists.
    /// </summary>
    public bool Exists { get { return m_fileInfo.Exists; } }

    /// <summary>
    /// Prefab parent in project if it exist.
    /// </summary>
    public GameObject ExistingPrefab { get; private set; }

    /// <summary>
    /// Prefab instance (in scene) if it exist.
    /// </summary>
    public GameObject PrefabInstance { get; private set; }

    /// <summary>
    /// Object database with UUID -> game object and assets.
    /// </summary>
    public UuidObjectDb ObjectDb { get { return m_uuidObjectDb; } }

    /// <summary>
    /// Construct given path to file.
    /// </summary>
    /// <param name="path"></param>
    public AGXFileInfo( string path )
    {
      Construct( path );
    }

    /// <summary>
    /// Construct given prefab instance.
    /// </summary>
    /// <param name="prefabInstance"></param>
    public AGXFileInfo( GameObject prefabInstance )
    {
      PrefabInstance = prefabInstance;
#if UNITY_2018_1_OR_NEWER
      Construct( AssetDatabase.GetAssetPath( PrefabUtility.GetCorrespondingObjectFromSource( prefabInstance ) as GameObject ) );
#else
      Construct( AssetDatabase.GetAssetPath( PrefabUtility.GetPrefabParent( prefabInstance ) as GameObject ) );
#endif
    }

    /// <summary>
    /// Creates an instance from an existing project prefab or creates
    /// a new game object. Accessible trough this.PrefabInstance.
    /// </summary>
    /// <returns>this.PrefabInstance</returns>
    public GameObject CreateInstance()
    {
      if ( ExistingPrefab != null )
        PrefabInstance = GameObject.Instantiate<GameObject>( ExistingPrefab );
      else
        PrefabInstance = new GameObject( Name );

      m_uuidObjectDb = new UuidObjectDb( this );

      return PrefabInstance;
    }

    /// <summary>
    /// Creates data directory if it doesn't exists.
    /// </summary>
    /// <returns>Data directory info.</returns>
    public DirectoryInfo GetOrCreateDataDirectory()
    {
      if ( !AssetDatabase.IsValidFolder( DataDirectory ) )
        AssetDatabase.CreateFolder( RootDirectory, Name + "_Data" );

      return new DirectoryInfo( DataDirectory );
    }

    /// <summary>
    /// Find asset path (in the data directory) given asset name.
    /// If asset.name contains '\\', it will be replaced with '_'
    /// </summary>
    /// <param name="asset">Asset.</param>
    /// <returns>Path (relative) including .asset extension.</returns>
    public string GetAssetPath( UnityEngine.Object asset )
    {
      // We cannot have \\ in the name
      asset.name = asset.name.Replace("\\", "_");
      return DataDirectory + "/" +
             ( asset != null ? asset.name : "null" ) + FindAssetExtension( asset );
    }

    /// <summary>
    /// Add an asset to the data directory or updates an existing asset
    /// of same type and name with the current values of <paramref name="asset"/>.
    /// Known assets types will be grouped together and AGXFileInfo.GetAssets
    /// has to be used to find them.
    /// 
    /// Type AssetType.Unknown will always create a new asset.
    /// </summary>
    /// <param name="asset">Asset to add.</param>
    /// <param name="type">Asset type.</param>
    /// <returns><paramref name="asset"/> if previous version doesn't exist - otherwise the already existing asset.</returns>
    public T AddOrUpdateExistingAsset<T>( T asset, AGXUnity.IO.AssetType type )
      where T : UnityEngine.Object
    {
      if ( asset == null ) {
        Debug.LogWarning( "Trying to add null asset to file: " + NameWithExtension );
        return null;
      }

      // Grouping assets given known types - unknown types are written directly to the data folder.
      if ( type == AGXUnity.IO.AssetType.Unknown ) {
        AssetDatabase.CreateAsset( asset, GetAssetPath( asset ) );
        return asset;
      }

      T[] assets = GetAssets<T>();
      foreach ( var existing in assets ) {
        if ( existing.name == asset.name ) {
          EditorUtility.CopySerialized( asset, existing );
          return existing;
        }
      }

      if ( assets.Length == 0 )
        AssetDatabase.CreateAsset( asset, GetAssetPath( asset ) );
      else
        AssetDatabase.AddObjectToAsset( asset, AssetDatabase.GetAssetPath( assets[ 0 ] ) );

      return asset;
    }

    /// <summary>
    /// Finds assets of given type in the data directory.
    /// </summary>
    /// <typeparam name="T">Asset type.</typeparam>
    /// <returns>Array of assets of given type in the data directory.</returns>
    public T[] GetAssets<T>()
      where T : UnityEngine.Object
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
      var guids = AssetDatabase.FindAssets( "t:" + typeName, new string[] { DataDirectory } ).Distinct();
      return ( from guid
               in guids
               from obj
               in AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath( guid ) )
               select obj as T ).ToArray();
    }

    /// <summary>
    /// Creates prefab given source game object and returns the prefab if successful.
    /// </summary>
    /// <returns>Prefab if successful - otherwise null.</returns>
    public GameObject SavePrefab()
    {
      if ( PrefabInstance == null ) {
        Debug.LogWarning( "Trying to save prefab without an existing instance: " + Name );
        return null;
      }

      var prefab = ExistingPrefab ?? PrefabUtility.CreateEmptyPrefab( PrefabPath );
      if ( prefab == null )
        return null;

      return PrefabUtility.ReplacePrefab( PrefabInstance, prefab, ReplacePrefabOptions.ReplaceNameBased );
    }

    /// <summary>
    /// Saves assets and refreshes the database.
    /// </summary>
    public void Save()
    {
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
    }

    private void Construct( string path )
    {
      if ( path == "" )
        return;

      m_fileInfo = new FileInfo( path );
      Name       = Path.GetFileNameWithoutExtension( m_fileInfo.Name );
      Type       = FindType( m_fileInfo );

      // Data directory wasn't found. The user has to manually select the directory.
      bool updateExistingPrefabWithDirectoryId = false;
      if ( Type == FileType.Unknown && PrefabInstance != null && PrefabInstance.GetComponent<AGXUnity.IO.RestoredAGXFile>() != null ) {
        if ( EditorUtility.DisplayDialog( "Data directory for prefab not found",
                                          "Would you like to manually select directory for the given prefab?",
                                          "Yes", "No" ) ) {
          var dataDirectory    = EditorUtility.OpenFolderPanel( "Data directory for: " + PrefabInstance.name, m_fileInfo.Directory.FullName, "" );
          var relDataDirectory = Utils.MakeRelative( dataDirectory, Application.dataPath ).Replace( '\\', '/' );
          if ( AssetDatabase.IsValidFolder( relDataDirectory ) ) {
            Type = FileType.AGXPrefab;
            PrefabInstance.GetComponent<AGXUnity.IO.RestoredAGXFile>().DataDirectoryId = AssetDatabase.AssetPathToGUID( relDataDirectory );
            updateExistingPrefabWithDirectoryId = true;
          }
        }
      }

      RootDirectory = Utils.MakeRelative( m_fileInfo.Directory.FullName, Application.dataPath ).Replace( '\\', '/' );
      // If the file is located in the root Assets folder the relative directory
      // is the empty string and Unity requires the relative path to include "Assets".
      if ( RootDirectory == string.Empty )
        RootDirectory = "Assets";

      if ( Type == FileType.AGXPrefab )
        DataDirectory = AssetDatabase.GUIDToAssetPath( PrefabInstance.GetComponent<AGXUnity.IO.RestoredAGXFile>().DataDirectoryId );
      else
        DataDirectory = RootDirectory + "/" + Name + "_Data";

      ExistingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>( PrefabPath );
      if ( ExistingPrefab && updateExistingPrefabWithDirectoryId ) {
        ExistingPrefab.GetComponent<AGXUnity.IO.RestoredAGXFile>().DataDirectoryId = PrefabInstance.GetComponent<AGXUnity.IO.RestoredAGXFile>().DataDirectoryId;
        Save();
      }
    }

    private FileInfo m_fileInfo         = null;
    private UuidObjectDb m_uuidObjectDb = null;
  }
}
