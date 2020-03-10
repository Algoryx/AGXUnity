using System;
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
    public static object Vector4Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector4Field( InspectorGUI.MakeLabel( wrapper.Member ).text,
                                           wrapper.Get<Vector4>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Vector3 ) )]
    public static object Vector3Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector3Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector3>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Vector2 ) )]
    public static object Vector2Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.Vector2Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector2>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( int ) )]
    public static object IntDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.IntField( InspectorGUI.MakeLabel( wrapper.Member ).text,
                                       wrapper.Get<int>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( bool ) )]
    public static object BoolDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Toggle( InspectorGUI.MakeLabel( wrapper.Member ),
                                  wrapper.Get<bool>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Color ) )]
    public static object ColorDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.ColorField( InspectorGUI.MakeLabel( wrapper.Member ),
                                         wrapper.Get<Color>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueFloat ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DefaultAndUserValueFloatDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var result = HandleDefaultAndUserValue<float>( objects, wrapper );
      return result.ContainsChanges ? (object)result : null;
    }

    public static void DefaultAndUserValueFloatDrawerCopyOp( object source, object destination )
    {
      var s = (DefaultAndUserValueResult)source;
      var d = destination as DefaultAndUserValueFloat;
      s.PropagateChanges( d );
    }

    [InspectorDrawer( typeof( DefaultAndUserValueVector3 ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object DefaultAndUserValueVector3Drawer( object[] objects, InvokeWrapper wrapper )
    {
      var result = HandleDefaultAndUserValue<Vector3>( objects, wrapper );
      return result.ContainsChanges ? (object)result : null;
    }

    public static void DefaultAndUserValueVector3DrawerCopyOp( object source, object destination )
    {
      var s = (DefaultAndUserValueResult)source;
      var d = destination as DefaultAndUserValueVector3;
      s.PropagateChanges( d );
    }

    private static MethodInfo s_floatFieldMethod = null;
    private static MethodInfo s_vector3FieldMethod = null;
    private static object[] s_fieldMethodArgs = new object[] { null, "", null };

    private struct DefaultAndUserValueResult
    {
      public bool DefaultToggleChanged;
      public bool UseDefault;

      public bool UpdateDefaultClicked;

      public bool[] ValuesChanged;
      public float[] Values;

      public void OnChange<ValueT>( object oldValueObject, object newValueObject )
        where ValueT : struct
      {
        if ( typeof( ValueT ) == typeof( float ) ) {
          ValuesChanged = new bool[] { true };
          Values = new float[] { (float)newValueObject };
        }
        else if ( typeof( ValueT ) == typeof( Vector3 ) ) {
          var oldValue = (Vector3)oldValueObject;
          var newValue = (Vector3)newValueObject;
          ValuesChanged = new bool[] { false, false, false };
          Values = new float[] { newValue.x, newValue.y, newValue.z };
          for ( int i = 0; i < 3; ++i )
            ValuesChanged[ i ] = !oldValue[ i ].Equals( Values[ i ] );
        }
      }

      public void PropagateChanges( DefaultAndUserValueFloat destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged[ 0 ] )
          destination.Value = Values[ 0 ];
      }

      public void PropagateChanges( DefaultAndUserValueVector3 destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged.Contains( true ) ) {
          var newValue = new Vector3();
          for ( int i = 0; i < 3; ++i )
            newValue[ i ] = ValuesChanged[ i ] ? Values[ i ] : destination.Value[ i ];
          destination.Value = newValue;
        }
      }

      private void PropagateChangesT<ValueT>( DefaultAndUserValue<ValueT> destination )
        where ValueT : struct
      {
        if ( DefaultToggleChanged )
          destination.UseDefault = UseDefault;
        if ( UpdateDefaultClicked )
          destination.OnForcedUpdate();
      }

      public bool ContainsChanges
      {
        get
        {
          return DefaultToggleChanged ||
                 UpdateDefaultClicked ||
                 ( ValuesChanged != null && ValuesChanged.Contains( true ) );
        }
      }
    }

    private static bool CompareMulti<ValueT>( object[] objects,
                                              InvokeWrapper wrapper,
                                              Func<DefaultAndUserValue<ValueT>, bool> validator )
      where ValueT : struct
    {
      var identical = true;
      for ( int i = 1; i < objects.Length; ++i )
        identical = identical && validator( wrapper.Get<DefaultAndUserValue<ValueT>>( objects[ i ] ) );
      return identical;
    }

    private static DefaultAndUserValueResult HandleDefaultAndUserValue<ValueT>( object[] objects,
                                                                                InvokeWrapper wrapper )
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

      var method = typeof( ValueT ) == typeof( float ) ?
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
      var widthUntilButton = rect.xMax;
      rect.xMax            = EditorGUIUtility.labelWidth - 28;

      // Result and reference instance.
      var result   = new DefaultAndUserValueResult();
      var instance = wrapper.Get<DefaultAndUserValue<ValueT>>( objects[ 0 ] );

      UnityEngine.GUI.changed = false;
      var hasMixedUseDefault  = !CompareMulti<ValueT>( objects,
                                                       wrapper,
                                                       other => other.UseDefault == instance.UseDefault );
      EditorGUI.showMixedValue = hasMixedUseDefault;

      var toggleInput = hasMixedUseDefault ?
                          false :
                          instance.UseDefault;
      // During showMixedValue - Toggle will always return true (enabled)
      // when the user clicks regardless of instance.UseDefault.
      var toggleOutput = EditorGUI.ToggleLeft( rect,
                                               GUI.MakeLabel( wrapper.Member.Name.SplitCamelCase(),
                                                              false,
                                                              "If checked - value will be default. Uncheck to manually enter value." ),
                                               toggleInput );
      if ( toggleOutput != toggleInput ) {
        result.DefaultToggleChanged = true;
        result.UseDefault = toggleOutput;
      }

      // Restore width and calculate new start of the float
      // field(s). Start is label width but we have to remove
      // the current indent level since label width is independent
      // of the indent level. Unsure why we have to add LayoutMagicNumber pixels...
      // could be float field(s) default minimum label size.
      rect.xMax  = widthUntilButton;
      rect.x     = EditorGUIUtility.labelWidth - InspectorGUI.IndentScope.PixelLevel + InspectorGUI.LayoutMagicNumber;
      rect.xMax += -rect.x + InspectorGUI.LayoutMagicNumber;

      s_fieldMethodArgs[ 0 ] = rect;
      s_fieldMethodArgs[ 2 ] = instance.Value;
      var newValue = default( ValueT );

      EditorGUI.showMixedValue = !CompareMulti<ValueT>( objects,
                                                        wrapper,
                                                        other => instance.Value.Equals( other.Value ) );
      using ( new GUI.EnabledBlock( !instance.UseDefault && !hasMixedUseDefault ) ) {
        EditorGUI.BeginChangeCheck();
        newValue = (ValueT)method.Invoke( null, s_fieldMethodArgs );
        if ( EditorGUI.EndChangeCheck() )
          result.OnChange<ValueT>( instance.Value, newValue );
      }

      rect.x                      = rect.xMax;
      rect.width                  = updateButtonWidth;
      rect.height                 = EditorGUIUtility.singleLineHeight -
                                    EditorGUIUtility.standardVerticalSpacing;
      result.UpdateDefaultClicked = InspectorGUI.Button( rect,
                                                         MiscIcon.Update,
                                                         instance.UseDefault,
                                                         InspectorEditor.Skin.ButtonRight,
                                                         "Force update of default value.",
                                                         1.2f );

      return result;
    }

    [InspectorDrawer( typeof( RangeReal ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object RangeRealDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.RangeRealField( InspectorGUI.MakeLabel( wrapper.Member ),
                                          wrapper.Get<RangeReal>( objects[ 0 ] ) );
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
    public static object DeformableTerrainShovelExcavationSettingsDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var data = new ExcavationSettingsResult()
      {
        Value = wrapper.Get<DeformableTerrainShovelSettings.ExcavationSettings>( objects[ 0 ] )
      };
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( objects[ 0 ] as Object, wrapper.Member.Name ),
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
    public static object StringDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return EditorGUILayout.TextField( InspectorGUI.MakeLabel( wrapper.Member ),
                                        wrapper.Get<string>( objects[ 0 ] ),
                                        InspectorEditor.Skin.TextField );
    }

    [InspectorDrawer( typeof( Enum ), IsBaseType = true )]
    public static object EnumDrawer( object[] objects, InvokeWrapper wrapper )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      if ( wrapper.GetContainingType().GetCustomAttribute<FlagsAttribute>() != null )
        return EditorGUILayout.EnumFlagsField( InspectorGUI.MakeLabel( wrapper.Member ),
                                               wrapper.Get<Enum>( objects[ 0 ] ),
                                               InspectorEditor.Skin.Popup );
      else
        return EditorGUILayout.EnumPopup( InspectorGUI.MakeLabel( wrapper.Member ),
                                          wrapper.Get<Enum>( objects[ 0 ] ),
                                          InspectorEditor.Skin.Popup );
    }

    [InspectorDrawer( typeof( float ) )]
    [InspectorDrawer( typeof( double ) )]
    public static object DecimalDrawer( object[] objects, InvokeWrapper wrapper )
    {
      float value = wrapper.GetContainingType() == typeof( double ) ?
                      Convert.ToSingle( wrapper.Get<double>( objects[ 0 ] ) ) :
                      wrapper.Get<float>( objects[ 0 ] );
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
    public static object GenericListDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var list = wrapper.Get<System.Collections.IList>( objects[ 0 ] );
      var target = objects[ 0 ] as Object;

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

              if ( InspectorGUI.Button( MiscIcon.EntryInsertBefore,
                                        true,
                                        "Insert new element before this.",
                                        buttonLayout ) )
                insertElementBefore = listObject;
              if ( InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                        true,
                                        "Insert new element after this.",
                                        buttonLayout ) )
                insertElementAfter = listObject;
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
    public static object ScriptDrawer( object[] objects, InvokeWrapper wrapper )
    {
      object result         = null;
      var type              = wrapper.GetContainingType();
      bool allowSceneObject = type == typeof( GameObject ) ||
                              typeof( ScriptComponent ).IsAssignableFrom( type );
      Object valInField     = wrapper.Get<Object>( objects[ 0 ] );
      bool recursiveEditing = wrapper.HasAttribute<AllowRecursiveEditing>();

      if ( recursiveEditing ) {
        result = InspectorGUI.FoldoutObjectField( InspectorGUI.MakeLabel( wrapper.Member ),
                                                  valInField,
                                                  type,
                                                  EditorData.Instance.GetData( objects[ 0 ] as Object,
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
