using UnityEngine;
using UnityEditor;
using AGXUnity;

namespace AGXUnityEditor
{
  public class AssetPostprocessorHandler : AssetPostprocessor
  {
    public static Object ReadAGXFile( string path )
    {
      return ReadAGXFile( new IO.AGXFileInfo( path ) );
    }

    public static Object ReadAGXFile( IO.AGXFileInfo info )
    {
      if ( info == null || !info.IsValid )
        return null;

      try {
        Object prefab = null;
        using ( var inputFile = new IO.InputAGXFile( info ) ) {
          inputFile.TryLoad();
          inputFile.TryParse();
          inputFile.TryGenerate();
          prefab = inputFile.TryCreatePrefab();
        }

        return prefab;
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }

      return null;
    }

    private static void OnPrefabUpdate( GameObject go )
    {
#if UNITY_2018_3_OR_NEWER
      // Can't remember why we do this and we're not allowed to add
      // components to a prefab.
      Debug.Log( "Update: " + go.name + ", instance status: " + PrefabUtility.GetPrefabInstanceStatus( go ) + ", prefab status: " + PrefabUtility.GetPrefabAssetType( go ) );
      Debug.Log( "Instance id: " + go.GetInstanceID() + ", source id: " + PrefabUtility.GetCorrespondingObjectFromSource<GameObject>( go ).GetInstanceID() );
#else
      // Collecting disabled collision groups for the created prefab.
      if ( AGXUnity.CollisionGroupsManager.HasInstance ) {
#if UNITY_2018_1_OR_NEWER
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource( go ) as GameObject;
#else
        var prefab = PrefabUtility.GetPrefabParent( go ) as GameObject;
#endif
        if ( prefab != null ) {
          var allGroups = prefab.GetComponentsInChildren<AGXUnity.CollisionGroups>();
          var tags = ( from objectGroups
                       in allGroups
                       from tag
                       in objectGroups.Groups
                       select tag ).Distinct( new CollisionGroupEntryEqualityComparer() ).ToList();
          var disabledPairs = new List<AGXUnity.IO.GroupPair>();
          foreach ( var t1 in tags )
            foreach ( var t2 in tags )
              if ( !AGXUnity.CollisionGroupsManager.Instance.GetEnablePair( t1.Tag, t2.Tag ) )
                disabledPairs.Add( new AGXUnity.IO.GroupPair() { First = t1.Tag, Second = t2.Tag } );

          if ( disabledPairs.Count > 0 ) {
            var prefabLocalData = prefab.GetOrCreateComponent<AGXUnity.IO.SavedPrefabLocalData>();
            foreach ( var groupPair in disabledPairs )
              prefabLocalData.AddDisabledPair( groupPair.First, groupPair.Second );
            EditorUtility.SetDirty( prefabLocalData );
          }
        }
      }
#endif
    }

    /// <summary>
    /// Callback when a prefab is created from a scene game object <paramref name="go"/>,
    /// i.e., drag-dropped from hierarchy to the assets folder.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    public static void OnPrefabCreatedFromScene( GameObject instance )
    {
      // Collect group ids that are disabled in the CollisionGroupsManager so that
      // when this prefab is added to a scene, the disabled collisions will be
      // added again.
      if ( CollisionGroupsManager.HasInstance ) {
      }
    }

    /// <summary>
    /// Callback when a prefab (likely) has been drag-dropped into a scene.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    public static void OnPrefabAddedToScene( GameObject instance )
    {
      if ( AutoUpdateSceneHandler.VerifyPrefabInstance( instance ) ) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      OnSavedPrefabAddedToScene( instance, instance.GetComponent<AGXUnity.IO.SavedPrefabLocalData>() );

      var fileInfo = new IO.AGXFileInfo( instance );
      if ( fileInfo.IsValid && fileInfo.Type == IO.AGXFileInfo.FileType.AGXPrefab )
        OnAGXPrefabAdddedToScene( instance, fileInfo );
    }

    private static void OnAGXPrefabAdddedToScene( GameObject instance, IO.AGXFileInfo fileInfo )
    {
      if ( fileInfo.ExistingPrefab == null ) {
        Debug.LogWarning( "Unable to load parent prefab from file: " + fileInfo.NameWithExtension );
        return;
      }

      Undo.SetCurrentGroupName( "Adding: " + instance.name + " to scene." );
      var grouId = Undo.GetCurrentGroup();

      foreach ( var cm in fileInfo.GetAssets<ContactMaterial>() )
        TopMenu.GetOrCreateUniqueGameObject<ContactMaterialManager>().Add( cm );

      var fileData = fileInfo.ExistingPrefab.GetComponent<AGXUnity.IO.RestoredAGXFile>();
      foreach ( var disabledPair in fileData.DisabledGroups )
        TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>().SetEnablePair( disabledPair.First, disabledPair.Second, false );

      var renderDatas = instance.GetComponentsInChildren<AGXUnity.Rendering.ShapeVisual>();
      foreach ( var renderData in renderDatas ) {
        renderData.hideFlags |= HideFlags.NotEditable;
        renderData.transform.hideFlags |= HideFlags.NotEditable;
      }

      // TODO: Handle fileData.SolverSettings?

      Undo.CollapseUndoOperations( grouId );
    }

    private static void OnSavedPrefabAddedToScene( GameObject instance, AGXUnity.IO.SavedPrefabLocalData savedPrefabData )
    {
      if ( savedPrefabData == null || savedPrefabData.DisabledGroups.Length == 0 )
        return;

      Undo.SetCurrentGroupName( "Adding prefab data for " + instance.name + " to scene." );
      var grouId = Undo.GetCurrentGroup();
      foreach ( var disabledGroup in savedPrefabData.DisabledGroups )
        TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>().SetEnablePair( disabledGroup.First, disabledGroup.Second, false );
      Undo.CollapseUndoOperations( grouId );
    }
  }
}
