using AGXUnity;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  /// <summary>
  /// Finds ScriptComponents in the scene and calls the EditorUpdate method when the 
  /// editor application updates in Edit Mode.
  /// </summary>
  [InitializeOnLoad]
  public class EditorUpdateManager : MonoBehaviour
  {
    static List<ScriptComponent> s_ScriptComponents = new List<ScriptComponent>();

    static EditorUpdateManager()
    {
      // Find references to ScriptComponents.
      // We perform one search initially and check all newly added components.
      // The list seems to be cleared when exiting Play Mode without calling this
      // constructor so we have to manually rebuild the cache when we re-enter edit mode.
      RebuildScriptCache();
      ObjectFactory.componentWasAdded += RebuildScriptCache;
      EditorApplication.hierarchyChanged += () => RebuildScriptCache();

      EditorApplication.playModeStateChanged += pmsc => {
        if ( pmsc == PlayModeStateChange.EnteredEditMode )
          RebuildScriptCache();

        if ( pmsc == PlayModeStateChange.EnteredEditMode )
          EditorApplication.update += RunUpdates;
        if ( pmsc == PlayModeStateChange.EnteredPlayMode )
          EditorApplication.update -= RunUpdates;
      };

      EditorApplication.update += RunUpdates;
    }

    static void RebuildScriptCache( Component addedComp = null )
    {
      // If a full rebuild was requested the entire cache is rebuild else we only add the script
      // of the added component if applicable
      if ( addedComp == null ) {
        s_ScriptComponents = new List<ScriptComponent>();

        // Find all ScriptComponents on all GameObjects
#if UNITY_6000_0_OR_NEWER
        var objects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
        var objects = FindObjectsOfType<GameObject>();
#endif
        foreach ( var o in objects ) {
          var scripts = o.GetComponents<ScriptComponent>();
          foreach ( var script in scripts )
            s_ScriptComponents.Add( script );
        }
      }
      else if ( addedComp is ScriptComponent script )
        s_ScriptComponents.Add( script );
    }

    static void RunUpdates()
    {
      foreach ( var script in s_ScriptComponents )
        if ( script != null )
          script.EditorUpdate();
    }
  }
}