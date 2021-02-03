using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.IO
{
  public class ObjectDb
  {
    public struct Statistics
    {
      public int NumAddedGameObjects;
      public int NumRemovedGameObjects;
      public int NumAddedAssets;
      public int NumRemovedAssets;
      public bool RootsAddedToExistingAssets;

      public bool HasAddedOrRemoved
      {
        get
        {
          return NumAddedGameObjects != 0 ||
                 NumRemovedGameObjects != 0 ||
                 NumAddedAssets != 0 ||
                 NumRemovedAssets != 0;
        }
      }
    }

    public static Object[] GetAssets( string dataDirectory, RestoredAssetsRoot.ContainingType assetType )
    {
      var root = Utils.FindAssetsOfType<RestoredAssetsRoot>( dataDirectory ).FirstOrDefault( assetRoot => assetRoot.Type == assetType );
      var assets = new List<Object>();
      if ( root != null )
        assets.Add( root );
      if ( assetType == RestoredAssetsRoot.ContainingType.Unknown ) {
        assets.AddRange( from type in s_unknownTypes
                         from asset in Utils.FindAssetsOfType( dataDirectory, type )
                         select asset );
      }
      else
        assets.AddRange( from asset in Utils.FindAssetsOfType( dataDirectory, RestoredAssetsRoot.GetType( assetType ) )
                         where asset.GetType() == RestoredAssetsRoot.GetType( assetType )
                         select asset );
      return assets.ToArray();
    }

    public ObjectDb( AGXFileInfo fileInfo )
    {
      m_dataDirectory = fileInfo.DataDirectory;
      m_filename      = fileInfo.Name;

      if ( fileInfo.PrefabInstance != null ) {
        var uuidGameObjects = fileInfo.PrefabInstance.GetComponentsInChildren<Uuid>( true );
        foreach ( var uuidComponent in uuidGameObjects )
          if ( !m_gameObjects.ContainsKey( uuidComponent.Native ) )
            m_gameObjects.Add( uuidComponent.Native,
                               new DbData()
                               {
                                 GameObject = uuidComponent.gameObject
                               } );
      }

      foreach ( var fileAssetType in RestoredAssetsRoot.Types )
        Initialize( fileAssetType );
    }

    public Statistics RemoveUnreferencedObjects( GameObject prefabInstance )
    {
      // If this is a re-import, remove any game object that hasn't been
      // referenced, i.e., UuidObjectDb.GetOrCreateGameObject hasn't been
      // called with the UUID of this game object. This doesn't affect game
      // objects created in the prefab editor since these objects doesn't
      // have an UUID component, still, other components added to this game
      // object will be removed as well.
      var unreferencedGameObjects = GetUnreferencedGameObjects();
      m_statistics.NumRemovedGameObjects += unreferencedGameObjects.Length;
      foreach ( var unreferencedGameObject in unreferencedGameObjects ) {
        // The unreferenced object could be a child of an already
        // destroyed GameObject.
        if ( unreferencedGameObject == null )
          continue;

        Debug.Log( $"{m_filename}: {GUI.AddColorTag( "Removing game object:", Color.yellow )} {unreferencedGameObject.name}" );
        Object.DestroyImmediate( unreferencedGameObject, true );
        EditorUtility.SetDirty( prefabInstance );
      }

      var unreferencedAssets = GetUnreferencedAssets();
      m_statistics.NumRemovedAssets += unreferencedAssets.Length;
      foreach ( var asset in unreferencedAssets ) {
        Debug.Log( $"{m_filename}: {GUI.AddColorTag( "Removing asset:", Color.red )} {asset.name}" );
#if UNITY_2018_1_OR_NEWER
        AssetDatabase.RemoveObjectFromAsset( asset );
#else
        Object.DestroyImmediate( asset, true );
#endif
      }

      return m_statistics;
    }

    public bool ContainsAsset<T>( T asset )
      where T : ScriptableObject
    {
      if ( asset == null )
        return false;

      return m_assets[ RestoredAssetsRoot.FindAssetTypeIndex<T>() ].ContainsKey( asset.GetHashCode() );
    }

    public bool ContainsAsset( Material material )
    {
      return m_assets[ (int)RestoredAssetsRoot.ContainingType.RenderMaterial ].ContainsKey( material.GetHashCode() );
    }

    public GameObject GetOrCreateGameObject( agx.Uuid uuid )
    {
      DbData data;
      if ( m_gameObjects.TryGetValue( uuid, out data ) ) {
        data.RefCount += 1;
        return data.GameObject;
      }

      data = new DbData() { GameObject = new GameObject(), RefCount = 1 };
      data.GameObject.AddComponent<Uuid>().Native = uuid;
      
      m_gameObjects.Add( uuid, data );

      ++m_statistics.NumAddedGameObjects;

      return data.GameObject;
    }

    public void Ref( agx.Uuid uuid )
    {
      if ( !m_gameObjects.ContainsKey( uuid ) ) {
        Debug.LogWarning( $"Unable to reference object with UUID: {uuid}" );
        return;
      }

      m_gameObjects[ uuid ].RefCount += 1;
    }

    public T GetOrCreateAsset<T>( T current,
                                  string name,
                                  System.Action<T> onFirstRef )
      where T : ScriptableObject
    {
      if ( onFirstRef == null )
        throw new System.ArgumentNullException( "onFirstRef" );

      return GetOrCreateAsset( current,
                               name,
                               onFirstRef,
                               () =>
                               {
                                 if ( typeof( AGXUnity.ScriptAsset ).IsAssignableFrom( typeof( T ) ) )
                                   return AGXUnity.ScriptAsset.Create( typeof( T ) ) as T;
                                 else
                                   return ScriptableObject.CreateInstance<T>();
                               } );
    }

    public T GetOrCreateAsset<T>( System.Predicate<T> predicate,
                                  string name,
                                  System.Action<T> onFirstRef )
      where T : ScriptableObject
    {
      if ( predicate == null )
        throw new System.ArgumentNullException( "predicate" );

      var assets = GetAssets<T>();
      T alreadyCreatedAsset = null;
      foreach ( var asset in assets ) {
        if ( predicate( asset ) ) {
          alreadyCreatedAsset = asset;
          break;
        }
      }

      return GetOrCreateAsset( alreadyCreatedAsset, name, onFirstRef );
    }

    public Mesh GetOrCreateAsset( Mesh mesh,
                                  string name,
                                  System.Action<Mesh> onFirstRef )
    {
      return GetOrCreateAsset( mesh,
                               name,
                               onFirstRef,
                               () => new Mesh() );
    }

    public Material GetOrCreateAsset( Material material,
                                      Shader shader,
                                      string name,
                                      System.Action<Material> onFirstRef )
    {
      if ( shader == null )
        throw new System.ArgumentNullException( "shader" );

      return GetOrCreateAsset( material,
                               name,
                               onFirstRef,
                               () => new Material( shader ) );
    }

    public Material GetOrCreateMaterial( Material material,
                                        string name,
                                        System.Action<Material> onFirstRef,
                                        System.Func<Material> factory )
    {
      return GetOrCreateAsset( material,
                               name,
                               onFirstRef,
                               factory );
    }

    public GameObject[] GetUnreferencedGameObjects()
    {
      return ( from uuidData in m_gameObjects
               where uuidData.Value.RefCount < 1
               select uuidData.Value.GameObject ).ToArray();
    }

    public Object[] GetUnreferencedAssets()
    {
      return ( from fileAssetType in RestoredAssetsRoot.Types
               from asset in GetAssets( fileAssetType )
               where !( asset is RestoredAssetsRoot ) && GetRefCount( asset ) < 1
               select asset ).ToArray();
    }

    public int GetRefCount( Object obj )
    {
      if ( obj == null )
        return -1;

      var dict = m_assets[ (int)RestoredAssetsRoot.FindAssetType( obj.GetType() ) ];
      if ( dict == null )
        return -1;

      AssetDbData data = null;
      if ( dict.TryGetValue( obj.GetHashCode(), out data ) )
        return data.RefCount;
      return -1;
    }

    private Object[] GetAssets( RestoredAssetsRoot.ContainingType assetType )
    {
      return GetAssets( m_dataDirectory, assetType );
    }

    private void Initialize( RestoredAssetsRoot.ContainingType assetType )
    {
      var dict = m_assets[ (int)assetType ] = new Dictionary<int, AssetDbData>();
      GetOrCreateRoot( assetType );

      var assets = GetAssets( assetType );
      foreach ( var asset in assets ) {
        if ( asset is RestoredAssetsRoot )
          continue;

        Debug.Assert( !dict.ContainsKey( asset.GetHashCode() ) );
        dict.Add( asset.GetHashCode(),
                  new AssetDbData()
                  {
                    Asset = asset,
                    RefCount = 0
                  } );
      }
    }

    private RestoredAssetsRoot GetRoot( RestoredAssetsRoot.ContainingType assetType )
    {
      Debug.Assert( m_assetRoots[ (int)assetType ] != null );
      return m_assetRoots[ (int)assetType ];
    }

    private RestoredAssetsRoot GetOrCreateRoot( RestoredAssetsRoot.ContainingType assetType )
    {
      var root = Utils.FindAssetsOfType<RestoredAssetsRoot>( m_dataDirectory ).FirstOrDefault( r => r != null &&
                                                                                               r.Type == assetType );
      if ( root == null ) {
        root = RestoredAssetsRoot.Create( m_filename, assetType );

        // Re-import of previous version without root if there are assets
        // of the given type. Add root and set it as main object.
        var existingAssets = GetAssets( assetType );
        if ( existingAssets.Length == 0 )
          AssetDatabase.CreateAsset( root, GetAssetPath( root ) );
        else {
          AssetDatabase.AddObjectToAsset( root,
                                          System.Array.Find( existingAssets,
                                                             asset => AssetDatabase.IsMainAsset( asset ) ) ??
                                          existingAssets[ 0 ] );
          m_statistics.RootsAddedToExistingAssets = true;
        }
      }

      return m_assetRoots[ (int)assetType ] = root;
    }

    private T GetOrCreateAsset<T>( T current,
                                   string name,
                                   System.Action<T> onFirstRef,
                                   System.Func<T> factory )
      where T : Object
    {
      if ( factory == null )
        throw new System.ArgumentNullException( "factory" );

      var isNewInstance = current == null;
      if ( current == null ) {
        if ( current == null )
          current = factory();
        else
          Debug.Assert( RestoredAssetsRoot.FindAssetType<T>() == RestoredAssetsRoot.ContainingType.Unknown );

        m_assets[ RestoredAssetsRoot.FindAssetTypeIndex<T>() ].Add( current.GetHashCode(),
                                                                    new AssetDbData()
                                                                    {
                                                                      Asset = current,
                                                                      RefCount = 0
                                                                    } );
      }

      var data = m_assets[ RestoredAssetsRoot.FindAssetTypeIndex<T>() ][ current.GetHashCode() ];
      if ( data.RefCount == 0 ) {
        onFirstRef( current );
        current.name = name;
        if ( isNewInstance ) {
          AssetDatabase.AddObjectToAsset( current, GetRoot( RestoredAssetsRoot.FindAssetType<T>() ) );
          ++m_statistics.NumAddedAssets;
        }
      }
      data.RefCount += 1;

      return current;
    }


    private T[] GetAssets<T>()
      where T : Object
    {
      return ( from pair in m_assets[ (int)RestoredAssetsRoot.FindAssetType<T>() ]
               where pair.Value.Asset is T
               select pair.Value.Asset as T ).ToArray();
    }

    /// <summary>
    /// Find asset path (in the data directory) given asset name.
    /// If asset.name contains '\\', it will be replaced with '_'
    /// </summary>
    /// <param name="asset">Asset.</param>
    /// <returns>Path (relative) including .asset extension.</returns>
    private string GetAssetPath( Object asset )
    {
      // We cannot have \\ in the name
      asset.name = asset.name.Replace( "\\", "_" );
      return m_dataDirectory + "/" +
             ( asset != null ? asset.name : "null" ) + AGXFileInfo.FindAssetExtension( asset.GetType() );
    }

    private class DbData
    {
      public GameObject GameObject = null;
      public int RefCount = 0;
    }

    private class AssetDbData
    {
      public Object Asset = null;
      public int RefCount = 0;
    }

    private Statistics m_statistics = new Statistics();
    private string m_dataDirectory = string.Empty;
    private string m_filename = string.Empty;
    private Dictionary<agx.Uuid, DbData> m_gameObjects = new Dictionary<agx.Uuid, DbData>( new UuidComparer() );
    private Dictionary<int, AssetDbData>[] m_assets = new Dictionary<int, AssetDbData>[ (int)RestoredAssetsRoot.ContainingType.Unknown + 1 ];
    private RestoredAssetsRoot[] m_assetRoots = new RestoredAssetsRoot[ (int)RestoredAssetsRoot.ContainingType.Unknown + 1 ];
    private static System.Type[] s_unknownTypes = new System.Type[] { typeof( AGXUnity.SolverSettings ) };
  }
}
