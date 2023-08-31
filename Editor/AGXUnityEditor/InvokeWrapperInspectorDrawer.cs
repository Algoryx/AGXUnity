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
      info.CopyOp = drawerClass.GetMethod( info.Drawer.Name + "CopyOp", BindingFlags.Public | BindingFlags.Static );
    }

    private static Dictionary<Type, DrawerInfo> m_drawerMethodsCache = new Dictionary<Type, DrawerInfo>();
    private static List<Type> m_drawerClasses = new List<Type>() { typeof( InvokeWrapperInspectorDrawer ) };

    [InspectorDrawer( typeof( Vector4 ) )]
    public static object Vector4Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Vector4Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                        wrapper.Get<Vector4>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Vector3 ) )]
    public static object Vector3Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Vector3Field( InspectorGUI.MakeLabel( wrapper.Member ),
                                        wrapper.Get<Vector3>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Vector2 ) )]
    public static object Vector2Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Vector2Field( InspectorGUI.MakeLabel( wrapper.Member ),
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

    [InspectorDrawer( typeof( OptionalOverrideValue<float> ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object OptionalOverrideFloatDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var result = HandleOptionalOverride<float>( objects, wrapper );
      return result.ContainsChanges ? (object)result : null;
    }

    public static void OptionalOverrideFloatDrawerCopyOp( object source, object destination )
    {
      var s = (OptionalOverrideValueResult)source;
      var d = destination as OptionalOverrideValue<float>;
      s.PropagateChanges( d );
    }

    [InspectorDrawer( typeof( OptionalOverrideValue<Vector3> ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object OptionalOverrideVector3Drawer( object[] objects, InvokeWrapper wrapper )
    {
      return HandleOptionalOverride<Vector3>( objects, wrapper );
    }

    public static void OptionalOverrideVector3DrawerCopyOp( object source, object destination )
    {
      var s = (OptionalOverrideValueResult)source;
      var d = destination as OptionalOverrideValue<Vector3>;
      s.PropagateChanges( d );
    }

    private static OptionalOverrideValueResult HandleOptionalOverride<ValueT>( object[] objects, InvokeWrapper wrapper )
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
        throw new NullReferenceException( "Unknown OptionalOverrideValue type: " + typeof( ValueT ).Name );

      var rect = EditorGUILayout.GetControlRect();

      // We don't want the tooltip of the toggle to show when
      // hovering the update button or float field(s) so use
      // xMax as label width minus some magic number
      var widthUntilButton = rect.xMax;
      rect.xMax = EditorGUIUtility.labelWidth;

      var result = new OptionalOverrideValueResult();
      var instance = wrapper.Get<OptionalOverrideValue<ValueT>>( objects[ 0 ] );

      UnityEngine.GUI.changed = false;
      var hasMixedUseOverride = !CompareMulti<ValueT>( objects,
                                                       wrapper,
                                                       other => other.UseOverride == instance.UseOverride );
      EditorGUI.showMixedValue = hasMixedUseOverride;

      var toggleInput = hasMixedUseOverride ?
                          false :
                          instance.UseOverride;

      // During showMixedValue - Toggle will always return true (enabled)
      // when the user clicks regardless of instance.UseOverride.
      var toggleOutput = EditorGUI.ToggleLeft( rect,
                                               GUI.MakeLabel( wrapper.Member.Name.SplitCamelCase(),
                                                              false,
                                                              "If checked, the override value will be used. Uncheck to use default." ),
                                               toggleInput );
      if ( toggleOutput != toggleInput ) {
        result.UseOverrideToggleChanged = true;
        result.UseOverride = toggleOutput;
      }

      // Restore width and calculate new start of the float
      // field(s). Start is label width but we have to remove
      // the current indent level since label width is independent
      // of the indent level. Unsure why we have to add LayoutMagicNumber pixels...
      // could be float field(s) default minimum label size.
      rect.xMax = widthUntilButton;
      rect.xMin = EditorGUIUtility.labelWidth - InspectorGUI.IndentScope.PixelLevel + InspectorGUI.LayoutMagicNumber;

      s_fieldMethodArgs[ 0 ] = rect;
      s_fieldMethodArgs[ 2 ] = instance.OverrideValue;
      var newValue = default( ValueT );

      EditorGUI.showMixedValue = !CompareMulti<ValueT>( objects,
                                                        wrapper,
                                                        other => instance.OverrideValue.Equals( other.OverrideValue ) );
      using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled &&
                                    instance.UseOverride &&
                                    !hasMixedUseOverride ) ) {
        EditorGUI.BeginChangeCheck();
        newValue = (ValueT)method.Invoke( null, s_fieldMethodArgs );
        if ( EditorGUI.EndChangeCheck() ) {
          // Validate input here so that, e.g., 0 isn't propagated. It's
          // not possible to check this in the CopyOp callback.
          var clampAttribute = wrapper.GetAttribute<ClampAboveZeroInInspector>();
          if ( clampAttribute == null || clampAttribute.IsValid( newValue ) )
            result.OnChange<ValueT>(instance.OverrideValue, newValue);
        }
      }

      return result;
    }

    private struct OptionalOverrideValueResult
    {
      public bool UseOverrideToggleChanged;
      public bool UseOverride;

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

      public void PropagateChanges( OptionalOverrideValue<float> destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged[ 0 ] )
          destination.OverrideValue = Values[ 0 ];
      }

      public void PropagateChanges( OptionalOverrideValue<Vector3> destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged.Contains( true ) ) {
          var newValue = new Vector3();
          for ( int i = 0; i < 3; ++i )
            newValue[ i ] = ValuesChanged[ i ] ? Values[ i ] : destination.OverrideValue[ i ];
          destination.OverrideValue = newValue;
        }
      }

      private void PropagateChangesT<ValueT>( OptionalOverrideValue<ValueT> destination )
        where ValueT : struct
      {
        if ( UseOverrideToggleChanged )
          destination.UseOverride = UseOverride;
      }

      public bool ContainsChanges
      {
        get
        {
          return UseOverrideToggleChanged ||
                 ( ValuesChanged != null && ValuesChanged.Contains( true ) );
        }
      }
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

    private static bool CompareMulti<ValueT>( object[] objects,
                                              InvokeWrapper wrapper,
                                              Func<OptionalOverrideValue<ValueT>, bool> validator )
      where ValueT : struct
    {
      var identical = true;
      for ( int i = 1; i < objects.Length; ++i )
        identical = identical && validator( wrapper.Get<OptionalOverrideValue<ValueT>>( objects[ i ] ) );
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
      var updateButtonWidthAndMargin = updateButtonWidth + 4.0f;
      var rect = EditorGUILayout.GetControlRect();

      // Now we know the total width if the Inspector. Remove
      // width of button and right most spacing.
      rect.xMax -= updateButtonWidthAndMargin;

      // We don't want the tooltip of the toggle to show when
      // hovering the update button or float field(s) so use
      // xMax as label width minus some magic number so that
      // e.g., Mass float field slider appears and works.
      var widthUntilButton = rect.xMax;
      rect.xMax = EditorGUIUtility.labelWidth - 28;

      // Result and reference instance.
      var result = new DefaultAndUserValueResult();
      var instance = wrapper.Get<DefaultAndUserValue<ValueT>>( objects[ 0 ] );

      UnityEngine.GUI.changed = false;
      var hasMixedUseDefault = !CompareMulti<ValueT>( objects,
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
      rect.xMax = widthUntilButton;
      rect.x = EditorGUIUtility.labelWidth - InspectorGUI.IndentScope.PixelLevel + InspectorGUI.LayoutMagicNumber;
      rect.xMax += -rect.x + InspectorGUI.LayoutMagicNumber;

      s_fieldMethodArgs[ 0 ] = rect;
      s_fieldMethodArgs[ 2 ] = instance.Value;
      var newValue = default( ValueT );

      EditorGUI.showMixedValue = !CompareMulti<ValueT>( objects,
                                                        wrapper,
                                                        other => instance.Value.Equals( other.Value ) );
      using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled &&
                                    !instance.UseDefault &&
                                    !hasMixedUseDefault ) ) {
        EditorGUI.BeginChangeCheck();
        newValue = (ValueT)method.Invoke( null, s_fieldMethodArgs );
        if ( EditorGUI.EndChangeCheck() ) {
          // Validate input here so that, e.g., 0 isn't propagated. It's
          // not possible to check this in the CopyOp callback.
          var clampAttribute = wrapper.GetAttribute<ClampAboveZeroInInspector>();
          if ( clampAttribute == null || clampAttribute.IsValid( newValue ) )
            result.OnChange<ValueT>( instance.Value, newValue );
        }
      }

      rect.x = rect.xMax;
      rect.width = updateButtonWidth;
      rect.height = EditorGUIUtility.singleLineHeight;
      result.UpdateDefaultClicked = InspectorGUI.Button( rect,
                                                         MiscIcon.Update,
                                                         instance.UseDefault,
                                                         InspectorEditor.Skin.ButtonRight,
                                                         "Force update of default value.",
                                                         0.9f );

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
      var value = (RangeReal)destination;
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
          data.Value.Enabled = InspectorGUI.Toggle( GUI.MakeLabel( "Enabled" ),
                                                                      data.Value.Enabled );
          data.EnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed = false;
          data.Value.CreateDynamicMassEnabled = InspectorGUI.Toggle( GUI.MakeLabel( "Create Dynamic Mass Enabled" ),
                                                                      data.Value.CreateDynamicMassEnabled );
          data.CreateDynamicMassEnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed = false;
          data.Value.ForceFeedbackEnabled = InspectorGUI.Toggle( GUI.MakeLabel( "Force Feedback Enabled" ),
                                                                      data.Value.ForceFeedbackEnabled );
          data.ForceFeedbackEnabledChanged = UnityEngine.GUI.changed;
          UnityEngine.GUI.changed = false;
        }
        UnityEngine.GUI.changed = data.ContainsChanges;
      }
      return data;
    }

    public static object DeformableTerrainShovelExcavationSettingsDrawerCopyOp( object data, object destination )
    {
      var result = (ExcavationSettingsResult)data;
      var value = (DeformableTerrainShovelSettings.ExcavationSettings)destination;
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
        object insertElementAfter = null;
        object eraseElement = null;
        var skin = InspectorEditor.Skin;
        var buttonLayout = new GUILayoutOption[]
        {
          GUILayout.Width( 1.0f * EditorGUIUtility.singleLineHeight ),
          GUILayout.Height( 1.0f * EditorGUIUtility.singleLineHeight )
        };
        foreach ( var listObject in list ) {
          using ( InspectorGUI.IndentScope.Single ) {
            GUILayout.BeginHorizontal();
            {
              InspectorGUI.Separator( 1.0f, EditorGUIUtility.singleLineHeight );

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

            InspectorEditor.DrawMembersGUI( new Object[] { target }, ignored => listObject );

          }
        }

        InspectorGUI.Separator( 1.0f, 0.5f * EditorGUIUtility.singleLineHeight );

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
      object result = null;
      var type = wrapper.GetContainingType();
      bool allowSceneObject = type == typeof( GameObject ) ||
                              typeof( ScriptComponent ).IsAssignableFrom( type );
      Object valInField = wrapper.Get<Object>( objects[ 0 ] );
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

    public static void DrawUrdfElement( AGXUnity.IO.URDF.Element element,
                                        int elementArrayIndex = -1 )
    {
      if ( element == null )
        return;

      var dropDownName = string.IsNullOrEmpty( element.Name ) ?
                            elementArrayIndex >= 0 ?
                              $"{element.GetType().Name}[{elementArrayIndex}]" :
                              element.GetType().Name :
                            element.Name;
      if ( !InspectorGUI.Foldout( GetEditorData( element, dropDownName ),
                                  GUI.MakeLabel( InspectorGUISkin.Instance.TagTypename( $"Urdf.{element.GetType().Name}" ) +
                                                  ' ' +
                                                  dropDownName ) ) )
        return;

      using ( InspectorGUI.IndentScope.Single ) {
        if ( element is AGXUnity.IO.URDF.Model model ) {
          var isSelectedPrefab = PrefabUtility.GetPrefabInstanceStatus( Selection.activeGameObject ) == PrefabInstanceStatus.Connected;
          var modelAssetPath = AssetDatabase.GetAssetPath( element );

          var savePrefabRect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect() );
          savePrefabRect.width = InspectorGUISkin.ToolButtonSize.x;
          savePrefabRect.height = InspectorGUISkin.ToolButtonSize.y;
          var savePrefab = InspectorGUI.Button( savePrefabRect,
                                                MiscIcon.Locate,
                                                !isSelectedPrefab,
                                                "Save game object as prefab and all URDF elements, STL meshes and render materials in project.",
                                                1.1f );
          savePrefabRect.x += savePrefabRect.width;
          savePrefabRect.xMax += savePrefabRect.width;
          savePrefabRect.width = InspectorGUISkin.ToolButtonSize.x;
          var saveAssets = false;
          using ( new GUI.EnabledBlock( string.IsNullOrEmpty( modelAssetPath ) ) ) {
            saveAssets = UnityEngine.GUI.Button( savePrefabRect,
                                                 GUI.MakeLabel( "",
                                                                false,
                                                                "Save all URDF elements, STL meshes and render materials in project." ),
                                                 InspectorEditor.Skin.ButtonMiddle );

            savePrefabRect.x -= 6.0f;
            savePrefabRect.y -= 4.0f;
            InspectorGUI.ButtonIcon( savePrefabRect,
                                     MiscIcon.Locate,
                                     UnityEngine.GUI.enabled,
                                     0.75f );
            savePrefabRect.x += 2.0f * 6.0f - 1.0f;
            savePrefabRect.y += 2.0f * 4.0f - 2.0f;
            InspectorGUI.ButtonIcon( savePrefabRect,
                                     MiscIcon.Locate,
                                     UnityEngine.GUI.enabled,
                                     0.75f );
          }

          if ( savePrefab )
            IO.URDF.Prefab.Create( model );
          if ( saveAssets )
            IO.URDF.Prefab.CreateAssets( model );

          InspectorGUI.SelectableTextField( GUI.MakeLabel( "Asset Path" ), modelAssetPath );
        }

        var ignoreName = element is AGXUnity.IO.URDF.Inertial;
        if ( !ignoreName ) {
          var nameRect = EditorGUILayout.GetControlRect();
          EditorGUI.PrefixLabel( nameRect, GUI.MakeLabel( "Name" ), InspectorEditor.Skin.Label );
          var orgXMax   = nameRect.xMax;
          nameRect.x   += EditorGUIUtility.labelWidth - 14.0f * InspectorGUI.IndentScope.Level;
          nameRect.xMax = orgXMax;
          EditorGUI.SelectableLabel( nameRect, element.Name, InspectorEditor.Skin.TextField );
        }

        if ( element is AGXUnity.IO.URDF.Pose )
          DrawUrdfPose( element as AGXUnity.IO.URDF.Pose );
        else if ( element is AGXUnity.IO.URDF.Material ) {
          DrawUrdfMaterial( element as AGXUnity.IO.URDF.Material );
          return;
        }

        var properties = GetOrFindProperties( element.GetType() );
        var elementArg = new object[] { element };
        var geometry = element as AGXUnity.IO.URDF.Geometry;
        foreach ( var property in properties ) {
          // Ignoring Unity specific properties such as "name" and "hideFlags".
          if ( !char.IsUpper( property.Member.Name[ 0 ] ) )
            continue;
          if ( !InspectorEditor.ShouldBeShownInInspector( property.Member ) )
            continue;

          var containingType = property.GetContainingType();
          if ( containingType.IsArray ) {
            if ( typeof( AGXUnity.IO.URDF.Element ).IsAssignableFrom( containingType.GetElementType() ) ) {
              var array = property.Get<System.Collections.ICollection>( element );
              if ( !InspectorGUI.Foldout( GetEditorData( element, property.Member.Name ),
                                          InspectorGUI.MakeLabel( property.Member,
                                                                  $" [{array.Count}]" ) ) )
                continue;

              using ( InspectorGUI.IndentScope.Single ) {
                var arrayIndex = 0;
                foreach ( var arrayItem in array )
                  DrawUrdfElement( arrayItem as AGXUnity.IO.URDF.Element, arrayIndex++ );
              }
            }
          }
          else if ( typeof( AGXUnity.IO.URDF.Element ).IsAssignableFrom( containingType ) ) {
            DrawUrdfElement( property.Get<AGXUnity.IO.URDF.Element>( element ), -1 );
          }
          else if ( geometry == null || IsValidGeometryProperty( geometry, property ) ) {
            var drawerMethod = GetDrawerMethod( containingType );
            drawerMethod.Drawer?.Invoke( null, new object[] { elementArg, property } );
          }
        }
      }
    }

    public static void DrawUrdfPose( AGXUnity.IO.URDF.Pose pose )
    {
      UnityEngine.GUI.Label( EditorGUI.IndentedRect( EditorGUILayout.GetControlRect() ),
                             GUI.MakeLabel( "Origin", true ),
                             InspectorEditor.Skin.Label );
      using ( new InspectorGUI.IndentScope() ) {
        InspectorGUI.Vector3Field( GUI.MakeLabel( "Position" ), pose.Xyz );
        InspectorGUI.Vector3Field( GUI.MakeLabel( "Roll, Pitch, Yaw" ), pose.Rpy, "R,P,Y" );
      }
    }

    public static void DrawUrdfMaterial( AGXUnity.IO.URDF.Material material )
    {
      // Name has already been rendered.
      InspectorGUI.Toggle( GUI.MakeLabel( "Is Reference" ), material.IsReference );
      if ( material.IsReference )
        return;
      var colorRect = EditorGUILayout.GetControlRect();
      EditorGUI.PrefixLabel( colorRect, GUI.MakeLabel( "Color" ) );
      var oldXMax = colorRect.xMax;
      colorRect.x += EditorGUIUtility.labelWidth;
      colorRect.xMax = oldXMax;
      EditorGUI.DrawRect( colorRect, material.Color );
    }

    private static EditorDataEntry GetEditorData( AGXUnity.IO.URDF.Element element, string name )
    {
      return EditorData.Instance.GetData( element, name, entry => entry.Bool = false );
    }

    private static bool IsValidGeometryProperty( AGXUnity.IO.URDF.Geometry geometry, PropertyWrapper wrapper )
    {
      // Geometry will throw when the wrong property is used.
      try {
        wrapper.Property.GetValue( geometry, null );
      }
      catch ( System.Exception ) {
        return false;
      }

      return true;
    }

    private static PropertyWrapper[] GetOrFindProperties( Type type )
    {
      if ( s_propertyWrapperCache.TryGetValue( type, out var cachedProperties ) )
        return cachedProperties;
      Func<PropertyWrapper, int> propertyPriority = wrapper =>
      {
        var containingType = wrapper.GetContainingType();
        return typeof( AGXUnity.IO.URDF.Element ).IsAssignableFrom( containingType ) ||
               containingType.IsArray ?
                 -1 :
               wrapper.Member.Name == "Name" ?
                 1 :
                 wrapper.Priority;
      };
      var properties = PropertyWrapper.FindProperties( type ).OrderByDescending( propertyPriority ).ToArray();
      s_propertyWrapperCache.Add( type, properties );
      return properties;
    }
    private static Dictionary<Type, PropertyWrapper[]> s_propertyWrapperCache = new Dictionary<Type, PropertyWrapper[]>();

    private static void DrawUrdfJointData<T>( AGXUnity.IO.URDF.UJoint parent,
                                              MemberInfo member,
                                              T jointData )
      where T : struct
    {
      var fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( typeof( T ) );
      var enabledFieldOrProperty = fieldsAndProperties.FirstOrDefault( wrapper => wrapper.Member.Name == "Enabled" );
      if ( enabledFieldOrProperty == null )
        return;
      var enabled = UnityEngine.GUI.enabled &&
                    enabledFieldOrProperty.Get<bool>( jointData );
      using ( new GUI.EnabledBlock( enabled ) ) {
        if ( !InspectorGUI.Foldout( GetEditorData( parent, member.Name ), InspectorGUI.MakeLabel( member ) ) )
          return;
        using ( InspectorGUI.IndentScope.Single ) {
          foreach ( var wrapper in fieldsAndProperties ) {
            if ( wrapper == enabledFieldOrProperty )
              continue;

            var drawer = GetDrawerMethod( wrapper.GetContainingType() );
            drawer.Drawer?.Invoke( null, new object[] { new object[] { jointData }, wrapper } );
          }
        }
      }
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.Inertia ) )]
    public static object UrdfInertiaDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var inertia = wrapper.Get<AGXUnity.IO.URDF.Inertia>( objects[ 0 ] );
      InspectorGUI.Vector3Field( InspectorGUI.MakeLabel( wrapper.Member ), inertia.GetRow( 0 ), "XX,XY,XZ" );
      InspectorGUI.Vector3Field( null, inertia.GetRow( 1 ), "YX,YY,YZ" );
      InspectorGUI.Vector3Field( null, inertia.GetRow( 2 ), "ZX,ZY,ZZ" );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.UJoint.CalibrationData ) )]
    public static object UrdfJointCalibrationDrawer( object[] objects, InvokeWrapper wrapper )
    {
      DrawUrdfJointData( objects[ 0 ] as AGXUnity.IO.URDF.UJoint,
                         wrapper.Member,
                         wrapper.Get<AGXUnity.IO.URDF.UJoint.CalibrationData>( objects[ 0 ] ) );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.UJoint.DynamicsData ) )]
    public static object UrdfJointDynamicsDrawer( object[] objects, InvokeWrapper wrapper )
    {
      DrawUrdfJointData( objects[ 0 ] as AGXUnity.IO.URDF.UJoint,
                         wrapper.Member,
                         wrapper.Get<AGXUnity.IO.URDF.UJoint.DynamicsData>( objects[ 0 ] ) );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.UJoint.LimitData ) )]
    public static object UrdfJointLimitDrawer( object[] objects, InvokeWrapper wrapper )
    {
      DrawUrdfJointData( objects[ 0 ] as AGXUnity.IO.URDF.UJoint,
                         wrapper.Member,
                         wrapper.Get<AGXUnity.IO.URDF.UJoint.LimitData>( objects[ 0 ] ) );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.UJoint.MimicData ) )]
    public static object UrdfJointMimicDrawer( object[] objects, InvokeWrapper wrapper )
    {
      DrawUrdfJointData( objects[ 0 ] as AGXUnity.IO.URDF.UJoint,
                         wrapper.Member,
                         wrapper.Get<AGXUnity.IO.URDF.UJoint.MimicData>( objects[ 0 ] ) );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.UJoint.SafetyControllerData ) )]
    public static object UrdfJointSafetyControllerDrawer( object[] objects, InvokeWrapper wrapper )
    {
      DrawUrdfJointData( objects[ 0 ] as AGXUnity.IO.URDF.UJoint,
                         wrapper.Member,
                         wrapper.Get<AGXUnity.IO.URDF.UJoint.SafetyControllerData>( objects[ 0 ] ) );
      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.IO.URDF.Element ) )]
    public static object UrdfElementDrawer( object[] objects, InvokeWrapper wrapper )
    {
      if ( objects.Length != 1 ) {
        InspectorGUI.WarningLabel( "Multi-select of URDF Elements isn't supported." );
        return null;
      }

      DrawUrdfElement( wrapper.Get<AGXUnity.IO.URDF.Element>( objects[ 0 ] ), -1 );

      return null;
    }
  }
}
