// Register a SettingsProvider using IMGUI for the drawing framework:
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  static class SettingsTab
  {
    // add EditorData
    [SettingsProvider]
    public static SettingsProvider CreateAGXSettingsProvider()
    {

      // First parameter is the path in the Settings window.
      // Second parameter is the scope of this setting: it only appears in the Project Settings window.
      var provider = new SettingsProvider("Project/AGXSettings", SettingsScope.Project)
      {
        // By default the last token of the path is used as display name if no label is provided.
        label = "AGX Settings",
        // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
        guiHandler = (searchContext) =>
      {
        float oldWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 250;

        EditorGUILayout.Space();

        using( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 10f );

          using( new GUILayout.VerticalScope() ){
            EditorSettings.Instance.OnInspectorGUI();
            InspectorGUI.Separator( 1, 4 );
            EditorGUILayout.Space( 5 );
            EditorData.Instance.OnInspectorGUI();
          }
        }

        EditorGUIUtility.labelWidth = oldWidth;
      },

        // Populate the search keywords to enable smart search filtering and label highlighting:
        keywords = new HashSet<string>(new[] { "AGX Dynamics", "Keybindings", "Rigid body" })
      };

      return provider;
    }
  }
}