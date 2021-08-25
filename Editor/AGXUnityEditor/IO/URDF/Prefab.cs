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
    public static GameObject Create( Model model, GameObject modelGameObject = null )
    {
      if ( model == null )
        return null;

      if ( !System.IO.Directory.Exists( CachedCreatedDirectoryData.String ) )
        CachedCreatedDirectoryData.String = "Assets";

      var directory = EditorUtility.OpenFolderPanel( "URDF Prefab Directory",
                                                     CachedCreatedDirectoryData.String,
                                                     string.Empty );
      if ( string.IsNullOrEmpty( directory ) )
        return null;

      directory = FileUtil.GetProjectRelativePath( directory );

      if ( System.IO.Directory.Exists( directory ) && !AssetDatabase.IsValidFolder( directory ) ) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      return Create( model, directory );
    }

    public static GameObject Create( Model model, string directory, GameObject modelGameObject = null )
    {
      if ( model == null )
        return null;

      directory = FileUtil.GetProjectRelativePath( directory );

      if ( !AssetDatabase.IsValidFolder( directory ) ) {
        Debug.LogWarning( $"URDF Prefab: Unable to create URDF prefab, directory {directory} isn't a valid project folder." );
        return null;
      }

      // Check if the model is saved from click in the Inspector,
      // and the model belongs to the selected game object.
      if ( modelGameObject == null &&
           Selection.activeGameObject?.GetComponent<ElementComponent>()?.Element as Model == model ) {
        modelGameObject = Selection.activeGameObject;
      }
      // Check so that modelGameObject is the root of the model.
      else if ( modelGameObject != null &&
                modelGameObject.GetComponent<ElementComponent>()?.Element as Model != model ) {
        Debug.LogWarning( $"URDF Prefab: Given model game object \"{modelGameObject.name}\" doesn't contain the " +
                          $"given URDF model \"{model.Name}\". Ignoring the given game object.", modelGameObject );
        modelGameObject = null;
      }

      CachedCreatedDirectoryData.String = directory;

      // Collecting STL meshes and visual with materials.
      var meshesToSave    = new List<Mesh>();
      var materialsToSave = new List<UnityEngine.Material>();
      var collisions      = new List<Collision>();
      var visuals         = new List<Visual>();
      var geometries      = new List<Geometry>();
      var urdfMaterials   = new List<Material>();
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
      if ( modelGameObject != null ) {
        Traverse( modelGameObject, ( go, element ) =>
        {
          if ( element is Link link ) {
            CollectAndNameLinkAssets( link );
          }
          else if ( element is Collision collision ) {
            if ( collision.Geometry.ResourceType == Geometry.MeshResourceType.STL &&
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
            var saveMaterial = visual.Material != null ||
                               visual.Geometry.ResourceType == Geometry.MeshResourceType.STL;
            var saveMesh = visual.Geometry.ResourceType == Geometry.MeshResourceType.STL;
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

      var rootAssetsName = modelGameObject != null ?
                             modelGameObject.name :
                             model.Name;
      AssetDatabase.CreateAsset( model, $"{directory}/{rootAssetsName}_Model.asset" );

      materialsToSave = ( from visualMaterial in materialsToSave
                          where !IsAsset( visualMaterial )
                          select visualMaterial ).Distinct().ToList();
      if ( materialsToSave.Count > 0 ) {
        AssetDatabase.CreateAsset( materialsToSave[ 0 ], $"{directory}/{rootAssetsName}_VisualMaterials.mat" );
        for ( int i = 1; i < materialsToSave.Count; ++i )
          AssetDatabase.AddObjectToAsset( materialsToSave[ i ], materialsToSave[ 0 ] );
      }

      meshesToSave = ( from mesh in meshesToSave
                       where !IsAsset( mesh )
                       select mesh ).Distinct().ToList();
      foreach ( var mesh in meshesToSave )
        AssetDatabase.AddObjectToAsset( mesh, model );

      foreach ( var material in urdfMaterials )
        AssetDatabase.AddObjectToAsset( material, model );
      foreach ( var geometry in geometries )
        AssetDatabase.AddObjectToAsset( geometry, model );
      foreach ( var collision in collisions )
        AssetDatabase.AddObjectToAsset( collision, model );
      foreach ( var visual in visuals )
        AssetDatabase.AddObjectToAsset( visual, model );
      foreach ( var link in model.Links )
        AssetDatabase.AddObjectToAsset( link, model );
      foreach ( var joint in model.Joints )
        AssetDatabase.AddObjectToAsset( joint, model );

      AssetDatabase.Refresh();
      AssetDatabase.SaveAssets();

      return null;
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

    private static EditorDataEntry CachedCreatedDirectoryData => EditorData.Instance.GetStaticData( "URDF.Prefab_CreateDirectory",
                                                                                                    entry => entry.String = "Assets" );
  }
}
