using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  public static class IconManager
  {
    /// <summary>
    /// Main editor icons directory.
    /// </summary>
    public static string Directory
    {
      get
      {
        if ( string.IsNullOrEmpty( m_directory ) )
          m_directory = IO.Utils.AGXUnityEditorDirectory +
                        Path.DirectorySeparatorChar +
                        "Icons" +
                        Path.DirectorySeparatorChar +
                        "White";
        return m_directory;
      }
      set
      {
        if ( m_directory != value )
          m_icons.Clear();
        m_directory = value;
      }
    }

    /// <summary>
    /// Icon scale relative IconButtonSize.
    /// </summary>
    public static float Scale { get; set; } = 0.9f;

    /// <summary>
    /// Finds icon relative to Directory and caches the result.
    /// </summary>
    /// <param name="name">Name of icon, including path relative to Directory.</param>
    /// <returns>Icon if found, otherwise null.</returns>
    public static Texture2D GetIcon( string name )
    {
      var iconIdentifier = Directory + Path.DirectorySeparatorChar + name;
      if ( m_icons.TryGetValue( iconIdentifier, out var icon ) )
        return icon;

      icon = EditorGUIUtility.Load( iconIdentifier + ".png" ) as Texture2D;
      if ( icon != null )
        m_icons.Add( iconIdentifier, icon );

      return icon;
    }

    /// <summary>
    /// Foreground color to be used with current state of the button.
    /// </summary>
    /// <param name="active">True if button is active.</param>
    /// <param name="enabled">True if buttin is enabled.</param>
    /// <returns>Icon foreground color.</returns>
    public static Color GetForegroundColor( bool active, bool enabled )
    {
      var color = InspectorGUISkin.BrandColor;
      if ( !enabled )
        color = Color.Lerp( color, InspectorGUI.BackgroundColor, 0.6f );
      if ( active )
        color = Color.Lerp( color, Color.white, 0.4f );
      return color;
    }

    /// <summary>
    /// Finds scaled icon rect given button rect. The icon rect is scaled
    /// given Scale.
    /// </summary>
    /// <param name="buttonRect"></param>
    /// <returns></returns>
    public static Rect GetIconRect( Rect buttonRect )
    {
      var buttonSize = new Vector2( buttonRect.width, buttonRect.height );
      var iconSize   = Scale * buttonSize;
      return new Rect( buttonRect.position + 0.5f * ( buttonSize - iconSize ), iconSize );
    }

    private static Dictionary<string, Texture2D> m_icons = new Dictionary<string, Texture2D>();
    private static string m_directory = string.Empty;
  }

  public class IconViewerWindow : EditorWindow
  {
    [MenuItem( "AGXUnity/Dev/Icon management" )]
    public static void Create()
    {
      EditorWindow.GetWindow<IconViewerWindow>( false, "Icon Management" );
    }

    private void OnEnable()
    {
      m_iconNames.Clear();
      IconManager.Directory = GetEditorData().String;
      var di = new DirectoryInfo( IconManager.Directory );
      if ( !di.Exists ) {
        Debug.LogError( $"Icon directory doesn't exist: {IconManager.Directory}" );
        return;
      }

      foreach ( var fi in di.GetFiles() )
        if ( fi.Extension.ToLower() == ".png" )
          m_iconNames.Add( Path.GetFileNameWithoutExtension( fi.Name ) );
    }

    private void OnDestroy()
    {
    }

    private void OnGUI()
    {
      var selectIconDir = false;
      var editorData = GetEditorData();

      using ( new EditorGUILayout.HorizontalScope() ) {
        EditorGUILayout.LabelField( GUI.MakeLabel( "Icons directory" ),
                                    GUI.MakeLabel( IconManager.Directory.Replace( '\\', '/' ) ),
                                    InspectorGUISkin.Instance.TextField );
        selectIconDir = GUILayout.Button( GUI.MakeLabel( "..." ),
                                          InspectorGUISkin.Instance.Button,
                                          GUILayout.Width( 24 ) );
      }
      EditorGUILayout.LabelField( GUI.MakeLabel( "Number of icons" ), 
                                  GUI.MakeLabel( m_iconNames.Count.ToString() ),
                                  InspectorGUISkin.Instance.Label );
      IconManager.Scale = editorData.Float = EditorGUILayout.Slider( GUI.MakeLabel( "Scale" ),
                                                                     editorData.Float,
                                                                     0.0f,
                                                                     1.0f );
      var newWidth  = EditorGUILayout.Slider( GUI.MakeLabel( "Button width" ),
                                              editorData.Vector2.x,
                                              6.0f,
                                              75.0f );
      var newHeight = EditorGUILayout.Slider( GUI.MakeLabel( "Button height" ),
                                              editorData.Vector2.y,
                                              6.0f,
                                              75.0f );
      InspectorGUISkin.ToolButtonSize = editorData.Vector2 = new Vector2( newWidth, newHeight );

      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, true, false );
      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, false, false );
      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, true, true);
      InspectorGUI.BrandSeparator( 1, 6 );

      if ( selectIconDir ) {
        var result = EditorUtility.OpenFolderPanel( "Icons directory",
                                                    new DirectoryInfo( IconManager.Directory ).Parent.FullName,
                                                    "" );
        if ( !string.IsNullOrEmpty( result ) ) {
          var di = new DirectoryInfo( result );
          if ( di.Exists ) {
            editorData.String = IO.Utils.MakeRelative( result, Application.dataPath );
            OnEnable();
          }
        }
      }
    }

    private void RenderButtons( Vector2 buttonSize,
                                bool buttonsEnabled,
                                bool buttonsActive )
    {
      var numIconsPerRow = (int)( position.width / buttonSize.x );
      var currIconIndex = 0;
      while ( currIconIndex < m_iconNames.Count ) {
        var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, buttonSize.y ) );
        rect.width = buttonSize.x;

        for ( int i = 0; currIconIndex < m_iconNames.Count && i < numIconsPerRow; ++currIconIndex, ++i ) {
          var buttonType = i == 0 && m_iconNames.Count - currIconIndex - 1 == 0              ? InspectorGUISkin.ButtonType.Normal :
                           i == 0                                                            ? InspectorGUISkin.ButtonType.Left :
                           i == numIconsPerRow - 1 || currIconIndex == m_iconNames.Count - 1 ? InspectorGUISkin.ButtonType.Right :
                                                                                               InspectorGUISkin.ButtonType.Middle;
          var content = new GUIContent( IconManager.GetIcon( m_iconNames[ currIconIndex ] ),
                                        m_iconNames[ currIconIndex ] + $" | active: {buttonsActive}, enabled: {buttonsEnabled}" );
          InspectorGUI.ToolButton( rect,
                                   content,
                                   buttonType,
                                   buttonsActive,
                                   buttonsEnabled );
          rect.x += rect.width;
        }
      }
    }

    private EditorDataEntry GetEditorData()
    {
      return EditorData.Instance.GetStaticData( "IconManager", entry =>
      {
        entry.Float = 0.75f;
        entry.String = IconManager.Directory;
        entry.Vector2 = new Vector2( 24.0f, 24.0f );
      } );
    }

    private List<string> m_iconNames = new List<string>();
  }
}
