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
        GUILayout.Label( GUI.MakeLabel( wrapper.Member.Name ) );
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

    [InspectorDrawer( typeof( ScriptAsset ), AssignableFrom = true )]
    [InspectorDrawer( typeof( ScriptComponent ), IsBaseType = true )]
    [InspectorDrawer( typeof( UnityEngine.Object ), IsBaseType = true )]
    [InspectorDrawerResult( IsNullable = true )]
    public static object ScriptDrawer( object obj, InvokeWrapper wrapper, GUISkin skin )
    {
      object result                 = null;
      var type                      = wrapper.GetContainingType();
      bool allowSceneObject         = type == typeof( GameObject ) ||
                                      type.BaseType == typeof( ScriptComponent );
      UnityEngine.Object valInField = wrapper.Get<UnityEngine.Object>( obj );
      bool recursiveEditing         = wrapper.HasAttribute<AllowRecursiveEditing>();
      bool createNewAssetButton     = false;

      if ( recursiveEditing ) {
        var foldoutData = EditorData.Instance.GetData( obj as UnityEngine.Object, wrapper.Member.Name );

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
          Event.current.Use();
          foldoutData.Bool = !foldoutData.Bool;
          GUIUtility.ExitGUI();
        }

        if ( foldoutData.Bool ) {
          using ( new GUI.Indent( 12 ) ) {
            GUI.Separator();

            GUILayout.Space( 6 );

            AGXUnity.Utils.GUI.WarningLabel( "Changes made to this object will affect all objects referencing this asset.",
                                             skin );

            GUILayout.Space( 6 );

            Editor editor = Editor.CreateEditor( result as UnityEngine.Object );
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
