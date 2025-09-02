using AGXUnity.IO.OpenPLX;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  [PreviousSettingsFile( FileName = "Settings.asset" )]
  public class EditorSettings : AGXUnityEditorSettings<EditorSettings>
  {
    [HideInInspector]
    public static readonly int ToggleButtonSize = 18;

    public Utils.KeyHandler BuiltInToolsTool_SelectGameObjectKeyHandler = new Utils.KeyHandler( KeyCode.S ) { Enable = false };
    public Utils.KeyHandler BuiltInToolsTool_SelectRigidBodyKeyHandler = new Utils.KeyHandler( KeyCode.B );
    public Utils.KeyHandler BuiltInToolsTool_PickHandlerKeyHandler = new Utils.KeyHandler( KeyCode.A );

    public bool BuildPlayer_CopyBinaries = true;

    public void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( new Object[] { this }, Event.current ) )
        return;
      EditorGUI.BeginChangeCheck();
      var skin = InspectorEditor.Skin;

      BuildPlayer_CopyBinaries = InspectorGUI.Toggle( GUI.MakeLabel( "<b>Build:</b> Copy AGX Dynamics binaries",
                                                                     false,
                                                                     "[Recommended enabled]\nCopy dependent AGX Dynamics binaries to target player directory." ),
                                                      BuildPlayer_CopyBinaries );

      if ( ExternalAGXInitializer.IsApplied ) {
        DirectoryInfo newAgxDir = null;
        if ( InspectorGUI.SelectFolder( GUI.MakeLabel( "AGX Dynamics directory" ),
                                        ExternalAGXInitializer.Instance.AGX_DIR,
                                        "AGX Dynamics directory",
                                        newFolder => newAgxDir = new DirectoryInfo( newFolder ) ) ) {
          if ( ExternalAGXInitializer.FindType( newAgxDir ) == ExternalAGXInitializer.AGXDirectoryType.Unknown )
            Debug.LogError( $"ERROR: {newAgxDir.FullName} doesn't seems to be an AGX Dynamics root folder." );
          else if ( EditorUtility.DisplayDialog( "Change AGX Dynamics directory",
                                                 $"Change from {ExternalAGXInitializer.Instance.AGX_DIR} to {newAgxDir.FullName}?\n\n" +
                                                 "Unity will restart during the change.",
                                                 "Yes",
                                                 "Cancel" ) ) {
            ExternalAGXInitializer.ChangeRootDirectory( newAgxDir );
          }
        }
        if ( !ExternalAGXInitializer.AppliedVersionCompatible ) {
          EditorGUILayout.HelpBox( "The specified AGX Dynamics directory contains an incompatible version of AGX Dynamics. " +
                                   $"(specified version: {ExternalAGXInitializer.AppliedAGXVersion}, required version: {PackageManifest.Instance.agx})\n" +
                                   "Using an incompatible version might lead to unexpected behaviour or crashes and should therefore be avoided. " +
                                   "It is recommended that a compatible AGX Dynamics version is installed and selected from the AGX Installer download site",
                                   MessageType.Warning );
          if ( GUILayout.Button( "Open download site" ) )
            EditorUtility.OpenWithDefaultApp( "https://www.algoryx.se/download/" );
        }
      }
      else if ( !IO.Utils.AGXDynamicsInstalledInProject && ExternalAGXInitializer.UserSaidNo ) {
        var rect     = EditorGUILayout.GetControlRect();
        var orgWidth = rect.width;
        rect.width = EditorGUIUtility.labelWidth;
        EditorGUI.PrefixLabel( rect, GUI.MakeLabel( "Select AGX Dynamics root folder" ), skin.Label );
        rect.x += rect.width;
        rect.width = orgWidth - EditorGUIUtility.labelWidth;
        if ( UnityEngine.GUI.Button( rect, GUI.MakeLabel( "AGX Dynamics root directory..." ) ) ) {
          var agxDir = EditorUtility.OpenFolderPanel( "AGX Dynamics root directory",
                                                      "Assets",
                                                      "" );
          if ( !string.IsNullOrEmpty( agxDir ) ) {
            var agxDirInfo = new DirectoryInfo( agxDir );
            var type = ExternalAGXInitializer.FindType( agxDirInfo );
            if ( type == ExternalAGXInitializer.AGXDirectoryType.Unknown )
              Debug.LogWarning( $"{agxDir} isn't recognized as an AGX Dynamics root directory." );
            else if ( EditorUtility.DisplayDialog( "Add AGX Dynamics directory",
                                                   $"Set AGX Dynamics root directory to {agxDir}?\n\n" +
                                                   "Unity will restart during the process.",
                                                   "Yes",
                                                   "Cancel" ) ) {
              ExternalAGXInitializer.UserSaidNo = false;
              ExternalAGXInitializer.ChangeRootDirectory( agxDirInfo );
            }
          }
        }
      }

      InspectorGUI.Separator( 1, 4 );

      // BuiltInToolsTool settings GUI.
      {
        HandleKeyHandlerGUI( GUI.MakeLabel( "Select game object" ), BuiltInToolsTool_SelectGameObjectKeyHandler );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Select rigid body game object" ), BuiltInToolsTool_SelectRigidBodyKeyHandler );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Pick handler (scene view)" ), BuiltInToolsTool_PickHandlerKeyHandler );
      }

      InspectorGUI.Separator( 1, 4 );
      EditorGUILayout.Space( 5 );
      EditorGUILayout.LabelField( GUI.AddSizeTag( "<b>OpenPLX settings</b>", 15 ) );
      EditorGUILayout.Space();

      EditorGUILayout.LabelField( "<b>Additional Bundle Directories</b>" );
      var bundles = OpenPLXSettings.Instance.AdditionalBundleDirs;
      int toRemove = -1;
      bool changed = false;
      using ( new InspectorGUI.IndentScope() ) {
        for ( int i = 0; i < bundles.Count; i++ ) {
          using var scope = new EditorGUILayout.HorizontalScope();
          InspectorGUI.SelectFile( GUI.MakeLabel( "" ), bundles[ i ], "Select Bundle Directory", Application.dataPath, newFile => { bundles[ i ] = newFile; changed = true; }, true );
          if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                    true,
                                    $"Remove bundle directory.",
                                    GUILayout.Width( 18 ) ) )
            toRemove = i;
        }

        if ( bundles.Count == 0 )
          EditorGUILayout.LabelField( "<i>No additional bundle directories added</i>" );
      }

      if ( toRemove >= 0 ) {
        bundles.RemoveAt( toRemove );
        changed = true;
      }

      using ( new EditorGUILayout.HorizontalScope() ) {
        GUILayout.FlexibleSpace();
        if ( InspectorGUI.Button( MiscIcon.EntryAdd,
                                    true,
                                    $"Add bundle directory.",
                                    GUILayout.Width( 18 ) ) ) {
          bundles.Add( "" );
          changed = true;
        }
      }

      if ( changed )
        OpenPLXSettings.Instance.Save();

      // Recommended settings
      InspectorGUI.Separator( 1, 4 );
      EditorGUILayout.Space( 5 );
      EditorGUILayout.LabelField( GUI.AddSizeTag( "<b>Unity Project Settings recommended for AGX</b>", 15 ) );
      EditorGUILayout.Space();

      var ok = GUI.AddColorTag( "<b>OK</b> ", Color.green ) + " <i>Using recommended setting</i>";
      var note = GUI.AddColorTag( "<b>Note</b> ", Color.yellow );

