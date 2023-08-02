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
        if ( Simulation.AutoSteppingMode == Simulation.AutoSteppingModes.FixedUpdate ) {
          GetUpdateTimeStepData().Float = Simulation.TimeStep;
          Simulation.TimeStep = Time.fixedDeltaTime;
        }
        else
          Simulation.TimeStep = GetUpdateTimeStepData().Float;
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

      InspectorGUI.Separator( 1, 4 );

      using ( new GUI.EnabledBlock( Application.isPlaying ) ) {
#if AGXUNITY_DEV_BUILD
        if ( GUILayout.Button( GUI.MakeLabel( "Save current step as (.agx)...",
                                              false,
                                              "Save scene in native file format when the editor is in play mode." ),
                               skin.Button ) ) {
          saveCurrentState();
        }

        if ( GUILayout.Button( GUI.MakeLabel( "Open in AGX native viewer",
                                              false,
                                              "Creates Lua file, saves current scene to an .agx file and executes luaagx.exe." ), skin.Button ) ) {
          Simulation.OpenInNativeViewer();
        }
#endif

        var rect = EditorGUILayout.GetControlRect();
        var orgWidth = rect.width;
        rect.width = EditorGUIUtility.labelWidth;
        EditorGUI.PrefixLabel( rect, GUI.MakeLabel( "Save current step as (.agx)" ), skin.Label );
        rect.x += EditorGUIUtility.labelWidth;
        rect.width = orgWidth - EditorGUIUtility.labelWidth;
        if ( UnityEngine.GUI.Button( rect, GUI.MakeLabel( "Output file..." ), skin.Button ) ) {
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
      }

      Simulation.SavePreFirstStepPath = InspectorGUI.ToggleSaveFile( GUI.MakeLabel( "Dump initial (.agx)" ),
                                                                     Simulation.SavePreFirstStep,
                                                                     enabled => Simulation.SavePreFirstStep = enabled,
                                                                     Simulation.SavePreFirstStepPath,
                                                                     UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name,
                                                                     "agx",
                                                                     "Path to initial dump (including file name and extension)",
                                                                     fileExtension => fileExtension == ".agx" || fileExtension == ".aagx" );

      Simulation.LogPath = InspectorGUI.ToggleSaveFile( GUI.MakeLabel( "AGX Dynamics log" ),
                                                         Simulation.LogEnabled,
                                                         enable => Simulation.LogEnabled = enable,
                                                         Simulation.LogPath,
                                                         "AGXDynamicsLog",
                                                         "txt",
                                                         "AGX Dynamics log filename",
                                                         extension => true );

      Simulation.AGXUnityLogLevel = InspectorGUI.ToggleEnum( GUI.MakeLabel( "Print AGX log in Unity console" ),
                                                             Simulation.LogToUnityConsole,
                                                             b => Simulation.LogToUnityConsole = b,
                                                             Simulation.AGXUnityLogLevel );
      using (new UnityEditor.EditorGUI.IndentLevelScope() ) {
        using ( new GUI.EnabledBlock( Simulation.LogToUnityConsole ) ) {
          Simulation.DisableMeshCreationWarnings = InspectorGUI.Toggle( GUI.MakeLabel( "Disable mesh creation warnings" ), Simulation.DisableMeshCreationWarnings );
        }
      }

#if AGXUNITY_DEV_ENV
      using ( new GUI.EnabledBlock( EditorApplication.isPlaying ) ) {
        var rect    = EditorGUILayout.GetControlRect();
        rect.x     += EditorGUIUtility.labelWidth;
        rect.width -= EditorGUIUtility.labelWidth;
        if ( UnityEngine.GUI.Button( rect, GUI.MakeLabel( "Open in native viewer..." ), skin.Button ) )
          ;

      }
#endif
    }

    private EditorDataEntry GetSaveInitialPathEditorData( string name )
    {
      return EditorData.Instance.GetData( Simulation, name, ( entry ) => entry.String = Application.dataPath );
    }

    private EditorDataEntry GetUpdateTimeStepData()
    {
      return EditorData.Instance.GetData( Simulation, "UpdateTimeStep", entry => entry.Float = 1.0f / 60.0f );
    }
  }
}
