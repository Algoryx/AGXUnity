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
    public static object Vector4Drawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.Vector4Field( MakeLabel( wrapper.Member ).text, wrapper.Get<Vector4>() );
    }

    [InspectorDrawer( typeof( Vector3 ) )]
    public static object Vector3Drawer( InvokeWrapper wrapper, GUISkin skin )
    {
      var valInField = wrapper.Get<Vector3>();
      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( GUI.MakeLabel( wrapper.Member.Name ) );
        valInField = EditorGUILayout.Vector3Field( "", valInField );
      }
      GUILayout.EndHorizontal();

      return valInField;
    }

    [InspectorDrawer( typeof( Vector2 ) )]
    public static object Vector2Drawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.Vector2Field( MakeLabel( wrapper.Member ).text, wrapper.Get<Vector2>() );
    }

    [InspectorDrawer( typeof( int ) )]
    public static object IntDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.IntField( MakeLabel( wrapper.Member ).text, wrapper.Get<int>(), skin.textField );
    }

    [InspectorDrawer( typeof( bool ) )]
    public static object BoolDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return GUI.Toggle( MakeLabel( wrapper.Member ), wrapper.Get<bool>(), skin.button, skin.label );
    }

    [InspectorDrawer( typeof( Color ) )]
    public static object ColorDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.ColorField( MakeLabel( wrapper.Member ), wrapper.Get<Color>() );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueFloat ) )]
    public static object DefaultAndUserValueFloatDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      var obj   = wrapper.Get<DefaultAndUserValueFloat>();
      var value = GUI.HandleDefaultAndUserValue( wrapper.Member.Name,
                                                 obj,
                                                 skin );

      if ( wrapper.IsValid( value ) ) {
        if ( !obj.UseDefault )
          obj.Value = value;
        return obj;
      }

      return null;
    }

    [InspectorDrawer( typeof( DefaultAndUserValueVector3 ) )]
    public static object DefaultAndUserValueVector3Drawer( InvokeWrapper wrapper, GUISkin skin )
    {
      var obj   = wrapper.Get<DefaultAndUserValueVector3>();
      var value = GUI.HandleDefaultAndUserValue( wrapper.Member.Name,
                                                 obj,
                                                 skin );

      if ( wrapper.IsValid( value ) ) {
        if ( !obj.UseDefault )
          obj.Value = value;
        return obj;
      }

      return null;
    }

    [InspectorDrawer( typeof( RangeReal ) )]
    public static object RangeRealDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      var value = wrapper.Get<RangeReal>();

      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( MakeLabel( wrapper.Member ), skin.label );
        value.Min = EditorGUILayout.FloatField( "", (float)value.Min, skin.textField, GUILayout.MaxWidth( 64 ) );
        value.Max = EditorGUILayout.FloatField( "", (float)value.Max, skin.textField, GUILayout.MaxWidth( 64 ) );
      }
      GUILayout.EndHorizontal();

      if ( value.Min > value.Max )
        value.Min = value.Max;

      return value;
    }

    [InspectorDrawer( typeof( string ) )]
    public static object StringDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      return EditorGUILayout.TextField( MakeLabel( wrapper.Member ), wrapper.Get<string>(), skin.textField );
    }

    [InspectorDrawer( typeof( Enum ), BaseType = true )]
    public static object EnumDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      return EditorGUILayout.EnumPopup( MakeLabel( wrapper.Member ), wrapper.Get<Enum>(), skin.button );
    }

    [InspectorDrawer( typeof( float ) )]
    [InspectorDrawer( typeof( double ) )]
    public static object DecimalDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      float value = wrapper.GetContainingType() == typeof( double ) ?
                      Convert.ToSingle( wrapper.Get<double>() ) :
                      wrapper.Get<float>();
      FloatSliderInInspector slider = wrapper.GetAttribute<FloatSliderInInspector>();
      if ( slider != null )
        return EditorGUILayout.Slider( MakeLabel( wrapper.Member ), value, slider.Min, slider.Max );
      else
        return EditorGUILayout.FloatField( MakeLabel( wrapper.Member ), value, skin.textField );
    }

    [InspectorDrawer( typeof( ScriptAsset ), AssignableFrom = true )]
    [InspectorDrawer( typeof( ScriptComponent ), BaseType = true )]
    [InspectorDrawer( typeof( UnityEngine.Object ), BaseType = true )]
    [InspectorDrawerResult( IsNullable = true )]
    public static object ScriptDrawer( InvokeWrapper wrapper, GUISkin skin )
    {
      object result                 = null;
      var type                      = wrapper.GetContainingType();
      bool allowSceneObject         = type == typeof( GameObject ) ||
                                      type.BaseType == typeof( ScriptComponent );
      UnityEngine.Object valInField = wrapper.Get<UnityEngine.Object>();
      bool recursiveEditing         = wrapper.HasAttribute<AllowRecursiveEditing>();
      bool createNewAssetButton     = false;

      if ( recursiveEditing ) {
        var foldoutData = EditorData.Instance.GetData( wrapper.Object as UnityEngine.Object, wrapper.Member.Name );

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
        GUILayout.EndHorizontal();

        if ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) &&
             Event.current.type == EventType.MouseDown &&
             Event.current.button == 0 ) {
          foldoutData.Bool = !foldoutData.Bool;
          GUIUtility.ExitGUI();
        }

        if ( foldoutData.Bool ) {
          using ( new Utils.GUI.Indent( 12 ) ) {
            Utils.GUI.Separator();

            GUILayout.Space( 6 );

            GUILayout.Label( Utils.GUI.MakeLabel( "Changes made to this object will affect all objects referencing this asset.",
                                                  Color.Lerp( Color.red, Color.white, 0.25f ),
                                                  true ),
                             new GUIStyle( skin.textArea ) { alignment = TextAnchor.MiddleCenter } );

            GUILayout.Space( 6 );

            Editor editor = Editor.CreateEditor( result as UnityEngine.Object );
            if ( editor != null )
              editor.OnInspectorGUI();

            Utils.GUI.Separator();
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

    public static MethodInfo GetDrawerMethod( Type type )
    {
      bool ignored = false;
      return GetDrawerMethod( type, out ignored );
    }

    public static MethodInfo GetDrawerMethod( Type type, out bool isNullable )
    {
      MethodInfo drawer = null;
      if ( !m_drawerMethodsCache.TryGetValue( type, out drawer ) ) {
        foreach ( var drawerClass in m_drawerClasses ) {
          var methods = drawerClass.GetMethods( BindingFlags.Public | BindingFlags.Static );
          foreach ( var method in methods ) {
            if ( method.GetCustomAttributes<InspectorDrawerAttribute>().FirstOrDefault( attribute => attribute.Match( type ) ) != null ) {
              drawer = method;
              isNullable = MayResultNull( drawer );
              m_drawerMethodsCache.Add( type, drawer );
              break;
            }
          }

          if ( drawer != null )
            break;
        }
      }

      isNullable = MayResultNull( drawer );

      return drawer;
    }

    public static bool MayResultNull( MethodInfo drawer )
    {
      InspectorDrawerResultAttribute attribute = null;
      return drawer != null &&
             ( attribute = drawer.GetCustomAttribute<InspectorDrawerResultAttribute>() ) != null &&
             attribute.IsNullable;
    }

    private static Dictionary<Type, MethodInfo> m_drawerMethodsCache = new Dictionary<Type, MethodInfo>();
    private static List<Type> m_drawerClasses = new List<Type>() { typeof( InspectorGUI ) };
  }
}
