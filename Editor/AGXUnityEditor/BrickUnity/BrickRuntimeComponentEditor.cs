using UnityEditor;
using AGXUnity.BrickUnity;
using UnityEngine;

namespace AGXUnityEditor.BrickUnity
{
  [CustomEditor(typeof(BrickRuntimeComponent))]
  public class BrickRuntimeComponentEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      DrawDefaultInspector();

      BrickRuntimeComponent runtimeComponent = (BrickRuntimeComponent)target;
      if (Application.platform == RuntimePlatform.WindowsEditor)
      {
        if (GUILayout.Button("Open Brick file folder"))
        {
          var args = "/select," + System.IO.Path.GetFullPath(runtimeComponent.filePath);
          System.Diagnostics.Process.Start("explorer", args);
        }
      }
    }

  }
}
