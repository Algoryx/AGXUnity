using UnityEngine;
using UnityEditor;
using System.Linq;

using AGXUnity.BrickUnity;

using Brick;

namespace AGXUnityEditor.BrickUnity
{
  [CustomEditor(typeof(BrickImporter))]
  public class BrickImporterEditor : Editor
  {
    private string[] m_models = new string[] { };
    private int m_modelIndex = 0;

    public override void OnInspectorGUI()
    {
      BrickImporter myScript = (BrickImporter)target;

      #region File path browser
      using (new EditorGUILayout.HorizontalScope())
      {
        myScript.filePath = EditorGUILayout.TextField("Filepath", myScript.filePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
          m_models = new string[] { };
          m_modelIndex = 0;
          myScript.filePath = EditorUtility.OpenFilePanel("Browse", "", "yml,yaml");
          if (!System.IO.File.Exists(myScript.filePath))
          {
            goto model_name_selector;
          }
          var file = File.FromFilepath(myScript.filePath);
          if (file.Models.IsEmpty())
          {
            Debug.LogWarning($"Brick file {myScript.filePath} does not seem to contain any models.");
          }
          else
          {
            m_models = file.Models.Select(m => m.Key).ToArray();
          }
        }
      }
    #endregion

    #region Model name selector
    model_name_selector:
      if (m_models.Length > 0)
      {
        using (new EditorGUILayout.HorizontalScope())
        {
          EditorGUILayout.PrefixLabel("Model Name");
          m_modelIndex = EditorGUILayout.Popup(m_modelIndex, m_models, EditorStyles.popup);
          myScript.modelName = m_models[m_modelIndex];
        }
      }
      #endregion

      #region Buttons
      if (GUILayout.Button("Import Brick File"))
      {
        myScript.ImportFile();
      }

      if (GUILayout.Button("Clear"))
      {
        myScript.Clear();
      }
      #endregion
    }
  }
}