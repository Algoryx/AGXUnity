using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Model;
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
  public static class InvokeWrapperInspectorDrawer
  {
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
    private static List<Type> m_drawerClasses = new List<Type>() { typeof( InvokeWrapperInspectorDrawer ) };

    [InspectorDrawer( typeof( Vector4 ) )]
    public static object Vector4Drawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector4Field( InspectorGUI.MakeLabel( wrapper.Member ).text,
                                           wrapper.Get<Vector4>( obj ) );
    }

    [InspectorDrawer( typeof( Vector3 ) )]
    public static object Vector3Drawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector3Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector3>( obj ) );
    }

    [InspectorDrawer( typeof( Vector2 ) )]
    public static object Vector2Drawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector2Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector2>( obj ) );
    }

    [InspectorDrawer( typeof( int ) )]
    public static object IntDrawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.IntField( InspectorGUI.MakeLabel( wrapper.Member ).text,
                                       wrapper.Get<int>( obj ) );
    }

    [InspectorDrawer( typeof( bool ) )]
    public static object BoolDrawer( object obj, InvokeWrapper wrapper )
    {
      return GUI.Toggle( InspectorGUI.MakeLabel( wrapper.Member ),
                         wrapper.Get<bool>( obj ) );
    }

    [InspectorDrawer( typeof( Color ) )]
    public static object ColorDrawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.ColorField( InspectorGUI.MakeLabel( wrapper.Member ),
                                         wrapper.Get<Color>( obj ) );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueFloat ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DefaultAndUserValueFloatDrawer( object obj, InvokeWrapper wrapper )
    {
      var dauvf = wrapper.Get<DefaultAndUserValueFloat>( obj );
      var value = HandleDefaultAndUserValue( wrapper.Member.Name,
                                             dauvf );

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
    public static object DefaultAndUserValueVector3Drawer( object obj, InvokeWrapper wrapper )
    {
      var dauvv = wrapper.Get<DefaultAndUserValueVector3>( obj );
      var value = HandleDefaultAndUserValue( wrapper.Member.Name,
                                             dauvv );

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

    private static ValueT HandleDefaultAndUserValue<ValueT>( string name,
                                                             DefaultAndUserValue<ValueT> valInField )
      where ValueT : struct
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
        useDefaultToggled = GUI.Toggle( GUI.MakeLabel( name.SplitCamelCase(),
                                                       false,
                                                       "If checked - value will be default. Uncheck to manually enter value." ),
                                        valInField.UseDefault ) != valInField.UseDefault;
        UnityEngine.GUI.enabled = !valInField.UseDefault;
        GUILayout.FlexibleSpace();
        newValue = (ValueT)method.Invoke( null, new object[] { "", valInField.Value, new GUILayoutOption[] { } } );
        UnityEngine.GUI.enabled = valInField.UseDefault;
        updateDefaultValue = GUILayout.Button( GUI.MakeLabel( "Update",
                                                              false,
                                                              "Update default value" ),
                                               InspectorEditor.Skin.Button,
                                               GUILayout.Width( 52 ) );
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
    public static object RangeRealDrawer( object obj, InvokeWrapper wrapper )
    {
      var value = wrapper.Get<RangeReal>( obj );
      var invalidRange = value.Min > value.Max;

      RangeRealResult result = new RangeRealResult()
      {
        Min = value.Min,
        MinChanged = false,
        Max = value.Max,
        MaxChanged = false
      };
      using ( new GUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( InspectorGUI.MakeLabel( wrapper.Member ) );

        // TODO GUI: Patch indent method
        GUILayout.Space( -30 );

        result.Min              = EditorGUILayout.FloatField( "", value.Min, EditorStyles.numberField, GUILayout.MaxWidth( 140 ) );
        result.MinChanged       = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = false;

        // TODO GUI: Patch indent method
        GUILayout.Space( -34 );

        result.Max              = EditorGUILayout.FloatField( "", value.Max, EditorStyles.numberField, GUILayout.MaxWidth( 140 ) );
        result.MaxChanged       = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = result.MinChanged || result.MaxChanged;
      }

      if ( invalidRange )
        GUI.WarningLabel( "Invalid range, Min > Max: (" + value.Min + " > " + value.Max + ")" );

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
    public static object DeformableTerrainShovelExcavationSettingsDrawer( object obj, InvokeWrapper wrapper )
    {
      var data = new ExcavationSettingsResult()
      {
        Value = wrapper.Get<DeformableTerrainShovelSettings.ExcavationSettings>( obj )
      };
      if ( GUI.Foldout( EditorData.Instance.GetData( obj as Object, wrapper.Member.Name ),
                        InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        using ( GUI.IndentScope.Create() ) {
          data.Value.Enabled                   = GUI.Toggle( GUI.MakeLabel( "Enabled" ),
                                                             data.Value.Enabled );
          data.EnabledChanged                  = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.CreateDynamicMassEnabled  = GUI.Toggle( GUI.MakeLabel( "Create Dynamic Mass Enabled" ),
                                                             data.Value.CreateDynamicMassEnabled );
          data.CreateDynamicMassEnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.ForceFeedbackEnabled      = GUI.Toggle( GUI.MakeLabel( "Force Feedback Enabled" ),
                                                             data.Value.ForceFeedbackEnabled );
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
    public static object StringDrawer( object obj, InvokeWrapper wrapper )
    {
      return EditorGUILayout.TextField( InspectorGUI.MakeLabel( wrapper.Member ),
                                        wrapper.Get<string>( obj ),
                                        InspectorEditor.Skin.TextField );
    }

    [InspectorDrawer( typeof( Enum ), IsBaseType = true )]
    public static object EnumDrawer( object obj, InvokeWrapper wrapper )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      if ( wrapper.GetContainingType().GetCustomAttribute<FlagsAttribute>() != null )
        return EditorGUILayout.EnumFlagsField( InspectorGUI.MakeLabel( wrapper.Member ),
                                               wrapper.Get<Enum>( obj ),
                                               InspectorEditor.Skin.Popup );
      else
        return EditorGUILayout.EnumPopup( InspectorGUI.MakeLabel( wrapper.Member ),
                                          wrapper.Get<Enum>( obj ),
                                          InspectorEditor.Skin.Popup );
    }

    [InspectorDrawer( typeof( float ) )]
    [InspectorDrawer( typeof( double ) )]
    public static object DecimalDrawer( object obj, InvokeWrapper wrapper )
    {
      float value = wrapper.GetContainingType() == typeof( double ) ?
                      Convert.ToSingle( wrapper.Get<double>( obj ) ) :
                      wrapper.Get<float>( obj );
      FloatSliderInInspector slider = wrapper.GetAttribute<FloatSliderInInspector>();
      if ( slider != null )
        return EditorGUILayout.Slider( InspectorGUI.MakeLabel( wrapper.Member ),
                                       value,
                                       slider.Min,
                                       slider.Max );
      else
        return EditorGUILayout.FloatField( InspectorGUI.MakeLabel( wrapper.Member ),
                                           value );
    }

    [InspectorDrawer( typeof( List<> ), IsGeneric = true )]
    public static object GenericListDrawer( object obj, InvokeWrapper wrapper )
    {
      System.Collections.IList list = wrapper.Get<System.Collections.IList>( obj );
      var target = obj as Object;

      if ( GUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
                        InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        object insertElementBefore = null;
        object insertElementAfter  = null;
        object eraseElement        = null;
        var skin                   = InspectorEditor.Skin;
        var buttonLayout = new GUILayoutOption[] { GUILayout.Width( 26 ), GUILayout.Height( 18 ) };
        using ( GUI.IndentScope.Create() ) {
          foreach ( var listObject in list ) {
            GUI.Separator();
            using ( GUI.IndentScope.Create() ) {
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
                                         skin.ButtonLeft,
                                         buttonLayout ) )
                    insertElementBefore = listObject;
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListInsertElementAfter.ToString(),
                                                        false,
                                                        "Insert new element after this" ),
                                         skin.ButtonMiddle,
                                         buttonLayout ) )
                    insertElementAfter = listObject;
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(),
                                                        false,
                                                        "Erase this element" ),
                                         skin.ButtonRight,
                                         buttonLayout ) )
                    eraseElement = listObject;
                }
              }
              GUILayout.EndHorizontal();
            }
          }

          if ( list.Count == 0 )
            GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.Label );
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
                                                 skin.Button,
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
    public static object ScriptDrawer( object obj, InvokeWrapper wrapper )
    {
      object result             = null;
      var type                  = wrapper.GetContainingType();
      bool allowSceneObject     = type == typeof( GameObject ) ||
                                  typeof( ScriptComponent ).IsAssignableFrom( type );
      Object valInField         = wrapper.Get<Object>( obj );
      bool recursiveEditing     = wrapper.HasAttribute<AllowRecursiveEditing>();
      //bool createNewAssetButton = false;
      //var skin                  = InspectorEditor.Skin;

      // TODO GUI: Use foldout and stuff.
      //           Clean code.
      if ( recursiveEditing ) {
        result = InspectorGUI.FoldoutObjectField( InspectorGUI.MakeLabel( wrapper.Member ),
                                                  valInField,
                                                  type,
                                                  EditorData.Instance.GetData( obj as Object,
                                                                               wrapper.Member.Name ),
                                                  !wrapper.CanWrite() );
        //var foldoutData   = EditorData.Instance.GetData( obj as Object, wrapper.Member.Name );
        //var objFieldLabel = InspectorGUI.MakeLabel( wrapper.Member );

        //var position     = EditorGUILayout.GetControlRect();
        //foldoutData.Bool = EditorGUI.Foldout( position,
        //                                      foldoutData.Bool,
        //                                      objFieldLabel,
        //                                      true );
        //if ( wrapper.CanWrite() ) {
        //  var supportsCreateAsset = typeof( ScriptAsset ).IsAssignableFrom( type );

        //  position.x    += EditorGUIUtility.labelWidth - GUI.IndentScope.PixelLevel;
        //  position.xMax -= EditorGUIUtility.labelWidth + Convert.ToInt32( supportsCreateAsset ) * 42 - GUI.IndentScope.PixelLevel;
        //  result         = EditorGUI.ObjectField( position, valInField, type, allowSceneObject );
        //  if ( supportsCreateAsset ) {
        //    var buttonRect = new Rect( position.xMax + 4, position.y, 42, 16 );
        //    buttonRect.xMax = buttonRect.x + 42 - 2;

        //    createNewAssetButton = UnityEngine.GUI.Button( buttonRect,
        //                                                   GUI.MakeLabel( "New", false, "Create new asset" ) );
        //  }
        //}
        //else
        //  result = valInField;

        //// Remove editor if object field is set to null or another object.
        //if ( valInField != ( result as Object ) ) {
        //  ToolManager.ReleaseRecursiveEditor( valInField );
        //  foldoutData.Bool = false;
        //}

        //if ( foldoutData.Bool ) {
        //  using ( GUI.IndentScope.Create() ) {
        //    GUI.Separator();

        //    GUILayout.Space( 6 );

        //    GUI.WarningLabel( "Changes made to this object will affect all objects referencing this asset." );

        //    GUILayout.Space( 6 );

        //    Editor editor = ToolManager.TryGetOrCreateRecursiveEditor( result as Object );
        //    if ( editor != null )
        //      editor.OnInspectorGUI();

        //    GUI.Separator();
        //  }
        //}
      }
      else
        result = EditorGUILayout.ObjectField( InspectorGUI.MakeLabel( wrapper.Member ),
                                              valInField,
                                              type,
                                              allowSceneObject );

      //if ( createNewAssetButton ) {
      //  var assetName = type.Name.SplitCamelCase().ToLower();
      //  var path = EditorUtility.SaveFilePanel( "Create new " + assetName, "Assets", "new " + assetName + ".asset", "asset" );
      //  if ( path != string.Empty ) {
      //    var info         = new System.IO.FileInfo( path );
      //    var relativePath = IO.Utils.MakeRelative( path, Application.dataPath );
      //    var newInstance  = ScriptAsset.Create( type );
      //    newInstance.name = info.Name;
      //    AssetDatabase.CreateAsset( newInstance, relativePath + ( info.Extension == ".asset" ? "" : ".asset" ) );
      //    AssetDatabase.SaveAssets();
      //    AssetDatabase.Refresh();

      //    result = newInstance;
      //  }
      //}

      return result;
    }
  }
}
