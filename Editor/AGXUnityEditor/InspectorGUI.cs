using System;
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
      return GetWidth( content, style ) + GUI.IndentScope.PixelLevel;
    }

    public class VerticalIndentLine : IDisposable
    {
      public VerticalIndentLine()
      {
        m_begin = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 1.0f ) );
      }

      public void Dispose()
      {
        var end = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 1.0f ) );
        var oldColor = Handles.color;
        Handles.color = InspectorGUISkin.BrandColor;
        Handles.DrawLine( new Vector3( 3, m_begin.position.y, 0 ), new Vector3( 3, end.position.y, 0 ) );
        Handles.DrawLine( new Vector3( 4, m_begin.position.y, 0 ), new Vector3( 4, end.position.y, 0 ) );
        Handles.color = oldColor;
      }

      private Rect m_begin;
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

        position.x    += EditorGUIUtility.labelWidth - GUI.IndentScope.PixelLevel;
        position.xMax -= EditorGUIUtility.labelWidth +
                         Convert.ToInt32( supportsCreateAsset ) * createNewButtonWidth -
                         GUI.IndentScope.PixelLevel;
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

      using ( GUI.IndentScope.Create() ) {
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
      using ( GUI.IndentScope.NoIndent ) {
        GUI.Separator3D();
        editor.DrawHeader();
        editor.OnInspectorGUI();
        GUI.Separator3D();
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
      var displayItemsList = GUI.Foldout( GetTargetToolArrayGUIData( tool.Targets[ 0 ], identifier ),
                                          GUI.MakeLabel( identifier + $" [{items.Length}]" ) );
      var itemTypename      = typeof( T ).Name;
      var isAsset           = typeof( ScriptableObject ).IsAssignableFrom( typeof( T ) );
      var itemTypenameSplit = itemTypename.SplitCamelCase();
      var targetTypename    = tool.Targets[ 0 ].GetType().Name;
      if ( displayItemsList ) {
        T itemToRemove = null;
        using ( GUI.IndentScope.Create() ) {
          for ( int itemIndex = 0; itemIndex < items.Length; ++itemIndex ) {
            var item = items[ itemIndex ];

            var displayItem = false;
            using ( new GUILayout.HorizontalScope() ) {
              displayItem = GUI.Foldout( GetItemToolArrayGUIData( tool.Targets[ 0 ], identifier, item ),
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
            using ( GUI.IndentScope.Create() ) {
              var editor = tool.GetOrCreateEditor( item );
              preItemEditor?.Invoke( item, itemIndex );
              editor.OnInspectorGUI();
              postItemEditor?.Invoke( item, itemIndex );
            }
          }

          T itemToAdd = null;
          var addButtonPressed = false;
          using ( new GUILayout.VerticalScope( GUI.FadeNormalBackground( InspectorEditor.Skin.Label, 0.1f ) ) ) {
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
  }
}
