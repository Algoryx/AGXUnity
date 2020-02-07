using System;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using GUI    = AGXUnityEditor.Utils.GUI;
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
      var content     = new GUIContent();
      content.text    = field.Name.SplitCamelCase();
      content.tooltip = field.GetCustomAttribute<DescriptionAttribute>( false )?.Description;

      return content;
    }

    public static float GetWidth( GUIContent content, GUIStyle style )
    {
      var width    = 0.0f;
      var maxWidth = 0.0f;
      style.CalcMinMaxWidth( content, out width, out maxWidth );
      return width;
    }

    public static float GetWidthIncludingIndent( GUIContent content, GUIStyle style )
    {
      return GetWidth( content, style ) + IndentScope.PixelLevel;
    }

    public class VerticalBrandLine : IDisposable
    {
      public VerticalBrandLine()
      {
        m_begin = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 1.0f ) );
      }

      public void Dispose()
      {
        var end = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 1.0f ) );
        var oldColor = Handles.color;
        Handles.color = InspectorGUISkin.BrandColor;
        //Handles.DrawLine( new Vector3( 1, m_begin.position.y, 0 ), new Vector3( 1, end.position.y, 0 ) );
        Handles.DrawLine( new Vector3( 2, m_begin.position.y, 0 ), new Vector3( 2, end.position.y, 0 ) );
        Handles.color = oldColor;
      }

      private Rect m_begin;
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
        Level    += numLevels;
      }

      public void Dispose()
      {
        Level -= NumLevels;
      }

      private static int m_pixelsPerLevel = 15;
    }

    public static void SeparatorSimple( float height = 1.0f, float space = 1.0f )
    {
      var rect = EditorGUILayout.GetControlRect( GUILayout.Height( space + height ) );
      rect.height = height;
      rect.y += space / 2.0f;
      EditorGUI.DrawRect( rect, InspectorGUISkin.BrandColor );
    }

    public static void Separator3D( float space = 2.0f )
    {
      GUILayout.Space( space );

      var r1 = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( GUILayout.ExpandWidth( true ),
                                                                       GUILayout.Height( 1f ) ) );
      EditorGUI.DrawRect( r1, InspectorGUISkin.BrandColor );

      var r2 = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( GUILayout.ExpandWidth( true ),
                                                                       GUILayout.Height( 1f ) ) );
      r2.x += 1.0f;
      r2.xMax -= 1.0f;
      r2.y -= 2.0f;
      EditorGUI.DrawRect( r2, Color.black );

      // Moving up 3 pixels since "shadow" rect is 1 pixel wide and moved
      // up 2 pixels. We basically want this to active like one control rect.
      GUILayout.Space( space - 3.0f );
    }

    public static bool Toggle( GUIContent content,
                               bool value )
    {
      return EditorGUILayout.Toggle( content, value );
    }

    public static bool Foldout( EditorDataEntry state, GUIContent content, Action<bool> onStateChanged = null )
    {
      var newState = EditorGUILayout.Foldout( state.Bool, content, true );
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
      var createNewButtonWidth = 35.0f;
      var createNewPressed     = false;
      Object result            = null;
      var allowSceneObject     = instanceType == typeof( GameObject ) ||
                                 typeof( ScriptComponent ).IsAssignableFrom( instanceType );

      // We're in control of the whole inspector entry.
      var position = EditorGUILayout.GetControlRect();

      // Foldout hijacks control meaning if we're rendering object field
      // or button they won't react/work if the foldout is going all the way.
      // The object field is starting at labelWidth so the foldout is
      // defined from 0 to labelWidth if we're rendering additional stuff.
      var oldWidth = position.xMax;
      if ( !isReadOnly )
        position.xMax = EditorGUIUtility.labelWidth;

      using ( new EditorGUI.DisabledScope( instance == null ) )
        foldoutData.Bool = EditorGUI.Foldout( position,
                                              foldoutData.Bool,
                                              content,
                                              true ) && instance != null;
      position.xMax    = oldWidth;

      // Entry may change, render object field and create-new-button if
      // the instance type supports it.
      if ( !isReadOnly ) {
        var supportsCreateAsset = typeof( ScriptAsset ).IsAssignableFrom( instanceType ) ||
                                  instanceType == typeof( Material );

        position.x    += EditorGUIUtility.labelWidth - IndentScope.PixelLevel;
        position.xMax -= EditorGUIUtility.labelWidth +
                         Convert.ToInt32( supportsCreateAsset ) * createNewButtonWidth -
                         IndentScope.PixelLevel;
        result         = EditorGUI.ObjectField( position, instance, instanceType, true );
        if ( supportsCreateAsset ) {
          var buttonRect = new Rect( position.xMax + 4, position.y, createNewButtonWidth, EditorGUIUtility.singleLineHeight );
          buttonRect.xMax = buttonRect.x + createNewButtonWidth - 2;

          using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, InspectorGUISkin.BrandColor, 0.25f ) ) )
            createNewPressed = UnityEngine.GUI.Button( buttonRect,
                                                       GUI.MakeLabel( "New", false, "Create new asset" ),
                                                       InspectorEditor.Skin.Button );
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
      if ( foldoutData.Bool ) {
        HandleEditorGUI( ToolManager.TryGetOrCreateRecursiveEditor( result ) );
      }

      if ( createNewPressed ) {
        var assetName      = instanceType.Name.SplitCamelCase().ToLower();
        var assetExtension = IO.AGXFileInfo.FindAssetExtension( instanceType );
        var path           = EditorUtility.SaveFilePanel( "Create new " + assetName,
                                                          "Assets",
                                                          "new " + assetName + assetExtension,
                                                          assetExtension.TrimStart( '.' ) );
        if ( path != string.Empty ) {
          var info         = new System.IO.FileInfo( path );
          var relativePath = IO.Utils.MakeRelative( path, Application.dataPath );
          var newInstance  = typeof( ScriptAsset ).IsAssignableFrom( instanceType ) ?
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

      using ( IndentScope.Single ) {
        if ( editor is MaterialEditor )
          HandleMaterialEditorGUI( editor as MaterialEditor );
        else {
          editor.OnInspectorGUI();
        }
      }
    }

    private static void HandleMaterialEditorGUI( MaterialEditor editor )
    {
      var isBuiltInMaterial = editor.target == null ||
                              !AssetDatabase.GetAssetPath( editor.target ).StartsWith( "Assets" ) ||
                              (editor.target as Material) == Manager.GetOrCreateShapeVisualDefaultMaterial();
      using ( new EditorGUI.DisabledGroupScope( isBuiltInMaterial ) )
      using ( IndentScope.NoIndent ) {
        SeparatorSimple();
        editor.DrawHeader();
        editor.OnInspectorGUI();
        SeparatorSimple();
      }
    }

    public struct ToolButtonData
    {
      public static GUI.ColorBlock ColorBlock
      {
        get
        {
          return new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color,
                                                 EditorGUIUtility.isProSkin ? InspectorGUISkin.BrandColor : ProBackgroundColor,
                                                 0.1f ) );
        }
      }

      public static ToolButtonData Create( char symbol,
                                           bool isActive,
                                           string toolTip,
                                           Action onClick,
                                           bool enabled = true,
                                           Action postRender = null )
      {
        return Create( GUI.MakeLabel( symbol.ToString(),
                                      false,
                                      toolTip ),
                       isActive,
                       onClick,
                       enabled );
      }

      public static ToolButtonData Create( string icon,
                                           bool isActive,
                                           string toolTip,
                                           Action onClick,
                                           bool enabled = true,
                                           Action postRender = null )
      {
        var content     = new GUIContent();
        content.image   = IconManager.GetIcon( icon );
        content.tooltip = toolTip;
        return Create( content,
                       isActive,
                       onClick,
                       enabled );
      }

      public static ToolButtonData Create( GUIContent content,
                                           bool isActive,
                                           Action onClick,
                                           bool enabled = true,
                                           Action postRender = null )
      {
        return new ToolButtonData()
        {
          GUIContent = content,
          IsActive   = isActive,
          Enabled    = enabled,
          OnClick    = onClick,
          PostRender = postRender
        };
      }

      public GUIContent GUIContent;
      public bool IsActive;
      public bool Enabled;
      public Action OnClick;
      public Action PostRender;
    }

    public static void ToolButtons( params ToolButtonData[] data )
    {
      if ( data.Length == 0 )
        return;

      float buttonWidth  = IconManager.IconButtonSize.x;
      float buttonHeight = IconManager.IconButtonSize.y;
      using ( ToolButtonData.ColorBlock ) {
        Separator3D();
        var position   = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, buttonHeight ) );
        position.width = buttonWidth;

        for ( int i = 0; i < data.Length; ++i ) {
          using ( new EditorGUI.DisabledGroupScope( !data[ i ].Enabled ) ) {
            var buttonType = data.Length > 1 && i == 0               ? InspectorGUISkin.ButtonType.Left :
                             data.Length > 1 && i == data.Length - 1 ? InspectorGUISkin.ButtonType.Right :
                                                                       InspectorGUISkin.ButtonType.Middle;
            var content = data[ i ].GUIContent.image != null ? GUIContent.none : data[ i ].GUIContent;
            var pressed = UnityEngine.GUI.Button( position,
                                                  content,
                                                  InspectorEditor.Skin.GetButton( data[ i ].IsActive, buttonType ) );
            if ( content == GUIContent.none && data[ i ].GUIContent.image != null ) {
              var color = IconManager.IsWhite ?
                            new GUI.ColorBlock( Color.Lerp( InspectorGUISkin.BrandColor, BackgroundColor, data[ i ].Enabled ? 0.0f : 0.6f ) ) :
                            new GUI.ColorBlock( Color.Lerp( Color.white, BackgroundColor, data[ i ].Enabled ? 0.0f : 0.6f ) );
              UnityEngine.GUI.DrawTexture( IconManager.GetIconRect( position ), data[ i ].GUIContent.image );
              color.Dispose();
            }
            position.x += buttonWidth;

            data[ i ].PostRender?.Invoke();

            if ( pressed )
              data[ i ].OnClick?.Invoke();
          }
        }
      }
    }

    public static void ToolArrayGUI<T>( Tools.CustomTargetTool tool,
                                        T[] items,
                                        string identifier,
                                        Color itemColorIdeintifier,
                                        Action<T> onAdd,
                                        Action<T> onRemove,
                                        Action<T, int> preItemEditor = null,
                                        Action<T, int> postItemEditor = null )
      where T : Object
    {
      var displayItemsList = Foldout( GetTargetToolArrayGUIData( tool.Targets[ 0 ], identifier ),
                                      GUI.MakeLabel( identifier + $" [{items.Length}]" ) );
      var itemTypename      = typeof( T ).Name;
      var isAsset           = typeof( ScriptableObject ).IsAssignableFrom( typeof( T ) );
      var itemTypenameSplit = itemTypename.SplitCamelCase();
      var targetTypename    = tool.Targets[ 0 ].GetType().Name;
      if ( displayItemsList ) {
        T itemToRemove = null;
        using ( IndentScope.Single ) {
          for ( int itemIndex = 0; itemIndex < items.Length; ++itemIndex ) {
            var item = items[ itemIndex ];

            var displayItem = false;
            using ( new GUILayout.HorizontalScope() ) {
              displayItem = Foldout( GetItemToolArrayGUIData( tool.Targets[ 0 ], identifier, item ),
                                     GUI.MakeLabel( "[" + GUI.AddColorTag( itemTypename,
                                                                           itemColorIdeintifier ) + "] " + item.name ) );

              using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
                if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(),
                                                      false,
                                                      $"Remove {item.name} from {targetTypename}." ),
                     InspectorEditor.Skin.Button,
                     GUILayout.Width( 18 ),
                     GUILayout.Height( 14 ) ) )
                  itemToRemove = item;
            }
            if ( !displayItem ) {
              HandleItemEditorDisable( tool, item );
              continue;
            }
            using ( IndentScope.Single ) {
              var editor = tool.GetOrCreateEditor( item );
              preItemEditor?.Invoke( item, itemIndex );
              editor.OnInspectorGUI();
              postItemEditor?.Invoke( item, itemIndex );
            }
          }

          T itemToAdd = null;
          var addButtonPressed = false;
          using ( new GUILayout.VerticalScope( FadeNormalBackground( InspectorEditor.Skin.Label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add item", true ), InspectorEditor.Skin.Label );
            using ( new GUILayout.HorizontalScope() ) {
              itemToAdd = EditorGUILayout.ObjectField( "", null, typeof( T ), true ) as T;
              addButtonPressed = GUILayout.Button( GUI.MakeLabel( "+" ), InspectorEditor.Skin.Button, GUILayout.Width( 24 ), GUILayout.Height( 14 ) );
            }
          }

          if ( addButtonPressed ) {
            var sceneItems = isAsset ?
                               IO.Utils.FindAssetsOfType<T>() :
                               Object.FindObjectsOfType<T>();
            GenericMenu addItemMenu = new GenericMenu();
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
          HandleItemEditorDisable( tool, itemToRemove );
          itemToRemove = null;
        }
      }
      else {
        foreach ( var item in items )
          HandleItemEditorDisable( tool, item );
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
      var buttonsHeight       = 16.0f;

      bool positivePressed = false;
      bool negativePressed = false;

      var position = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( GUILayout.Height( buttonsHeight ) ) );

      var negativeRect = new Rect( position.xMax - positiveButtonWidth - negativeButtonWidth,
                                   position.y,
                                   negativeButtonWidth,
                                   buttonsHeight );
      using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
        negativePressed = UnityEngine.GUI.Button( negativeRect,
                                                  GUI.MakeLabel( negativeButtonName ),
                                                  InspectorEditor.Skin.ButtonLeft );

      var positiveRect = new Rect( position.xMax - positiveButtonWidth,
                                   position.y,
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
      GUIStyle fadedStyle = new GUIStyle( style );
      Texture2D background = EditorGUIUtility.isProSkin ?
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
      var skin           = InspectorEditor.Skin;
      bool guiWasEnabled = UnityEngine.GUI.enabled;
      var refFrame       = frames[ 0 ];

      using ( IndentScope.Create( indentLevelInc ) ) {
        UnityEngine.GUI.enabled = true;
        EditorGUI.showMixedValue = frames.Any( frame => !Equals( refFrame.Parent, frame.Parent ) );
        GameObject newParent = (GameObject)EditorGUILayout.ObjectField( GUI.MakeLabel( "Parent" ),
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
        Vector3 inputEuler = refFrame.LocalRotation.eulerAngles;
        EditorGUI.showMixedValue = frames.Any( frame => !Equals( refFrame.LocalRotation, frame.LocalRotation ) );
        Vector3 outputEuler = EditorGUILayout.Vector3Field( GUI.MakeLabel( "Local rotation" ), inputEuler );
        if ( !Equals( inputEuler, outputEuler ) ) {
          foreach ( var frame in frames )
            frame.LocalRotation = Quaternion.Euler( outputEuler );
          UnityEngine.GUI.changed = false;
        }
        EditorGUI.showMixedValue = false;

        Tools.FrameTool frameTool = frames.Length == 1 && includeFrameToolIfPresent ?
                                      Tools.FrameTool.FindActive( refFrame ) :
                                      null;
        if ( frameTool != null )
          using ( IndentScope.Single )
            frameTool.OnPreTargetMembersGUI();
      }
    }
  }
}
