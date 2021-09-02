using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO.URDF;

using Material = AGXUnity.IO.URDF.Material;
using Collision = AGXUnity.IO.URDF.Collision;

namespace AGXUnityEditor.IO.URDF
{
  public static class Prefab
  {
    /// <summary>
    /// Create prefab of the given <paramref name="rootGameObject"/> containing
    /// the given URDF Model <paramref name="model"/>. If <paramref name="rootGameObject"/>
    /// is null, the current selected game object will be used if it contains
    /// <paramref name="model"/>. A folder panel will be opened to select directory
    /// where the prefab and assets should be saved.
    /// </summary>
    /// <param name="model">URDF Model to save assets for.</param>
    /// <param name="rootGameObject">Root game object instance to become prefab.</param>
    /// <returns>Prefab game object.</returns>
    public static GameObject Create( Model model, GameObject rootGameObject = null )
    {
      if ( model == null )
        return null;

      var directory = OpenFolderPanel( "URDF Prefab Directory" );
      if ( string.IsNullOrEmpty( directory ) )
        return null;

      return Create( model, rootGameObject, directory );
    }

    /// <summary>
    /// Create prefab of the given <paramref name="rootGameObject"/> containing
    /// the given URDF Model <paramref name="model"/>. If <paramref name="rootGameObject"/>
    /// is null, the current selected game object will be used if it contains
    /// <paramref name="model"/>.
    /// </summary>
    /// <param name="model">URDF Model to save assets for.</param>
    /// <param name="rootGameObject">Root game object instance to become prefab.</param>
    /// <param name="directory">Directory to save the prefab and other assets.</param>
    /// <returns>Prefab game object.</returns>
    public static GameObject Create( Model model,
                                     GameObject rootGameObject,
                                     string directory )
    {
      if ( model == null )
        return null;

      if ( !directory.StartsWith( "Assets" ) )
        directory = FileUtil.GetProjectRelativePath( directory );
      if ( !AssetDatabase.IsValidFolder( directory ) ) {
        Debug.LogError( $"URDF Prefab: Unable to create URDF prefab, directory {directory} isn't a valid project folder." );
        return null;
      }

      rootGameObject = FindModelGameObject( model, rootGameObject );

      var rootAssetsName = rootGameObject != null ?
                             rootGameObject.name :
                             model.Name;
      if ( !CreateAssets( model, rootGameObject, directory, rootAssetsName ) )
        return null;

      var isPrefab = PrefabUtility.GetPrefabInstanceStatus( rootGameObject ) == PrefabInstanceStatus.Connected;
      GameObject prefab = null;
      var prefabPath = $"{directory}/{rootAssetsName}.prefab";
      if ( isPrefab )
        prefab = PrefabUtility.GetCorrespondingObjectFromSource( rootGameObject );
      else {
        prefab = PrefabUtility.SaveAsPrefabAssetAndConnect( rootGameObject,
                                                            prefabPath,
                                                            InteractionMode.AutomatedAction );
      }
      if ( prefab != null )
        Debug.Log( $"URDF Prefab: Prefab {prefab.name} successfully saved in {AssetDatabase.GetAssetPath( prefab )}." );

      return prefab;
    }

    /// <summary>
    /// Create all assets (URDF Elements, meshes, render materials) given
    /// URDF model and root game object. The root game object is important
    /// in order to find STL meshes and render materials. If root game object
    /// is null, current selection is examined to contain <paramref name="model"/>.
    /// </summary>
    /// <param name="model">URDF Model to save assets for.</param>
    /// <param name="rootGameObject">Root game object instance containing the model in its hierarchy.</param>
    /// <returns></returns>
    public static bool CreateAssets( Model model,
                                     GameObject rootGameObject = null )
    {
      if ( model == null )
        return false;

      var directory = OpenFolderPanel( "URDF Assets Directory" );
      if ( string.IsNullOrEmpty( directory ) )
        return false;

      rootGameObject = FindModelGameObject( model, rootGameObject );
      return CreateAssets( model,
                           rootGameObject,
                           directory, rootGameObject != null ?
                             rootGameObject.name :
                             model.Name );
    }