#if UNITY_2023_3_OR_NEWER
      var hasMonoRuntime = PlayerSettings.GetScriptingBackend( UnityEditor.Build.NamedBuildTarget.Standalone ) == ScriptingImplementation.Mono2x;
#else
      var hasMonoRuntime = PlayerSettings.GetScriptingBackend( BuildTargetGroup.Standalone ) == ScriptingImplementation.Mono2x;
#endif
      EditorGUILayout.LabelField( "<b>.NET Runtime</b>" );
      if ( hasMonoRuntime )
        EditorGUILayout.LabelField( ok );
      else {
        EditorGUILayout.LabelField( note + "AGX Dynamics for Unity requires .NET Runtime: Mono", skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
#if UNITY_2023_3_OR_NEWER
          PlayerSettings.SetScriptingBackend( UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x );
#else
          PlayerSettings.SetScriptingBackend( BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x );
#endif
          Debug.Log( "Updated Unity Player Settings -> Scripting Backend to compatible runtime." );
        }
      }

      EditorGUILayout.LabelField( "<b>Maximum Allowed Timestep</b>" );
      var usingRecommendedMaxTimestep = Mathf.Abs(Time.fixedDeltaTime - Time.maximumDeltaTime) < 1e-4;
      if ( usingRecommendedMaxTimestep )
        EditorGUILayout.LabelField( ok );
      else {
        EditorGUILayout.LabelField( note + "It is recommended to use a <b>maximum allowed timestep</b> that is equal to the <b>fixed timestep</b> when using AGXUnity!", skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
          Time.maximumDeltaTime = Time.fixedDeltaTime;
          Debug.Log( "Updated Unity Maximum Allowed Timestep to the same as Fixed Timestep " + Time.fixedDeltaTime + " seconds" );
          EditorUtility.SetDirty( this );

          // Reserialize TimeManager to persist change
          var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset").FirstOrDefault();
          if ( asset == null ) {
            Debug.LogError( "Could not find TimeManager.asset, updated setting will not be persisted across restarts of the editor!" );
            return;
          }
          EditorUtility.SetDirty( asset );
          AssetDatabase.SaveAssets();
        }
      }

      EditorGUILayout.LabelField( "<b>Disable Unity Physics Auto Simulation</b>" );
