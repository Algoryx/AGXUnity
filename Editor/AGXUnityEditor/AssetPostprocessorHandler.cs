using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;

namespace AGXUnityEditor
{
  public class AssetPostprocessorHandler : AssetPostprocessor
  {
    private class CollisionGroupEntryEqualityComparer : IEqualityComparer<CollisionGroupEntry>
    {
      public bool Equals( CollisionGroupEntry cg1, CollisionGroupEntry cg2 )
      {
        return cg1.Tag == cg2.Tag;
      }

      public int GetHashCode( CollisionGroupEntry entry )
      {
        return entry.Tag.GetHashCode();
      }
    }

    private static bool m_isProcessingPrefabInstance = false;

    /// <summary>
    /// Callback when a prefab is created from a scene game object <paramref name="go"/>,
    /// i.e., drag-dropped from hierarchy to the assets folder.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    public static void OnPrefabCreatedFromScene( GameObject instance )
    {
      // Avoiding recursion when we're manipulating the prefab from inside
      // this callback.
      if ( m_isProcessingPrefabInstance )
        return;

      var isAGXPrefab = instance.GetComponent<AGXUnity.IO.RestoredAGXFile>() != null;

      // Collect group ids that are disabled in the CollisionGroupsManager so that
      // when this prefab is added to a scene, the disabled collisions will be
      // added again.
      if ( !isAGXPrefab && CollisionGroupsManager.HasInstance ) {
#if UNITY_2018_1_OR_NEWER
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource( instance ) as GameObject;
#else
        var prefab = PrefabUtility.GetPrefabParent( instance ) as GameObject;
#endif
        if ( prefab != null ) {
          try {
            m_isProcessingPrefabInstance = true;

            var groups = prefab.GetComponentsInChildren<CollisionGroups>();
            var tags   = ( from componentGroups
                           in groups
                           from tag
                           in componentGroups.Groups
                           select tag ).Distinct( new CollisionGroupEntryEqualityComparer() );
            var disabledPairs = from tag1
                                in tags
                                from tag2
                                in tags
                                where !CollisionGroupsManager.Instance.GetEnablePair( tag1.Tag, tag2.Tag )
                                select new AGXUnity.IO.GroupPair() { First = tag1.Tag, Second = tag2.Tag };
            if ( disabledPairs.Count() > 0 ) {
#if UNITY_2017_3_OR_NEWER
#if UNITY_2018_3_OR_NEWER
              var savedData = instance.GetComponent<AGXUnity.IO.SavedPrefabLocalData>();
              if ( savedData == null ) {
                savedData = instance.AddComponent<AGXUnity.IO.SavedPrefabLocalData>();
                PrefabUtility.ApplyAddedComponent( savedData, AssetDatabase.GetAssetPath( prefab ), InteractionMode.AutomatedAction );
              }
#else
              var savedData = prefab.GetComponent<AGXUnity.IO.SavedPrefabLocalData>();
              if ( savedData == null )
                savedData = prefab.AddComponent<AGXUnity.IO.SavedPrefabLocalData>();
#endif
#endif
              foreach ( var disabledPair in disabledPairs )
                savedData.AddDisabledPair( disabledPair.First, disabledPair.Second );

#if UNITY_2018_3_OR_NEWER
              PrefabUtility.ApplyPrefabInstance( instance, InteractionMode.AutomatedAction );
#else
              EditorUtility.SetDirty( prefab );
#endif
            }
          }
          finally {
            m_isProcessingPrefabInstance = false;
          }
        }
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

      var contactMaterialManager = TopMenu.GetOrCreateUniqueGameObject<ContactMaterialManager>();
      Undo.RecordObject( contactMaterialManager, "Adding contact materials" );
      foreach ( var cm in IO.Utils.FindAssetsOfType<ContactMaterial>( fileInfo.DataDirectory ) )
        contactMaterialManager.Add( cm );

      var fileData = fileInfo.ExistingPrefab.GetComponent<AGXUnity.IO.RestoredAGXFile>();
      var collisionGroupsManager = TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>();
      Undo.RecordObject( collisionGroupsManager, "Adding disabled collision groups" );
      foreach ( var disabledPair in fileData.DisabledGroups )
        collisionGroupsManager.SetEnablePair( disabledPair.First, disabledPair.Second, false );

      var renderDatas = instance.GetComponentsInChildren<AGXUnity.Rendering.ShapeVisual>();
      Undo.RecordObjects( renderDatas, "Applying render data hide flags" );
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
