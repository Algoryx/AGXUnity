using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.BrickUnity
{
  public class BrickObject : MonoBehaviour
  {
    public string path;
    public string type;
    public bool synchronize = true;


    /// <summary>
    /// Set the parameters of this BrickObjects object from a Brick.Object.
    /// </summary>
    /// <param name="b_object">The Brick.Object from which to set the parameters</param>
    /// <param name="go_parent">An optional parent GameObject. If this is not null, the parent's BrickObject's path
    /// will prefix the path of this Brick object.</param>
    public void SetObject(Brick.Object b_object, GameObject go_parent = null)
    {
      SetPath(b_object, go_parent);
      SetType(b_object);

      // This object has probably been created by the Brick runtime and should not be synchronized
      if (b_object._ModelValue is null)
        synchronize = false;
    }


    public Brick.Path GetBrickPath()
    {
      return new Brick.Path(path);
    }


    /// <summary>
    /// Get a Brick value from a Brick.Physics.Component given the "path" of this BrickObject.
    /// </summary>
    /// <typeparam name="T">The desired type of the Brick value. If the "type" of this BrickObject does not match T,
    /// null will be returned and a warning will be logged.</typeparam>
    /// <param name="b_component">The Component from which to get the value.</param>
    /// <returns>The Brick value if T matches the type of this Brick value, null otherwise.</returns>
    public T GetBrickValue<T>(Brick.Physics.Component b_component) where T : Brick.Object
    {
      var b_path = this.GetBrickPath().Subpath(1);
      var b_object = b_component._Get(b_path);
      if (b_object is T b_T)
        return b_T;
      Debug.LogWarning($"Type of Brick object {b_path} ({b_object.GetType()}) does not match the expected type {typeof(T)}");
      return null;
    }


    private void SetType(object obj)
    {
      type = obj.GetType().ToString().Replace(@"+", @".");
    }


    private void SetPath(Brick.Object b_object, GameObject go_parent = null)
    {
      var name = b_object.GetValueNameOrModelPath();
      if (go_parent is null)
      {
        path = name;
        return;
      }

      var parentBrickObject = go_parent.GetComponent<BrickObject>();
      if (parentBrickObject is null)
      {
        path = name;
        return;
      }

      var parentPath = go_parent.GetComponent<BrickObject>().path;
      path = $"{parentPath}.{name}";
    }
  }
}