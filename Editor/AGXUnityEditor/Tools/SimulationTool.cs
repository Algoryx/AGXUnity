using System;
using AGXUnity;
using UnityEngine;
using UnityEditor;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Simulation ) )]
  public class SimulationTool : Tool
  {
    public Simulation Simulation { get; private set; }

    public string SaveInitialPath
    {
      get
      {
        return GetSaveInitialPathEditorData( "SimulationInitialDumpPath" ).String;
      }
      set
      {
        GetSaveInitialPathEditorData( "SimulationInitialDumpPath" ).String = value;
      }
    }

    public SimulationTool( Simulation simulation )
    {
      Simulation = simulation;
    }

    public override void OnPostTargetMembersGUI( GUISkin skin )
    {
      GUI.Separator();

      Simulation.DisplayStatistics = GUI.Toggle( GUI.MakeLabel( "Display Statistics" ), Simulation.DisplayStatistics, skin.button, skin.label );
      if ( Simulation.DisplayStatistics ) {
        using ( new GUI.Indent( 12 ) )
          Simulation.DisplayMemoryAllocations = GUI.Toggle( GUI.MakeLabel( "Display Memory Allocations" ), Simulation.DisplayMemoryAllocations, skin.button, skin.label );
      }

      GUI.Separator();

      using ( new GUILayout.HorizontalScope() ) {
        EditorGUI.BeginDisabledGroup( !Application.isPlaying );
        if ( GUILayout.Button( GUI.MakeLabel( "Save current step as (.agx)...",
                                              false,
                                              "Save scene in native file format when the editor is in play mode." ), skin.button ) ) {
          string result = EditorUtility.SaveFilePanel( "Save scene as .agx", "Assets", UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name, "agx" );
          if ( result != string.Empty ) {
            var success = Simulation.SaveToNativeFile( result );
            if ( success )
              Debug.Log( "Successfully wrote simulation to file: " + result );
          }
        }

        if ( GUILayout.Button( GUI.MakeLabel( "Open in AGX native viewer",
                                              false,
                                              "Creates Lua file, saves current scene to an .agx file and executes luaagx.exe." ), skin.button ) ) {
          Simulation.OpenInNativeViewer();
        }
        EditorGUI.EndDisabledGroup();
      }

      GUI.Separator();

      Simulation.SavePreFirstStep = GUI.Toggle( GUI.MakeLabel( "Dump initial (.agx):" ),
                                                                Simulation.SavePreFirstStep,
                                                                skin.button,
                                                                skin.label );
      EditorGUI.BeginDisabledGroup( !Simulation.SavePreFirstStep );
      {
        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 26 );
          Simulation.SavePreFirstStepPath = GUILayout.TextField( Simulation.SavePreFirstStepPath, skin.textField );
          if ( GUILayout.Button( GUI.MakeLabel( "...", false, "Open file panel" ),
                                 skin.button,
                                 GUILayout.Width( 28 ) ) ) {
            string result = EditorUtility.SaveFilePanel( "Path to initial dump (including file name and extension)",
                                                         SaveInitialPath,
                                                         UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name, "agx" );
            if ( result != string.Empty ) {
              SaveInitialPath = result;
              var fileInfo = new System.IO.FileInfo( SaveInitialPath );
              if ( fileInfo.Extension == ".agx" || fileInfo.Extension == ".aagx" )
                Simulation.SavePreFirstStepPath = SaveInitialPath;
              else
                Debug.Log( "Unknown file extension: " + fileInfo.Extension );
            }
          }
        }
      }
      EditorGUI.EndDisabledGroup();
    }

    private EditorDataEntry GetSaveInitialPathEditorData( string name )
    {
      return EditorData.Instance.GetData( Simulation, name, ( entry ) => entry.String = Application.dataPath );
    }
  }
}
