using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;
using System.Collections.Generic;

namespace AGXUnityEditor
{
  // Register a SettingsProvider using IMGUI for the drawing framework:
  static class AGXSettingsIMGUIRegister
  {
    public static void RenderSettings<T>( string name, string file ) where T : AGXUnity.ScriptAsset
    {
      string settingsPathAndName = IO.Utils.AGXUnityResourceDirectory + "/" + file;
      T instance = AssetDatabase.LoadAssetAtPath<T>( settingsPathAndName );
      if ( instance == null ) {
        instance = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset( instance, settingsPathAndName );
        AssetDatabase.SaveAssets();
      }

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.BeginVertical();
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( instance, "ProjectSettings-" + name ), GUI.MakeLabel( name, 15, true ) ) ) {
        using ( new InspectorGUI.IndentScope() ) {
          var e = Editor.CreateEditor(instance);
          e.OnInspectorGUI();
        }
      }
      EditorGUILayout.EndVertical();


      bool showDropdownPressed = false;
      GUILayout.BeginVertical( GUILayout.Width( 16 ) );
      {
        GUIStyle tmp = new GUIStyle( InspectorEditor.Skin.Button );
        tmp.fontSize = 6;

        showDropdownPressed = InspectorGUI.Button( MiscIcon.ContextDropdown,
                                                       true,
                                                       "Reset settings to default",
                                                       GUILayout.Width( 16 ) );
        GUILayout.FlexibleSpace();
      }
      GUILayout.EndVertical();

      if ( showDropdownPressed ) {
        GenericMenu menu = new GenericMenu();
        menu.AddItem( GUI.MakeLabel( "Reset to default" ), false, () =>
        {
          if ( EditorUtility.DisplayDialog( "Reset to default", "Reset settings to default?", "OK", "Cancel" ) )
            // Recreate asset to reset
            AssetDatabase.DeleteAsset( settingsPathAndName );
            instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset( instance, settingsPathAndName );
            AssetDatabase.SaveAssets();
        } );

        menu.ShowAsContext();
      }

      EditorGUILayout.EndHorizontal();
    }

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

          using( new GUILayout.VerticalScope() ) {
            EditorSettings.Instance.OnInspectorGUI();

            RenderSettings<AGXUnity.SolverSettings>("Solver Settings", "SolverSettings.asset");
          }
        }

        EditorGUIUtility.labelWidth = oldWidth;
      },

        // Populate the search keywords to enable smart search filtering and label highlighting:
        keywords = new HashSet<string>(new[] { "agx", "solver", "simulation" })
      };

      return provider;
    }
  }
}