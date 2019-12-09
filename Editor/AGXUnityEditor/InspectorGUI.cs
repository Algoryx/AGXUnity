using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using GUI = AGXUnityEditor.Utils.GUI;
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
      GUIContent guiContent = new GUIContent();

      guiContent.text    = field.Name.SplitCamelCase();
      guiContent.tooltip = field.GetCustomAttribute<DescriptionAttribute>( false )?.Description;

      return guiContent;
    }

    [InspectorDrawer( typeof( Vector4 ) )]
    public static object Vector4Drawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.Vector4Field( MakeLabel( wrapper.Member ).text, wrapper.Get<Vector4>( obj ) );
    }

    [InspectorDrawer( typeof( Vector3 ) )]
    public static object Vector3Drawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      var valInField = wrapper.Get<Vector3>( obj );
      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( MakeLabel( wrapper.Member ) );
        valInField = EditorGUILayout.Vector3Field( "", valInField );
      }
      GUILayout.EndHorizontal();

      return valInField;
    }

    [InspectorDrawer( typeof( Vector2 ) )]
    public static object Vector2Drawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.Vector2Field( MakeLabel( wrapper.Member ).text, wrapper.Get<Vector2>( obj ) );
    }

    [InspectorDrawer( typeof( int ) )]
    public static object IntDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.IntField( MakeLabel( wrapper.Member ).text, wrapper.Get<int>( obj ), skin.textField );
    }

    [InspectorDrawer( typeof( bool ) )]
    public static object BoolDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return GUI.Toggle( MakeLabel( wrapper.Member ), wrapper.Get<bool>( obj ), skin.button, skin.label );
    }

    [InspectorDrawer( typeof( Color ) )]
    public static object ColorDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.ColorField( MakeLabel( wrapper.Member ), wrapper.Get<Color>( obj ) );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueFloat ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DefaultAndUserValueFloatDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      var dauvf = wrapper.Get<DefaultAndUserValueFloat>( obj );
      var value = GUI.HandleDefaultAndUserValue( wrapper.Member.Name,
                                                 dauvf,
                                                 skin );

      if ( wrapper.IsValid( value ) ) {
        if ( !dauvf.UseDefault )
          dauvf.Value = value;
        return obj;
      }

      return null;
    }

    public static void DefaultAndUserValueFloatDrawerCopyOp( object source, object destination )
    {
      var s = source as DefaultAndUserValueFloat;
      var d = destination as DefaultAndUserValueFloat;
      if ( s == null || d == null )
        return;

      d.CopyFrom( s );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueVector3 ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DefaultAndUserValueVector3Drawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      var dauvv = wrapper.Get<DefaultAndUserValueVector3>( obj );
      var value = GUI.HandleDefaultAndUserValue( wrapper.Member.Name,
                                                 dauvv,
                                                 skin );

      if ( wrapper.IsValid( value ) ) {
        if ( !dauvv.UseDefault )
          dauvv.Value = value;
        return obj;
      }

      return null;
    }

    public static void DefaultAndUserValueVector3DrawerCopyOp( object source, object destination )
    {
      var s = source as DefaultAndUserValueVector3;
      var d = destination as DefaultAndUserValueVector3;
      if ( s == null || d == null )
        return;

      d.CopyFrom( s );
    }

    private static GUIStyle m_rangeRealInvalidStyle = null;
    private struct RangeRealResult
    {
      public float Min;
      public bool MinChanged;
      public float Max;
      public bool MaxChanged;
    }

    [InspectorDrawer( typeof( RangeReal ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    [Obsolete( "Needs patch to not propagate unchanged values." )]
    public static object RangeRealDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      var value = wrapper.Get<RangeReal>( obj );
      GUIStyle labelStyle = skin.label;
      if ( value.Min > value.Max ) {
        if ( m_rangeRealInvalidStyle == null ) {
          m_rangeRealInvalidStyle = new GUIStyle( skin.label );
          m_rangeRealInvalidStyle.normal.background = GUI.CreateColoredTexture( 4, 4, Color.Lerp( UnityEngine.GUI.color, Color.red, 0.75f ) );
        }
        labelStyle = m_rangeRealInvalidStyle;
      }

      RangeRealResult result = new RangeRealResult()
      {
        Min = value.Min,
        MinChanged = false,
        Max = value.Max,
        MaxChanged = false
      };
      using ( new GUILayout.HorizontalScope( labelStyle ) ) {
        GUILayout.Label( MakeLabel( wrapper.Member ), skin.label );
        result.Min              = EditorGUILayout.FloatField( "", value.Min, skin.textField, GUILayout.MaxWidth( 64 ) );
        result.MinChanged       = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = false;
        result.Max              = EditorGUILayout.FloatField( "", value.Max, skin.textField, GUILayout.MaxWidth( 64 ) );
        result.MaxChanged       = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = result.MinChanged || result.MaxChanged;
      }

      if ( labelStyle == m_rangeRealInvalidStyle )
        GUI.WarningLabel( "Invalid range, Min > Max: (" + value.Min + " > " + value.Max + ")", skin );

      return result;
    }

    public static object RangeRealDrawerCopyOp( object data, object destination )
    {
      var result = (RangeRealResult)data;
      var value  = (RangeReal)destination;
      if ( result.MinChanged )
        value.Min = result.Min;
      if ( result.MaxChanged )
        value.Max = result.Max;

      return value;
    }

    private struct ExcavationSettingsResult
    {
      public DeformableTerrainShovelSettings.ExcavationSettings Value;
      public bool EnabledChanged;
      public bool CreateDynamicMassEnabledChanged;
      public bool ForceFeedbackEnabledChanged;
      public bool ContainsChanges { get { return EnabledChanged || CreateDynamicMassEnabledChanged || ForceFeedbackEnabledChanged; } }
    }

    [InspectorDrawer( typeof( DeformableTerrainShovelSettings.ExcavationSettings ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DeformableTerrainShovelExcavationSettingsDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      var data = new ExcavationSettingsResult()
      {
        Value = wrapper.Get<DeformableTerrainShovelSettings.ExcavationSettings>( obj )
      };
      if ( GUI.Foldout( EditorData.Instance.GetData( obj as Object, wrapper.Member.Name ),
                        MakeLabel( wrapper.Member ),
                        skin ) ) {
        using ( new GUI.Indent( 12 ) ) {
          data.Value.Enabled                   = GUI.Toggle( GUI.MakeLabel( "Enabled" ),
                                                             data.Value.Enabled,
                                                             skin.button,
                                                             skin.label );
          data.EnabledChanged                  = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.CreateDynamicMassEnabled  = GUI.Toggle( GUI.MakeLabel( "Create Dynamic Mass Enabled" ),
                                                             data.Value.CreateDynamicMassEnabled,
                                                             skin.button,
                                                             skin.label );
          data.CreateDynamicMassEnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.ForceFeedbackEnabled      = GUI.Toggle( GUI.MakeLabel( "Force Feedback Enabled" ),
                                                             data.Value.ForceFeedbackEnabled,
                                                             skin.button,
                                                             skin.label );
          data.ForceFeedbackEnabledChanged     = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
        }
        UnityEngine.GUI.changed = data.ContainsChanges;
      }
      return data;
    }

    public static object DeformableTerrainShovelExcavationSettingsDrawerCopyOp( object data, object destination )
    {
      var result = (ExcavationSettingsResult)data;
      var value  = (DeformableTerrainShovelSettings.ExcavationSettings)destination;
      if ( result.EnabledChanged )
        value.Enabled = result.Value.Enabled;
      if ( result.CreateDynamicMassEnabledChanged )
        value.CreateDynamicMassEnabled = result.Value.CreateDynamicMassEnabled;
      if ( result.ForceFeedbackEnabledChanged )
        value.ForceFeedbackEnabled = result.Value.ForceFeedbackEnabled;

      return value;
    }

    [InspectorDrawer( typeof( string ) )]
    public static object StringDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.TextField( MakeLabel( wrapper.Member ), wrapper.Get<string>( obj ), skin.textField );
    }

    [InspectorDrawer( typeof( Enum ), IsBaseType = true )]
    public static object EnumDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      return EditorGUILayout.EnumPopup( MakeLabel( wrapper.Member ), wrapper.Get<Enum>( obj ), skin.button );
    }

    [InspectorDrawer( typeof( float ) )]
    [InspectorDrawer( typeof( double ) )]
    public static object DecimalDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      float value = wrapper.GetContainingType() == typeof( double ) ?
                      Convert.ToSingle( wrapper.Get<double>( obj ) ) :
                      wrapper.Get<float>( obj );
      FloatSliderInInspector slider = wrapper.GetAttribute<FloatSliderInInspector>();
      if ( slider != null )
        return EditorGUILayout.Slider( MakeLabel( wrapper.Member ), value, slider.Min, slider.Max );
      else
        return EditorGUILayout.FloatField( MakeLabel( wrapper.Member ), value, skin.textField );
    }

    [InspectorDrawer( typeof( List<> ), IsGeneric = true )]
    public static object GenericListDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      System.Collections.IList list = wrapper.Get<System.Collections.IList>( obj );
      var target = obj as Object;

      if ( GUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
                        MakeLabel( wrapper.Member ),
                        skin ) ) {
        object insertElementBefore = null;
        object insertElementAfter = null;
        object eraseElement = null;
        var buttonLayout = new GUILayoutOption[] { GUILayout.Width( 26 ), GUILayout.Height( 18 ) };
        using ( new GUI.Indent( 12 ) ) {
          foreach ( var listObject in list ) {
            GUI.Separator();
            using ( new GUI.Indent( 12 ) ) {
              GUILayout.BeginHorizontal();
              {
                GUILayout.BeginVertical();
                {
                  // Using target to render listObject since it normally (CollisionGroupEntry) isn't an Object.
                  InspectorEditor.DrawMembersGUI( new Object[] { target }, ignored => listObject );
                }
                GUILayout.EndVertical();

                using ( GUI.NodeListButtonColor ) {
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementBefore.ToString(),
                                                        false,
                                                        "Insert new element before this" ),
                                         skin.button,
                                         buttonLayout ) )
                    insertElementBefore = listObject;
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementAfter.ToString(),
                                                        false,
                                                        "Insert new element after this" ),
                                         skin.button,
                                         buttonLayout ) )
                    insertElementAfter = listObject;
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(),
                                                        false,
                                                        "Erase this element" ),
                                         skin.button,
                                         buttonLayout ) )
                    eraseElement = listObject;
                }
              }
              GUILayout.EndHorizontal();
            }
          }

          if ( list.Count == 0 )
            GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.label );
          else
            GUI.Separator();
        }

        bool addElementToList = false;
        GUILayout.BeginHorizontal();
        {
          GUILayout.FlexibleSpace();
          using ( GUI.NodeListButtonColor )
            addElementToList = GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementAfter.ToString(),
                                                                false,
                                                                "Add new element to list" ),
                                                 skin.button,
                                                 buttonLayout );
        }
        GUILayout.EndHorizontal();

        object newObject = null;
        if ( addElementToList || insertElementBefore != null || insertElementAfter != null )
          newObject = Activator.CreateInstance( list.GetType().GetGenericArguments()[ 0 ], new object[] { } );

        if ( eraseElement != null )
          list.Remove( eraseElement );
        else if ( newObject != null ) {
          if ( addElementToList || ( list.Count > 0 && insertElementAfter != null && insertElementAfter == list[ list.Count - 1 ] ) )
            list.Add( newObject );
          else if ( insertElementAfter != null )
            list.Insert( list.IndexOf( insertElementAfter ) + 1, newObject );
          else if ( insertElementBefore != null )
            list.Insert( list.IndexOf( insertElementBefore ), newObject );
        }

        if ( eraseElement != null || newObject != null )
          EditorUtility.SetDirty( target );
      }

      // A bit of a hack until I figure out how to handle multi-selection
      // of lists, if that should be possible at all. We're handling the
      // list from inside this drawer and by returning null the return
      // value isn't propagated to any targets.
      return null;
    }

    [InspectorDrawer( typeof( ScriptAsset ), AssignableFrom = true )]
    [InspectorDrawer( typeof( ScriptComponent ), AssignableFrom = true )]
    [InspectorDrawer( typeof( Object ), IsBaseType = true )]
    [InspectorDrawerResult( IsNullable = true )]
    public static object ScriptDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      object result             = null;
      var type                  = wrapper.GetContainingType();
      bool allowSceneObject     = type == typeof( GameObject ) ||
                                  type.BaseType == typeof( ScriptComponent );
      Object valInField         = wrapper.Get<Object>( obj );
      bool recursiveEditing     = wrapper.HasAttribute<AllowRecursiveEditing>();
      bool createNewAssetButton = false;

      if ( recursiveEditing ) {
        var foldoutData = EditorData.Instance.GetData( obj as Object, wrapper.Member.Name );

        GUILayout.BeginHorizontal();
        {
          var objFieldLabel = MakeLabel( wrapper.Member );
          var buttonSize = skin.label.CalcHeight( objFieldLabel, Screen.width );
          UnityEngine.GUI.enabled = valInField != null;
          foldoutData.Bool = GUILayout.Button( GUI.MakeLabel( foldoutData.Bool ? "-" : "+" ),
                                               skin.button,
                                               new GUILayoutOption[] { GUILayout.Width( 20.0f ), GUILayout.Height( buttonSize ) } ) ?
                               // Button clicked - toggle current value.
                               !foldoutData.Bool :
                               // If foldout were enabled but valInField has changed to null - foldout will become disabled.
                               valInField != null && foldoutData.Bool;
          UnityEngine.GUI.enabled = true;
          if ( wrapper.CanWrite() ) {
            result = EditorGUILayout.ObjectField( objFieldLabel,
                                                  valInField,
                                                  type,
                                                  allowSceneObject,
                                                  new GUILayoutOption[] { } );
            if ( typeof( ScriptAsset ).IsAssignableFrom( type ) ) {
              GUILayout.Space( 4 );
              using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
                createNewAssetButton = GUILayout.Button( GUI.MakeLabel( "New", false, "Create new asset" ),
                                                         GUILayout.Width( 42 ),
                                                         GUILayout.Height( buttonSize ) );
            }
          }
          else {
            GUILayout.Label( objFieldLabel, skin.label );
            result = valInField;
          }
        }
        GUILayout.EndHorizontal();

        // Remove editor if object field is set to null or another object.
        if ( valInField != ( result as Object ) ) {
          ToolManager.ReleaseRecursiveEditor( valInField );
          foldoutData.Bool = false;
        }

        if ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) &&
             Event.current.type == EventType.MouseDown &&
             Event.current.button == 0 ) {
          Event.current.Use();
          foldoutData.Bool = !foldoutData.Bool;

          // Unfolding - remove editor.
          if ( !foldoutData.Bool )
            ToolManager.ReleaseRecursiveEditor( result as Object );

          GUIUtility.ExitGUI();
        }

        if ( foldoutData.Bool ) {
          using ( new GUI.Indent( 12 ) ) {
            GUI.Separator();

            GUILayout.Space( 6 );

            AGXUnity.Utils.GUI.WarningLabel( "Changes made to this object will affect all objects referencing this asset.",
                                             skin );

            GUILayout.Space( 6 );

            Editor editor = ToolManager.TryGetOrCreateRecursiveEditor( result as Object );
            if ( editor != null )
              editor.OnInspectorGUI();

            GUI.Separator();
          }
        }
      }
      else
        result = EditorGUILayout.ObjectField( MakeLabel( wrapper.Member ),
                                              valInField,
                                              type,
                                              allowSceneObject,
                                              new GUILayoutOption[] { } );

      if ( createNewAssetButton ) {
        var assetName = type.Name.SplitCamelCase().ToLower();
        var path = EditorUtility.SaveFilePanel( "Create new " + assetName, "Assets", "new " + assetName + ".asset", "asset" );
        if ( path != string.Empty ) {
          var info         = new System.IO.FileInfo( path );
          var relativePath = IO.Utils.MakeRelative( path, Application.dataPath );
          var newInstance  = ScriptAsset.Create( type );
          newInstance.name = info.Name;
          AssetDatabase.CreateAsset( newInstance, relativePath + ( info.Extension == ".asset" ? "" : ".asset" ) );
          AssetDatabase.SaveAssets();
          AssetDatabase.Refresh();

          result = newInstance;
        }
      }

      return result;
    }

    public static void ToolArrayGUI<T>( Tools.CustomTargetTool tool,
                                        T[] items,
                                        string identifier,
                                        Color itemColorIdeintifier,
                                        Action<T> onAdd,
                                        Action<T> onRemove )
      where T : Object
    {
      var displayItemsList = GUI.Foldout( GetTargetToolArrayGUIData( tool.Targets[ 0 ], identifier ),
                                          GUI.MakeLabel( identifier ),
                                          InspectorEditor.Skin );
      var itemTypename = typeof( T ).Name;
      var itemTypenameSplit = itemTypename.SplitCamelCase();
      var targetTypename = tool.Targets[ 0 ].GetType().Name;
      if ( displayItemsList ) {
        T itemToRemove = null;
        using ( new GUI.Indent( 12 ) ) {
          foreach ( var item in items ) {
            GUI.Separator();

            var displayItem = false;
            using ( new GUILayout.HorizontalScope() ) {
              displayItem = GUI.Foldout( GetItemToolArrayGUIData( tool.Targets[ 0 ], identifier, item ),
                                         GUI.MakeLabel( "[" + GUI.AddColorTag( itemTypename,
                                                                               itemColorIdeintifier ) + "] " + item.name ),
                                         InspectorEditor.Skin );

              using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
                if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(),
                                                      false,
                                                      $"Remove {item.name} from {targetTypename}." ),
                     InspectorEditor.Skin.button,
                     GUILayout.Width( 18 ),
                     GUILayout.Height( 14 ) ) )
                  itemToRemove = item;
            }
            if ( !displayItem ) {
              HandleItemEditorDisable( tool, item );
              continue;
            }
            using ( new GUI.Indent( 12 ) ) {
              var editor = tool.GetOrCreateEditor( item );
              editor.OnInspectorGUI();
            }
          }

          GUI.Separator( 3 );

          T itemToAdd = null;
          var addButtonPressed = false;
          using ( new GUILayout.VerticalScope( GUI.FadeNormalBackground( InspectorEditor.Skin.label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add item", true ), InspectorEditor.Skin.label );
            using ( new GUILayout.HorizontalScope() ) {
              itemToAdd = EditorGUILayout.ObjectField( "", null, typeof( T ), true ) as T;
              addButtonPressed = GUILayout.Button( GUI.MakeLabel( "+" ), InspectorEditor.Skin.button, GUILayout.Width( 24 ), GUILayout.Height( 14 ) );
            }
          }

          if ( addButtonPressed ) {
            var sceneItems = Object.FindObjectsOfType<T>();
            GenericMenu addItemMenu = new GenericMenu();
            addItemMenu.AddDisabledItem( GUI.MakeLabel( itemTypenameSplit + "(s) in scene" ) );
            addItemMenu.AddSeparator( string.Empty );
            foreach ( var sceneItem in sceneItems ) {
              if ( System.Array.IndexOf( items, sceneItem ) >= 0 )
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

          GUI.Separator( 3 );
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

    public static EditorDataEntry GetTargetToolArrayGUIData( Object target, string identifier )
    {
      return EditorData.Instance.GetData( target, identifier );
    }

    public static EditorDataEntry GetItemToolArrayGUIData( Object target, string identifier, Object item )
    {
      return EditorData.Instance.GetData( target, $"{identifier}_" + item.GetInstanceID().ToString() );
    }

    private static void HandleItemEditorDisable<T>( Tools.CustomTargetTool tool, T item )
      where T : Object
    {
      if ( tool.HasEditor( item ) ) {
        tool.RemoveEditor( item );
        SceneView.RepaintAll();
      }
    }

    public struct DrawerInfo
    {
      public MethodInfo Drawer;
      public MethodInfo CopyOp;
      public bool IsNullable;

      public bool IsValid { get { return Drawer != null; } }
    }

    public static DrawerInfo GetDrawerMethod( Type type )
    {
      DrawerInfo drawerInfo;
      if ( !m_drawerMethodsCache.TryGetValue( type, out drawerInfo ) ) {
        drawerInfo = new DrawerInfo() { Drawer = null, CopyOp = null, IsNullable = false };
        foreach ( var drawerClass in m_drawerClasses ) {
          var methods = drawerClass.GetMethods( BindingFlags.Public | BindingFlags.Static );
          foreach ( var method in methods ) {
            if ( method.GetCustomAttributes<InspectorDrawerAttribute>().FirstOrDefault( attribute => attribute.Match( type ) ) != null ) {
              drawerInfo.Drawer = method;
              FindDrawerResult( ref drawerInfo, drawerClass );
              m_drawerMethodsCache.Add( type, drawerInfo );
              break;
            }
          }

          if ( drawerInfo.Drawer != null )
            break;
        }
      }

      return drawerInfo;
    }

    public static void FindDrawerResult( ref DrawerInfo info, Type drawerClass )
    {
      if ( info.Drawer == null )
        return;

      var resultAttribute = info.Drawer.GetCustomAttribute<InspectorDrawerResultAttribute>();
      if ( resultAttribute == null )
        return;

      info.IsNullable = resultAttribute.IsNullable;
      info.CopyOp     = drawerClass.GetMethod( info.Drawer.Name + "CopyOp", BindingFlags.Public | BindingFlags.Static );
    }

    private static Dictionary<Type, DrawerInfo> m_drawerMethodsCache = new Dictionary<Type, DrawerInfo>();
    private static List<Type> m_drawerClasses = new List<Type>() { typeof( InspectorGUI ) };
  }
}
