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

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      var prevMode = Simulation.AutoSteppingMode;

      Simulation.AutoSteppingMode = (Simulation.AutoSteppingModes)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Auto Stepping Mode",
                                                                                                            false,
                                                                                                            "Location (in Unity frame loop) where simulation step forward is called.\n\n" +
                                                                                                            "Fixed Update: Called from MonoBehaviour.FixedUpdate with Time.fixedDeltaTime time step size.\n" +
                                                                                                            "Update: Called from MonoBehaviour.Update with arbitrary time step size.\n" +
                                                                                                            "Disabled: User has to manually invoke Simulation.Instance.DoStep()."),
                                                                                             Simulation.AutoSteppingMode,
                                                                                             skin.button );

      if ( prevMode != Simulation.AutoSteppingMode ) {
        if ( Simulation.AutoSteppingMode == Simulation.AutoSteppingModes.FixedUpdate )
          Simulation.TimeStep = Time.fixedDeltaTime;
        else
          Simulation.TimeStep = 1.0f / 60.0f;
      }

      UnityEngine.GUI.enabled = Simulation.AutoSteppingMode != Simulation.AutoSteppingModes.FixedUpdate;
      Simulation.TimeStep = Mathf.Max( EditorGUILayout.FloatField( GUI.MakeLabel( "Time Step",
                                                                                  false,
                                                                                  "Simulation step size in seconds.\n\n" +
                                                                                  "Fixed Update: Project Settings -> Time -> Fixed Timestep (Time.fixedDeltaTime)\n" +
                                                                                  "Update: User defined - verify it's compatible with current VSync settings. Default: 0.01666667\n" +
                                                                                  "Disabled: User defined. Default: 0.01666667" ),
                                                                   Simulation.AutoSteppingMode != Simulation.AutoSteppingModes.FixedUpdate ?
                                                                     Simulation.TimeStep :
                                                                     Time.fixedDeltaTime,
                                                                   skin.textField ),
                                       0.0f );
      UnityEngine.GUI.enabled = true;

      if ( prevMode == Simulation.AutoSteppingModes.FixedUpdate )
        Simulation.FixedUpdateRealTimeFactor = EditorGUILayout.FloatField( GUI.MakeLabel( "Real Time Factor",
                                                                                          false,
                                                                                          "< 1 means AGX is allowed to spend more time than Time.fixedDeltaTime executing stepForward, " +
                                                                                          "resulting in low camera FPS in performance heavy simulations.\n\n" +
                                                                                          "= 1 means AGX wont execute stepForward during additional FixedUpdate callbacks, " +
                                                                                          "resulting in slow motion looking simulations but (relatively) high camera FPS in " +
                                                                                          "performance heavy simulations." ),
                                                                           Simulation.FixedUpdateRealTimeFactor,
                                                                           skin.textField );
      else if ( prevMode == Simulation.AutoSteppingModes.Update )
        Simulation.UpdateRealTimeCorrectionFactor = EditorGUILayout.FloatField( GUI.MakeLabel( "Real Time Correction Factor",
                                                                                               false,
                                                                                               "Given 60 Hz, 1 frame VSync, the Update callbacks will be executed in 58 - 64 Hz. " +
                                                                                               "This value scales the time since last frame so that we don't lose a stepForward " +
                                                                                               "call when Update is called > 60 Hz. Default: 0.9." ),
                                                                                Simulation.UpdateRealTimeCorrectionFactor,
                                                                                skin.textField );

      GUI.Separator();
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
