using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

namespace AGXUnityEditor.Utils
{
  public partial class GUI : AGXUnity.Utils.GUI
  {
    public static class Symbols
    {
      public const char ToggleEnabled           = '\u2714';
      public const char ToggleDisabled          = ' ';

      public const char ShapeResizeTool         = '\u21C4';
      public const char ShapeCreateTool         = '\u210C';
      public const char ShapeVisualCreateTool   = '\u274D';

      public const char SelectInSceneViewTool   = 'p';
      public const char SelectPointTool         = '\u22A1';
      public const char SelectEdgeTool          = '\u2196';
      public const char PositionHandleTool      = 'L';

      public const char ConstraintCreateTool    = '\u2102';

      public const char DisableCollisionsTool   = '\u2229';

      public const char ListInsertElementBefore = '\u21B0';
      public const char ListInsertElementAfter  = '\u21B2';
      public const char ListEraseElement        = 'x';

      public const char Synchronized            = '\u2194';
    }

    public static Vector3 Vector3Field( GUIContent content, Vector3 value, GUIStyle style = null )
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label( content, style ?? Skin.label );
      value = EditorGUILayout.Vector3Field( "", value );
      GUILayout.EndHorizontal();

      return value;
    }

    public static GUILayoutOption[] DefaultToggleButtonOptions { get { return new GUILayoutOption[] { GUILayout.Width( 20 ), GUILayout.Height( 14 ) }; } }
    public static GUILayoutOption[] DefaultToggleLabelOptions { get { return new GUILayoutOption[] { }; } }

    public static bool Toggle( GUIContent content, bool value, GUIStyle buttonStyle, GUIStyle labelStyle, GUILayoutOption[] buttonOptions = null, GUILayoutOption[] labelOptions = null )
    {
      if ( buttonOptions == null )
        buttonOptions = DefaultToggleButtonOptions;
      if ( labelOptions == null )
        labelOptions = DefaultToggleLabelOptions;

      bool buttonDown = false;
      GUILayout.BeginHorizontal();
      {
        string buttonText = value ? Symbols.ToggleEnabled.ToString() : Symbols.ToggleDisabled.ToString();
        buttonDown = GUILayout.Button( MakeLabel( buttonText, false, content.tooltip ), ConditionalCreateSelectedStyle( value, buttonStyle ), buttonOptions );
        GUILayout.Label( content, labelStyle, labelOptions );
      }
      GUILayout.EndHorizontal();
      
      return buttonDown ? !value : value;
    }

    public static ValueT HandleDefaultAndUserValue<ValueT>( string name, DefaultAndUserValue<ValueT> valInField, GUISkin skin ) where ValueT : struct
    {
      bool guiWasEnabled       = UnityEngine.GUI.enabled;
      ValueT newValue          = default( ValueT );
      MethodInfo floatMethod   = typeof( EditorGUILayout ).GetMethod( "FloatField", new[] { typeof( string ), typeof( float ), typeof( GUILayoutOption[] ) } );
      MethodInfo vector3Method = typeof( EditorGUILayout ).GetMethod( "Vector3Field", new[] { typeof( string ), typeof( Vector3 ), typeof( GUILayoutOption[] ) } );
      MethodInfo method        = typeof( ValueT ) == typeof( float ) ?
                                   floatMethod :
                                 typeof( ValueT ) == typeof( Vector3 ) ?
                                   vector3Method :
                                   null;
      if ( method == null )
        throw new NullReferenceException( "Unknown DefaultAndUserValue type: " + typeof( ValueT ).Name );

      bool useDefaultToggled = false;
      bool updateDefaultValue = false;
      GUILayout.BeginHorizontal();
      {
        // Note that we're checking if the value has changed!
        useDefaultToggled = Toggle( MakeLabel( name.SplitCamelCase(), false, "If checked - value will be default. Uncheck to manually enter value." ),
                                    valInField.UseDefault,
                                    skin.button,
                                    Align( skin.label, TextAnchor.MiddleLeft ),
                                    new GUILayoutOption[] { GUILayout.Width( 22 ) },
                                    new GUILayoutOption[] { GUILayout.MaxWidth( 120 ) } ) != valInField.UseDefault;
        UnityEngine.GUI.enabled = !valInField.UseDefault;
        GUILayout.FlexibleSpace();
        newValue = (ValueT)method.Invoke( null, new object[] { "", valInField.Value, new GUILayoutOption[] { } } );
        UnityEngine.GUI.enabled = valInField.UseDefault;
        updateDefaultValue = GUILayout.Button( MakeLabel( "Update", false, "Update default value" ), skin.button, GUILayout.Width( 52 ) );
        UnityEngine.GUI.enabled = guiWasEnabled;
      }
      GUILayout.EndHorizontal();

      if ( useDefaultToggled ) {
        valInField.UseDefault = !valInField.UseDefault;
        updateDefaultValue    = valInField.UseDefault;

        // We don't want the default value to be written to
        // the user specified.
        if ( !valInField.UseDefault )
          newValue = valInField.UserValue;
      }

      if ( updateDefaultValue )
        valInField.OnForcedUpdate();

      return newValue;
    }

