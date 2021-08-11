using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  public static class Find
  {
    public class LeafData
    {
      public Collide.Shape[] Shapes = new Collide.Shape[] { };
      public Wire[] Wires = new Wire[] { };
      public Cable[] Cables = new Cable[] { };
      public Model.Track[] Tracks = new Model.Track[] { };
      public Model.DeformableTerrain[] Terrains = new Model.DeformableTerrain[] { };
    }

    /// <summary>
    /// Finds leaf objects (Shape, Wire and Cable instances) given parent
    /// game object.
    /// </summary>
    /// <param name="parent">Parent game object.</param>
    /// <param name="searchChildren">True to search in children, false to only collect data in <paramref name="parent"/>.</param>
    /// <returns>Collections of shapes, wires and cables.</returns>
    public static LeafData LeafObjects( GameObject parent, bool searchChildren )
    {
      var data = new LeafData();
      if ( parent == null )
        return data;

      var rb     = parent.GetComponent<RigidBody>();
      var shape  = rb != null ?
                     null :
                     parent.GetComponent<Collide.Shape>();
      var wire   = rb != null || shape != null ?
                     null :
                     parent.GetComponent<Wire>();
      var cable  = rb != null || shape != null || wire != null ?
                     null :
                     parent.GetComponent<Cable>();
      
      // Possible to have multiple tracks per game object.
      var tracks = rb != null || shape != null || wire != null || cable != null ?
                     null :
                     parent.GetComponents<Model.Track>();
      if ( tracks != null && tracks.Length == 0 )
        tracks = null;

      var terrain = rb != null || shape != null || wire != null || cable != null || tracks != null ?
                      null :
                      parent.GetComponent<Model.DeformableTerrain>();

      bool allPredefinedAreNull = rb == null &&
                                  shape == null &&
                                  wire == null &&
                                  cable == null &&
                                  terrain == null;

      // If tracks != null && search children we collect all children
      // since track component may be "anywhere".
      if ( allPredefinedAreNull && searchChildren ) {
        data.Shapes   = parent.GetComponentsInChildren<Collide.Shape>();
        data.Wires    = parent.GetComponentsInChildren<Wire>();
        data.Cables   = parent.GetComponentsInChildren<Cable>();
        data.Tracks   = parent.GetComponentsInChildren<Model.Track>();
        data.Terrains = parent.GetComponentsInChildren<Model.DeformableTerrain>();
      }
      // A wire is by definition independent of PropagateToChildren, since
      // it's not defined to add children to a wire game object.
      else if ( wire != null ) {
        data.Wires = new Wire[] { wire };
      }
      // Same logics for Cable.
      else if ( cable != null ) {
        data.Cables = new Cable[] { cable };
      }
      else if ( tracks != null ) {
        data.Shapes = CollectShapes( parent, rb, shape, searchChildren );
        data.Tracks = searchChildren ?
                        parent.GetComponentsInChildren<Model.Track>() :
                        tracks;
      }
      else if ( terrain != null ) {
        data.Terrains = searchChildren ?
                          parent.GetComponentsInChildren<Model.DeformableTerrain>() :
                          new Model.DeformableTerrain[] { terrain };
      }
      // Bodies have shapes so if 'rb' != null we should collect all shape children
      // independent of 'propagate' flag.
      // If 'shape' != null and propagate is true we have the same condition as for bodies.
      else if ( rb != null ||
                shape != null ||
                ( rb == null && shape == null && searchChildren ) ) {
        data.Shapes = CollectShapes( parent, rb, shape, searchChildren );
      }
      else {
        // These groups has no effect. It's not possible to issue warning
        // where when CollisionGroups.Initialize searches both with
        // searchChildren = false and searchChildren = true regardless
        // of the collision group "Propagate to children" state.
      }

      return data;
    }

    private static Collide.Shape[] CollectShapes( GameObject parent,
                                                  RigidBody rb,
                                                  Collide.Shape shape,
                                                  bool searchChildren )
    {
      // Search children - just collect all we can find.
      if ( searchChildren )
        return parent.GetComponentsInChildren<Collide.Shape>();
      // Rigid body given, beware of articulated systems so use
      // the shapes the rigid body knows it owns.
      else if ( rb != null )
        return rb.Shapes;
      // If there's a shape on parent, check for more shapes.
      else if ( shape != null )
        return parent.GetComponents<Collide.Shape>();
      else
        return new Collide.Shape[] { };
    }

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
