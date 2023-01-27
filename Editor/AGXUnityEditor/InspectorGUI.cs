using System;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using GUI    = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  /// <summary>
  /// Class containing GUI drawing methods for currently supported types.
  /// The drawing method registers through InspectorDrawer where the
  /// type it draws is defined.
  /// </summary>
  public static class InspectorGUI
  {
    public static GUIContent MakeLabel( MemberInfo field,
                                        string postText = "" )
    {
      var content = new GUIContent();
      content.text = field.Name.SplitCamelCase() + postText;
      content.tooltip = field.GetCustomAttribute<DescriptionAttribute>( false )?.Description;

      return content;
    }

    public static float LayoutMagicNumber
    {
      get
      {
#if UNITY_2019_3_OR_NEWER
        return 22.0f;
#else
        return 14.0f;
#endif
      }
    }

    public class VerticalScopeMarker : IDisposable
    {
      public VerticalScopeMarker( Color color )
      {
        m_begin = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 0.0f ) );
        m_color = color;
      }

      public void Dispose()
      {
        var end = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 0.0f ) );
        var oldColor = Handles.color;
        Handles.color = m_color;
        Handles.DrawLine( new Vector3( m_begin.xMax + 1,
                                       m_begin.position.y + 1.0f,
                                       0 ),
                          new Vector3( end.xMax + 1,
                                       end.position.y - 1.0f,
                                       0 ) );
        Handles.DrawLine( new Vector3( m_begin.xMax + 1 / EditorGUIUtility.pixelsPerPoint,
                                       m_begin.position.y + 1.0f,
                                       0 ),
                          new Vector3( end.xMax + 1 / EditorGUIUtility.pixelsPerPoint,
                                       end.position.y - 1.0f,
                                       0 ) );
        Handles.color = oldColor;
      }

      private Rect m_begin;
      private Color m_color;
    }

    public class IndentScope : IDisposable
    {
      public static IndentScope Create( int numLevels = 1 )
      {
        return new IndentScope( numLevels );
      }

      public static IndentScope NoIndent
      {
        get { return new IndentScope( -Level ); }
      }

      public static IndentScope Single
      {
        get { return Create(); }
      }

      public static float PixelLevel
      {
        get
        {
          return Level * m_pixelsPerLevel;
        }
      }

      public static int Level
      {
        get
        {
          return EditorGUI.indentLevel;
        }
        private set
        {
          EditorGUI.indentLevel = value;
        }
      }

      public int NumLevels { get; private set; } = 0;

      public IndentScope( int numLevels = 1 )
      {
        if ( Level + numLevels < 0 )
          throw new AGXUnity.Exception( "Trying to reach negative indent level: current_level + num_levels < 0" );

        NumLevels = numLevels;
        Level += numLevels;
      }

      public void Dispose()
      {
        Level -= NumLevels;
      }

      private static int m_pixelsPerLevel = 15;
    }

    public static void BrandSeparator( float height = 1.0f, float space = 1.0f )
    {
      Separator( height, space, InspectorGUISkin.BrandColor, 1.0f );
    }

    public static void Separator( float height = 1.0f, float space = 1.0f )
    {
      Separator( height, space, Color.black );
    }

    public static void Separator( float height, float space, Color color )
    {
      Separator( height, space, color, EditorGUIUtility.isProSkin ? 0.35f : 0.25f );
    }

    public static void Separator( float height, float space, Color color, float intensity01 )
    {
      var rect = EditorGUILayout.GetControlRect( GUILayout.Height( space + height ) );
      rect.height = height;
      rect.y += space / 2.0f;
      EditorGUI.DrawRect( rect, Color.Lerp( BackgroundColor, color, intensity01 ) );
    }

    public static void DashedBrandSeparator( float height = 1.0f, float space = 1.0f )
    {
      var rect = EditorGUILayout.GetControlRect( false, space + height );
      rect.height = height;
      rect.y += space / 2.0f;
      var width = EditorGUIUtility.currentViewWidth;
      var dw = 6.0f;
      rect.width = dw;
      while ( rect.x < width ) {
        EditorGUI.DrawRect( rect, InspectorGUISkin.BrandColor );
        rect.x += 2.0f * dw;
      }
    }

    public static bool Link( GUIContent content )
    {
      content.text = GUI.AddColorTag( content.text, EditorGUIUtility.isProSkin ?
                                                      InspectorGUISkin.BrandColorBlue :
                                                      Color.Lerp( InspectorGUISkin.BrandColorBlue,
                                                                  Color.black,
                                                                  0.20f ) );
      var clicked = GUILayout.Button( content, InspectorEditor.Skin.Label );
      EditorGUIUtility.AddCursorRect( GUILayoutUtility.GetLastRect(), MouseCursor.Link );
      return clicked;
    }

    private static GUIContent s_miscIconButtonContent = new GUIContent();

    public static bool Button( MiscIcon icon,
                               bool enabled,
                               string tooltip = "",
                               params GUILayoutOption[] options )
    {

      return Button( icon,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     1.0f,
                     options );
    }

    public static bool Button( MiscIcon icon,
                               bool enabled,
                               string tooltip = "",
                               float buttonScale = 1.0f,
                               params GUILayoutOption[] options )
    {

      return Button( icon,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     buttonScale,
                     options );
    }

    public static bool Button( MiscIcon icon,
                               bool enabled,
                               GUIStyle buttonStyle,
                               string tooltip = "",
                               float iconScale = 1.0f,
                               params GUILayoutOption[] options )
    {
      s_miscIconButtonContent.tooltip = tooltip;
      var pressed = GUILayout.Button( s_miscIconButtonContent,
                                      buttonStyle,
                                      options );
      ButtonIcon( GUILayoutUtility.GetLastRect(), icon, enabled, iconScale );

      return pressed;
    }

    public static bool Button( Rect rect,
                               MiscIcon icon,
                               bool enabled,
                               string tooltip = "",
                               float iconScale = 1.0f )
    {
      return Button( rect,
                     icon,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     iconScale );
    }

    public static bool Button( Rect rect,
                               MiscIcon icon,
                               bool enabled,
                               GUIStyle buttonStyle,
                               string tooltip = "",
                               float iconScale = 1.0f )
    {
      s_miscIconButtonContent.tooltip = tooltip;
      var pressed = false;
      using ( new GUI.EnabledBlock( enabled ) )
        pressed = UnityEngine.GUI.Button( rect,
                                          s_miscIconButtonContent,
                                          buttonStyle );
      ButtonIcon( rect, icon, enabled, iconScale );

      return pressed;
    }

    public static void ButtonIcon( Rect buttonRect, MiscIcon iconType, bool enabled, float scale )
    {
      var icon = IconManager.GetIcon( iconType );
      if ( icon == null )
        return;

      using ( IconManager.ForegroundColorBlock( false, enabled ) )
        UnityEngine.GUI.DrawTexture( IconManager.GetIconRect( buttonRect, scale ), icon );
    }

    public static bool Toggle( GUIContent content,
                               bool value )
    {
      return EditorGUILayout.Toggle( content, value );
    }

    public static bool Toggle( MiscIcon icon,
                               bool active,
                               bool enabled,
                               string tooltip = "",
                               params GUILayoutOption[] options )
    {

      return Toggle( icon,
                     active,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     1.0f,
                     options );
    }

    public static bool Toggle( MiscIcon icon,
                               bool active,
                               bool enabled,
                               string tooltip = "",
                               float buttonScale = 1.0f,
                               params GUILayoutOption[] options )
    {

      return Toggle( icon,
                     active,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     buttonScale,
                     options );
    }

    public static bool Toggle( MiscIcon icon,
                               bool active,
                               bool enabled,
                               GUIStyle buttonStyle,
                               string tooltip = "",
                               float iconScale = 1.0f,
                               params GUILayoutOption[] options )
    {
      s_miscIconButtonContent.tooltip = tooltip;
      var result = GUILayout.Toggle( active,
                                     s_miscIconButtonContent,
                                     buttonStyle,
                                     options );
      ButtonIcon( GUILayoutUtility.GetLastRect(), icon, enabled, iconScale );

      return result;
    }

    public static bool Toggle( Rect rect,
                               MiscIcon icon,
                               bool active,
                               bool enabled,
                               string tooltip = "",
                               float iconScale = 1.0f )
    {
      return Toggle( rect,
                     icon,
                     active,
                     enabled,
                     InspectorEditor.Skin.ButtonMiddle,
                     tooltip,
                     iconScale );
    }

    public static bool Toggle( Rect rect,
                               MiscIcon icon,
                               bool active,
                               bool enabled,
                               GUIStyle buttonStyle,
                               string tooltip = "",
                               float iconScale = 1.0f )
    {
      s_miscIconButtonContent.tooltip = tooltip;
      var result = false;
      using ( new GUI.EnabledBlock( enabled ) )
        result = UnityEngine.GUI.Toggle( rect,
                                         active,
                                         s_miscIconButtonContent,
                                         buttonStyle );
      ButtonIcon( rect, icon, enabled, iconScale );

      return result;
    }

    public static bool Foldout( EditorDataEntry state, GUIContent content, Action<bool> onStateChanged = null )
    {
      // There's a indentation bug (a few pixels off) in EditorGUILayout.Foldout.
      var newState = EditorGUI.Foldout( EditorGUILayout.GetControlRect(),
                                        state.Bool,
                                        content,
                                        true );

      if ( newState != state.Bool )
        UnityEngine.GUI.changed = false;

      if ( onStateChanged != null && newState != state.Bool )
        onStateChanged.Invoke( newState );

      return state.Bool = newState;
    }

    public static Object FoldoutObjectField( GUIContent content,
                                             Object instance,
                                             Type instanceType,
                                             EditorDataEntry foldoutData,
                                             bool isReadOnly )
    {
      var createNewPressed = false;
      var allowSceneObject = instanceType == typeof( GameObject ) ||
                                 typeof( MonoBehaviour ).IsAssignableFrom( instanceType );

      // We're in control of the whole inspector entry.
      var position = EditorGUILayout.GetControlRect();

      // Foldout hijacks control meaning if we're rendering object field
      // or button they won't react/work if the foldout is going all the way.
      // The object field is starting at labelWidth so the foldout is
      // defined from 0 to labelWidth if we're rendering additional stuff.
      var oldWidth = position.xMax;
      if ( !isReadOnly )
        position.xMax = EditorGUIUtility.labelWidth;

      using ( new EditorGUI.DisabledScope( instance == null ) ) {
        var newState = EditorGUI.Foldout( position,
                                          foldoutData.Bool,
                                          content,
                                          true ) && instance != null;
        if ( newState != foldoutData.Bool ) {
          foldoutData.Bool = newState;
          UnityEngine.GUI.changed = false;
        }
      }
      position.xMax = oldWidth;

      // Entry may change, render object field and create-new-button if
      // the instance type supports it.
      Object result;
      if ( !isReadOnly ) {
        var createNewButtonWidth = 18.0f;
        var supportsCreateAsset  = typeof( ScriptAsset ).IsAssignableFrom( instanceType ) ||
                                   instanceType == typeof( Material );

        position.x += EditorGUIUtility.labelWidth - IndentScope.PixelLevel;
        position.xMax -= EditorGUIUtility.labelWidth +
                         Convert.ToInt32( supportsCreateAsset ) * createNewButtonWidth -
                         IndentScope.PixelLevel;
        result = EditorGUI.ObjectField( position, instance, instanceType, allowSceneObject );
        if ( supportsCreateAsset ) {
          var buttonRect = new Rect( position.xMax + 2, position.y, createNewButtonWidth, EditorGUIUtility.singleLineHeight );
          buttonRect.xMax = buttonRect.x + createNewButtonWidth - 2;

          createNewPressed = Button( buttonRect,
                                     MiscIcon.CreateAsset,
                                     UnityEngine.GUI.enabled,
                                     "Create new asset." );
        }
      }
      else
        result = instance;

      // Remove editor if object field is set to null or another object.
      if ( instance != result ) {
        ToolManager.ReleaseRecursiveEditor( instance );
        foldoutData.Bool = false;
      }

      // Recursive editor rendered indented with respect to foldout.
      if ( foldoutData.Bool )
        HandleEditorGUI( ToolManager.TryGetOrCreateRecursiveEditor( result ) );

      if ( createNewPressed ) {
        var assetName = instanceType.Name.SplitCamelCase().ToLower();
        var assetExtension = IO.AGXFileInfo.FindAssetExtension( instanceType );
        var path = EditorUtility.SaveFilePanel( "Create new " + assetName,
                                                "Assets",
                                                "new " + assetName + assetExtension,
                                                assetExtension.TrimStart( '.' ) );
        if ( path != string.Empty ) {
          var info = new System.IO.FileInfo( path );
          var relativePath = IO.Utils.MakeRelative( path, Application.dataPath );
          var newInstance = typeof( ScriptAsset ).IsAssignableFrom( instanceType ) ?
                               ScriptAsset.Create( instanceType ) as Object :
                               new Material( Shader.Find( "Standard" ) );
          newInstance.name = info.Name;
          AssetDatabase.CreateAsset( newInstance, relativePath + ( info.Extension != assetExtension ? assetExtension : "" ) );
          AssetDatabase.SaveAssets();
          AssetDatabase.Refresh();

          result = newInstance;
        }
      }

      return result;
    }

    public static void UnityMaterial( GUIContent objFieldLabel,
                                      Material material,
                                      Action<Material> onMaterialChanged )
    {
      var newMaterial = FoldoutObjectField( objFieldLabel,
                                            material,
                                            typeof( Material ),
                                            EditorData.Instance.GetData( material, objFieldLabel.text ),
                                            false ) as Material;
      if ( newMaterial != null && newMaterial != material && onMaterialChanged != null )
        onMaterialChanged.Invoke( newMaterial );
    }

    private static void HandleEditorGUI( Editor editor )
    {
      if ( editor == null )
        return;

      // Mesh inspector with interactive preview.
      if ( editor.GetType().FullName == "UnityEditor.ModelInspector" ) {
        editor.DrawPreview( EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false,
                                                                                    120.0f ) ) );
        return;
      }

      using ( IndentScope.Single ) {
        if ( editor is MaterialEditor )
          HandleMaterialEditorGUI( editor as MaterialEditor );
        else
          editor.OnInspectorGUI();
      }
    }

    private static void HandleMaterialEditorGUI( MaterialEditor editor )
    {
      var isBuiltInMaterial = editor.target == null ||
                              !AssetDatabase.GetAssetPath( editor.target ).StartsWith( "Assets" ) ||
                              ( editor.target as Material ) == Manager.GetOrCreateShapeVisualDefaultMaterial();
      using ( new GUI.EnabledBlock( !isBuiltInMaterial ) )
      using ( IndentScope.NoIndent ) {
        editor.DrawHeader();
        editor.OnInspectorGUI();
      }
    }

    public static bool SelectFolder( GUIContent label,
                                     string currentFolder,
                                     string openFolderTitle,
                                     Action<string> onNewFolder )
    {
      var updated = false;
      SelectableTextField( label,
                           currentFolder,
                           MiscButtonData.Create( GUI.MakeLabel( "...",
                                                                 InspectorGUISkin.BrandColor,
                                                                 true ),
                                                  () =>
                                                  {
                                                    string result = EditorUtility.OpenFolderPanel( openFolderTitle,
                                                                                                   currentFolder,
                                                                                                   "" );
                                                    if ( !string.IsNullOrEmpty( result ) && result != currentFolder ) {
                                                      onNewFolder?.Invoke( result );
                                                      // Remove focus from any control so that the field is updated.
                                                      UnityEngine.GUI.FocusControl( "" );
                                                      updated = true;
                                                    }
                                                  },
                                                  UnityEngine.GUI.enabled,
                                                  "Open select folder panel." ) );
      return updated;
    }

    public static bool SelectFile( GUIContent label,
                                   string currentFile,
                                   string openFileTitle,
                                   string openFileDirectory,
                                   Action<string> onNewFileSelected )
    {
      var selectNewFolderButtonWidth = 28.0f;

      var rect     = EditorGUILayout.GetControlRect();
      var orgWidth = rect.width;
      rect.width   = EditorGUIUtility.labelWidth;

      EditorGUI.PrefixLabel( rect, label );

      var indentOffset = IndentScope.PixelLevel - 2;

      rect.x    += EditorGUIUtility.labelWidth - indentOffset;
      rect.width = orgWidth -
                   EditorGUIUtility.labelWidth -
                   selectNewFolderButtonWidth + indentOffset;
      EditorGUI.SelectableLabel( rect,
                                 currentFile,
                                 InspectorEditor.Skin.TextField );
      rect.x    += rect.width;
      rect.width = selectNewFolderButtonWidth;
      if ( UnityEngine.GUI.Button( rect,
                                   GUI.MakeLabel( "...",
                                                  InspectorGUISkin.BrandColor,
                                                  true ),
                                   InspectorEditor.Skin.ButtonMiddle ) ) {
        string result = EditorUtility.OpenFilePanel( openFileTitle,
                                                     openFileDirectory,
                                                     "" );
        if ( !string.IsNullOrEmpty( result ) && result != currentFile ) {
          onNewFileSelected?.Invoke( result );
          // Remove focus from any control so that the field is updated.
          UnityEngine.GUI.FocusControl( "" );
          return true;
        }
      }

      return false;
    }

    public static string ToggleSaveFile( GUIContent label,
                                         bool enabled,
                                         Action<bool> enabledResult,
                                         string currentEntry,
                                         string defaultFilename,
                                         string fileExtensionWithoutDot,
                                         string saveFilePanelTitle,
                                         Predicate<string> fileExtensionValidator )
    {
      var saveInitialToggleWidth              = 18.0f;
      var saveInitialSaveFilePanelButtonWidth = 28.0f;

      var saveInitialRect     = EditorGUILayout.GetControlRect();
      var saveInitialOrgWidth = saveInitialRect.width;
      saveInitialRect.width   = EditorGUIUtility.labelWidth;

      EditorGUI.PrefixLabel( saveInitialRect, label );

      var indentOffset = IndentScope.PixelLevel - 2;

      saveInitialRect.x    += EditorGUIUtility.labelWidth - indentOffset;
      saveInitialRect.width = saveInitialToggleWidth;
      enabled               = EditorGUI.Toggle( saveInitialRect,
                                                enabled );
      enabledResult( enabled );
      using ( new GUI.EnabledBlock( enabled ) ) {
        saveInitialRect.x    += saveInitialToggleWidth;
        saveInitialRect.width = saveInitialOrgWidth -
                                EditorGUIUtility.labelWidth -
                                saveInitialToggleWidth -
                                saveInitialSaveFilePanelButtonWidth +
                                indentOffset;
        currentEntry = EditorGUI.TextField( saveInitialRect,
                                            currentEntry,
                                            InspectorEditor.Skin.TextField );
        saveInitialRect.x    += saveInitialRect.width;
        saveInitialRect.width = saveInitialSaveFilePanelButtonWidth;
        if ( UnityEngine.GUI.Button( saveInitialRect,
                                     GUI.MakeLabel( "...", InspectorGUISkin.BrandColor, true ),
                                     InspectorEditor.Skin.ButtonMiddle ) ) {
          string result = EditorUtility.SaveFilePanel( saveFilePanelTitle,
                                                       currentEntry,
                                                       defaultFilename,
                                                       fileExtensionWithoutDot );
          if ( result != string.Empty ) {
            var fileInfo = new System.IO.FileInfo( result );
            if ( fileExtensionValidator( fileInfo.Extension ) )
              currentEntry = result;
            else
              Debug.Log( "Unknown file extension: " + fileInfo.Extension );
          }
        }
      }

      return currentEntry;
    }

    public struct ToolButtonData
    {
      public static ToolButtonData Create( ToolIcon icon,
                                           bool isActive,
                                           string toolTip,
                                           Action onClick,
                                           bool enabled = true,
                                           Action postRender = null )
      {
        return new ToolButtonData()
        {
          Icon       = icon,
          IsActive   = isActive,
          Tooltip    = toolTip,
          Enabled    = enabled,
          OnClick    = onClick,
          PostRender = postRender
        };
      }

      public ToolIcon Icon;
      public bool IsActive;
      public string Tooltip;
      public bool Enabled;
      public Action OnClick;
      public Action PostRender;
    }

    public static void ToolButtons( params ToolButtonData[] data )
    {
      if ( data.Length == 0 )
        return;

      float buttonWidth = InspectorGUISkin.ToolButtonSize.x;
      float buttonHeight = InspectorGUISkin.ToolButtonSize.y;

      var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( true, buttonHeight ) );
      rect.width = buttonWidth;
      for ( int i = 0; i < data.Length; ++i ) {
        var buttonType = data.Length > 1 && i == 0                ? InspectorGUISkin.ButtonType.Left :
                          data.Length > 1 && i == data.Length - 1 ? InspectorGUISkin.ButtonType.Right :
                                                                    InspectorGUISkin.ButtonType.Middle;
        ToolButton( rect, data[ i ], buttonType );
        rect.x += rect.width;
      }
    }

    private static GUIContent s_tooltipContent = new GUIContent( "", "" );

    private static GUIContent ToolButtonTooltip( string tooltip )
    {
      s_tooltipContent.tooltip = tooltip;
      return s_tooltipContent;
    }

    public static bool ToolButton( Rect rect,
                                   ToolButtonData data,
                                   InspectorGUISkin.ButtonType buttonType )
    {
      var texture = IconManager.GetIcon( data.Icon );
      var pressed = false;
      using ( new GUI.EnabledBlock( data.Enabled ) ) {
        var active = UnityEngine.GUI.Toggle( rect,
                                             data.IsActive,
                                             ToolButtonTooltip( data.Tooltip ),
                                             InspectorEditor.Skin.GetButton( buttonType ) );
        pressed = active != data.IsActive;
      }

      if ( texture != null ) {
        using ( IconManager.ForegroundColorBlock( data.IsActive, data.Enabled ) )
          UnityEngine.GUI.DrawTexture( IconManager.GetIconRect( rect ), texture );
      }

      data.PostRender?.Invoke();
      if ( pressed )
        data.OnClick?.Invoke();

      return pressed;
    }

    public static void ToolArrayGUI<T>( Tools.CustomTargetTool context,
                                        T[] items,
                                        string name )
      where T : Object
    {
      if ( !Foldout( EditorData.Instance.GetData( context.Targets[ 0 ], name ),
                     GUI.MakeLabel( name, true ) ) ) {
        context.RemoveEditors( items );
        return;
      }

      if ( items.Length == 0 ) {
        using ( IndentScope.Single )
          EditorGUILayout.LabelField( GUI.MakeLabel( "Empty", true ), InspectorEditor.Skin.Label );
        return;
      }

      Func<Object, string> getConstraintTypename = obj => ( obj as Constraint ).Type.ToString();
      Func<Object, string> getDefaultTypename = obj => obj.GetType().Name;
      var getTypename = items[ 0 ] is Constraint ?
                                                     getConstraintTypename :
                                                     getDefaultTypename;
      using ( IndentScope.Single ) {
        foreach ( var item in items ) {
          if ( !Foldout( EditorData.Instance.GetData( context.Targets[ 0 ],
                                                      item.GetInstanceID().ToString() ),
                         GUI.MakeLabel( InspectorEditor.Skin.TagTypename( getTypename( item ) ) +
                                        ' ' +
                                        item.name ) ) ) {
            context.RemoveEditor( item );
            continue;
          }

          var editor = context.GetOrCreateEditor( item );
          HandleEditorGUI( editor );
        }
      }
    }

    public static void ToolListGUI<T>( Tools.CustomTargetTool context,
                                       T[] items,
                                       string identifier,
                                       T[] availableItemsToAdd,
                                       Action<T> onAdd,
                                       Action<T> onRemove )
      where T : Object
    {
      ToolListGUI( context,
                   items,
                   identifier,
                   onAdd,
                   onRemove,
                   null,
                   null,
                   availableItemsToAdd );
    }

    public static void ToolListGUI<T>( Tools.CustomTargetTool context,
                                       T[] items,
                                       string identifier,
                                       Action<T> onAdd,
                                       Action<T> onRemove,
                                       Action<T, int> preItemEditor = null,
                                       Action<T, int> postItemEditor = null,
                                       T[] availableItemsToAdd = null )
      where T : Object
    {
      var displayItemsList = Foldout( GetTargetToolArrayGUIData( context.Targets[ 0 ], identifier ),
                                      GUI.MakeLabel( identifier + $" [{items.Length}]" ) );
      var itemTypename = typeof( T ).Name;
      var isAsset = typeof( ScriptableObject ).IsAssignableFrom( typeof( T ) );
      var itemTypenameSplit = itemTypename.SplitCamelCase();
      var targetTypename = context.Targets[ 0 ].GetType().Name;
      if ( displayItemsList ) {
        T itemToRemove = null;
        using ( IndentScope.Single ) {
          for ( int itemIndex = 0; itemIndex < items.Length; ++itemIndex ) {
            var item = items[ itemIndex ];

            var displayItem = false;
            using ( new GUILayout.HorizontalScope() ) {
              displayItem = Foldout( GetItemToolArrayGUIData( context.Targets[ 0 ], identifier, item ),
                                     GUI.MakeLabel( InspectorEditor.Skin.TagTypename( itemTypename ) +
                                                    ' ' +
                                                    item.name ) );

              if ( Button( MiscIcon.EntryRemove,
                           true,
                           $"Remove {item.name} from {targetTypename}.",
                           GUILayout.Width( 18 ) ) )
                itemToRemove = item;
            }

            if ( !displayItem ) {
              HandleItemEditorDisable( context, item );
              continue;
            }

            var editor = context.GetOrCreateEditor( item );
            preItemEditor?.Invoke( item, itemIndex );
            HandleEditorGUI( editor );
            postItemEditor?.Invoke( item, itemIndex );
          }

          T itemToAdd = null;
          var addButtonPressed = false;
          GUILayout.Space( 2.0f * EditorGUIUtility.standardVerticalSpacing );
          using ( new GUILayout.VerticalScope( FadeNormalBackground( InspectorEditor.Skin.Label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add item", true ), InspectorEditor.Skin.Label );
            var buttonWidth = 16.0f;
            var rect = EditorGUILayout.GetControlRect();
            var xMax = rect.xMax;
            rect.xMax = rect.xMax - buttonWidth - EditorGUIUtility.standardVerticalSpacing;
            itemToAdd = EditorGUI.ObjectField( rect, (Object)null, typeof( T ), true ) as T;
            rect.x = rect.xMax + 1.25f * EditorGUIUtility.standardVerticalSpacing;
            rect.xMax = xMax;
            rect.width = buttonWidth;
            addButtonPressed = Button( rect, MiscIcon.ContextDropdown, UnityEngine.GUI.enabled );
          }

          if ( addButtonPressed ) {
            var sceneItems = availableItemsToAdd ?? ( isAsset ?
                                                        IO.Utils.FindAssetsOfType<T>( string.Empty ) :
                                                        Object.FindObjectsOfType<T>() );
            var addItemMenu = new GenericMenu();
            addItemMenu.AddDisabledItem( GUI.MakeLabel( itemTypenameSplit +
                                                        "(s) in " +
                                                        ( isAsset || availableItemsToAdd != null ? "project" : "scene" ) ) );
            addItemMenu.AddSeparator( string.Empty );
            foreach ( var sceneItem in sceneItems ) {
              if ( Array.IndexOf( items, sceneItem ) >= 0 )
                continue;
              addItemMenu.AddItem( GUI.MakeLabel( sceneItem.name ),
                                   false,
                                   () =>
                                   {
                                     onAdd( sceneItem );
                                   } );
            }
            addItemMenu.ShowAsContext();
          }

          if ( itemToAdd != null )
            onAdd( itemToAdd );
        }

        if ( itemToRemove != null ) {
          onRemove( itemToRemove );
          HandleItemEditorDisable( context, itemToRemove );
          itemToRemove = null;
        }
      }
      else {
        foreach ( var item in items )
          HandleItemEditorDisable( context, item );
      }
    }

    public static EditorDataEntry GetTargetToolArrayGUIData( Object target,
                                                             string identifier,
                                                             Action<EditorDataEntry> onCreate = null )
    {
      return EditorData.Instance.GetData( target, identifier, onCreate );
    }

    public static EditorDataEntry GetItemToolArrayGUIData( Object target,
                                                           string identifier,
                                                           Object item,
                                                           Action<EditorDataEntry> onCreate = null )
    {
      return EditorData.Instance.GetData( target, $"{identifier}_" + item.GetInstanceID().ToString(), onCreate );
    }

    private static void HandleItemEditorDisable<T>( Tools.CustomTargetTool tool, T item )
      where T : Object
    {
      if ( tool.HasEditor( item ) ) {
        tool.RemoveEditor( item );
        SceneView.RepaintAll();
      }
    }

    public enum PositiveNegativeResult
    {
      Neutral,
      Positive,
      Negative
    }

    public static PositiveNegativeResult PositiveNegativeButtons( bool positiveButtonActive,
                                                                  string positiveButtonName,
                                                                  string positiveButtonTooltip,
                                                                  string negativeButtonName,
                                                                  string negativeButtonTooltip = "" )
    {
      var negativeButtonWidth = 80.0f;
      var positiveButtonWidth = 80.0f;
      var buttonsHeight = 16.0f;

      bool positivePressed = false;
      bool negativePressed = false;

      var position = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false,
                                                                             buttonsHeight +
                                                                             EditorGUIUtility.standardVerticalSpacing ) );

      var negativeRect = new Rect( position.xMax - positiveButtonWidth - negativeButtonWidth,
                                   position.y + EditorGUIUtility.standardVerticalSpacing,
                                   negativeButtonWidth,
                                   buttonsHeight );
      using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
        negativePressed = UnityEngine.GUI.Button( negativeRect,
                                                  GUI.MakeLabel( negativeButtonName ),
                                                  InspectorEditor.Skin.ButtonLeft );

      var positiveRect = new Rect( position.xMax - positiveButtonWidth,
                                   position.y + EditorGUIUtility.standardVerticalSpacing,
                                   positiveButtonWidth,
                                   buttonsHeight );
      using ( new EditorGUI.DisabledGroupScope( !positiveButtonActive ) )
      using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
        positivePressed = UnityEngine.GUI.Button( positiveRect,
                                                  GUI.MakeLabel( positiveButtonName,
                                                                 true,
                                                                 positiveButtonTooltip ),
                                                  InspectorEditor.Skin.ButtonRight );

      return positivePressed ? PositiveNegativeResult.Positive :
             negativePressed ? PositiveNegativeResult.Negative :
                               PositiveNegativeResult.Neutral;

    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick,
                                              Predicate<EnumT> filter = null,
                                              GUIStyle style = null,
                                              GUILayoutOption[] options = null )
    {
      return EnumButtonList( onClick, filter, e => { return style ?? InspectorEditor.Skin.Button; }, options );
    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick,
                                              Predicate<EnumT> filter = null,
                                              Func<EnumT, GUIStyle> styleCallback = null,
                                              GUILayoutOption[] options = null )
    {
      if ( styleCallback == null )
        styleCallback = e => { return InspectorEditor.Skin.Button; };

      foreach ( var eVal in Enum.GetValues( typeof( EnumT ) ) ) {
        bool filterPass = filter == null ||
                          filter( (EnumT)eVal );
        // Execute onClick if eVal passed the filter and the button is pressed.
        if ( filterPass && GUILayout.Button( GUI.MakeLabel( eVal.ToString().SplitCamelCase() ),
                                             styleCallback( (EnumT)eVal ),
                                             options ) ) {
          onClick( (EnumT)eVal );
          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Text field with selectable text which isn't possible to edit.
    /// </summary>
    /// <param name="label">Text field label.</param>
    /// <param name="text">Text in the text field.</param>
    public static void SelectableTextField( GUIContent label,
                                            string text )
    {
      SelectableTextField( label, text, InspectorEditor.Skin.TextField );
    }

    /// <summary>
    /// Text field with selectable text which isn't possible to edit.
    /// </summary>
    /// <param name="label">Text field label.</param>
    /// <param name="text">Text in the text field.</param>
    /// <param name="textFieldStyle">Style of text field.</param>
    public static void SelectableTextField( GUIContent label,
                                            string text,
                                            GUIStyle textFieldStyle )
    {
      var rect = EditorGUILayout.GetControlRect();
      var orgWidth = rect.width;

      rect.width = EditorGUIUtility.labelWidth;

      EditorGUI.PrefixLabel( rect, label );

      var indentOffset = IndentScope.PixelLevel - 2;
      rect.x += EditorGUIUtility.labelWidth - indentOffset;
      rect.width = orgWidth -
                   EditorGUIUtility.labelWidth +
                   indentOffset;

      EditorGUI.SelectableLabel( rect,
                                 text,
                                 textFieldStyle );
    }

    /// <summary>
    /// Misc button data.
    /// </summary>
    public struct MiscButtonData
    {
      /// <summary>
      /// Create given misc icon and an on click callback.
      /// </summary>
      /// <param name="icon">Misc icon for the button.</param>
      /// <param name="onClick">Callback when the button is clicked.</param>
      /// <param name="enabled">True if the button is enabled, otherwise false.</param>
      /// <param name="tooltip">Optional tool-tip of the button.</param>
      /// <param name="width">Width of the button.</param>
      /// <returns></returns>
      public static MiscButtonData Create( MiscIcon icon,
                                           Action onClick,
                                           bool enabled = true,
                                           string tooltip = "",
                                           float width = 28.0f )
      {
        return new MiscButtonData()
        {
          Icon      = icon,
          IconLabel = null,
          OnClick   = onClick,
          Enabled   = enabled,
          Tooltip   = tooltip,
          Width     = width
        };
      }

      /// <summary>
      /// Create given button content and an on click callback.
      /// </summary>
      /// <param name="buttonContent">Button content.</param>
      /// <param name="onClick">Callback when the button is clicked.</param>
      /// <param name="enabled">True if the button is enabled, otherwise false.</param>
      /// <param name="tooltip">Option tool-tip of the button.</param>
      /// <param name="width">Width of the button.</param>
      /// <returns></returns>
      public static MiscButtonData Create( GUIContent buttonContent,
                                           Action onClick,
                                           bool enabled = true,
                                           string tooltip = "",
                                           float width = 28.0f )
      {
        buttonContent.tooltip = tooltip;

        return new MiscButtonData()
        {
          IconLabel = buttonContent,
          OnClick   = onClick,
          Enabled   = enabled,
          Tooltip   = tooltip,
          Width     = width
        };
      }

      public MiscIcon Icon;
      public GUIContent IconLabel;
      public Action OnClick;
      public bool Enabled;
      public string Tooltip;
      public float Width;
    }

    /// <summary>
    /// Selectable text field which isn't possible to edit. On the right
    /// of the text field an arbitrary number of buttons can be added.
    /// </summary>
    /// <param name="label">Text field label.</param>
    /// <param name="text">Text in text field.</param>
    /// <param name="buttonData">Buttons.</param>
    public static void SelectableTextField( GUIContent label,
                                            string text,
                                            params MiscButtonData[] buttonData )
    {
      if ( buttonData.Length == 0 ) {
        SelectableTextField( label, text );
        return;
      }

      var buttonSectionTotalWidth = buttonData.Sum( data => data.Width );
      var rect = EditorGUILayout.GetControlRect();
      var orgWidth = rect.width;

      rect.width = EditorGUIUtility.labelWidth;

      EditorGUI.PrefixLabel( rect, label );

      var indentOffset = IndentScope.PixelLevel - 2;

      rect.x    += EditorGUIUtility.labelWidth - indentOffset;
      rect.width = orgWidth -
                   EditorGUIUtility.labelWidth -
                   buttonSectionTotalWidth +
                   indentOffset;

      EditorGUI.SelectableLabel( rect,
                                 text,
                                 InspectorEditor.Skin.TextField );

      rect.x += rect.width;

      Action clickAction = null;
      foreach ( var data in buttonData ) {
        rect.width = data.Width;
        var clicked = false;
        if ( data.IconLabel != null )
          using ( new GUI.EnabledBlock( data.Enabled ) )
            clicked = UnityEngine.GUI.Button( rect,
                                              data.IconLabel,
                                              InspectorEditor.Skin.ButtonMiddle );
        else
          clicked = Button( rect,
                            data.Icon,
                            data.Enabled,
                            data.Tooltip );
        if ( clicked )
          clickAction = data.OnClick;

        rect.x += rect.width;
      }

      clickAction?.Invoke();
    }

    /// <summary>
    /// Displays license information as:
    ///   License expires            2020-05-13 (14 days 7 hours remaining)
    /// or
    ///   License expired            License not found
    /// or
    ///   License expired            2020-05-13 (3 days ago)
    /// </summary>
    /// <param name="info">License info.</param>
    public static void LicenseEndDateField( LicenseInfo info )
    {
      var fieldColor = EditorGUIUtility.isProSkin ?
                         Color.white :
                         Color.black;
      var fieldErrorColor = Color.Lerp( Color.red,
                                        Color.black,
                                        0.25f );
      var fieldOkColor = Color.Lerp( Color.green,
                                     Color.black,
                                     0.35f );
      var fieldWarningColor = Color.Lerp( Color.yellow,
                                          Color.black,
                                          0.45f );
      EditorGUILayout.LabelField( GUI.MakeLabel( info.IsExpired ?
                                                   "License expired" :
                                                   "License expires" ),
                                  info.ValidEndDate ?
                                    GUI.MakeLabel( info.EndDate.ToString( "yyyy-MM-dd" ) +
                                                   GUI.AddColorTag( $" ({info.DiffString} {( info.IsExpired ? "ago" : "remaining" )})",
                                                                    info.IsExpired ?
                                                                      fieldErrorColor :
                                                                      info.IsAboutToBeExpired( 10 ) ?
                                                                        fieldWarningColor :
                                                                        fieldOkColor ),
                                                   fieldColor ) :
                                  info.IsParsed ?
                                    GUI.MakeLabel( string.IsNullOrEmpty( info.Status ) ?
                                                     "Invalid license" :
                                                     info.Status,
                                                   fieldErrorColor,
                                                   false,
                                                   info.Status ) :
                                    GUI.MakeLabel( "License not found", fieldErrorColor ),
                                  InspectorEditor.Skin.Label );
    }

    public static Color ProBackgroundColor = new Color32( 56, 56, 56, 255 );
    public static Color IndieBackgroundColor = new Color32( 194, 194, 194, 255 );

    public static Color BackgroundColor
    {
      get
      {
        return EditorGUIUtility.isProSkin ? ProBackgroundColor : IndieBackgroundColor;
      }
    }

    public static GUI.ColorBlock NodeListButtonColor
    {
      get
      {
        return new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) );
      }
    }

    public static GUIStyle FadeNormalBackground( GUIStyle style, float t )
    {
      var fadedStyle = new GUIStyle( style );
      var background = EditorGUIUtility.isProSkin ?
                               GUI.CreateColoredTexture( 1, 1, Color.Lerp( ProBackgroundColor, Color.white, t ) ) :
                               GUI.CreateColoredTexture( 1, 1, Color.Lerp( IndieBackgroundColor, Color.black, t ) );
      fadedStyle.normal.background = background;
      return fadedStyle;
    }

    public static void WarningLabel( string warning )
    {
      using ( new GUI.BackgroundColorBlock( Color.Lerp( Color.white, Color.black, 0.55f ) ) )
        EditorGUILayout.LabelField( GUI.MakeLabel( warning,
                                                   Color.Lerp( Color.red, Color.white, 0.25f ),
                                                   true ),
                                    InspectorEditor.Skin.TextAreaMiddleCenter );
    }

    /// <summary>
    /// Handles drag and drop over given area.
    /// </summary>
    /// <example>
    /// GUILayout.Label( "Drag and drop asset here to apply the stuff" );
    /// Utils.GUI.HandleDragDrop&lt; MyAsset &gt;( GUILayoutUtility.GetLastRect(),
    ///                                            Event.current,
    ///                                            ( myAsset ) => { ApplyStuff( myAsset ); } );
    /// </example>
    /// <typeparam name="T">Expected dropped object type.</typeparam>
    /// <param name="dropArea">Drop rect.</param>
    /// <param name="current">Current event.</param>
    /// <param name="validator">Validate if the drop event is valid for the given object in current context.</param>
    /// <param name="onDrop">Callback when an object has been dropped.</param>
    public static void HandleDragDrop<T>( Rect dropArea,
                                          Event current,
                                          Predicate<T> validator,
                                          Action<T> onDrop )
      where T : Object
    {
      bool isDragDropEventInDropArea = current != null &&
                                       ( current.type == EventType.DragPerform ||
                                         current.type == EventType.DragUpdated ) &&
                                       dropArea.Contains( current.mousePosition ) &&
                                       DragAndDrop.objectReferences.Length == 1;
      if ( !isDragDropEventInDropArea )
        return;

      var objectDragged = DragAndDrop.objectReferences[ 0 ] as T;
      if ( objectDragged == null )
        return;

      DragAndDrop.visualMode = validator( objectDragged ) ?
                                  DragAndDropVisualMode.Copy :
                                  DragAndDropVisualMode.Rejected;
      if ( DragAndDrop.visualMode == DragAndDropVisualMode.Copy &&
           Event.current.type == EventType.DragPerform ) {
        DragAndDrop.AcceptDrag();

        onDrop( DragAndDrop.objectReferences[ 0 ] as T );
      }

      current.Use();
    }

    /// <summary>
    /// Handles drag and drop over Scene View.
    /// </summary>
    /// <typeparam name="T">Type dragged.</typeparam>
    /// <param name="current">Current event.</param>
    /// <param name="mouseOverObjectValidator">Predicate if the mouse-over object in scene view supports <typeparamref name="T"/>.</param>
    /// <param name="onDrop">Callback when an object has been dropped.</param>
    public static void HandleSceneViewDragDrop<T>( Event current,
                                                   Predicate<GameObject> mouseOverObjectValidator,
                                                   Action<GameObject, T> onDrop )
      where T : Object
    {
      var isDragDropEvent = current != null &&
                            mouseOverObjectValidator != null &&
                            onDrop != null &&
                            ( current.type == EventType.DragPerform ||
                              current.type == EventType.DragUpdated ) &&
                            Manager.IsMouseOverWindow( SceneView.currentDrawingSceneView ) &&
                            DragAndDrop.objectReferences.Length == 1;
      if ( !isDragDropEvent )
        return;

      var objectDragged = DragAndDrop.objectReferences[ 0 ] as T;
      if ( objectDragged == null )
        return;

      Manager.UpdateMouseOverPrimitives( current, true );

      var isValidMouseOverGameObject = Manager.MouseOverObject != null &&
                                       mouseOverObjectValidator( Manager.MouseOverObject );
      DragAndDrop.visualMode = isValidMouseOverGameObject ?
                                 DragAndDropVisualMode.Copy :
                                 DragAndDropVisualMode.Rejected;
      if ( DragAndDrop.visualMode == DragAndDropVisualMode.Copy &&
           current.type == EventType.DragPerform ) {
        DragAndDrop.AcceptDrag();

        onDrop( Manager.MouseOverObject, objectDragged );
      }

      current.Use();
    }

    public static void HandleFrame( IFrame frame,
                                    int indentLevelInc = 0,
                                    bool includeFrameToolIfPresent = true )
    {
      if ( frame == null )
        return;

      HandleFrames( new IFrame[] { frame },
                    indentLevelInc,
                    includeFrameToolIfPresent );
    }

    public static void HandleFrames( IFrame[] frames,
                                     int indentLevelInc = 0,
                                     bool includeFrameToolIfPresent = true )
    {
      var skin = InspectorEditor.Skin;
      var guiWasEnabled = UnityEngine.GUI.enabled;
      var refFrame = frames[ 0 ];
      var isMultiSelect = frames.Length > 1;

        var frameTool = includeFrameToolIfPresent ?
                          Tools.FrameTool.FindActive( refFrame ) :
                          null;
        if ( frameTool != null )
          frameTool.ToolsGUI( isMultiSelect );

      using ( IndentScope.Create( indentLevelInc ) ) {
        UnityEngine.GUI.enabled = true;
        EditorGUI.showMixedValue = frames.Any( frame => !Equals( refFrame.Parent, frame.Parent ) );
        var newParent = (GameObject)EditorGUILayout.ObjectField( GUI.MakeLabel( "Parent" ),
                                                                 refFrame.Parent,
                                                                 typeof( GameObject ),
                                                                 true );
        EditorGUI.showMixedValue = false;
        UnityEngine.GUI.enabled = guiWasEnabled;

        if ( newParent != refFrame.Parent ) {
          foreach ( var frame in frames )
            frame.SetParent( newParent );
        }

        UnityEngine.GUI.changed = false;

        EditorGUI.showMixedValue = frames.Any( frame => !Equals( refFrame.LocalPosition, frame.LocalPosition ) );
        var localPosition = EditorGUILayout.Vector3Field( GUI.MakeLabel( "Local position" ), refFrame.LocalPosition );
        if ( UnityEngine.GUI.changed ) {
          foreach ( var frame in frames )
            frame.LocalPosition = localPosition;
          UnityEngine.GUI.changed = false;
        }
        EditorGUI.showMixedValue = false;

        // Converting from quaternions to Euler - make sure the actual Euler values has
        // changed before updating local rotation to not mess up the undo stack.
        var inputEuler = refFrame.LocalRotation.eulerAngles;
        EditorGUI.showMixedValue = frames.Any( frame => !Equals( refFrame.LocalRotation, frame.LocalRotation ) );
        var outputEuler = EditorGUILayout.Vector3Field( GUI.MakeLabel( "Local rotation" ), inputEuler );
        if ( !Equals( inputEuler, outputEuler ) ) {
          foreach ( var frame in frames )
            frame.LocalRotation = Quaternion.Euler( outputEuler );
          UnityEngine.GUI.changed = false;
        }
        EditorGUI.showMixedValue = false;
      }
    }

    public struct RangeRealResult
    {
      public float Min;
      public bool MinChanged;
      public float Max;
      public bool MaxChanged;
    }

    private static float[] s_rangeRealValues = new float[]
    {
      0.0f,
      0.0f
    };
    private static GUIContent[] s_rangeRealContent = new GUIContent[]
    {
      GUIContent.none,
      GUIContent.none
    };

    public static RangeRealResult RangeRealField( GUIContent content,
                                                  RangeReal value,
                                                  bool displayInvalidRangeWarning = true )
    {
      return RangeRealField( content,
                             value,
                             GUIContent.none,
                             GUIContent.none,
                             displayInvalidRangeWarning );
    }

    public static RangeRealResult RangeRealField( GUIContent content,
                                                  RangeReal value,
                                                  GUIContent minContent,
                                                  bool displayInvalidRangeWarning = true )
    {
      return RangeRealField( content,
                             value,
                             minContent,
                             GUIContent.none,
                             displayInvalidRangeWarning );
    }

    /// <summary>
    /// Prefix label of EditorGUI.MultiFloatField, e.g.,
    ///     EditorGUI.MultiFloatField( MultiFloatFieldPrefixLabel( label ),
    ///                                GUIContent.none,
    ///                                ... );
    /// </summary>
    /// <param name="label">Label, no label if null.</param>
    /// <returns>Rect to be used for the EditorGUI.MultiFloatField.</returns>
    public static Rect MultiFloatFieldPrefixLabel( GUIContent label )
    {
      var numRectRows = ( EditorGUIUtility.wideMode || label == null ? 1 : 2 );
      var rectHeight = EditorGUIUtility.singleLineHeight * numRectRows;
      var position = EditorGUILayout.GetControlRect( false, rectHeight );

      var orgXMax = position.xMax;
      // No label, indent resulting rect by label width in wide mode.
      // In narrow mode we indent by one indent level (15).
      if ( label == null ) {
        // Wide mode (normal), indent by labelWidth and correction.
        if ( EditorGUIUtility.wideMode ) {
          var indentOffset = IndentScope.PixelLevel - 2;
          position.x += EditorGUIUtility.labelWidth - indentOffset;
        }
        // Narrow mode, indent by one indent level.
        else
          position.x += 15;
      }
      // Prefix label is given, draw label and correct for indentation
      // and/or wide mode state.
      else {
        EditorGUI.PrefixLabel( position, label );
        if ( EditorGUIUtility.wideMode )
          position.x += EditorGUIUtility.labelWidth - IndentScope.PixelLevel + 2;
        else
          position.x += 15;
        if ( numRectRows == 2 )
          position.y += EditorGUIUtility.singleLineHeight;
      }
      position.xMax = orgXMax;

      return position;
    }

    public static RangeRealResult RangeRealField( GUIContent content,
                                                  RangeReal value,
                                                  GUIContent minContent,
                                                  GUIContent maxContent,
                                                  bool displayInvalidRangeWarning = true )
    {
      var invalidRange = displayInvalidRangeWarning && value.Min > value.Max;

      var result = new RangeRealResult()
      {
        Min = value.Min,
        MinChanged = false,
        Max = value.Max,
        MaxChanged = false
      };

      var position = MultiFloatFieldPrefixLabel( content );

      s_rangeRealContent[ 0 ] = minContent;
      s_rangeRealContent[ 1 ] = maxContent;
      s_rangeRealValues[ 0 ]  = value.Min;
      s_rangeRealValues[ 1 ]  = value.Max;

      EditorGUI.BeginChangeCheck();
      EditorGUI.MultiFloatField( position,
                                 GUIContent.none,
                                 s_rangeRealContent,
                                 s_rangeRealValues );
      if ( EditorGUI.EndChangeCheck() ) {
        result.Min = s_rangeRealValues[ 0 ];
        result.MinChanged = s_rangeRealValues[ 0 ] != value.Min;

        result.Max = s_rangeRealValues[ 1 ];
        result.MaxChanged = s_rangeRealValues[ 1 ] != value.Max;
      }

      if ( invalidRange )
        WarningLabel( "Invalid range, Min > Max: (" + value.Min + " > " + value.Max + ")" );

      return result;
    }

    /// <summary>
    /// Draws Vector2 field with custom sub-labels (default "X,Y").
    /// </summary>
    /// <param name="label">Vector2 label.</param>
    /// <param name="value">Current value.</param>
    /// <param name="subLabels">Comma separated string with name of each element.</param>
    /// <returns>Updated value of the Vector2 field.</returns>
    public static Vector2 Vector2Field( GUIContent label, Vector2 value, string subLabels = "X,Y" )
    {
      for ( int i = 0; i < s_multiFloat2Values.Length; ++i )
        s_multiFloat2Values[ i ] = value[ i ];
      Vector234FieldEx( label, s_multiFloat2Values, subLabels, "X,Y", values =>
      {
        for ( int i = 0; i < values.Length; ++i )
          value[ i ] = values[ i ];
      } );
      return value;
    }

    /// <summary>
    /// Draws Vector3 field with custom sub-labels (default "X,Y,Z").
    /// </summary>
    /// <param name="label">Vector3 label.</param>
    /// <param name="value">Current value.</param>
    /// <param name="subLabels">Comma separated string with name of each element.</param>
    /// <returns>Updated value of the Vector3 field.</returns>
    public static Vector3 Vector3Field( GUIContent label, Vector3 value, string subLabels = "X,Y,Z" )
    {
      for ( int i = 0; i < s_multiFloat3Values.Length; ++i )
        s_multiFloat3Values[ i ] = value[ i ];
      Vector234FieldEx( label, s_multiFloat3Values, subLabels, "X,Y,Z", values =>
      {
        for ( int i = 0; i < values.Length; ++i )
          value[ i ] = values[ i ];
      } );
      return value;
    }

    /// <summary>
    /// Draws Vector4 field with custom sub-labels (default "X,Y,Z,W").
    /// </summary>
    /// <param name="label">Vector4 label.</param>
    /// <param name="value">Current value.</param>
    /// <param name="subLabels">Comma separated string with name of each element.</param>
    /// <returns>Updated value of the Vector4 field.</returns>
    public static Vector4 Vector4Field( GUIContent label, Vector4 value, string subLabels = "X,Y,Z,W" )
    {
      for ( int i = 0; i < s_multiFloat4Values.Length; ++i )
        s_multiFloat4Values[ i ] = value[ i ];
      Vector234FieldEx( label, s_multiFloat4Values, subLabels, "X,Y,Z", values =>
      {
        for ( int i = 0; i < values.Length; ++i )
          value[ i ] = values[ i ];
      } );
      return value;
    }

    internal static void Vector234FieldEx( GUIContent label,
                                           float[] values,
                                           string subLabels,
                                           string defaultSubLabels,
                                           Action<float[]> onChange )
    {
      string[] subs = null;
      if ( subLabels == defaultSubLabels )
        subs = values.Length == 2 ?
                 s_multiFloat2DefaultSubLabels :
               values.Length == 3 ?
                 s_multiFloat3DefaultSubLabels :
                 s_multiFloat4DefaultSubLabels;
      else
        subs = subLabels.Split( ',' );
      if ( subs.Length != values.Length )
        throw new AGXUnity.Exception( $"Wrong number of sub-labels for vector, expected {values.Length} commas, got {subLabels.Length}: '{subLabels}'" );
      var contents = values.Length == 2 ?
                       s_multiFloat2Contents :
                     values.Length == 3 ?
                       s_multiFloat3Contents :
                       s_multiFloat4Contents;
      for ( int i = 0; i < values.Length; ++i )
        contents[ i ].text = subs[ i ];

      var position = MultiFloatFieldPrefixLabel( label );
      EditorGUI.BeginChangeCheck();
      EditorGUI.MultiFloatField( position,
                                 GUIContent.none,
                                 contents,
                                 values );
      if ( EditorGUI.EndChangeCheck() )
        onChange?.Invoke( values );
    }

    private static float[] s_multiFloat2Values = new float[] { 0, 0 };
    private static float[] s_multiFloat3Values = new float[] { 0, 0, 0 };
    private static float[] s_multiFloat4Values = new float[] { 0, 0, 0, 0 };
    private static readonly string[] s_multiFloat2DefaultSubLabels = new string[] { "X", "Y" };
    private static readonly string[] s_multiFloat3DefaultSubLabels = new string[] { "X", "Y", "Z" };
    private static readonly string[] s_multiFloat4DefaultSubLabels = new string[] { "X", "Y", "Z", "W" };
    private static GUIContent[] s_multiFloat2Contents = new GUIContent[]
    {
      new GUIContent( s_multiFloat2DefaultSubLabels[ 0 ] ),
      new GUIContent( s_multiFloat2DefaultSubLabels[ 1 ] )
    };
    private static GUIContent[] s_multiFloat3Contents = new GUIContent[]
    {
      new GUIContent( s_multiFloat3DefaultSubLabels[ 0 ] ),
      new GUIContent( s_multiFloat3DefaultSubLabels[ 1 ] ),
      new GUIContent( s_multiFloat3DefaultSubLabels[ 2 ] )
    };
    private static GUIContent[] s_multiFloat4Contents = new GUIContent[]
    {
      new GUIContent( s_multiFloat4DefaultSubLabels[ 0 ] ),
      new GUIContent( s_multiFloat4DefaultSubLabels[ 1 ] ),
      new GUIContent( s_multiFloat4DefaultSubLabels[ 2 ] ),
      new GUIContent( s_multiFloat4DefaultSubLabels[ 3 ] )
    };

    private static GUIContent[] s_customFloatFieldSubLabelContents = new GUIContent[] { GUIContent.none };
    private static float[] s_customFloatFieldData = new float[] { 0.0f };

    public static float CustomFloatField( GUIContent labelContent, GUIContent fieldContent, float value )
    {
      var position = MultiFloatFieldPrefixLabel( labelContent );

      s_customFloatFieldSubLabelContents[ 0 ] = fieldContent;
      s_customFloatFieldData[ 0 ]             = value;

      EditorGUI.BeginChangeCheck();
      EditorGUI.MultiFloatField( position,
                                 GUIContent.none,
                                 s_customFloatFieldSubLabelContents,
                                 s_customFloatFieldData );
      if ( EditorGUI.EndChangeCheck() )
        return s_customFloatFieldData[ 0 ];
      return value;
    }

    /// <summary>
    /// Draws float fields with custom sub-labels
    /// </summary>
    /// <param name="label">Label value.</param>
    /// <param name="subLabels">GUI content .</param>
    /// <param name="values">Current values.</param>
    /// <returns>Updated value of the float fields.</returns>
    public static float[] MultiFloatField(GUIContent label, GUIContent[] subLabels, float[] values)
    {
      var numRectRows = ( EditorGUIUtility.wideMode || label == null ? 1 : 2 );
      var rectHeight = EditorGUIUtility.singleLineHeight * numRectRows;
      var position = EditorGUILayout.GetControlRect( false, rectHeight );
      EditorGUI.MultiFloatField(position, label, subLabels, values);
      return values;
    }

    /// <summary>
    /// Draws a main label and labels for each entry in a MultiField. Intended to be used to provide column headers for a MultiField.
    /// </summary>
    /// <param name="mainLabel">Prefix label GUIContent.</param>
    /// <param name="subLabels">Column labels GUIContents.</param>
    public static void MultiFieldColumnLabels(GUIContent mainLabel, GUIContent[] subLabels)
    {
      var position = mainLabel != null ? InspectorGUI.MultiFloatFieldPrefixLabel(mainLabel) : EditorGUILayout.GetControlRect( false, EditorGUIUtility.singleLineHeight );

      float spacingSubLabel = 4; // From EditorGui.cs
      int count = subLabels.Length;
      var indentOffset = InspectorGUI.IndentScope.PixelLevel - 2;
      float fieldWidth = (position.width - (count - 1) * spacingSubLabel - indentOffset) / count;
      Rect subRect = new Rect(position) {width = fieldWidth, x = position.x + indentOffset};
      int oldIndentLevel = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;
      for (int i = 0; i < count; i++)
      {
        EditorGUI.LabelField(subRect, subLabels[i]);
        subRect.x += fieldWidth + spacingSubLabel;
      }
      EditorGUI.indentLevel = oldIndentLevel;
    }

    private static GUIStyle s_dropdownToolStyle = null;

    private static GUIStyle DropdownToolStyle
    {
      get
      {
        if ( s_dropdownToolStyle == null ) {
          s_dropdownToolStyle = new GUIStyle( InspectorEditor.Skin.Label )
          {
            padding = new RectOffset( 16, 6, 6, 6 )
          };
        }
        return s_dropdownToolStyle;
      }
    }

    public static void ToolDescription( string desc )
    {
      if ( string.IsNullOrEmpty( desc ) )
        return;

      var descContent = new GUIContent( desc );
      //descContent.image = EditorGUIUtility.IconContent( "console.infoicon" ).image;
      var descRect = EditorGUI.IndentedRect( EditorGUILayout.BeginVertical( DropdownToolStyle ) );
      UnityEngine.GUI.Label( descRect, "", InspectorEditor.Skin.TextArea );
      EditorGUILayout.LabelField( descContent, InspectorEditor.Skin.LabelWordWrap );
      EditorGUILayout.EndVertical();
    }

    public static Rect OnDropdownToolBegin( string toolDescription = "" )
    {
      ToolDescription( toolDescription );

      var rect = EditorGUI.IndentedRect( EditorGUILayout.BeginVertical( DropdownToolStyle ) );
      UnityEngine.GUI.Label( rect, "", InspectorEditor.Skin.TextArea );

      EditorGUIUtility.labelWidth -= DropdownToolStyle.padding.left;

      return rect;
    }

    public static void OnDropdownToolEnd()
    {
      EditorGUIUtility.labelWidth += DropdownToolStyle.padding.left;

      EditorGUILayout.EndVertical();
    }
  }
}