    /// <summary>
    /// Create all assets (URDF Elements, meshes, render materials) given
    /// root game object containing an URDF Model in its hierarchy.
    /// </summary>
    /// <param name="rootGameObject">Root game object containing an URDF Model in its hierarchy.</param>
    /// <param name="directory">Directory in project where the assets should be saved.</param>
    /// <param name="rootAssetsName">Common name of main assets (without extension) - normally rootGameObject.name.</param>
    /// <returns>True if save is successful, otherwise false.</returns>
    public static bool CreateAssets( GameObject rootGameObject,
                                     string directory,
                                     string rootAssetsName )
    {
      var model = AGXUnity.IO.URDF.Utils.GetElementInChildren<Model>( rootGameObject );
      if ( model == null ) {
        Debug.LogError( $"URDF Prefab: Unable to create assets, URDF Model not present in root game object.",
                        rootGameObject );
        return false;
      }

      return CreateAssets( model, rootGameObject, directory, rootAssetsName );
    }

    /// <summary>
    /// Create all assets (URDF Elements, meshes, render materials) given
    /// URDF Model instance and root game object. If root game object is
    /// null, STL meshes and render materials won't be saved (warning in Console).
    /// </summary>
    /// <param name="model"></param>
    /// <param name="rootGameObject">Root game object containing an URDF Model in its hierarchy.</param>
    /// <param name="directory">Directory in project where the assets should be saved.</param>
    /// <param name="rootAssetsName">Common name of main assets (without extension) - normally rootGameObject.name.</param>
    /// <returns>True if save is successful, otherwise false.</returns>
    public static bool CreateAssets( Model model,
                                     GameObject rootGameObject,
                                     string directory,
                                     string rootAssetsName )
    {
      if ( model == null ) {
        Debug.LogError( $"URDF Prefab: Unable to create assets, URDF Model instance is null." );
        return false;
      }

      if ( !directory.StartsWith( "Assets" ) )
        directory = FileUtil.GetProjectRelativePath( directory );
      if ( !AssetDatabase.IsValidFolder( directory ) ) {
        Debug.LogError( $"URDF Prefab: Unable to create URDF prefab, directory {directory} isn't a valid project folder." );
        return false;
      }

      // Collecting STL meshes and visual with materials.
      var meshesToSave = new List<Mesh>();
      var materialsToSave = new List<UnityEngine.Material>();
      var collisions = new List<Collision>();
      var visuals = new List<Visual>();
      var geometries = new List<Geometry>();
      var urdfMaterials = new List<Material>();
      System.Action<Link> CollectAndNameLinkAssets = link =>
      {
        CollectAndNameAssets( link.Collisions,
                              collision => collision,
                              link.name,
                              "Collision",
                              collisions );
        CollectAndNameAssets( link.Visuals,
                              visual => visual,
                              link.name,
                              "Visual",
                              visuals );
        CollectAndNameAssets( link.Collisions,
                              collision => collision.Geometry,
                              link.name,
                              "CollisionGeometry",
                              geometries );
        CollectAndNameAssets( link.Visuals,
                              visual => visual.Geometry,
                              link.name,
                              "VisualGeometry",
                              geometries );
        CollectAndNameAssets( link.Visuals,
                              visual => visual.Material,
                              link.name,
                              "VisualMaterial",
                              urdfMaterials );
      };

      // Traversing modelGameObject (if valid) to collect visual materials
      // and STL meshes. Those two entities are only available if we have
      // access to the game object and components. Geometries are also
      // collected and named here.
      if ( rootGameObject != null ) {
        Traverse( rootGameObject, ( go, element ) =>
        {
          if ( element is Link link ) {
            CollectAndNameLinkAssets( link );
          }
          else if ( element is Collision collision ) {
            if ( collision.Geometry.Type == Geometry.GeometryType.Mesh &&
                 collision.Geometry.ResourceType == Geometry.MeshResourceType.STL &&
                 go.GetComponent<AGXUnity.Collide.Mesh>() != null ) {
              var collisionMesh = go.GetComponent<AGXUnity.Collide.Mesh>();
              CollectAndNameAssets( collisionMesh.SourceObjects,
                                    cMesh => cMesh,
                                    go.name,
                                    "CollisionMesh",
                                    meshesToSave );
            }
          }
          else if ( element is Visual visual ) {
            var isMesh = visual.Geometry.Type == Geometry.GeometryType.Mesh;
            var saveMaterial = visual.Material != null ||
                               ( 
                                 isMesh &&
                                 visual.Geometry.ResourceType == Geometry.MeshResourceType.STL
                               );
            var saveMesh = isMesh && visual.Geometry.ResourceType == Geometry.MeshResourceType.STL;
            if ( saveMaterial ) {
              var renderers = go.GetComponentsInChildren<MeshRenderer>();
              CollectAndNameAssets( renderers,
                                    renderer => renderer.sharedMaterial,
                                    go.name,
                                    "RenderMaterial",
                                    materialsToSave );
            }
            if ( saveMesh ) {
              var filters = go.GetComponentsInChildren<MeshFilter>();
              CollectAndNameAssets( filters,
                                    filter => filter.sharedMesh,
                                    go.name,
                                    "RenderMesh",
                                    meshesToSave );
            }
          }
        } );
      }
      else {
        Debug.LogWarning( $"URDF Prefab: The model game object is null, STL meshes and visual materials " +
                          $"won't be exported as assets.", model );

        foreach ( var link in model.Links )
          CollectAndNameLinkAssets( link );
      }

      materialsToSave = materialsToSave.Distinct().ToList();
      var mainMaterialAsset = materialsToSave.FirstOrDefault( renderMaterial => !IsAsset( renderMaterial ) );

      var numCreatedAssets = 0;
      var numAlreadyExistingAssets = 0;
      CreateOrUpdateMainAsset( model,
                               $"{directory}/{rootAssetsName}_Model.asset",
                               ref numCreatedAssets,
                               ref numAlreadyExistingAssets );

      if ( mainMaterialAsset != null ) {
        CreateOrUpdateMainAsset( mainMaterialAsset,
                                 $"{directory}/{rootAssetsName}_VisualMaterials.mat",
                                 ref numCreatedAssets,
                                 ref numAlreadyExistingAssets );
        for ( int i = 1; i < materialsToSave.Count; ++i )
          AddObjectToAsset( materialsToSave[ i ],
                            mainMaterialAsset,
                            ref numCreatedAssets,
                            ref numAlreadyExistingAssets );
      }

      meshesToSave = meshesToSave.Distinct().ToList();
      foreach ( var mesh in meshesToSave )
        AddObjectToAsset( mesh, model, ref numCreatedAssets, ref numAlreadyExistingAssets );

      foreach ( var material in urdfMaterials )
        AddObjectToAsset( material, model, ref numCreatedAssets, ref numAlreadyExistingAssets );
      foreach ( var geometry in geometries )
        AddObjectToAsset( geometry, model, ref numCreatedAssets, ref numAlreadyExistingAssets );
      foreach ( var collision in collisions )
        AddObjectToAsset( collision, model, ref numCreatedAssets, ref numAlreadyExistingAssets );
      foreach ( var visual in visuals )
        AddObjectToAsset( visual, model, ref numCreatedAssets, ref numAlreadyExistingAssets );
      foreach ( var link in model.Links )
        AddObjectToAsset( link, model, ref numCreatedAssets, ref numAlreadyExistingAssets );
      foreach ( var joint in model.Joints )
        AddObjectToAsset( joint, model, ref numCreatedAssets, ref numAlreadyExistingAssets );

      AssetDatabase.Refresh();
      AssetDatabase.SaveAssets();

      Debug.Log( $"URDF Prefab: {numCreatedAssets} created and {numAlreadyExistingAssets} already existing assets " +
                 $"successfully saved to {directory}/{rootAssetsName}." );

      return true;
    }

