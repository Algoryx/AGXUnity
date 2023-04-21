using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  public class EditorSettings : ScriptableObject
  {
    [HideInInspector]
    public static EditorSettings Instance { get { return GetOrCreateInstance(); } }

    public static string EditorDataDirectory { get { return IO.Utils.AGXUnityEditorDirectory + "/Data"; } }

    [HideInInspector]
    public static readonly int ToggleButtonSize = 18;

    public Utils.KeyHandler BuiltInToolsTool_SelectGameObjectKeyHandler = new Utils.KeyHandler( KeyCode.S ) { Enable = false };
    public Utils.KeyHandler BuiltInToolsTool_SelectRigidBodyKeyHandler = new Utils.KeyHandler( KeyCode.B );
    public Utils.KeyHandler BuiltInToolsTool_PickHandlerKeyHandler = new Utils.KeyHandler( KeyCode.A );

    public bool BuildPlayer_CopyBinaries = true;

    public void OnInspectorGUI()
    {
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


      // Recommended settings
      InspectorGUI.Separator( 1, 4 );
      EditorGUILayout.Space( 5 );
      EditorGUILayout.LabelField( GUI.AddSizeTag( "<b>Unity Project Settings recommended for AGX</b>", 15 ) );
      EditorGUILayout.Space();

      var ok = GUI.AddColorTag( "<b>OK</b> ", Color.green ) + " <i>Using recommended setting</i>";
      var note = GUI.AddColorTag( "<b>Note</b> ", Color.yellow );
      var apiCompatibilityLevelName =
#if UNITY_2021_2_OR_NEWER
          ".NET Framework";
#else
          ".NET 4.x";
#endif

      var hasPlayerNetCompatibility = PlayerSettings.GetApiCompatibilityLevel( BuildTargetGroup.Standalone ) == ApiCompatibilityLevel.NET_4_6;
      EditorGUILayout.LabelField( "<b>.NET Compatibility Level</b>" );
      if ( hasPlayerNetCompatibility ) {
        EditorGUILayout.LabelField( ok );
      }
      else {
        EditorGUILayout.LabelField( note + "AGX Dynamics for Unity requires .NET API Compatibility Level: " + apiCompatibilityLevelName, skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
          UnityEditor.PlayerSettings.SetApiCompatibilityLevel( BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_4_6 );
          Debug.Log( "Updated Unity Player Settings -> Api Compatibility Level to compatible version" );
        }
      }

      EditorGUILayout.Space();

      EditorGUILayout.LabelField( "<b>Maximum Allowed Timestep</b>" );
      var usingRecommendedMaxTimestep = Time.fixedDeltaTime == Time.maximumDeltaTime;
      if ( usingRecommendedMaxTimestep ) {
        EditorGUILayout.LabelField( ok );
      }
      else {
        EditorGUILayout.LabelField( note + "It is recommended to use a <b>maximum allowed timestep</b> that is equal to the <b>fixed timestep</b> when using AGXUnity!", skin.LabelWordWrap );
        if ( InspectorGUI.Link( GUI.MakeLabel( "Click here to update this setting!" ) ) ) {
          Time.maximumDeltaTime = Time.fixedDeltaTime;
          Debug.Log( "Updated Unity Maximum Allowed Timestep to the same as Fixed Timestep " + Time.fixedDeltaTime + " seconds" );
        }
      }
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
          menu.AddItem( GUI.MakeLabel( "Reset to default" ), false, () =>
          {
            if ( EditorUtility.DisplayDialog( "Reset to default", "Reset key(s) to default?", "OK", "Cancel" ) )
              keyHandler.ResetToDefault();
          } );
          menu.AddItem( GUI.MakeLabel( "Add key" ), false, () =>
          {
            keyHandler.Add( KeyCode.None );
          } );

          if ( keyHandler.NumKeyCodes > 1 ) {
            menu.AddItem( GUI.MakeLabel( "Remove key" ), false, () =>
            {
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

    public static bool PrepareEditorDataFolder()
    {
      if ( !AssetDatabase.IsValidFolder( IO.Utils.AGXUnityEditorDirectory + "/Data" ) ) {
        AssetDatabase.CreateFolder( IO.Utils.AGXUnityEditorDirectory, "Data" );
        AssetDatabase.SaveAssets();
      }

      return true;
    }

    public static T GetOrCreateEditorDataFolderFileInstance<T>( string name,
                                                                Action onCreate = null ) where T : ScriptableObject
    {
      if ( !PrepareEditorDataFolder() )
        return null;

      string settingsPathAndName = EditorDataDirectory + @name;
      T instance = AssetDatabase.LoadAssetAtPath<T>( settingsPathAndName );
      if ( instance == null ) {
        instance = CreateInstance<T>();

        // Don't create Data.asset or Settings.asset during builds.
        // These files are not removed during update of the package
        // and must not be included in the package.
#if !AGXUNITY_BUILD_PACKAGE
        AssetDatabase.CreateAsset( instance, settingsPathAndName );
        AssetDatabase.SaveAssets();

        onCreate?.Invoke();
#endif
      }

      return instance;
    }

    private static EditorSettings GetOrCreateInstance()
    {
      if ( m_instance != null )
        return m_instance;

      return (m_instance = GetOrCreateEditorDataFolderFileInstance<EditorSettings>( "/Settings.asset" ));
    }

    [NonSerialized]
    private static EditorSettings m_instance = null;

    /// <summary>
    /// Call from CI when the package is built, avoiding Data.asset
    /// and Settings.asset to be created and included in the package.
    /// </summary>
    private static void OnBuildPackage()
    {
      var dataFilesToExclude = new string[]
      {
        EditorDataDirectory + "/Data.asset",
        EditorDataDirectory + "/Settings.asset",
        EditorDataDirectory + "/AGXInitData.asset"
      };

      if ( Manager.ConfigureEnvironment() != Manager.EnvironmentState.Initialized ) {
        Debug.LogError( "AGXUnity Build: Unable to initialize AGX Dynamics - missing libraries?" );
        EditorApplication.Exit( 1 );
        return;
      }

      Debug.Log( "AGXUnity Build: Applying package build settings..." );
      foreach ( var excludedFile in dataFilesToExclude ) {
        var fi = new FileInfo( excludedFile );
        var fiMeta = new FileInfo( excludedFile + ".meta" );
        Debug.Log( $"    - Deleting {fi.FullName}, exist = {fi.Exists}." );
        if ( fi.Exists ) {
          fi.Delete();
          if ( fiMeta.Exists )
            fiMeta.Delete();
        }
      }

      Debug.Log( "    - Adding define symbol AGXUNITY_BUILD_PACKAGE." );
      Build.DefineSymbols.Add( "AGXUNITY_BUILD_PACKAGE" );
    }

    internal static SerializedObject GetSerializedSettings()
    {
      return new SerializedObject( GetOrCreateInstance() );
    }
  }

  [CustomEditor( typeof( EditorSettings ) )]
  public class EditorSettingsEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( this.targets, Event.current ) )
        return;

      EditorSettings.Instance.OnInspectorGUI();
    }
  }

  // Register a SettingsProvider using IMGUI for the drawing framework:
  static class AGXSettingsIMGUIRegister
  {
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

            using( new GUILayout.VerticalScope() )
              EditorSettings.Instance.OnInspectorGUI();
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
