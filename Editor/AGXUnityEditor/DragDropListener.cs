using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  public static class DragDropListener
  {
    public delegate void GameObjectArrayCallback( GameObject[] droppedInstances );

    public static GameObjectArrayCallback OnPrefabsDroppedInScene;

    static DragDropListener()
    {
      Selection.selectionChanged += OnSelectionChanged;
      EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
      // Not holding assets.
      if ( DragAndDrop.paths.Length == 0 )
        return;

      if ( IsValidDragState( DragAndDrop.visualMode ) ) {
        if ( s_assetDragData == null )
          s_assetDragData = PrefabAssetDragData.Create( DragAndDrop.paths );
      }
      // When dropping a prefab directly into the scene view we have that
      // DragAndDrop.visualMode == DragAndDropVisualMode.None for at least
      // one OnUpdate call. Defer the reset for some calls to catch the
      // OnSelectionChanged call.
      else if ( s_assetDragData != null && s_assetDragData.Value.IsExpired() )
        s_assetDragData = null;
    }

    private static void OnSelectionChanged()
    {
      if ( s_assetDragData != null ) {
        var selection = Selection.GetFiltered<GameObject>( SelectionMode.Editable | SelectionMode.TopLevel )
                                 .Where( selected =>
                                           s_assetDragData.Value.Prefabs.Contains( PrefabUtility.GetCorrespondingObjectFromSource( selected ) ) ).ToArray();
        if ( selection.Length > 0 )
          OnPrefabsDroppedInScene.Invoke( selection );

        s_assetDragData = null;
      }
    }

    private static bool IsValidDragState( DragAndDropVisualMode visualMode )
    {
      return visualMode == DragAndDropVisualMode.Copy ||
             visualMode == DragAndDropVisualMode.Link;
    }

    private struct PrefabAssetDragData
    {
      public static PrefabAssetDragData? Create( string[] assetPaths )
      {
        var paths = assetPaths.Where( path => Path.GetExtension( path ).ToLower() == ".prefab" );
        if ( paths.Count() == 0 )
          return null;

        return new PrefabAssetDragData()
        {
          Paths = paths.ToArray(),
          Prefabs = paths.Select( path => AssetDatabase.LoadAssetAtPath<GameObject>( path ) ).ToArray()
        };
      }

      public bool IsExpired( int maxNumUpdates = 4 )
      {
        ++m_invalidDragNumUpdates;
        return m_invalidDragNumUpdates >= maxNumUpdates;
      }

      public string[] Paths;
      public GameObject[] Prefabs;
      private int m_invalidDragNumUpdates;
    }

    private static PrefabAssetDragData? s_assetDragData = null;
  }
}
