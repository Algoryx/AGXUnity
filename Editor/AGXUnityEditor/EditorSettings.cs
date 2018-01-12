using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor
{
  public class EditorSettings : ScriptableObject
  {
    #region Static properties
    [HideInInspector]
    public static string AGXUnityPath { get { return @"Assets/AGXUnity"; } }

    [HideInInspector]
    public static string AGXUnityEditorPath { get { return AGXUnityPath + @"/Editor"; } }

    [HideInInspector]
    public static string AGXUnityEditorDataPath { get { return AGXUnityEditorPath + @"/Data"; } }

    [HideInInspector]
    public static bool AGXUnityFolderExist { get { return AssetDatabase.IsValidFolder( AGXUnityPath ); } }

    [HideInInspector]
    public static bool AGXUnityEditorFolderExist { get { return AssetDatabase.IsValidFolder( AGXUnityEditorPath ); } }

    [HideInInspector]
    public static bool AGXUnityEditorDataFolderExist { get { return AssetDatabase.IsValidFolder( AGXUnityEditorDataPath ); } }

    [HideInInspector]
    public static EditorSettings Instance { get { return GetOrCreateInstance(); } }

    [HideInInspector]
    public static readonly int ToggleButtonSize = 18;
    #endregion Static properties

    #region BuiltInToolsTool settings
    public Utils.KeyHandler BuiltInToolsTool_SelectGameObjectKeyHandler = new Utils.KeyHandler( KeyCode.S );
    public Utils.KeyHandler BuiltInToolsTool_SelectRigidBodyKeyHandler = new Utils.KeyHandler( KeyCode.B );
    public Utils.KeyHandler BuiltInToolsTool_PickHandlerKeyHandler = new Utils.KeyHandler( KeyCode.A );
    #endregion BuiltInToolsTool settings

    #region Rendering GUI
    public void OnInspectorGUI( GUISkin skin )
    {
      using ( GUI.AlignBlock.Center )
        GUILayout.Label( GUI.MakeLabel( "AGXUnity Editor Settings", 24, true ), skin.label );

      GUI.Separator3D();

      // BuiltInToolsTool settings GUI.
      {
        using ( GUI.AlignBlock.Center )
          GUILayout.Label( GUI.MakeLabel( "Built in tools", 16, true ), skin.label );

        HandleKeyHandlerGUI( GUI.MakeLabel( "Select game object" ), BuiltInToolsTool_SelectGameObjectKeyHandler, skin );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Select rigid body game object" ), BuiltInToolsTool_SelectRigidBodyKeyHandler, skin );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Pick handler (scene view)" ), BuiltInToolsTool_PickHandlerKeyHandler, skin );
      }

      GUI.Separator3D();
    }

    private bool m_showDropDown = false;

    private void HandleKeyHandlerGUI( GUIContent name, Utils.KeyHandler keyHandler, GUISkin skin )
    {
      const int keyButtonWidth = 90;

      GUILayout.BeginHorizontal();
      {
        keyHandler.Enable = GUI.Toggle( name,
                                        keyHandler.Enable,
                                        skin.button,
                                        GUI.Align( skin.label, TextAnchor.MiddleLeft ),
                                        new GUILayoutOption[] { GUILayout.Width( ToggleButtonSize ), GUILayout.Height( ToggleButtonSize ) },
                                        new GUILayoutOption[] { GUILayout.Height( ToggleButtonSize ) } );
        GUILayout.FlexibleSpace();

        UnityEngine.GUI.enabled = keyHandler.Enable;

        for ( int iKey = 0; iKey < keyHandler.NumKeyCodes; ++iKey ) {
          GUIContent buttonLabel = keyHandler.IsDetectingKey( iKey ) ?
                                     GUI.MakeLabel( "Detecting..." ) :
                                     GUI.MakeLabel( keyHandler.Keys[ iKey ].ToString() );

          bool toggleDetecting = GUILayout.Button( buttonLabel, skin.button, GUILayout.Width( keyButtonWidth ), GUILayout.Height( ToggleButtonSize ) );
          if ( toggleDetecting )
            keyHandler.DetectKey( this, !keyHandler.IsDetectingKey( iKey ), iKey );
        }

        Rect dropDownButtonRect = new Rect();
        GUILayout.BeginVertical( GUILayout.Height( ToggleButtonSize ) );
        {
          GUIStyle tmp = new GUIStyle( skin.button );
          tmp.fontSize = 6;

          m_showDropDown = GUILayout.Button( GUI.MakeLabel( "v", true ), tmp, GUILayout.Width( 16 ), GUILayout.Height( 14 ) ) ?
                             !m_showDropDown :
                              m_showDropDown;
          dropDownButtonRect = GUILayoutUtility.GetLastRect();
          GUILayout.FlexibleSpace();
        }
        GUILayout.EndVertical();

        UnityEngine.GUI.enabled = true;

        if ( m_showDropDown && dropDownButtonRect.Contains( Event.current.mousePosition ) ) {
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
    #endregion Rendering GUI

    #region Static singleton initialization methods
    public static bool PrepareEditorDataFolder()
    {
      if ( !AGXUnityFolderExist ) {
        Debug.LogError( "AGXUnity folder is not present in the Assets folder. Something is wrong with the configuration." );
        return false;
      }

      if ( !AGXUnityEditorFolderExist ) {
        AssetDatabase.CreateFolder( AGXUnityEditorPath, "Editor" );
        AssetDatabase.SaveAssets();
      }

      if ( !AGXUnityEditorDataFolderExist ) {
        AssetDatabase.CreateFolder( AGXUnityEditorPath, "Data" );
        AssetDatabase.SaveAssets();
      }

      return true;
    }

    public static T GetOrCreateEditorDataFolderFileInstance<T>( string name ) where T : ScriptableObject
    {
      if ( !PrepareEditorDataFolder() )
        return null;

      string settingsPathAndName = AGXUnityEditorDataPath + @name;
      T instance = AssetDatabase.LoadAssetAtPath<T>( settingsPathAndName );
      if ( instance == null ) {
        instance = CreateInstance<T>();
        AssetDatabase.CreateAsset( instance, settingsPathAndName );
        AssetDatabase.SaveAssets();
      }

      return instance;
    }

    [ MenuItem( "AGXUnity/Settings..." ) ]
    private static void Init()
    {
      EditorSettings instance = GetOrCreateInstance();
      if ( instance == null )
        return;

      EditorUtility.FocusProjectWindow();
      Selection.activeObject = instance;
    }

    private static EditorSettings GetOrCreateInstance()
    {
      if ( m_instance != null )
        return m_instance;

      return ( m_instance = GetOrCreateEditorDataFolderFileInstance<EditorSettings>( "/Settings.asset" ) );
    }

    [NonSerialized]
    private static EditorSettings m_instance = null;
    #endregion Static singleton initialization methods
  }

  [CustomEditor( typeof( EditorSettings ) )]
  public class EditorSettingsEditor : BaseEditor<EditorSettings>
  {
    protected override bool OverrideOnInspectorGUI( EditorSettings target, GUISkin skin )
    {
      EditorSettings.Instance.OnInspectorGUI( CurrentSkin );
      return true;
    }
  }
}
