using AGXUnity;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds scripts containing methods marked with the AGXUnity.EditorUpdate attribute in the scene
/// and calls them manually when the editor application updates in Edit Mode.
/// </summary>
[InitializeOnLoad]
public class UpdateInEditMode : MonoBehaviour
{
  static List<System.Tuple<MonoBehaviour,MethodInfo>> invokeInfos = new List<System.Tuple<MonoBehaviour, MethodInfo>>();
  static List<System.Tuple<MethodInfo,List<MonoBehaviour>>> staticInvokeInfos = new List<System.Tuple<MethodInfo, List<MonoBehaviour>>>();

  static UpdateInEditMode()
  {
    // Find references to scripts with methods containing the EditorUpdateAttribute
    // We perform one search initially and check all newly added components.
    // The list seems to be cleared when exiting Play Mode without calling this
    // constructor so we have to manually rebuild the cache when we re-enter play mode.
    RebuildFunctionCache();
    ObjectFactory.componentWasAdded += RebuildFunctionCache;
    EditorApplication.hierarchyChanged += () => RebuildFunctionCache();
    EditorApplication.playModeStateChanged += pmsc =>
    {
      if ( pmsc == PlayModeStateChange.EnteredEditMode )
        RebuildFunctionCache();

      if ( pmsc == PlayModeStateChange.EnteredEditMode )
        EditorApplication.update += RunUpdates;
      if ( pmsc == PlayModeStateChange.EnteredPlayMode )
        EditorApplication.update -= RunUpdates;
    };

    EditorApplication.update += RunUpdates;
  }

  static void RebuildFunctionCache( Component addedComp = null )
  {
    // If a full rebuild was requested the entire cache is rebuild else we only add the script
    // of the added component if applicable
    if ( addedComp == null ) {
      invokeInfos = new List<System.Tuple<MonoBehaviour, MethodInfo>>();
      staticInvokeInfos = new List<System.Tuple<MethodInfo, List<MonoBehaviour>>>();

      // Find all MonoBehaviours on all GameObjects
      var objects = FindObjectsOfType<GameObject>();
      foreach ( var o in objects ) {
        var scripts = o.GetComponents<MonoBehaviour>();
        foreach ( var script in scripts )
          AddMonoBehaviour( script );
      }
    }
    else if ( addedComp is MonoBehaviour script )
      AddMonoBehaviour( script );
  }

  static void AddMonoBehaviour( MonoBehaviour script )
  {
    // Find all methods in script marked with EditorUpdateAttribute
    var scriptClass = script.GetType();
    var methods = scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
    foreach ( var method in methods ) {
      bool hasAttribute = System.Attribute.IsDefined( method, typeof( EditorUpdateAttribute), true );
      if ( hasAttribute ) {
        if ( !method.IsStatic )
          invokeInfos.Add( new System.Tuple<MonoBehaviour, MethodInfo>( script, method ) );
        else {
          var list = staticInvokeInfos.FirstOrDefault( ii => ii.Item1 == method ); 
          if ( list != null )
            list.Item2.Add( script );
          else
            staticInvokeInfos.Add( new System.Tuple<MethodInfo, List<MonoBehaviour>>( method, new List<MonoBehaviour> { script } ) );
        }
      }
    }
  }

  static void RunUpdates()
  {
    Debug.Log( "Updating" );
    for ( int i = 0; i < staticInvokeInfos.Count; i++ ) {
      var method = staticInvokeInfos[ i ].Item1;
      var list = staticInvokeInfos[ i ].Item2;
      for ( int j = 0; j < list.Count; j++ ) {
        if ( list[ j ] == null ) {
          list[ j ] = list[ list.Count - 1 ];
          list.RemoveAt( list.Count - 1 );
          j--;
        }
        else if ( j == 0 || method.GetCustomAttribute<EditorUpdateAttribute>().StaticCallMultiple )
          method.Invoke( null, null );
      }
      if ( list.Count == 0 ) {
        staticInvokeInfos[ i ] = staticInvokeInfos[ staticInvokeInfos.Count - 1 ];
        staticInvokeInfos.RemoveAt( staticInvokeInfos.Count - 1 );
        i--;
      }

    }

    for ( int i = 0; i < invokeInfos.Count; i++ ) {
      if ( invokeInfos[ i ].Item1 == null ) {
        invokeInfos[ i ] = invokeInfos[ invokeInfos.Count - 1 ];
        invokeInfos.RemoveAt( invokeInfos.Count - 1 );
        i--;
      }
      else
        invokeInfos[ i ].Item2.Invoke( invokeInfos[ i ].Item1, null );
    }
  }
}
