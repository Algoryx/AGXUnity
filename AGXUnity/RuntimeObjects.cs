using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [ExecuteInEditMode]
  public class RuntimeObjects : UniqueGameObject<RuntimeObjects>
  {
    private Dictionary<MonoBehaviour, string> m_potentiallyDeletedParents = new Dictionary<MonoBehaviour, string>();

    public static GameObject GetOrCreateRoot( MonoBehaviour script )
    {
      if ( script == null )
        return null;

      var instance = RecoverAndGetInstance();
      if ( instance == null )
        return null;

      var name  = GetName( script );
      var child = instance.transform.Find( name );
      if ( child != null )
        return child.gameObject;

      var go = new GameObject( name );

      go.hideFlags = HideFlags.DontSaveInEditor;
      go.transform.hideFlags = go.hideFlags | HideFlags.NotEditable;

      go.transform.SetParent( instance.transform );

      return go;
    }

    public static bool HasRoot( MonoBehaviour script )
    {
      var instance = RecoverAndGetInstance();
      return script != null && instance != null && instance.transform.Find( GetName( script ) );
    }

    public static void RegisterAsPotentiallyDeleted( MonoBehaviour script )
    {
      // If we're destroyed it's likely the script isn't deleted.
      // The editor is probably going into play.
      if ( IsDestroyed )
        return;

      var instance = RecoverAndGetInstance();
      if ( instance == null )
        return;

      if ( instance.m_potentiallyDeletedParents.ContainsKey( script ) )
        return;

      instance.m_potentiallyDeletedParents.Add( script, GetName( script ) );
    }

    protected override bool Initialize()
    {
      gameObject.transform.hideFlags = gameObject.hideFlags | HideFlags.NotEditable;

      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }

    private void Update()
    {
      gameObject.transform.position = Vector3.zero;
      gameObject.transform.rotation = Quaternion.identity;
      // Change parent before scale is set - otherwise scale will be preserved.
      // E.g., move "this" to a parent with scale x, scale will be set,
      // parent = null will remove the parent but the scale will be preserved.
      // Fix - set scale after set parent.
      gameObject.transform.parent = null;
      gameObject.transform.localScale = Vector3.one;

      List<GameObject> rootsToRemove = new List<GameObject>();
      foreach ( Transform rootTransform in transform ) {
        rootTransform.position   = Vector3.zero;
        rootTransform.rotation   = Quaternion.identity;
        rootTransform.localScale = Vector3.one;

        if ( rootTransform.childCount == 0 )
          continue;
        var selectionProxy = rootTransform.GetChild( 0 ).GetComponent<Utils.OnSelectionProxy>();
        if ( selectionProxy == null || selectionProxy.Component == null )
          rootsToRemove.Add( rootTransform.gameObject );
      }

      foreach ( var kvp in m_potentiallyDeletedParents ) {
        if ( kvp.Key != null )
          continue;

        var rootTransform = transform.Find( kvp.Value );
        if ( rootTransform == null )
          continue;

        if ( !rootsToRemove.Contains( rootTransform.gameObject ) )
          rootsToRemove.Add( rootTransform.gameObject );
      }
      m_potentiallyDeletedParents.Clear();

      while ( rootsToRemove.Count > 0 ) {
        DestroyImmediate( rootsToRemove.Last() );
        rootsToRemove.RemoveAt( rootsToRemove.Count - 1 );
      }

      if ( transform.childCount == 0 )
        DestroyImmediate( gameObject );
    }

    private static string GetName( MonoBehaviour script )
    {
      return script.name + "_ro_" + script.GetInstanceID().ToString();
    }

    private static RuntimeObjects RecoverAndGetInstance()
    {
      return Instance;
    }
  }
}
