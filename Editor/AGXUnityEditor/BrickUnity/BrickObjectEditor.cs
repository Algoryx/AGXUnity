using UnityEditor;
using UnityEngine;
using System.Linq;

using AGXUnity.BrickUnity;

namespace AGXUnityEditor.BrickUnity
{
  [CustomEditor(typeof(BrickObject))]
  public class BrickObjectEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      BrickObject myScript = (BrickObject)target;
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel("Path");
        EditorGUILayout.SelectableLabel(myScript.path, GUILayout.Height(15));
      }

      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel("Type");
        EditorGUILayout.SelectableLabel(myScript.type, GUILayout.Height(15));
      }

      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel("Synchronize?");
        EditorGUILayout.Toggle(myScript.synchronize, GUILayout.Height(15));
      }

      var runtimeComponent = myScript.GetComponent<BrickRuntimeComponent>();
      if (runtimeComponent != null)
      {
        if (GUILayout.Button("Reload"))
        {
          Debug.Log("Reloading");
          var reloader = new BrickReloader(runtimeComponent);
          reloader.Reload();
        }
      }
    }
  }
}