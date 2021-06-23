using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
#endif

namespace AGXUnity.Utils
{
  public static class PrefabUtils
  {
    /// <summary>
    /// True if the prefab editing stage is active, otherwise false.
    /// </summary>
    public static bool IsEditingPrefab
    {
      get
      {
#if UNITY_EDITOR
        return PrefabStageUtility.GetCurrentPrefabStage() != null;
#else
        return false;
#endif
      }
    }

    /// <summary>
    /// Finds if <paramref name="gameObject"/> is part of a prefab, currently
    /// open in the prefab editing stage.
    /// </summary>
    /// <param name="gameObject">Game object to check.</param>
    /// <returns>True if <paramref name="gameObject"/> is currently being edited as part of a prefab.</returns>
    public static bool IsPartOfEditingPrefab( GameObject gameObject )
    {
#if UNITY_EDITOR
      var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
      return prefabStage != null &&
             prefabStage.IsPartOfPrefabContents( gameObject );
#else
      return false;
#endif
    }

    /// <summary>
    /// Place the given <paramref name="gameObject"/> in the current stage,
    /// normally the scene but may be a prefab being edited. If the prefab
    /// stage is open, the <paramref name="gameObject"/> will be added
    /// under the root prefab.
    /// </summary>
    /// <param name="gameObject"></param>
    public static void PlaceInCurrentStange( GameObject gameObject )
    {
#if UNITY_EDITOR
      StageUtility.PlaceGameObjectInCurrentStage( gameObject );
#endif
    }
  }
}
