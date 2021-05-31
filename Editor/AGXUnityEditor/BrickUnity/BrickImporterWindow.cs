using UnityEditor;
using UnityEngine;
using System.Linq;

using AGXUnity.BrickUnity;

using Brick;

namespace AGXUnityEditor.BrickUnity
{
  public class BrickImporterWindow : EditorWindow
  {
    public string filepath;
    private string modelName;
    public string[] models = new string[] { };
    public int modelIndex = 0;

    [MenuItem("Assets/Import Brick file as prefab...")]
    public static void GenerateBrickFileAsPrefab()
    {
      string filepath = EditorUtility.OpenFilePanel("Browse", "", "yml,yaml");
      if (string.IsNullOrEmpty(filepath))
      {
        return;
      }

      BrickUtils.SetupBrickEnvironment();
      var brickFile = File.FromFilepath(filepath);
      if (brickFile is null)
      {
        EditorUtility.DisplayDialog($"Brick Import Error", $"Something went wrong when parsing {filepath}. " +
                                    $"Make sure that a brick.config.yml file exists and is configured correctly.", "Close");
        return;
      }
      if (brickFile.Models.IsEmpty())
      {
        EditorUtility.DisplayDialog("Brick Import Error", $"Brick file {filepath} does not seem to contain any models.", "Close");
        return;
      }
      var models = brickFile.Models.Select(m => m.Key).ToArray();

      // Get existing open window or if none, make a new one:
      BrickImporterWindow window = (BrickImporterWindow)GetWindow(typeof(BrickImporterWindow));
      window.filepath = filepath;
      window.models = models;
      window.modelIndex = models.Length - 1;
      window.Show();
    }

    void OnGUI()
    {
      if (models.Length > 0)
      {
        using (new EditorGUILayout.HorizontalScope())
        {
          EditorGUILayout.PrefixLabel("Model Name");
          modelIndex = EditorGUILayout.Popup(modelIndex, models, EditorStyles.popup);
          modelName = models[modelIndex];
        }

        if (GUILayout.Button("Import Brick File"))
        {
          var brickImporter = new BrickPrefabImporter();
          try
          {
            // TODO: Check if prefab already exists. If so load it beforehand and send an gameobject to the importFile method.
            // That way importFile can modify that prefab instead of overwriting it. And stuff that was added using AGXUnity can be kept.
            var go = brickImporter.ImportFile(filepath, modelName);

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, $"Assets/{go.name}.prefab", InteractionMode.UserAction);
          }
          catch (System.Exception)
          {
            Close();
            throw;
          }
          Close();
        }
      }
    }
  }
}