    private static void CreateOrUpdateMainAsset( Object mainAsset,
                                                 string path,
                                                 ref int numCreatedAssets,
                                                 ref int numAlreadyExistingAssets )
    {
      if ( IsAsset( mainAsset ) ) {
        // Not sure if there's something that may have changed that
        // we should take care of here. If it's already on disk,
        // Unity should take care of it.
        ++numAlreadyExistingAssets;
        return;
      }

      AssetDatabase.CreateAsset( mainAsset, path );
      ++numCreatedAssets;
    }

    private static void AddObjectToAsset( Object asset,
                                          Object mainAsset,
                                          ref int numCreatedAssets,
                                          ref int numAlreadyExistingAssets )
    {
      if ( IsAsset( asset ) ) {
        // Don't think we want to move 'asset' to 'mainAsset'
        // in the case where 'asset' is saved elsewhere.
        ++numAlreadyExistingAssets;
        return;
      }

      AssetDatabase.AddObjectToAsset( asset, mainAsset );
      ++numCreatedAssets;
    }

    private static void Traverse( GameObject go, System.Action<GameObject, Element> callback )
    {
      if ( go == null || callback == null )
        return;

      var element = go.GetComponent<ElementComponent>()?.Element;
      if ( element != null )
        callback( go, element );

      foreach ( Transform child in go.transform )
        Traverse( child.gameObject, callback );
    }