    public class ToolButtonData
    {
      public static GUILayoutOption Width { get { return GUILayout.Width( 25f ); } }
      public static GUILayoutOption Height { get { return GUILayout.Height( 25f ); } }
      public static GUIStyle Style( GUISkin skin, int fontSize = 18 )
      {
        GUIStyle style = new GUIStyle( skin.button );
        style.fontSize = fontSize;
        return style;
      }
      public static ColorBlock ColorBlock { get { return new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.yellow, 0.1f ) ); } }
    }

    public static ColorBlock NodeListButtonColor { get { return new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ); } }

    public static void ToolsLabel( GUISkin skin )
    {
      GUILayout.Label( GUI.MakeLabel( "Tools:", true ), Align( skin.label, TextAnchor.MiddleLeft ), new GUILayoutOption[] { GUILayout.Width( 64 ), GUILayout.Height( 25 ) } );
    }

    public static bool ToolButton( char symbol, bool active, string toolTip, GUISkin skin, int fontSize = 18 )
    {
      return GUILayout.Button( MakeLabel( symbol.ToString(), false, toolTip ),
                               ConditionalCreateSelectedStyle( active, ToolButtonData.Style( skin, fontSize ) ),
                               ToolButtonData.Width,
                               ToolButtonData.Height );
    }

    public static void HandleFrame( IFrame frame, InspectorEditor editor, float numPixelsIndentation = 0.0f, bool includeFrameToolIfPresent = true )
    {
      var skin           = InspectorEditor.Skin;
      bool guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new Indent( numPixelsIndentation ) ) {
        UnityEngine.GUI.enabled = true;
        GameObject newParent = (GameObject)EditorGUILayout.ObjectField( MakeLabel( "Parent" ), frame.Parent, typeof( GameObject ), true );
        UnityEngine.GUI.enabled = guiWasEnabled;

        if ( newParent != frame.Parent )
          frame.SetParent( newParent );

        frame.LocalPosition = Vector3Field( MakeLabel( "Local position" ), frame.LocalPosition, skin.label );

        // Converting from quaternions to Euler - make sure the actual Euler values has
        // changed before updating local rotation to not mess up the undo stack.
        Vector3 inputEuler  = frame.LocalRotation.eulerAngles;
        Vector3 outputEuler = Vector3Field( MakeLabel( "Local rotation" ), inputEuler, skin.label );
        if ( !ValueType.Equals( inputEuler, outputEuler ) )
          frame.LocalRotation = Quaternion.Euler( outputEuler );

        Separator();

        Tools.FrameTool frameTool = null;
        if ( includeFrameToolIfPresent && ( frameTool = Tools.FrameTool.FindActive( frame ) ) != null )
          using ( new Indent( 12 ) )
            frameTool.OnPreTargetMembersGUI( editor );
      }
    }

    [Obsolete( "BaseEditor is obsolete - use InspectorEditor version." )]
    public static void HandleFrame( IFrame frame, GUISkin skin, float numPixelsIndentation = 0.0f, bool includeFrameToolIfPresent = true )
    {
      bool guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new Indent( numPixelsIndentation ) ) {
        UnityEngine.GUI.enabled = true;
        GameObject newParent = (GameObject)EditorGUILayout.ObjectField( MakeLabel( "Parent" ), frame.Parent, typeof( GameObject ), true );
        UnityEngine.GUI.enabled = guiWasEnabled;

        if ( newParent != frame.Parent )
          frame.SetParent( newParent );

        frame.LocalPosition = Vector3Field( MakeLabel( "Local position" ), frame.LocalPosition, skin.label );

        // Converting from quaternions to Euler - make sure the actual Euler values has
        // changed before updating local rotation to not mess up the undo stack.
        Vector3 inputEuler  = frame.LocalRotation.eulerAngles;
        Vector3 outputEuler = Vector3Field( MakeLabel( "Local rotation" ), inputEuler, skin.label );
        if ( !ValueType.Equals( inputEuler, outputEuler ) )
          frame.LocalRotation = Quaternion.Euler( outputEuler );

        Separator();

        Tools.FrameTool frameTool = null;
        if ( includeFrameToolIfPresent && ( frameTool = Tools.FrameTool.FindActive( frame ) ) != null )
          using ( new Indent( 12 ) )
            frameTool.OnPreTargetMembersGUI( skin );
      }
    }

    public static bool Foldout( EditorDataEntry state, GUIContent label, GUISkin skin, Action<bool> onStateChanged = null )
    {
      return FoldoutEx( state, skin.button, label, skin.label, onStateChanged );
    }

    public static bool FoldoutEx( EditorDataEntry state, GUIStyle buttonStyle, GUIContent label, GUIStyle labelStyle, Action<bool> onStateChanged = null )
    {
      GUILayout.BeginHorizontal();
      {
        var buttonSize = labelStyle.CalcHeight( label, Screen.width );
        bool expandPressed = GUILayout.Button( MakeLabel( state.Bool ? "-" : "+" ), buttonStyle, GUILayout.Width( 20.0f ), GUILayout.Height( buttonSize ) );
        GUILayout.Label( label, labelStyle, GUILayout.ExpandWidth( true ) );
        if ( expandPressed ||
             ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) &&
               Event.current.type == EventType.MouseDown &&
               Event.current.button == 0 ) ) {
          state.Bool = !state.Bool;

          if ( onStateChanged != null )
            onStateChanged.Invoke( state.Bool );

          if ( !expandPressed )
            GUIUtility.ExitGUI();
        }
      }
      GUILayout.EndHorizontal();

      return state.Bool;
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
    public static void HandleDragDrop<T>( Rect dropArea, Event current, Action<T> onDrop ) where T : UnityEngine.Object
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

    public static void Separator( float height = 1.0f, float space = 2.0f )
    {
      Texture2D lineTexture = EditorGUIUtility.isProSkin ?
                                Texture2D.whiteTexture :
                                Texture2D.blackTexture;

      GUILayout.BeginVertical();
      {
        GUILayout.Space( space );
        EditorGUI.DrawPreviewTexture( EditorGUILayout.GetControlRect( new GUILayoutOption[] { GUILayout.ExpandWidth( true ), GUILayout.Height( height ) } ), lineTexture );
      }
      GUILayout.EndVertical();
    }

    public static void Separator3D( float space = 2.0f )
    {
      GUILayout.Space( space );
      EditorGUI.DrawPreviewTexture( EditorGUILayout.GetControlRect( new GUILayoutOption[] { GUILayout.ExpandWidth( true ), GUILayout.Height( 1f ) } ), Texture2D.whiteTexture );
      EditorGUI.DrawPreviewTexture( EditorGUILayout.GetControlRect( new GUILayoutOption[] { GUILayout.ExpandWidth( true ), GUILayout.Height( 1f ) } ), Texture2D.blackTexture );
      GUILayout.Space( space );
    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick, Predicate<EnumT> filter = null, GUIStyle style = null, GUILayoutOption[] options = null )
    {
      return EnumButtonList( onClick, filter, e => { return style ?? Skin.button; }, options );
    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick, Predicate<EnumT> filter = null, Func<EnumT, GUIStyle> styleCallback = null, GUILayoutOption[] options = null )
    {
      if ( styleCallback == null )
        styleCallback = e => { return Skin.button; };

      foreach ( var eVal in Enum.GetValues( typeof( EnumT ) ) ) {
        bool filterPass = filter == null ||
                          filter( (EnumT)eVal );
        // Execute onClick if eVal passed the filter and the button is pressed.
        if ( filterPass && GUILayout.Button( MakeLabel( eVal.ToString().SplitCamelCase() ), styleCallback( (EnumT)eVal ), options ) ) {
          onClick( (EnumT)eVal );
          return true;
        }
      }
        
      return false;
    }

    public static Color ProBackgroundColor = new Color32( 56, 56, 56, 255 );
    public static Color IndieBackgroundColor = new Color32( 194, 194, 194, 255 );

    public static GUIStyle FadeNormalBackground( GUIStyle style, float t )
    {
      GUIStyle fadedStyle = new GUIStyle( style );
      Texture2D background = EditorGUIUtility.isProSkin ?
                               CreateColoredTexture( 1, 1, Color.Lerp( ProBackgroundColor, Color.white, t ) ) :
                               CreateColoredTexture( 1, 1, Color.Lerp( IndieBackgroundColor, Color.black, t ) );
      fadedStyle.normal.background = background;
      return fadedStyle;
    }

    public static GUIStyle ConditionalCreateSelectedStyle( bool selected, GUIStyle orgStyle )
    {
      return selected ? CreateSelectedStyle( orgStyle ) : orgStyle;
    }

    public static void MaterialEditor( GUIContent objFieldLabel,
                                       float objFieldLabelWidth,
                                       Material material,
                                       GUISkin skin,
                                       Action<Material> onMaterialChanged,
                                       bool forceEnableEditing = false )
    {
      Material newMaterial = null;
      bool createNewMaterialButton = false;
      GUILayout.BeginHorizontal();
      {
        var buttonSize = skin.label.CalcHeight( objFieldLabel, Screen.width );
        GUILayout.Label( objFieldLabel, skin.label, GUILayout.Width( objFieldLabelWidth ) );
        newMaterial = EditorGUILayout.ObjectField( material, typeof( Material ), false ) as Material;
        GUILayout.Space( 4 );
        using ( new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
          createNewMaterialButton = GUILayout.Button( MakeLabel( "New", false, "Create new material" ),
                                                      GUILayout.Width( 42 ),
                                                      GUILayout.Height( buttonSize ) );
      }
      GUILayout.EndHorizontal();

      bool isBuiltInMaterial = material == null ||
                               !AssetDatabase.GetAssetPath( material ).StartsWith( "Assets" ) ||
                               material == Manager.GetOrCreateShapeVisualDefaultMaterial();

      var materialEditor = Editor.CreateEditor( material, typeof( MaterialEditor ) ) as MaterialEditor;
      using ( new EditorGUI.DisabledGroupScope( !forceEnableEditing && isBuiltInMaterial ) ) {
        if ( materialEditor != null ) {
          materialEditor.DrawHeader();
          materialEditor.OnInspectorGUI();
        }
      }

      if ( createNewMaterialButton ) {
        string result = EditorUtility.SaveFilePanel( "Create new material", "Assets", "new material.mat", "mat" );
        if ( result != string.Empty ) {
          System.IO.FileInfo info = new System.IO.FileInfo( result );
          var relativePath = IO.Utils.MakeRelative( result, Application.dataPath );

          newMaterial = new Material( material ?? Manager.GetOrCreateShapeVisualDefaultMaterial() );
          newMaterial.name = info.Name;
          AssetDatabase.CreateAsset( newMaterial, relativePath + ( info.Extension == ".mat" ? "" : ".mat" ) );
          AssetDatabase.SaveAssets();
          AssetDatabase.Refresh();
        }
      }

      Editor.DestroyImmediate( materialEditor );

      if ( newMaterial != null && newMaterial != material && onMaterialChanged != null )
        onMaterialChanged.Invoke( newMaterial );
    }

    public enum CreateCancelState
    {
      Nothing,
      Create,
      Cancel
    }

    public static CreateCancelState CreateCancelButtons( bool validToPressCreate, GUISkin skin, string tooltip = "" )
    {
      bool createPressed = false;
      bool cancelPressed = false;
      GUILayout.BeginHorizontal();
      {
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        {
          GUILayout.Space( 13 );
          using ( new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
            cancelPressed = GUILayout.Button( MakeLabel( "Cancel", false ), skin.button, GUILayout.Width( 96 ), GUILayout.Height( 16 ) );
          GUILayout.EndVertical();
        }

        using ( new EditorGUI.DisabledGroupScope( !validToPressCreate ) )
        using ( new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
          createPressed = GUILayout.Button( MakeLabel( "Create", true, tooltip ), skin.button, GUILayout.Width( 120 ), GUILayout.Height( 26 ) );
        UnityEngine.GUI.enabled = true;
      }
      GUILayout.EndHorizontal();

      return createPressed ? CreateCancelState.Create :
             cancelPressed ? CreateCancelState.Cancel :
                             CreateCancelState.Nothing;
    }

    public static Mesh ShapeMeshSourceGUI( Mesh currentSource, GUISkin skin )
    {
      Mesh newSource = null;
      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( MakeLabel( "Source:" ), skin.label, GUILayout.Width( 76 ) );
        newSource = EditorGUILayout.ObjectField( currentSource, typeof( Mesh ), false ) as Mesh;
      }
      GUILayout.EndHorizontal();

      return newSource != currentSource ? newSource : null;
    }
  }
}
