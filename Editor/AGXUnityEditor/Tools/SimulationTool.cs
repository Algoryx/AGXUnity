using System;
using AGXUnity;
using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Simulation ) )]
  public class SimulationTool : CustomTargetTool
  {
    public Simulation Simulation
    {
      get
      {
        return Targets[ 0 ] as Simulation;
      }
    }

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

    public SimulationTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;
      var prevMode = Simulation.AutoSteppingMode;

      Simulation.AutoSteppingMode = (Simulation.AutoSteppingModes)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Auto Stepping Mode",
                                                                                                            false,
                                                                                                            "Location (in Unity frame loop) where simulation step forward is called.\n\n" +
                                                                                                            "Fixed Update: Called from MonoBehaviour.FixedUpdate with Time.fixedDeltaTime time step size.\n" +
                                                                                                            "Update: Called from MonoBehaviour.Update with arbitrary time step size.\n" +
                                                                                                            "Disabled: User has to manually invoke Simulation.Instance.DoStep()."),
                                                                                             Simulation.AutoSteppingMode,
                                                                                             skin.Popup );

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
                                                                   skin.TextField ),
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
                                                                           skin.TextField );
      else if ( prevMode == Simulation.AutoSteppingModes.Update )
        Simulation.UpdateRealTimeCorrectionFactor = EditorGUILayout.FloatField( GUI.MakeLabel( "Real Time Correction Factor",
                                                                                               false,
                                                                                               "Given 60 Hz, 1 frame VSync, the Update callbacks will be executed in 58 - 64 Hz. " +
                                                                                               "This value scales the time since last frame so that we don't lose a stepForward " +
                                                                                               "call when Update is called > 60 Hz. Default: 0.9." ),
                                                                                Simulation.UpdateRealTimeCorrectionFactor,
                                                                                skin.TextField );
    }

    public override void OnPostTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      Simulation.DisplayStatistics = InspectorGUI.Toggle( GUI.MakeLabel( "Display Statistics" ), Simulation.DisplayStatistics );
      if ( Simulation.DisplayStatistics ) {
        using ( InspectorGUI.IndentScope.Single )
          Simulation.DisplayMemoryAllocations = InspectorGUI.Toggle( GUI.MakeLabel( "Display Memory Allocations" ), Simulation.DisplayMemoryAllocations );
      }

      using ( new GUILayout.HorizontalScope() ) {
        EditorGUI.BeginDisabledGroup( !Application.isPlaying );
        if ( GUILayout.Button( GUI.MakeLabel( "Save current step as (.agx)...",
                                              false,
                                              "Save scene in native file format when the editor is in play mode." ),
                               skin.Button ) ) {
          string result = EditorUtility.SaveFilePanel( "Save scene as .agx",
                                                       "Assets",
                                                       UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name,
                                                       "agx" );
          if ( result != string.Empty ) {
            var success = Simulation.SaveToNativeFile( result );
            if ( success )
              Debug.Log( GUI.AddColorTag( "Successfully wrote simulation to file: ", Color.green ) + result );
          }
        }

        if ( GUILayout.Button( GUI.MakeLabel( "Open in AGX native viewer",
                                              false,
                                              "Creates Lua file, saves current scene to an .agx file and executes luaagx.exe." ), skin.Button ) ) {
          Simulation.OpenInNativeViewer();
        }
        EditorGUI.EndDisabledGroup();
      }

      Simulation.SavePreFirstStep = InspectorGUI.Toggle( GUI.MakeLabel( "Dump initial (.agx):" ),
                                                                        Simulation.SavePreFirstStep );
      EditorGUI.BeginDisabledGroup( !Simulation.SavePreFirstStep );
      {
        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 26 );
          Simulation.SavePreFirstStepPath = GUILayout.TextField( Simulation.SavePreFirstStepPath, skin.TextField );
          if ( GUILayout.Button( GUI.MakeLabel( "...", false, "Open file panel" ),
                                 skin.Button,
                                 GUILayout.Width( 28 ) ) ) {
            string result = EditorUtility.SaveFilePanel( "Path to initial dump (including file name and extension)",
                                                         SaveInitialPath,
                                                         UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name,
                                                         "agx" );
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
