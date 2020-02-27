﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Model;
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
      return InspectorGUI.Toggle( InspectorGUI.MakeLabel( wrapper.Member ),
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

    private static MethodInfo s_floatFieldMethod = null;
    private static MethodInfo s_vector3FieldMethod = null;
    private static object[] s_fieldMethodArgs = new object[] { null, "", null };

    private static ValueT HandleDefaultAndUserValue<ValueT>( string name,
                                                             DefaultAndUserValue<ValueT> valInField )
      where ValueT : struct
    {
      if ( s_floatFieldMethod == null )
        s_floatFieldMethod = typeof( EditorGUI ).GetMethod( "FloatField",
                                                            new[]
                                                            {
                                                              typeof( Rect ),
                                                              typeof( string ),
                                                              typeof( float )
                                                            } );
      if ( s_vector3FieldMethod == null )
        s_vector3FieldMethod = typeof( EditorGUI ).GetMethod( "Vector3Field",
                                                              new[]
                                                              {
                                                                typeof( Rect ),
                                                                typeof( string ),
                                                                typeof( Vector3 )
                                                              } );

      var guiWasEnabled = UnityEngine.GUI.enabled;
      var method        = typeof( ValueT ) == typeof( float ) ?
                            s_floatFieldMethod :
                          typeof( ValueT ) == typeof( Vector3 ) ?
                            s_vector3FieldMethod :
                            null;
      if ( method == null )
        throw new NullReferenceException( "Unknown DefaultAndUserValue type: " + typeof( ValueT ).Name );

      var updateButtonWidth = 20.0f;
      var rect              = EditorGUILayout.GetControlRect();
      
      // Now we know the total width if the Inspector. Remove
      // width of button and right most spacing.
      rect.xMax -= updateButtonWidth;
      
      // We don't want the tooltip of the toggle to show when
      // hovering the update button or float field(s) so use
      // xMax as label width minus some magic number so that
      // e.g., Mass float field slider appears and works.
      var widthUntilButton  = rect.xMax;
      rect.xMax             = EditorGUIUtility.labelWidth - 28;
      var useDefaultToggled = EditorGUI.ToggleLeft( rect,
                                                    GUI.MakeLabel( name.SplitCamelCase(),
                                                                   false,
                                                                   "If checked - value will be default. Uncheck to manually enter value." ),
                                                    valInField.UseDefault ) != valInField.UseDefault;
      // Restore width and calculate new start of the float
      // field(s). Start is label width but we have to remove
      // the current indent level since label width is independent
      // of the indent level. Unsure why we have to add LayoutMagicNumber pixels...
      // could be float field(s) default minimum label size.
      rect.xMax   = widthUntilButton;
      rect.x      = EditorGUIUtility.labelWidth - InspectorGUI.IndentScope.PixelLevel + InspectorGUI.LayoutMagicNumber;
      rect.xMax += -rect.x + InspectorGUI.LayoutMagicNumber;

      s_fieldMethodArgs[ 0 ] = rect;
      s_fieldMethodArgs[ 2 ] = valInField.Value;
      var newValue           = default( ValueT );
      using ( new GUI.EnabledBlock( !valInField.UseDefault ) )
        newValue = (ValueT)method.Invoke( null, s_fieldMethodArgs );

      rect.x                 = rect.xMax;
      rect.width             = updateButtonWidth;
      rect.height            = EditorGUIUtility.singleLineHeight -
                               EditorGUIUtility.standardVerticalSpacing;
      var updateDefaultValue = InspectorGUI.Button( rect,
                                                    MiscIcon.Update,
                                                    valInField.UseDefault,
                                                    InspectorEditor.Skin.ButtonRight,
                                                    "Force update of default value.",
                                                    1.2f );
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

    [InspectorDrawer( typeof( RangeReal ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object RangeRealDrawer( object obj, InvokeWrapper wrapper )
    {
      return InspectorGUI.RangeRealField( InspectorGUI.MakeLabel( wrapper.Member ),
                                          wrapper.Get<RangeReal>( obj ) );
    }

    public static object RangeRealDrawerCopyOp( object data, object destination )
    {
      // We have this copy operation to handle the case when the
      // user is changing either min or max, i.e., we shouldn't
      // propagate the unchanged value to other instances during
      // multi-select.
      var result = (InspectorGUI.RangeRealResult)data;
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
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( obj as Object, wrapper.Member.Name ),
                                 InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        using ( InspectorGUI.IndentScope.Single ) {
          data.Value.Enabled                   = InspectorGUI.Toggle( GUI.MakeLabel( "Enabled" ),
                                                                      data.Value.Enabled );
          data.EnabledChanged                  = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.CreateDynamicMassEnabled  = InspectorGUI.Toggle( GUI.MakeLabel( "Create Dynamic Mass Enabled" ),
                                                                      data.Value.CreateDynamicMassEnabled );
          data.CreateDynamicMassEnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed              = false;
          data.Value.ForceFeedbackEnabled      = InspectorGUI.Toggle( GUI.MakeLabel( "Force Feedback Enabled" ),
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
      var list = wrapper.Get<System.Collections.IList>( obj );
      var target = obj as Object;

      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
                                 InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        object insertElementBefore = null;
        object insertElementAfter  = null;
        object eraseElement        = null;
        var skin                   = InspectorEditor.Skin;
        var buttonLayout = new GUILayoutOption[]
        {
          GUILayout.Width( 1.0f * EditorGUIUtility.singleLineHeight ),
          GUILayout.Height( 1.0f * EditorGUIUtility.singleLineHeight )
        };
        foreach ( var listObject in list ) {
          using ( InspectorGUI.IndentScope.Single ) {
            GUILayout.BeginHorizontal();
            {
              GUILayout.BeginVertical();
              {
                // Using target to render listObject since it normally (CollisionGroupEntry) isn't an Object.
                InspectorEditor.DrawMembersGUI( new Object[] { target }, ignored => listObject );
              }
              GUILayout.EndVertical();

              //if ( GUILayout.Button( GUI.MakeLabel( GUI.AddColorTag( GUI.Symbols.ListInsertElementBefore.ToString(),
              //                                                       InspectorGUISkin.BrandColor ),
              //                                      false,
              //                                      "Insert new element before this" ),
              //                        skin.ButtonMiddle,
              //                        buttonLayout ) )
              if ( InspectorGUI.Button( MiscIcon.EntryInsertBefore,
                                        true,
                                        "Insert new element before this.",
                                        buttonLayout ) )
                insertElementBefore = listObject;
              //if ( GUILayout.Button( GUI.MakeLabel( GUI.AddColorTag( GUI.Symbols.ListInsertElementAfter.ToString(),
              //                                                       InspectorGUISkin.BrandColor ),
              //                                      false,
              //                                      "Insert new element after this" ),
              //                        skin.ButtonMiddle,
              //                        buttonLayout ) )
              if ( InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                        true,
                                        "Insert new element after this.",
                                        buttonLayout ) )
                insertElementAfter = listObject;
              //if ( GUILayout.Button( GUI.MakeLabel( GUI.AddColorTag( GUI.Symbols.ListEraseElement.ToString(),
              //                                                       InspectorGUISkin.BrandColor ),
              //                                      false,
              //                                      "Erase this element" ),
              //                        skin.ButtonMiddle,
              //                        buttonLayout ) )
              if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                        true,
                                        "Remove this element.",
                                        buttonLayout ) )
                eraseElement = listObject;
            }
            GUILayout.EndHorizontal();
          }

          GUILayout.Space( 4.0f );
        }

        if ( list.Count == 0 )
          GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.Label );

        bool addElementToList = false;
        GUILayout.BeginHorizontal();
        {
          GUILayout.FlexibleSpace();
          //addElementToList = GUILayout.Button( GUI.MakeLabel( GUI.AddColorTag( GUI.Symbols.ListInsertElementAfter.ToString(),
          //                                                                     InspectorGUISkin.BrandColor ),
          //                                                    false,
          //                                                    "Add new element to list" ),
          //                                      skin.ButtonMiddle,
          //                                      buttonLayout );
          addElementToList = InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                                  true,
                                                  "Add new element.",
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
      object result         = null;
      var type              = wrapper.GetContainingType();
      bool allowSceneObject = type == typeof( GameObject ) ||
                              typeof( ScriptComponent ).IsAssignableFrom( type );
      Object valInField     = wrapper.Get<Object>( obj );
      bool recursiveEditing = wrapper.HasAttribute<AllowRecursiveEditing>();

      if ( recursiveEditing ) {
        result = InspectorGUI.FoldoutObjectField( InspectorGUI.MakeLabel( wrapper.Member ),
                                                  valInField,
                                                  type,
                                                  EditorData.Instance.GetData( obj as Object,
                                                                               wrapper.Member.Name ),
                                                  !wrapper.CanWrite() );
      }
      else
        result = EditorGUILayout.ObjectField( InspectorGUI.MakeLabel( wrapper.Member ),
                                              valInField,
                                              type,
                                              allowSceneObject );

      return result;
    }
  }
}
