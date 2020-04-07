using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  public class EditorSettings : ScriptableObject
  {
    #region Static properties
    [HideInInspector]
    public static EditorSettings Instance { get { return GetOrCreateInstance(); } }

    public static string EditorDataDirectory { get { return IO.Utils.AGXUnityEditorDirectory + "/Data"; } }

    [HideInInspector]
    public static readonly int ToggleButtonSize = 18;
    #endregion Static properties

    #region BuiltInToolsTool settings
    public Utils.KeyHandler BuiltInToolsTool_SelectGameObjectKeyHandler = new Utils.KeyHandler( KeyCode.S );
    public Utils.KeyHandler BuiltInToolsTool_SelectRigidBodyKeyHandler = new Utils.KeyHandler( KeyCode.B );
    public Utils.KeyHandler BuiltInToolsTool_PickHandlerKeyHandler = new Utils.KeyHandler( KeyCode.A );
    #endregion BuiltInToolsTool settings

    public bool BuildPlayer_CopyBinaries = true;

    #region Rendering GUI
    public void OnInspectorGUI()
    {
      var skin = InspectorEditor.Skin;

      using ( GUI.AlignBlock.Center )
        GUILayout.Label( GUI.MakeLabel( "AGXUnity Editor Settings", 24, true ), skin.Label );

      // BuiltInToolsTool settings GUI.
      {
        using ( GUI.AlignBlock.Center )
          GUILayout.Label( GUI.MakeLabel( "Built in tools", 16, true ), skin.Label );

        HandleKeyHandlerGUI( GUI.MakeLabel( "Select game object" ), BuiltInToolsTool_SelectGameObjectKeyHandler );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Select rigid body game object" ), BuiltInToolsTool_SelectRigidBodyKeyHandler );
        HandleKeyHandlerGUI( GUI.MakeLabel( "Pick handler (scene view)" ), BuiltInToolsTool_PickHandlerKeyHandler );
      }

      BuildPlayer_CopyBinaries = InspectorGUI.Toggle( GUI.MakeLabel( "<b>Build Player:</b> Copy AGX Dynamics binaries",
                                                                     false,
                                                                     "[Recommended enabled]\nCopy dependent AGX Dynamics binaries to target player directory." ),
                                                      BuildPlayer_CopyBinaries );

      if ( GUILayout.Button( GUI.MakeLabel( "Regenerate custom editors" ), skin.Button ) )
        Utils.CustomEditorGenerator.Synchronize( true );
    }

    private bool m_showDropDown = false;

    private void HandleKeyHandlerGUI( GUIContent name, Utils.KeyHandler keyHandler )
    {
      const int keyButtonWidth = 90;

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
                                                   InspectorEditor.Skin.Button,
                                                   GUILayout.Width( keyButtonWidth ),
                                                   GUILayout.Height( ToggleButtonSize ) );
          if ( toggleDetecting )
            keyHandler.DetectKey( this, !keyHandler.IsDetectingKey( iKey ), iKey );
        }

        Rect dropDownButtonRect = new Rect();
        GUILayout.BeginVertical( GUILayout.Height( ToggleButtonSize ) );
        {
          GUIStyle tmp = new GUIStyle( InspectorEditor.Skin.Button );
          tmp.fontSize = 6;

          m_showDropDown = GUILayout.Button( GUI.MakeLabel( "v", true ),
                                             tmp,
                                             GUILayout.Width( 16 ),
                                             GUILayout.Height( 14 ) ) ?
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
      if ( !AssetDatabase.IsValidFolder( IO.Utils.AGXUnityEditorDirectory + "/Data" ) ) {
        AssetDatabase.CreateFolder( IO.Utils.AGXUnityEditorDirectory, "Data" );
        AssetDatabase.SaveAssets();
      }

      return true;
    }

    public static T GetOrCreateEditorDataFolderFileInstance<T>( string name ) where T : ScriptableObject
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
#endif
      }

      return instance;
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

    /// <summary>
    /// Call from CI when the package is built, avoiding Data.asset
    /// and Settings.asset to be created and included in the package.
    /// </summary>
    private static void OnBuildPackage()
    {
      var dataFilesToExclude = new string[]
      {
        EditorDataDirectory + "/Data.asset",
        EditorDataDirectory + "/Settings.asset"
      };

      Debug.Log( "Applying package build settings..." );
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
}
