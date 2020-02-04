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

      public const char ArrowRight              = '\u21D2';
      public const char ArrowLeftRight          = '\u2194';

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

      public const char CircleArrowAcw          = '\u21ba';
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

    public static bool Toggle( GUIContent content,
                               bool value )
    {
      return EditorGUILayout.Toggle( content, value );
    }

   public static ColorBlock NodeListButtonColor
    {
      get
      {
        return new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) );
      }
    }

    public struct ToolButtonData
    {
      public static ColorBlock ColorBlock
      {
        get
        {
          return new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.yellow, 0.1f ) );
        }
      }

      public static ToolButtonData Create( char symbol,
                                           bool isActive,
                                           string toolTip,
                                           Action onClick,
                                           bool enabled = true,
                                           Action postRender = null )
      {
        return Create( MakeLabel( symbol.ToString(),
                                  false,
                                  toolTip ),
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

      float buttonWidth  = 25.0f;
      float buttonHeight = 25.0f;
      float buttonOffset = 12.0f;

      using ( ToolButtonData.ColorBlock ) {
        var position   = EditorGUILayout.GetControlRect( false, buttonHeight );
        var toolsLabel = MakeLabel( "<b>Tools:</b>" );
        EditorGUI.LabelField( position,
                              toolsLabel,
                              InspectorEditor.Skin.LabelMiddleLeft );

        position.x += InspectorGUI.GetWidthIncludingIndent( toolsLabel,
                                                            InspectorEditor.Skin.LabelMiddleLeft ) +
                      buttonOffset;
        position.width = buttonWidth;

        for ( int i = 0; i < data.Length; ++i ) {
          using ( new EditorGUI.DisabledGroupScope( !data[ i ].Enabled ) ) {
            var buttonType = data.Length > 1 && i == 0               ? InspectorGUISkin.ButtonType.Left :
                             data.Length > 1 && i == data.Length - 1 ? InspectorGUISkin.ButtonType.Right :
                                                                       InspectorGUISkin.ButtonType.Middle;
            var pressed = UnityEngine.GUI.Button( position,
                                                  data[ i ].GUIContent,
                                                  InspectorEditor.Skin.GetButton( data[ i ].IsActive, buttonType ) );
            position.x += buttonWidth;

            data[ i ].PostRender?.Invoke();

            if ( pressed )
              data[ i ].OnClick?.Invoke();
          }
        }
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
        GameObject newParent = (GameObject)EditorGUILayout.ObjectField( MakeLabel( "Parent" ),
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
        var localPosition = EditorGUILayout.Vector3Field( MakeLabel( "Local position" ), refFrame.LocalPosition );
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
        Vector3 outputEuler = EditorGUILayout.Vector3Field( MakeLabel( "Local rotation" ), inputEuler );
        if ( !Equals( inputEuler, outputEuler ) ) {
          foreach ( var frame in frames )
            frame.LocalRotation = Quaternion.Euler( outputEuler );
          UnityEngine.GUI.changed = false;
        }
        EditorGUI.showMixedValue = false;

        Separator();

        Tools.FrameTool frameTool = frames.Length == 1 && includeFrameToolIfPresent ?
                                      Tools.FrameTool.FindActive( refFrame ) :
                                      null;
        if ( frameTool != null )
          using ( IndentScope.Create() )
            frameTool.OnPreTargetMembersGUI();
      }
    }

    public static bool Foldout( EditorDataEntry state, GUIContent content, Action<bool> onStateChanged = null )
    {
      var newState = EditorGUILayout.Foldout( state.Bool, content, true );
      if ( onStateChanged != null && newState != state.Bool )
        onStateChanged.Invoke( newState );
      return state.Bool = newState;
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
      where T : UnityEngine.Object
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

    public static void Separator( float height = 1.0f, float space = 1.0f )
    {
      //var rect = EditorGUILayout.GetControlRect( GUILayout.Height( space + height ) );
      //rect.height = height;
      //rect.y += space / 2.0f;
      //EditorGUI.DrawRect( rect,
      //                    EditorGUIUtility.isProSkin ?
      //                      Color.Lerp( Color.black, Color.white, 0.7f ) :
      //                      Color.Lerp( Color.white, Color.black, 0.7f ) );
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
      r2.x    += 1.0f;
      r2.xMax -= 1.0f;
      r2.y    -= 2.0f;
      EditorGUI.DrawRect( r2, Color.black );

      // Moving up 3 pixels since "shadow" rect is 1 pixel wide and moved
      // up 2 pixels. We basically want this to active like one control rect.
      GUILayout.Space( space - 3.0f );
    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick,
                                              Predicate<EnumT> filter = null,
                                              GUIStyle style = null,
                                              GUILayoutOption[] options = null )
    {
      return EnumButtonList( onClick, filter, e => { return style ?? Skin.button; }, options );
    }

    public static bool EnumButtonList<EnumT>( Action<EnumT> onClick,
                                              Predicate<EnumT> filter = null,
                                              Func<EnumT, GUIStyle> styleCallback = null,
                                              GUILayoutOption[] options = null )
    {
      if ( styleCallback == null )
        styleCallback = e => { return Skin.button; };

      foreach ( var eVal in Enum.GetValues( typeof( EnumT ) ) ) {
        bool filterPass = filter == null ||
                          filter( (EnumT)eVal );
        // Execute onClick if eVal passed the filter and the button is pressed.
        if ( filterPass && GUILayout.Button( MakeLabel( eVal.ToString().SplitCamelCase() ),
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

    public static GUIStyle FadeNormalBackground( GUIStyle style, float t )
    {
      GUIStyle fadedStyle = new GUIStyle( style );
      Texture2D background = EditorGUIUtility.isProSkin ?
                               CreateColoredTexture( 1, 1, Color.Lerp( ProBackgroundColor, Color.white, t ) ) :
                               CreateColoredTexture( 1, 1, Color.Lerp( IndieBackgroundColor, Color.black, t ) );
      fadedStyle.normal.background = background;
      return fadedStyle;
    }

    public enum CreateCancelState
    {
      Nothing,
      Create,
      Cancel
    }

    public static CreateCancelState CreateCancelButtons( bool validToPressCreate,
                                                         string tooltip = "",
                                                         string createName = "Create",
                                                         string cancelName = "Cancel" )
    {
      bool createPressed = false;
      bool cancelPressed = false;

      var cancelButtonWidth = 80.0f;
      var createButtonWidth = 80.0f;
      var buttonsMaxHeight  = 16.0f;

      var position = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( GUILayout.Height( buttonsMaxHeight ) ) );

      var cancelRect = new Rect( position.xMax - createButtonWidth - cancelButtonWidth,
                                 position.y,
                                 cancelButtonWidth,
                                 buttonsMaxHeight );
      using ( new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
        cancelPressed = UnityEngine.GUI.Button( cancelRect,
                                                MakeLabel( cancelName ),
                                                InspectorEditor.Skin.ButtonLeft );

      var createRect = new Rect( position.xMax - createButtonWidth,
                                 position.y,
                                 createButtonWidth,
                                 buttonsMaxHeight );
      using ( new EditorGUI.DisabledGroupScope( !validToPressCreate ) )
      using ( new ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
        createPressed = UnityEngine.GUI.Button( createRect,
                                                MakeLabel( createName,
                                                           true,
                                                           tooltip ),
                                                InspectorEditor.Skin.ButtonRight );

      return createPressed ? CreateCancelState.Create :
             cancelPressed ? CreateCancelState.Cancel :
                             CreateCancelState.Nothing;
    }

    public static Mesh ShapeMeshSourceGUI( Mesh currentSource )
    {
      var newSource = EditorGUILayout.ObjectField( MakeLabel( "Source:" ),
                                                   currentSource,
                                                   typeof( Mesh ),
                                                   false ) as Mesh;
      return newSource != currentSource ? newSource : null;
    }

    public static void WarningLabel( string warning )
    {
      var prevBgc = UnityEngine.GUI.backgroundColor;
      UnityEngine.GUI.backgroundColor = Color.Lerp( Color.white, Color.black, 0.55f );
      EditorGUILayout.LabelField( MakeLabel( warning,
                                  Color.Lerp( Color.red, Color.white, 0.25f ),
                                  true ),
                       InspectorEditor.Skin.TextAreaMiddleCenter );
      UnityEngine.GUI.backgroundColor = prevBgc;
    }
  }
}
