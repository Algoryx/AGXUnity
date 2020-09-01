using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

namespace AGXUnityEditor.IO
{
  public static class AGXFileImporter
  {
    public static GameObject[] Import( Object[] objects )
    {
      var prefabs = new List<GameObject>();
      foreach ( var obj in objects ) {
        var prefab = Import( obj );
        if ( prefab != null )
          prefabs.Add( prefab );
      }
      return prefabs.ToArray();
    }

    public static GameObject Import( Object @object )
    {
      var assetPath = AssetDatabase.GetAssetPath( @object );
      if ( string.IsNullOrEmpty( assetPath ) ||
           !( Path.GetExtension( assetPath ).ToLower() == ".agx" ||
              Path.GetExtension( assetPath ).ToLower() == ".aagx" ) )
        return null;
      return Import( assetPath );
    }

    public static GameObject Import( string file )
    {
      GameObject prefab = null;
      bool renameRoots = false;
      var dataDirectory = string.Empty;
      try {
        int iteration = 0;
        while ( ++iteration < 4 ) {
          var fileInfo = new AGXFileInfo( file );
          using ( var inputFile = new InputAGXFile( fileInfo ) ) {
            inputFile.TryLoad();
            inputFile.TryParse();
            var statistics = inputFile.TryGenerate();
            renameRoots = renameRoots || ( iteration == 1 && statistics.RootsAddedToExistingAssets );
            prefab = inputFile.TryCreatePrefab();
            dataDirectory = fileInfo.DataDirectory;

            if ( !statistics.HasAddedOrRemoved )
              break;
          }
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        return null;
      }

      PropagateChanges( prefab );

      if ( prefab != null && renameRoots && !string.IsNullOrEmpty( dataDirectory) ) {
        Debug.Log( $"{prefab.name.Color( Color.green )}: Updating main assets and names in " +
                   dataDirectory.Color( Color.gray ) + "." );
        var roots = Utils.FindAssetsOfType<AGXUnity.IO.RestoredAssetsRoot>( dataDirectory );
        foreach ( var root in roots ) {
          var assets = ObjectDb.GetAssets( dataDirectory, root.Type );
          var mainAsset = System.Array.Find( assets,
                                             asset => AssetDatabase.IsMainAsset( asset ) );
          if ( mainAsset == null )
            continue;
          else if ( !( mainAsset is AGXUnity.IO.RestoredAssetsRoot ) ) {
            AssetDatabase.SetMainObject( root, AssetDatabase.GetAssetPath( mainAsset ) );
            mainAsset = root;
          }

          AssetDatabase.RenameAsset( AssetDatabase.GetAssetPath( root ),
                                     AGXUnity.IO.RestoredAssetsRoot.FindName( prefab.name, root.Type ) );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      return prefab;
    }

    public static void PropagateChanges( GameObject prefab )
    {
      if ( prefab == null )
        return;

      var restoredFileInstances = Object.FindObjectsOfType<AGXUnity.IO.RestoredAGXFile>();
      foreach ( var restoredFileInstance in restoredFileInstances ) {
#if UNITY_2018_3_OR_NEWER
        var isReadPrefabInstance = PrefabUtility.GetPrefabInstanceStatus( restoredFileInstance.gameObject ) == PrefabInstanceStatus.Connected &&
                                   PrefabUtility.GetCorrespondingObjectFromSource( restoredFileInstance.gameObject ) == prefab;
#else
        var isReadPrefabInstance = PrefabUtility.GetPrefabType( restoredFileInstance.gameObject ) == PrefabType.PrefabInstance &&
#if UNITY_2018_1_OR_NEWER
                                   PrefabUtility.GetCorrespondingObjectFromSource( restoredFileInstance.gameObject ) == prefab;
#else
                                   PrefabUtility.GetPrefabParent( restoredFileInstance.gameObject ) == prefab;
#endif
#endif
        if ( !isReadPrefabInstance )
          continue;

        var shapes = restoredFileInstance.GetComponentsInChildren<AGXUnity.Collide.Shape>();
        foreach ( var shape in shapes ) {
          var visual = AGXUnity.Rendering.ShapeVisual.Find( shape );
          if ( visual != null )
            visual.OnSizeUpdated();

          var debugRenderData = shape.GetComponent<AGXUnity.Rendering.ShapeDebugRenderData>();
          if ( debugRenderData != null && debugRenderData.Node != null )
            Object.DestroyImmediate( debugRenderData.Node );
        }
      }
    }
  }
}
