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
    public static GUIContent MakeLabel( MemberInfo field )
    {
      var content = new GUIContent();
      content.text = field.Name.SplitCamelCase();
      content.tooltip = field.GetCustomAttribute<DescriptionAttribute>( false )?.Description;

      return content;
    }

    public static float GetWidth( GUIContent content, GUIStyle style )
    {
      var width = 0.0f;
      var maxWidth = 0.0f;
      style.CalcMinMaxWidth( content, out width, out maxWidth );
      return width;
    }

    public static float GetWidthIncludingIndent( GUIContent content, GUIStyle style )
    {
      return GetWidth( content, style ) + IndentScope.PixelLevel;
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
      var rect = EditorGUILayout.GetControlRect( GUILayout.Height( space + height ) );
      rect.height = height;
      rect.y += space / 2.0f;
      EditorGUI.DrawRect( rect, InspectorGUISkin.BrandColor );
    }

    public static void Separator( float height = 1.0f, float space = 1.0f )
    {
      var rect = EditorGUILayout.GetControlRect( GUILayout.Height( space + height ) );
      rect.height = height;
      rect.y += space / 2.0f;
      EditorGUI.DrawRect( rect,
                          Color.Lerp( BackgroundColor,
                                      Color.black,
                                      EditorGUIUtility.isProSkin ? 0.35f : 0.25f ) );
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

    public static bool Foldout( EditorDataEntry state, GUIContent content, Action<bool> onStateChanged = null )
    {
      var newState = EditorGUILayout.Foldout( state.Bool, content, true );

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
                                     true,
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
      using ( new EditorGUI.DisabledGroupScope( isBuiltInMaterial ) )
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
      var selectNewFolderButtonWidth = 28.0f;

      var rect     = EditorGUILayout.GetControlRect();
      var orgWidth = rect.width;
      rect.width   = EditorGUIUtility.labelWidth;

      EditorGUI.PrefixLabel( rect, label );

      rect.x    += EditorGUIUtility.labelWidth;
      rect.width = orgWidth -
                   EditorGUIUtility.labelWidth -
                   selectNewFolderButtonWidth;
      EditorGUI.TextField( rect,
                           currentFolder,
                           InspectorEditor.Skin.TextField );
      rect.x    += rect.width;
      rect.width = selectNewFolderButtonWidth;
      if ( UnityEngine.GUI.Button( rect,
                                   GUI.MakeLabel( "..." ),
                                   InspectorEditor.Skin.ButtonMiddle ) ) {
        string result = EditorUtility.OpenFolderPanel( openFolderTitle,
                                                       currentFolder,
                                                       "" );
        if ( !string.IsNullOrEmpty( result ) && result != currentFolder ) {
          onNewFolder( result );
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

      saveInitialRect.x    += EditorGUIUtility.labelWidth;
      saveInitialRect.width = saveInitialToggleWidth;
      enabled               = EditorGUI.Toggle( saveInitialRect,
                                                enabled );
      enabledResult( enabled );
      using ( new GUI.EnabledBlock( enabled ) ) {
        saveInitialRect.x    += saveInitialToggleWidth;
        saveInitialRect.width = saveInitialOrgWidth -
                                EditorGUIUtility.labelWidth -
                                saveInitialToggleWidth -
                                saveInitialSaveFilePanelButtonWidth;
        currentEntry = EditorGUI.TextField( saveInitialRect,
                                            currentEntry,
                                            InspectorEditor.Skin.TextField );
        saveInitialRect.x    += saveInitialRect.width;
        saveInitialRect.width = saveInitialSaveFilePanelButtonWidth;
        if ( UnityEngine.GUI.Button( saveInitialRect,
                                     GUI.MakeLabel( "..." ),
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
      using ( new GUI.EnabledBlock( data.Enabled ) )
        pressed = UnityEngine.GUI.Button( rect,
                                          ToolButtonTooltip( data.Tooltip ),
                                          InspectorEditor.Skin.GetButton( data.IsActive, buttonType ) );
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
                                       Action<T> onAdd,
                                       Action<T> onRemove,
                                       Action<T, int> preItemEditor = null,
                                       Action<T, int> postItemEditor = null )
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
            addButtonPressed = Button( rect, MiscIcon.ContextDropdown, true );
          }

          if ( addButtonPressed ) {
            var sceneItems = isAsset ?
                               IO.Utils.FindAssetsOfType<T>( string.Empty ) :
                               Object.FindObjectsOfType<T>();
            var addItemMenu = new GenericMenu();
            addItemMenu.AddDisabledItem( GUI.MakeLabel( itemTypenameSplit + "(s) in " + ( isAsset ? "project" : "scene:" ) ) );
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
      var prevBgc = UnityEngine.GUI.backgroundColor;
      UnityEngine.GUI.backgroundColor = Color.Lerp( Color.white, Color.black, 0.55f );
      EditorGUILayout.LabelField( GUI.MakeLabel( warning,
                                                 Color.Lerp( Color.red, Color.white, 0.25f ),
                                                 true ),
                                  InspectorEditor.Skin.TextAreaMiddleCenter );
      UnityEngine.GUI.backgroundColor = prevBgc;
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
    /// <param name="onDrop">Callback when an object has been dropped.</param>
    public static void HandleDragDrop<T>( Rect dropArea, Event current, Action<T> onDrop )
      where T : Object
    {
      bool isDragDropEventInDropArea = ( current.type == EventType.DragPerform || current.type == EventType.DragUpdated ) && dropArea.Contains( current.mousePosition );
      if ( !isDragDropEventInDropArea )
        return;

      bool validObject = DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[ 0 ] is T;
      DragAndDrop.visualMode = validObject ?
                                  DragAndDropVisualMode.Copy :
                                  DragAndDropVisualMode.Rejected;

      if ( Event.current.type == EventType.DragPerform && validObject ) {
        DragAndDrop.AcceptDrag();

        onDrop( DragAndDrop.objectReferences[ 0 ] as T );
      }
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

      var position = EditorGUILayout.GetControlRect();
      s_rangeRealContent[ 0 ] = minContent;
      s_rangeRealContent[ 1 ] = maxContent;
      s_rangeRealValues[ 0 ]  = value.Min;
      s_rangeRealValues[ 1 ]  = value.Max;

      EditorGUI.BeginChangeCheck();
      EditorGUI.MultiFloatField( position,
                                 content,
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

    private static GUIContent s_customFloatFieldEmptyContent = new GUIContent( " " );
    private static GUIContent[] s_customFloatFieldSubLabelContents = new GUIContent[] { GUIContent.none };
    private static float[] s_customFloatFieldData = new float[] { 0.0f };

    public static float CustomFloatField( GUIContent labelContent, GUIContent fieldContent, float value )
    {
      var content                             = labelContent ?? s_customFloatFieldEmptyContent;
      var position                            = EditorGUILayout.GetControlRect();
      s_customFloatFieldSubLabelContents[ 0 ] = fieldContent;
      s_customFloatFieldData[ 0 ]             = value;

      EditorGUI.BeginChangeCheck();
      EditorGUI.MultiFloatField( position,
                                 content,
                                 s_customFloatFieldSubLabelContents,
                                 s_customFloatFieldData );
      if ( EditorGUI.EndChangeCheck() )
        return s_customFloatFieldData[ 0 ];
      return value;
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
