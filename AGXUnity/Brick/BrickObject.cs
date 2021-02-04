using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrickObject : MonoBehaviour
{
  public string path;
  public string type;
  public bool synchronize = true;

  public void SetType(object obj)
  {
    type = obj.GetType().ToString().Replace(@"+", @".");
  }

  public void SetPath(Brick.Object b_object, GameObject go_parent = null)
  {
    var name = b_object.GetValueNameOrModelPath();
    if (go_parent is null) {
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

  public void SetObject(Brick.Object b_object, GameObject go_parent = null)
  {
    SetPath(b_object, go_parent);
    SetType(b_object);
  }

  public Brick.Path GetBrickPath()
  {
    return new Brick.Path(path);
  }

  public Brick.Path GetBrickPathRelativeRoot()
  {
    return GetBrickPath().Subpath(1);
  }
}