    private static void CollectAndNameAssets<T, U>( T[] origins,
                                                    System.Func<T, U> transformer,
                                                    string parentName,
                                                    string identifier,
                                                    List<U> result )
      where T : Object
      where U : Object
    {
      var counter = 0;
      foreach ( var origin in origins ) {
        var asset = transformer( origin );
        if ( asset == null || result.Contains( asset ) )
          continue;

        if ( string.IsNullOrEmpty( asset.name ) )
          asset.name = $"{parentName}_{identifier}" +
                       ( origins.Length > 1 ? $"_{counter++}" :
                         string.Empty );

        result.Add( asset );
      }
    }

    private static bool IsAsset( Object @object )
    {
      return @object != null &&
             !string.IsNullOrEmpty( AssetDatabase.GetAssetPath( @object.GetInstanceID() ) );
    }

    public static GameObject FindModelGameObject( Model model, GameObject rootGameObject )
    {
      // Check if the model is saved from click in the Inspector,
      // and the model belongs to the selected game object.
      if ( rootGameObject == null &&
           AGXUnity.IO.URDF.Utils.GetElementInChildren<Model>( Selection.activeGameObject ) == model ) {
        rootGameObject = Selection.activeGameObject;
      }
      // Check so that modelGameObject is the root of the model.
      else if ( rootGameObject != null &&
                !AGXUnity.IO.URDF.Utils.GetElementsInChildren<Model>( rootGameObject ).Contains( model ) ) {
        Debug.LogWarning( $"URDF Prefab: Given model game object \"{rootGameObject.name}\" doesn't contain the " +
                          $"given URDF model \"{model.Name}\". Ignoring the given game object.", rootGameObject );
        rootGameObject = null;
      }

      return rootGameObject;
    }

    public static string OpenFolderPanel( string title )
    {
      if ( !System.IO.Directory.Exists( CachedCreatedDirectoryData.String ) )
        CachedCreatedDirectoryData.String = "Assets";

      var directory = EditorUtility.OpenFolderPanel( title,
                                                     CachedCreatedDirectoryData.String,
                                                     string.Empty );
      if ( string.IsNullOrEmpty( directory ) )
        return string.Empty;

      if ( !directory.StartsWith( "Assets" ) )
        directory = FileUtil.GetProjectRelativePath( directory );

      CachedCreatedDirectoryData.String = directory;

      if ( System.IO.Directory.Exists( directory ) && !AssetDatabase.IsValidFolder( directory ) ) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      return directory;
    }

    private static EditorDataEntry CachedCreatedDirectoryData => EditorData.Instance.GetStaticData( "URDF.Prefab_CreateDirectory",
                                                                                                    entry => entry.String = "Assets" );
  }
}
