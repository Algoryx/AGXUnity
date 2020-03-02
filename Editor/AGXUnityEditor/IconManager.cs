﻿using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  public enum ToolIcon
  {
    FindTransformGivenPoint,
    FindTransformGivenEdge,
    CreateShapeGivenVisual,
    CreateConstraint,
    DisableCollisions,
    CreateVisual,
    ShapeResize,
    SelectParent,
    TransformHandle,
    VisualizeLineDirection,
    FlipDirection,
    FindTireRim,
    FindTire,
    FindRim,
    FindTrackWheel,
    None
  }

  public enum MiscIcon
  {
    CreateAsset,
    ContextDropdown,
    EntryAdd,
    EntryInsertBefore,
    EntryInsertAfter,
    EntryRemove,
    SynchEnabled,
    SynchDisabled,
    Update,
    ArrowRight,
    Box,
    Sphere,
    Capsule,
    Cylinder,
    Mesh
  }

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
                        "Icons";
        return m_directory;
      }
      set
      {
        if ( m_directory != value ) {
          m_iconCache.Clear();
          m_toolIcons = null;
          m_miscIcons = null;
        }
        m_directory = value;
      }
    }

    /// <summary>
    /// Icon scale relative IconButtonSize.
    /// </summary>
    public static float Scale { get; set; } = 1.0f;

    public static Color NormalColorDark { get; set; } = new Color( 0.952941f, 0.545098f, 0.000000f, 0.733333f );

    public static Color ActiveColorDark { get; set; } = new Color( 1.000000f, 0.625469f, 0.119000f, 0.886275f );

    public static Color DisabledColorDark { get; set; } = new Color( 0.952941f, 0.545098f, 0.000000f, 0.372549f );

    public static Color NormalColorLight { get; set; } = new Color( 0.920000f, 0.526326f, 0.000000f, 0.847059f );

    public static Color ActiveColorLight { get; set; } = new Color( 1.000000f, 0.773440f, 0.469000f, 1.000000f );

    public static Color DisabledColorLight { get; set; } = new Color( 0.921569f, 0.525490f, 0.000000f, 0.552941f );

    public static Color ActiveColor { get { return EditorGUIUtility.isProSkin ? ActiveColorDark : ActiveColorLight; } }

    public static Color NormalColor { get { return EditorGUIUtility.isProSkin ? NormalColorDark : NormalColorLight; } }

    public static Color DisabledColor { get { return EditorGUIUtility.isProSkin ? DisabledColorDark : DisabledColorLight; } }

    /// <summary>
    /// Finds icon relative to Directory and caches the result.
    /// </summary>
    /// <param name="name">Name of icon, including path relative to Directory.</param>
    /// <returns>Icon if found, otherwise null.</returns>
    public static Texture2D GetIcon( string name )
    {
      if ( name.Length > 3 && name.Substring( name.Length - 4 ).ToLower().EndsWith( ".png" ) )
        name = name.Remove( name.Length - 4 );

      var iconIdentifier = Directory + Path.DirectorySeparatorChar + name;
      Texture2D icon = null;
      if ( m_iconCache.TryGetValue( iconIdentifier, out icon ) )
        return icon;

      icon = EditorGUIUtility.Load( iconIdentifier + ".png" ) as Texture2D;
      if ( icon != null )
        m_iconCache.Add( iconIdentifier, icon );

      return icon;
    }

    /// <summary>
    /// Tool icon texture given tool icon type.
    /// </summary>
    /// <param name="toolIcon">Tool icon type.</param>
    /// <returns>Tool icon texture for given type.</returns>
    public static Texture2D GetIcon( ToolIcon toolIcon )
    {
      if ( m_toolIcons == null )
        LoadToolIconContent();

      return m_toolIcons[ (int)toolIcon ];
    }

    public static Texture2D GetIcon( MiscIcon miscIcon )
    {
      if ( m_miscIcons == null )
        LoadMiscIconContent();

      return m_miscIcons[ (int)miscIcon ];
    }

    /// <summary>
    /// Foreground color to be used with current state of the button.
    /// </summary>
    /// <param name="active">True if button is active.</param>
    /// <param name="enabled">True if button is enabled.</param>
    /// <returns>Icon foreground color.</returns>
    public static Color GetForegroundColor( bool active, bool enabled )
    {
      return active && enabled ? ActiveColor : enabled ? NormalColor : DisabledColor;
    }

    /// <summary>
    /// Disposable scope foreground color block.
    /// </summary>
    /// <param name="active">True if button is active.</param>
    /// <param name="enabled">True if button is enabled.</param>
    /// <returns>Icon foreground color block.</returns>
    public static GUI.ColorBlock ForegroundColorBlock( bool active, bool enabled )
    {
      return new GUI.ColorBlock( GetForegroundColor( active, enabled ) );
    }

    /// <summary>
    /// Finds scaled icon rect given button rect. The icon rect is scaled
    /// given Scale.
    /// </summary>
    /// <param name="buttonRect">Button rect.</param>
    /// <returns>Icon rect.</returns>
    public static Rect GetIconRect( Rect buttonRect )
    {
      return GetIconRect( buttonRect, Scale );
    }

    /// <summary>
    /// Finds scaled icon rect given button rect and scale.
    /// </summary>
    /// <param name="buttonRect">Button rect.</param>
    /// <param name="scale">Scale relative button.</param>
    /// <returns>Icon rect.</returns>
    public static Rect GetIconRect( Rect buttonRect, float scale )
    {
      var diff = buttonRect.width - buttonRect.height;
      if ( diff != 0.0f ) {
        buttonRect.width = buttonRect.height;
        buttonRect.x += 0.5f * diff;
      }

#if UNITY_2019_3_OR_NEWER
      //buttonRect.y += 1.0f;
#endif

      var buttonSize = new Vector2( buttonRect.width, buttonRect.height );
      var iconSize   = scale * buttonSize;
      return new Rect( buttonRect.position + 0.5f * ( buttonSize - iconSize ), iconSize );
    }

    private static void LoadToolIconContent()
    {
      var toolIconFilenames = CreateNameArray<ToolIcon>();

      toolIconFilenames[ (int)ToolIcon.FindTransformGivenPoint ] = "find_point_2_icon";
      toolIconFilenames[ (int)ToolIcon.FindTransformGivenEdge ]  = "find_edge_icon";
      toolIconFilenames[ (int)ToolIcon.CreateShapeGivenVisual ]  = "shape_from_icon";
      toolIconFilenames[ (int)ToolIcon.CreateConstraint ]        = "hinge_icon";
      toolIconFilenames[ (int)ToolIcon.DisableCollisions ]       = "disable_collision_icon";
      toolIconFilenames[ (int)ToolIcon.CreateVisual ]            = "shape_from_2_icon";
      toolIconFilenames[ (int)ToolIcon.ShapeResize ]             = "resize_icon";
      toolIconFilenames[ (int)ToolIcon.SelectParent ]            = "parent_icon";
      toolIconFilenames[ (int)ToolIcon.TransformHandle ]         = "position_icon";
      toolIconFilenames[ (int)ToolIcon.VisualizeLineDirection ]  = "visulize_line_2_icon";
      toolIconFilenames[ (int)ToolIcon.FlipDirection ]           = "flip_direction_icon";
      toolIconFilenames[ (int)ToolIcon.FindTireRim ]             = "wheel_all_selected_icon";
      toolIconFilenames[ (int)ToolIcon.FindTire ]                = "wheel_outer_icon";
      toolIconFilenames[ (int)ToolIcon.FindRim ]                 = "wheel_inside_icon";
      toolIconFilenames[ (int)ToolIcon.FindTrackWheel ]          = "wheel_outer_selected_icon";
      toolIconFilenames[ (int)ToolIcon.None ]                    = string.Empty;

      m_toolIcons = LoadIconContent<ToolIcon>( toolIconFilenames );
    }

    private static void LoadMiscIconContent()
    {
      var miscIconFilenames = CreateNameArray<MiscIcon>();

      miscIconFilenames[ (int)MiscIcon.CreateAsset ]       = "small_fat_add_left_icon";//"shape_from_icon";
      miscIconFilenames[ (int)MiscIcon.ContextDropdown ]   = "small_add_dots_icon";
      miscIconFilenames[ (int)MiscIcon.EntryAdd ]          = "small_fat_add_left_icon";
      miscIconFilenames[ (int)MiscIcon.EntryInsertBefore ] = "small_insert_before_2_icon";
      miscIconFilenames[ (int)MiscIcon.EntryInsertAfter ]  = "small_insert_after_2_icon";
      miscIconFilenames[ (int)MiscIcon.EntryRemove ]       = "delete_fat_icon";
      miscIconFilenames[ (int)MiscIcon.SynchEnabled ]      = "small_sync_icon";
      miscIconFilenames[ (int)MiscIcon.SynchDisabled ]     = "small_un_sync_icon";
      miscIconFilenames[ (int)MiscIcon.Update ]            = "small_update_icon";
      miscIconFilenames[ (int)MiscIcon.ArrowRight ]        = "line_direction_icon";
      miscIconFilenames[ (int)MiscIcon.Box ]               = "cube_icon";
      miscIconFilenames[ (int)MiscIcon.Sphere ]            = "sphere_icon";
      miscIconFilenames[ (int)MiscIcon.Capsule ]           = "capsule_icon";
      miscIconFilenames[ (int)MiscIcon.Cylinder ]          = "cylinder_icon";
      miscIconFilenames[ (int)MiscIcon.Mesh ]              = "mesh_icon";

      m_miscIcons = LoadIconContent<MiscIcon>( miscIconFilenames );
    }

    private static string[] CreateNameArray<T>()
      where T : struct
    {
      return new string[ System.Enum.GetValues( typeof( T ) ).Length ];
    }

    private static Texture2D[] LoadIconContent<T>( string[] iconFilenames )
      where T : struct
    {
      var enumValues = System.Enum.GetValues( typeof( T ) );
      var enumNames  = System.Enum.GetNames( typeof( T ) );
      var icons      = new Texture2D[ enumValues.Length ];
      foreach ( int index in enumValues ) {
        if ( string.IsNullOrEmpty( iconFilenames[ index ] ) ) {
          if ( enumNames[ index ] != "None" )
            Debug.LogWarning( "Filename for tool icon "
                              + (ToolIcon)index +
                              " not given - ignoring icon." );
          else
            icons[ index ] = null;

          continue;
        }

        icons[ index ] = GetIcon( iconFilenames[ index ] );
        if ( icons[ index ] == null )
          Debug.LogWarning( "Unable to load tool icon " +
                            (ToolIcon)index +
                            " at: " +
                            Directory + '/' + iconFilenames[ index ] );
      }

      return icons;
    }

    private static Dictionary<string, Texture2D> m_iconCache = new Dictionary<string, Texture2D>();
    private static Texture2D[] m_toolIcons                   = null;
    private static Texture2D[] m_miscIcons                   = null;
    private static string m_directory                        = string.Empty;
  }

  public class IconViewerWindow : EditorWindow
  {
    [MenuItem( "Window/AGXUnity/Icon management" )]
    public static void Create()
    {
      GetWindow<IconViewerWindow>( false, "Icon Management" );
    }

    public static bool ToolButton( Rect rect,
                                   GUIContent content,
                                   InspectorGUISkin.ButtonType buttonType,
                                   bool active,
                                   bool enabled )
    {
      var disabledScope = new EditorGUI.DisabledScope( !enabled );
      var buttonContent = content.image != null ? ToolButtonTooltip( content ) : content;
      var pressed = UnityEngine.GUI.Button( rect,
                                            buttonContent,
                                            InspectorEditor.Skin.GetButton( active, buttonType ) );
      if ( buttonContent == s_tooltipContent && content.image != null ) {
        using ( IconManager.ForegroundColorBlock( active, enabled ) )
          UnityEngine.GUI.DrawTexture( IconManager.GetIconRect( rect ), content.image );
      }

      disabledScope.Dispose();

      return pressed;
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

      foreach ( var fi in di.GetFiles().OrderBy( fi => fi.Name ) )
        if ( fi.Extension.ToLower() == ".png" )
          m_iconNames.Add( Path.GetFileNameWithoutExtension( fi.Name ) );

      GetEditorData( "NormalColorDark",   entry => entry.Color = IconManager.NormalColorDark );
      GetEditorData( "ActiveColorDark",   entry => entry.Color = IconManager.ActiveColorDark );
      GetEditorData( "DisabledColorDark", entry => entry.Color = IconManager.DisabledColorDark );

      GetEditorData( "NormalColorLight",   entry => entry.Color = IconManager.NormalColorLight );
      GetEditorData( "ActiveColorLight",   entry => entry.Color = IconManager.ActiveColorLight );
      GetEditorData( "DisabledColorLight", entry => entry.Color = IconManager.DisabledColorLight );
    }

    private void OnDestroy()
    {
    }

    private Vector2 m_scroll;
    private void OnGUI()
    {
      var iconDirectoryInfo = new DirectoryInfo( IconManager.Directory );
      if ( !iconDirectoryInfo.Exists )
        return;

      if ( iconDirectoryInfo.GetFiles( "*.png.meta" ).Length != m_iconNames.Count ) {
        Debug.LogWarning( "Icon count changed - reloading icons..." );
        OnEnable();
      }

      Undo.RecordObject( EditorData.Instance, "IconManager" );

      var selectIconDir = false;
      var editorData = GetEditorData();

      m_scroll = EditorGUILayout.BeginScrollView( m_scroll );

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
      IconManager.Scale = editorData.Float = Mathf.Clamp( EditorGUILayout.Slider( GUI.MakeLabel( "Scale" ),
                                                                                  editorData.Float,
                                                                                  0.0f,
                                                                                  2.0f ), 1.0E-3f, 2.0f );
      var newWidth  = EditorGUILayout.Slider( GUI.MakeLabel( "Button width" ),
                                              editorData.Vector2.x,
                                              6.0f,
                                              75.0f );
      using ( new GUI.EnabledBlock( false ) ) {
        var newHeight = EditorGUILayout.Slider( GUI.MakeLabel( "Button height" ),
                                                editorData.Vector2.y,
                                                6.0f,
                                                75.0f );
        InspectorGUISkin.ToolButtonSize = editorData.Vector2 = new Vector2( newWidth, newHeight );
      }

      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, true, false );
      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, true, true);
      InspectorGUI.BrandSeparator( 1, 6 );
      RenderButtons( editorData.Vector2, false, false );
      InspectorGUI.BrandSeparator( 1, 6 );

      IconManager.NormalColorDark   = GetEditorData( "NormalColorDark" ).Color   = EditorGUILayout.ColorField( GUI.MakeLabel( "Normal Dark" ),
                                                                                                               GetEditorData( "NormalColorDark" ).Color );
      IconManager.ActiveColorDark   = GetEditorData( "ActiveColorDark" ).Color   = EditorGUILayout.ColorField( GUI.MakeLabel( "Active Dark" ),
                                                                                                               GetEditorData( "ActiveColorDark" ).Color );
      IconManager.DisabledColorDark = GetEditorData( "DisabledColorDark" ).Color = EditorGUILayout.ColorField( GUI.MakeLabel( "Disabled Dark" ),
                                                                                                               GetEditorData( "DisabledColorDark" ).Color );

      IconManager.NormalColorLight   = GetEditorData( "NormalColorLight" ).Color   = EditorGUILayout.ColorField( GUI.MakeLabel( "Normal Light" ),
                                                                                                                 GetEditorData( "NormalColorLight" ).Color );
      IconManager.ActiveColorLight   = GetEditorData( "ActiveColorLight" ).Color   = EditorGUILayout.ColorField( GUI.MakeLabel( "Active Light" ),
                                                                                                                 GetEditorData( "ActiveColorLight" ).Color );
      IconManager.DisabledColorLight = GetEditorData( "DisabledColorLight" ).Color = EditorGUILayout.ColorField( GUI.MakeLabel( "Disabled Light" ),
                                                                                                                 GetEditorData( "DisabledColorLight" ).Color );

      EditorGUILayout.LabelField( GUI.MakeLabel( "Brand color" ),
                                  new GUIContent( GUI.CreateColoredTexture( (int)EditorGUIUtility.currentViewWidth,
                                                                            (int)EditorGUIUtility.singleLineHeight,
                                                                            InspectorGUISkin.BrandColor ) ) );

      var numLines = 6;
      var rect = EditorGUILayout.GetControlRect( false, numLines * EditorGUIUtility.singleLineHeight );
      EditorGUI.SelectableLabel( rect, GetColorsString(), InspectorEditor.Skin.TextFieldMiddleLeft );

      InspectorGUI.BrandSeparator( 1, 6 );

      RenderIcons( IconManager.Scale * 24.0f * Vector2.one );

      InspectorGUI.BrandSeparator( 1, 6 );

      RenderIcons();

      InspectorGUI.BrandSeparator( 1, 6 );

      EditorGUILayout.EndScrollView();

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
      GUILayout.Label( GUI.MakeLabel( buttonsEnabled && buttonsActive ?
                                        "Enabled and active" :
                                      buttonsEnabled == buttonsActive ?
                                        "Disabled and inactive" :
                                      buttonsEnabled ?
                                       "Enabled and inactive" :
                                       "Disabled and active??????????" ),
                       InspectorGUISkin.Instance.LabelMiddleCenter );
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
          var pressed = ToolButton( rect,
                                    content,
                                    buttonType,
                                    buttonsActive,
                                    buttonsEnabled );
          if ( pressed )
            EditorGUIUtility.systemCopyBuffer = m_iconNames[ currIconIndex ];
          rect.x += rect.width;
        }
      }
    }

    private void RenderIcons( Vector2 size )
    {
      var numIconsPerRow = (int)( position.width / size.x );
      var currIconIndex  = 0;
      while ( currIconIndex < m_iconNames.Count ) {
        var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, size.y ) );
        rect.width = size.x;

        for ( int i = 0; currIconIndex < m_iconNames.Count && i < numIconsPerRow; ++currIconIndex, ++i ) {
          using ( new GUI.ColorBlock( IconManager.ActiveColor ) )
            UnityEngine.GUI.DrawTexture( rect, IconManager.GetIcon( m_iconNames[ currIconIndex ] ) );
          rect.x += rect.width + 4.0f;
        }
      }
    }

    private void RenderIcons()
    {
      var currIconIndex = 0;
      while ( currIconIndex < m_iconNames.Count ) {
        var numIconsOnRow = 0;
        var currWidth = 0.0f;
        var maxHeight = 0.0f;
        for ( numIconsOnRow = 0; currIconIndex + numIconsOnRow < m_iconNames.Count; ) {
          var icon   = IconManager.GetIcon( m_iconNames[ numIconsOnRow + currIconIndex ] );
          maxHeight  = Mathf.Max( icon.height, maxHeight );
          if ( currWidth + icon.width < EditorGUIUtility.currentViewWidth ) {
            currWidth += icon.width;
            ++numIconsOnRow;
          }
          else
            break;
        }

        if ( numIconsOnRow == 0 )
          break;

        var rect = EditorGUILayout.GetControlRect( false, maxHeight );
        for ( int i = 0; i < numIconsOnRow && currIconIndex < m_iconNames.Count; ++i, ++currIconIndex ) {
          var icon = IconManager.GetIcon( m_iconNames[ currIconIndex ] );
          rect.width = icon.width;
          rect.height = icon.height;
          using ( new GUI.ColorBlock( IconManager.ActiveColor ) )
            UnityEngine.GUI.DrawTexture( rect, icon );
          rect.x    += icon.width;
        }

        if ( currIconIndex < m_iconNames.Count )
          InspectorGUI.DashedBrandSeparator( 1, 6 );
      }
    }

    private string GetColorString( string name )
    {
      var color = GetEditorData( name ).Color;
      return $"public static Color {name} {{ get; set; }} = new Color( {color.r:F6}f, {color.g:F6}f, {color.b:F6}f, {color.a:F6}f );";
    }

    private string GetColorsString()
    {
      return GetColorString( "NormalColorDark" ) + '\n' +
             GetColorString( "ActiveColorDark" ) + '\n' +
             GetColorString( "DisabledColorDark" ) + '\n' +
             GetColorString( "NormalColorLight" ) + '\n' +
             GetColorString( "ActiveColorLight" ) + '\n' +
             GetColorString( "DisabledColorLight" );
    }

    private EditorDataEntry GetEditorData()
    {
      return EditorData.Instance.GetStaticData( "IconManager", entry =>
      {
        entry.Float = IconManager.Scale;
        entry.String = IconManager.Directory;
        entry.Vector2 = InspectorGUISkin.ToolButtonSize;
      } );
    }

    private EditorDataEntry GetEditorData( string id, System.Action<EditorDataEntry> onCreate = null )
    {
      return EditorData.Instance.GetStaticData( "IconManager_" + id, onCreate );
    }

    private static GUIContent ToolButtonTooltip( GUIContent originalContent )
    {
      s_tooltipContent.tooltip = originalContent.tooltip;
      return s_tooltipContent;
    }

    private static GUIContent s_tooltipContent = new GUIContent( "", "" );
    private List<string> m_iconNames = new List<string>();
  }
}
