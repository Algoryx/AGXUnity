using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  public static class Find
  {
    public static GameObject RootGameObject( GameObject parent )
    {
      if ( parent.transform.parent == null )
        return parent;
      return RootGameObject( parent.transform.parent.gameObject );
    }

    public static T FirstParentWithComponent<T>( Transform parent ) where T : UnityEngine.Component
    {
      if ( parent == null )
        return null;

      T component = parent.GetComponent<T>();
      if ( component != null )
        return component;

      return FirstParentWithComponent<T>( parent.transform.parent );
    }

    public static T FirstParentWithComponent<T>( GameObject gameObject ) where T : UnityEngine.Component
    {
      if ( gameObject == null )
        return null;

      return FirstParentWithComponent<T>( gameObject.transform );
    }

    public static GameObject[] ChildrenList( GameObject gameObject )
    {
      if ( gameObject == null )
        return null;

      GameObject root = Find.RootGameObject( gameObject );
      RigidBody[] bodies = root.GetComponentsInChildren<RigidBody>();
      Collide.Shape[] shapes = root.GetComponentsInChildren<Collide.Shape>();

      List<GameObject> gameObjects = new List<GameObject>();
      foreach ( var rb in bodies )
        gameObjects.Add( rb.gameObject );
      foreach ( var shape in shapes )
        gameObjects.Add( shape.gameObject );

      gameObjects.Add( null );

      int indexOfSelected = gameObjects.IndexOf( gameObject );
      if ( indexOfSelected > 0 && gameObjects.Count > 1 ) {
        GameObject tmp = gameObjects[ 0 ];
        gameObjects[ 0 ] = gameObject;
        gameObjects[ indexOfSelected ] = tmp;
      }

      return gameObjects.ToArray();
    }

    public static string[] ChildrenNames( GameObject gameObject )
    {
      GameObject[] gameObjects = ChildrenList( gameObject );
      if ( gameObjects == null || gameObjects.Length == 0 )
        return new string[] { "None" };

      return ( from go in gameObjects select go != null ? go.name : "None" ).ToArray();
    }
  }
}
