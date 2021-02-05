using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.BrickUnity
{
  public class BrickImporter : MonoBehaviour
  {
    [HideInInspector]
    public string filePath;
    public string modelName;


    public void ImportFile()
    {
      Brick.AgxBrick._BrickModule.Init();
      Clear();
      var importer = new BrickPrefabImporter();
      var go = importer.ImportFile(filePath, modelName);
      go.transform.SetParent(this.transform, false);
    }


    public void Clear()
    {
      Transform[] children = new Transform[transform.childCount];
      for (int i = 0; i < transform.childCount; i++)
      {
        children[i] = transform.GetChild(i);
      }

      foreach (Transform child in children)
      {
        DestroyImmediate(child.gameObject);
      }
    }
  }
}