#if UNITY_2022_2_OR_NEWER
      if ( Physics.simulationMode == SimulationMode.Script )
        EditorGUILayout.LabelField( ok );
      else {
        EditorGUILayout.LabelField( note + "It is recommended to set Unity's <b>Physics > Simulation mode</b> option to <b>Script</b> to increase project performance and reduce the risk of mixing physics components.", skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
          Physics.simulationMode = SimulationMode.Script;
          Debug.Log( "Unity's Physics simulation mode set to <b>Script</b>" );
        }
      }
#else
      if ( Physics.autoSimulation == false )
        EditorGUILayout.LabelField( ok );
      else {
        EditorGUILayout.LabelField( note + "It is recommended to disable Unity's <b>Physics > Autosimulation</b> option to increase project performance and reduce the risk of mixing physics components.", skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
          Physics.autoSimulation = false;
          Debug.Log( "Disabled Unity's Physics auto simulation" );
        }
      }
#endif

      if ( EditorGUI.EndChangeCheck() )
        Instance.Save();
    }

    private void HandleKeyHandlerGUI( GUIContent name, Utils.KeyHandler keyHandler )
    {
      const int keyButtonWidth = 90;
      var showDropdownPressed = false;

      GUILayout.BeginHorizontal();
      {
        keyHandler.Enable = InspectorGUI.Toggle( name, keyHandler.Enable );
        GUILayout.FlexibleSpace();

        UnityEngine.GUI.enabled = keyHandler.Enable;

        for ( int iKey = 0; iKey < keyHandler.NumKeyCodes; ++iKey ) {
          GUIContent buttonLabel = keyHandler.IsDetectingKey( iKey ) ?
                                     GUI.MakeLabel( "Detecting..." ) :
                                     GUI.MakeLabel( keyHandler.Keys[ iKey ].ToString() );

          bool toggleDetecting = GUILayout.Button( buttonLabel,
                                                   InspectorEditor.Skin.ButtonMiddle,
                                                   GUILayout.Width( keyButtonWidth ),
                                                   GUILayout.Height( ToggleButtonSize ) );
          if ( toggleDetecting )
            keyHandler.DetectKey( this, !keyHandler.IsDetectingKey( iKey ), iKey );
        }

        GUILayout.BeginVertical( GUILayout.Height( ToggleButtonSize ) );
        {
          GUIStyle tmp = new GUIStyle( InspectorEditor.Skin.Button );
          tmp.fontSize = 6;

          showDropdownPressed = InspectorGUI.Button( MiscIcon.ContextDropdown,
                                                         true,
                                                         "Add or remove key or reset key to default.",
                                                         GUILayout.Width( 16 ) );
          GUILayout.FlexibleSpace();
        }
        GUILayout.EndVertical();

        UnityEngine.GUI.enabled = true;

        if ( showDropdownPressed ) {
          GenericMenu menu = new GenericMenu();
          menu.AddItem( GUI.MakeLabel( "Reset to default" ), false, () => {
            if ( EditorUtility.DisplayDialog( "Reset to default", "Reset key(s) to default?", "OK", "Cancel" ) )
              keyHandler.ResetToDefault();
          } );
          menu.AddItem( GUI.MakeLabel( "Add key" ), false, () => {
            keyHandler.Add( KeyCode.None );
          } );

          if ( keyHandler.NumKeyCodes > 1 ) {
            menu.AddItem( GUI.MakeLabel( "Remove key" ), false, () => {
              if ( EditorUtility.DisplayDialog( "Remove key", "Remove key: " + keyHandler[ keyHandler.NumKeyCodes - 1 ].ToString() + "?", "OK", "Cancel" ) )
                keyHandler.Remove( keyHandler.NumKeyCodes - 1 );
            } );
          }

          menu.ShowAsContext();
        }
      }
      GUILayout.EndHorizontal();

      if ( UnityEngine.GUI.changed )
        EditorUtility.SetDirty( this );
    }

    /// <summary>
    /// Call from CI when the package is built, avoiding Data.asset
    /// and Settings.asset to be created and included in the package.
    /// </summary>
    private static void OnBuildPackage()
    {
      if ( Manager.ConfigureEnvironment() != Manager.EnvironmentState.Initialized ) {
        Debug.LogError( "AGXUnity Build: Unable to initialize AGX Dynamics - missing libraries?" );
        EditorApplication.Exit( 1 );
        return;
      }

      Debug.Log( "    - Adding define symbol AGXUNITY_BUILD_PACKAGE." );
      Build.DefineSymbols.Add( "AGXUNITY_BUILD_PACKAGE" );
    }
  }
}
