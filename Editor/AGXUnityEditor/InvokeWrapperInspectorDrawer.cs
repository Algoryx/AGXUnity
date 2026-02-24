using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Sensor;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;
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
      return EditorGUILayout.IntField( InspectorGUI.MakeLabel( wrapper.Member ),
                                       wrapper.Get<int>( objects[ 0 ] ) );
    }


    [InspectorDrawer( typeof( uint ) )]
    public static object UIntDrawer( object[] objects, InvokeWrapper wrapper )
    {
      // We need to clamp before we convert as the uint will simply wrap around otherwise, this loses some precision but should be fine in most cases.
      return (uint)Mathf.Max( EditorGUILayout.IntField( InspectorGUI.MakeLabel( wrapper.Member ),
                                       (int)wrapper.Get<uint>( objects[ 0 ] ) ), 0 );
    }

    [InspectorDrawer( typeof( Vector2Int ) )]
    public static object Vector2IntDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Vector2IntField( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector2Int>( objects[ 0 ] ) );
    }

    [InspectorDrawer( typeof( Vector3Int ) )]
    public static object Vector3IntDrawer( object[] objects, InvokeWrapper wrapper )
    {
      return InspectorGUI.Vector3IntField( InspectorGUI.MakeLabel( wrapper.Member ),
                                           wrapper.Get<Vector3Int>( objects[ 0 ] ) );
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

    [InspectorDrawer( typeof( OptionalOverrideValue<int> ) )]
    [InspectorDrawerResult( HasCopyOp = true )]
    public static object OptionalOverrideIntDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var result = HandleOptionalOverride<int>( objects, wrapper );
      return result.ContainsChanges ? (object)result : null;
    }

    public static void OptionalOverrideIntDrawerCopyOp( object source, object destination )
    {
      var s = (OptionalOverrideValueResult)source;
      var d = destination as OptionalOverrideValue<int>;
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
      if ( s_intFieldMethod == null )
        s_intFieldMethod = typeof( EditorGUI ).GetMethod( "IntField",
                                                              new[]
                                                              {
                                                                typeof( Rect ),
                                                                typeof( string ),
                                                                typeof( int )
                                                              } );

      var method = typeof( ValueT ) == typeof( float ) ?
                      s_floatFieldMethod :
                    typeof( ValueT ) == typeof( Vector3 ) ?
                      s_vector3FieldMethod :
                    typeof( ValueT ) == typeof( int ) ?
                      s_intFieldMethod :
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
                                               InspectorGUI.MakeLabel( wrapper.Member),
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
            result.OnChange<ValueT>( instance.OverrideValue, newValue );
        }
      }

      return result;
    }

    private struct OptionalOverrideValueResult
    {
      public bool UseOverrideToggleChanged;
      public bool UseOverride;

      public bool[] ValuesChanged;
      public object[] Values;

      public void OnChange<ValueT>( object oldValueObject, object newValueObject )
        where ValueT : struct
      {
        if ( typeof( ValueT ) == typeof( float ) ) {
          ValuesChanged = new bool[] { true };
          Values = new object[] { newValueObject };
        }
        else if ( typeof( ValueT ) == typeof( int ) ) {
          ValuesChanged = new bool[] { true };
          Values = new object[] { newValueObject };
        }
        else if ( typeof( ValueT ) == typeof( Vector3 ) ) {
          var oldValue = (Vector3)oldValueObject;
          var newValue = (Vector3)newValueObject;
          ValuesChanged = new bool[] { false, false, false };
          Values = new object[] { newValue.x, newValue.y, newValue.z };
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
          destination.OverrideValue = (float)Values[ 0 ];
      }

      public void PropagateChanges( OptionalOverrideValue<Vector3> destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged.Contains( true ) ) {
          var newValue = new Vector3();
          for ( int i = 0; i < 3; ++i )
            newValue[ i ] = ValuesChanged[ i ] ? (float)Values[ i ] : destination.OverrideValue[ i ];
          destination.OverrideValue = newValue;
        }
      }

      public void PropagateChanges( OptionalOverrideValue<int> destination )
      {
        if ( !ContainsChanges )
          return;

        PropagateChangesT( destination );

        if ( ValuesChanged != null && ValuesChanged[ 0 ] )
          destination.OverrideValue = (int)Values[ 0 ];
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
    private static MethodInfo s_intFieldMethod = null;
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
          EditorGUI.BeginChangeCheck();
          data.Value.Enabled = InspectorGUI.Toggle( GUI.MakeLabel( "Enabled", false, "Whether this excavation mode should be enabled, creating dynamic mass and generating force feedback. " ),
                                                                      data.Value.Enabled );
          data.EnabledChanged = EditorGUI.EndChangeCheck();

          EditorGUI.BeginChangeCheck();
          data.Value.CreateDynamicMassEnabled = InspectorGUI.Toggle( GUI.MakeLabel( "Create Dynamic Mass Enabled", false, "Whether this excavation mode should create dynamic mass. " ),
                                                                      data.Value.CreateDynamicMassEnabled );
          data.CreateDynamicMassEnabledChanged = EditorGUI.EndChangeCheck();

          EditorGUI.BeginChangeCheck();
          data.Value.ForceFeedbackEnabled = InspectorGUI.Toggle( GUI.MakeLabel( "Force Feedback Enabled", false, "Whether this excavation mode should generate force feedback from created aggregates. " ),
                                                                      data.Value.ForceFeedbackEnabled );
          data.ForceFeedbackEnabledChanged = EditorGUI.EndChangeCheck();
        }
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
      var filePicker = wrapper.Member.GetCustomAttribute<StringAsFilePicker>();
      if ( filePicker != null ) {
        string result = wrapper.Get<string>( objects[ 0 ] );
        if ( filePicker.IsFolder )
          InspectorGUI.SelectFolder( InspectorGUI.MakeLabel( wrapper.Member ), wrapper.Get<string>( objects[ 0 ] ), "Select folder", s => result = s );
        else
          InspectorGUI.SelectFile( InspectorGUI.MakeLabel( wrapper.Member ), wrapper.Get<string>( objects[ 0 ] ), wrapper.Get<string>( objects[ 0 ] ), "Select file", s => result = s );
        return result;
      }
      return EditorGUILayout.TextField( InspectorGUI.MakeLabel( wrapper.Member ),
                                        wrapper.Get<string>( objects[ 0 ] ),
                                        InspectorEditor.Skin.TextField );
    }

    [InspectorDrawer( typeof( agx.Angle.Axis ) )]
    public static object AxisDrawer( object[] objects, InvokeWrapper wrapper )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      var prev = wrapper.Get<agx.Angle.Axis>( objects[ 0 ] );
      var newState = agx.Angle.Axis.U;


      using ( new GUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( InspectorGUI.MakeLabel( wrapper.Member ) );

        var xSel = prev == agx.Angle.Axis.U;
        var ySel = prev == agx.Angle.Axis.V;
        var zSel = prev == agx.Angle.Axis.N;

        var skin = InspectorGUISkin.Instance;

        var buttonWidth = 20;

        if ( GUILayout.Toggle( xSel,
                               GUI.MakeLabel( "X", xSel ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Left ),
                               GUILayout.Width( buttonWidth ) ) != xSel )
          newState = agx.Angle.Axis.U;
        if ( GUILayout.Toggle( ySel,
                               GUI.MakeLabel( "Y", ySel ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Middle ),
                               GUILayout.Width( buttonWidth ) ) != ySel )
          newState = agx.Angle.Axis.V;
        if ( GUILayout.Toggle( zSel,
                               GUI.MakeLabel( "Z", zSel ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Right ),
                               GUILayout.Width( buttonWidth ) ) != zSel )
          newState = agx.Angle.Axis.N;
      }

      return newState;
    }

    [InspectorDrawer( typeof( agx.Angle.Type ) )]
    public static object AngleTypeDrawer( object[] objects, InvokeWrapper wrapper )
    {
      if ( !wrapper.GetContainingType().IsVisible )
        return null;

      var prev = wrapper.Get<agx.Angle.Type>( objects[ 0 ] );
      var newState = agx.Angle.Type.ROTATIONAL;


      using ( new GUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( InspectorGUI.MakeLabel( wrapper.Member ) );

        var rotSel = prev == agx.Angle.Type.ROTATIONAL;
        var transSel = prev == agx.Angle.Type.TRANSLATIONAL;

        var skin = InspectorGUISkin.Instance;

        var buttonWidth = 30;

        if ( GUILayout.Toggle( rotSel,
                               GUI.MakeLabel( GUI.Symbols.CircleArrowAcw.ToString(), rotSel, "Control rotational DOF around the selected axis." ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Left ),
                               GUILayout.Width( buttonWidth ) ) != rotSel )
          newState = agx.Angle.Type.ROTATIONAL;
        if ( GUILayout.Toggle( transSel,
                               GUI.MakeLabel( GUI.Symbols.ArrowRight.ToString(), transSel, "Control translational DOF around the selected axis." ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Right ),
                               GUILayout.Width( buttonWidth ) ) != transSel )
          newState = agx.Angle.Type.TRANSLATIONAL;
      }

      return newState;
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

    private static void RenderLidarRayAngleGaussianNose( LidarRayAngleGaussianNoise noise )
    {
      using var _ = new GUILayout.HorizontalScope();
      GUILayout.FlexibleSpace();
      noise.Enable = EditorGUILayout.Toggle( noise.Enable, GUILayout.Width( 25 ) );

      using var enabledScope = new EditorGUI.DisabledScope(!noise.Enable);
      var axis = noise.DistortionAxis;
      var skin = InspectorEditor.Skin;
      var xAxis = !EditorGUI.showMixedValue && axis == agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_X;
      var yAxis = !EditorGUI.showMixedValue && axis == agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Y;
      var zAxis = !EditorGUI.showMixedValue && axis == agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Z;

      if ( GUILayout.Toggle( xAxis, GUI.MakeLabel( "X", xAxis, "Apply distortion around X-axis." ),
                              skin.GetButton( InspectorGUISkin.ButtonType.Left ),
                              GUILayout.Width( 20 ) ) != xAxis )
        noise.DistortionAxis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_X;

      if ( GUILayout.Toggle( yAxis, GUI.MakeLabel( "Y", yAxis, "Apply distortion around Y-axis." ),
                              skin.GetButton( InspectorGUISkin.ButtonType.Middle ),
                              GUILayout.Width( 20 ) ) != yAxis )
        noise.DistortionAxis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Y;

      if ( GUILayout.Toggle( zAxis, GUI.MakeLabel( "Z", zAxis, "Apply distortion around Z-axis." ),
                              skin.GetButton( InspectorGUISkin.ButtonType.Right ),
                              GUILayout.Width( 20 ) ) != zAxis )
        noise.DistortionAxis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Z;
      GUILayout.Space( 5 );

      var preWidth = EditorGUIUtility.labelWidth;
      EditorGUIUtility.labelWidth = 25;

      noise.Mean =
        EditorGUILayout.FloatField(
          GUI.MakeLabel(
            "<b>\u03BC</b>",
            toolTip: "The mean of the gaussian distribution of the ray angle noise applied." ),
          noise.Mean );

      GUILayout.Space( 5 );

      noise.StandardDeviation =
        Mathf.Max(
          0.0f,
          EditorGUILayout.FloatField(
            GUI.MakeLabel(
              "<b>\u03C3</b>",
              toolTip: "The standard deviation of the gaussian distribution of the ray angle noise applied." ),
            noise.StandardDeviation ) );

      EditorGUIUtility.labelWidth = preWidth;
    }

    [InspectorDrawer( typeof( AGXUnity.Sensor.QOS ) )]
    public static object QOSDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var qos = wrapper.Get<AGXUnity.Sensor.QOS>( objects[0] );
      var target = objects[0] as Object;
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ), InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        using var indent = new InspectorGUI.IndentScope();
        using ( new InspectorGUI.MixedValueScope( objects.Any( o => wrapper.Get<QOS>( o ).durabilityPolicy != qos.durabilityPolicy ) ) )
          qos.durabilityPolicy = (agxROS2.QOS_DURABILITY)EditorGUILayout.EnumPopup( "Durability Policy", qos.durabilityPolicy );
        using ( new InspectorGUI.MixedValueScope( objects.Any( o => wrapper.Get<QOS>( o ).reliabilityPolicy != qos.reliabilityPolicy ) ) )
          qos.reliabilityPolicy = (agxROS2.QOS_RELIABILITY)EditorGUILayout.EnumPopup( "Reliability Policy", qos.reliabilityPolicy );
        using ( new InspectorGUI.MixedValueScope( objects.Any( o => wrapper.Get<QOS>( o ).historyPolicy != qos.historyPolicy ) ) )
          qos.historyPolicy = (agxROS2.QOS_HISTORY)EditorGUILayout.EnumPopup( "History Policy", qos.historyPolicy );
        using ( new InspectorGUI.MixedValueScope( objects.Any( o => wrapper.Get<QOS>( o ).historyDepth != qos.historyDepth ) ) )
          qos.historyDepth = (uint)EditorGUILayout.IntField( "History Depth", (int)qos.historyDepth );
      }

      return qos;
    }

    [InspectorDrawer( typeof( List<AGXUnity.Sensor.LidarRayAngleGaussianNoise> ) )]
    public static object LidarRayAngleGaussianNoiseDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var skin = InspectorEditor.Skin;
      var buttonLayout = new GUILayoutOption[]
      {
        GUILayout.Width( 1.0f * EditorGUIUtility.singleLineHeight ),
        GUILayout.Height( 1.0f * EditorGUIUtility.singleLineHeight )
      };
      var target = objects[0] as Object;
      LidarRayAngleGaussianNoise insertElementBefore = null;
      LidarRayAngleGaussianNoise insertElementAfter = null;
      LidarRayAngleGaussianNoise eraseElement = null;
      var data = wrapper.Get<List<AGXUnity.Sensor.LidarRayAngleGaussianNoise>>( objects[0] );

      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
                                 InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        foreach ( var noise in data ) {
          using ( new GUILayout.HorizontalScope() ) {
            RenderLidarRayAngleGaussianNose( noise );

            GUILayout.Space( 5 );

            if ( InspectorGUI.Button( MiscIcon.EntryInsertBefore,
                                      true,
                                      "Insert new element before this.",
                                      buttonLayout ) )
              insertElementBefore = noise;
            if ( InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                      true,
                                      "Insert new element after this.",
                                      buttonLayout ) )
              insertElementAfter = noise;
            if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                      true,
                                      "Remove this element.",
                                      buttonLayout ) )
              eraseElement = noise;
          }

        }

        InspectorGUI.Separator( 1.0f, 0.5f * EditorGUIUtility.singleLineHeight );

        if ( data.Count == 0 )
          GUILayout.Label( GUI.MakeLabel( "Empty", true ), skin.Label );

        bool addElementToList = false;
        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.FlexibleSpace();
          addElementToList = InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                                  true,
                                                  "Add new element.",
                                                  buttonLayout );
        }

        LidarRayAngleGaussianNoise newObject = null;
        if ( addElementToList || insertElementBefore != null || insertElementAfter != null )
          newObject = new LidarRayAngleGaussianNoise();

        if ( eraseElement != null )
          data.Remove( eraseElement );
        else if ( newObject != null ) {
          if ( addElementToList || ( data.Count > 0 && insertElementAfter != null && insertElementAfter == data[ data.Count - 1 ] ) )
            data.Add( newObject );
          else if ( insertElementAfter != null )
            data.Insert( data.IndexOf( insertElementAfter ) + 1, newObject );
          else if ( insertElementBefore != null )
            data.Insert( data.IndexOf( insertElementBefore ), newObject );
        }
      }
      return null;
    }

    [InspectorDrawer( typeof( List<string> ) )]
    public static object StringListDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var list = wrapper.Get<List<string>>( objects[ 0 ] );
      var target = objects[ 0 ] as Object;

      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
                                 InspectorGUI.MakeLabel( wrapper.Member ) ) ) {
        int insertElementBefore = -1;
        int insertElementAfter = -1;
        int eraseElement = -1;
        var skin = InspectorEditor.Skin;
        var buttonLayout = new GUILayoutOption[]
        {
          GUILayout.Width( 1.0f * EditorGUIUtility.singleLineHeight ),
          GUILayout.Height( 1.0f * EditorGUIUtility.singleLineHeight )
        };
        for ( int i = 0; i < list.Count; i++ ) {
          using ( InspectorGUI.IndentScope.Single ) {
            GUILayout.BeginHorizontal();
            {
              InspectorGUI.Separator( 1.0f, EditorGUIUtility.singleLineHeight );

              if ( InspectorGUI.Button( MiscIcon.EntryInsertBefore,
                                        true,
                                        "Insert new element before this.",
                                        buttonLayout ) )
                insertElementBefore = i;
              if ( InspectorGUI.Button( MiscIcon.EntryInsertAfter,
                                        true,
                                        "Insert new element after this.",
                                        buttonLayout ) )
                insertElementAfter = i;
              if ( InspectorGUI.Button( MiscIcon.EntryRemove,
                                        true,
                                        "Remove this element.",
                                        buttonLayout ) )
                eraseElement = i;
            }
            GUILayout.EndHorizontal();

            list[ i ] = EditorGUILayout.TextField( list[ i ] );
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

        string newObject = null;
        if ( addElementToList || insertElementBefore != -1 || insertElementAfter != -1 )
          newObject = "";

        if ( eraseElement != -1 )
          list.RemoveAt( eraseElement );
        else if ( newObject != null ) {
          if ( addElementToList || ( list.Count > 0 && insertElementAfter == list.Count - 1 ) )
            list.Add( newObject );
          else if ( insertElementAfter != -1 )
            list.Insert( insertElementAfter + 1, newObject );
          else if ( insertElementBefore != -1 )
            list.Insert( insertElementBefore, newObject );
        }

        if ( eraseElement != -1 || newObject != null )
          EditorUtility.SetDirty( target );
      }

      // A bit of a hack until I figure out how to handle multi-selection
      // of lists, if that should be possible at all. We're handling the
      // list from inside this drawer and by returning null the return
      // value isn't propagated to any targets.
      return null;
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

      if ( recursiveEditing && wrapper.AreValuesEqual( objects ) ) {
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
          if ( !InspectorEditor.ShouldBeShownInInspector( property.Member, new object[] { element } ) )
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

    private static GUIContent FindGUIContentFor( Type parentType, string memberName, string postText = "" )
    {
      var member = parentType.GetMember( memberName )[ 0 ];
      return InspectorGUI.MakeLabel( member, postText );
    }

    public static void DrawOusterModelData( AGXUnity.Sensor.OusterData data )
    {
      using var _ = new GUI.EnabledBlock( UnityEngine.GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode );
      data.ChannelCount = (agxSensor.LidarModelOusterOS.ChannelCount)EditorGUILayout.EnumPopup( FindGUIContentFor( data.GetType(), nameof( data.ChannelCount ) ), data.ChannelCount );
      data.BeamSpacing  = (agxSensor.LidarModelOusterOS.BeamSpacing)EditorGUILayout.EnumPopup( FindGUIContentFor( data.GetType(), nameof( data.BeamSpacing ) ), data.BeamSpacing );
      data.LidarMode    = (agxSensor.LidarModelOusterOS.LidarMode)EditorGUILayout.EnumPopup( FindGUIContentFor( data.GetType(), nameof( data.LidarMode ) ), data.LidarMode );
    }

    public static void DrawGenericSweepModelData( AGXUnity.Sensor.GenericSweepData data )
    {
      bool isRuntime = EditorApplication.isPlayingOrWillChangePlaymode;
      using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && !isRuntime ) ) {
        data.Frequency     = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.Frequency ) ), data.Frequency );
        data.FoVMode       = (GenericSweepData.FoVModes)EditorGUILayout.EnumPopup( FindGUIContentFor( data.GetType(), nameof( data.FoVMode ) ), data.FoVMode );
        if ( data.FoVMode == GenericSweepData.FoVModes.Centered ) {
          data.HorizontalFoV = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.HorizontalFoV ) ), data.HorizontalFoV );
          data.VerticalFoV   = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.VerticalFoV ) ), data.VerticalFoV );
        }
        else if ( data.FoVMode == GenericSweepData.FoVModes.Window ) {
          var newWindow = InspectorGUI.RangeRealField( FindGUIContentFor( data.GetType(), nameof( data.HorizontalFoVWindow ) ), data.HorizontalFoVWindow );
          if ( newWindow.MaxChanged || newWindow.MinChanged )
            data.HorizontalFoVWindow = new RangeReal( newWindow.Min, newWindow.Max );
          newWindow = InspectorGUI.RangeRealField( FindGUIContentFor( data.GetType(), nameof( data.VerticalFoVWindow ) ), data.VerticalFoVWindow );
          if ( newWindow.MaxChanged || newWindow.MinChanged )
            data.VerticalFoVWindow = new RangeReal( newWindow.Min, newWindow.Max );
        }
        data.ResolutionMode = (GenericSweepData.ResolutionModes)EditorGUILayout.EnumPopup( FindGUIContentFor( data.GetType(), nameof( data.ResolutionMode ) ), data.ResolutionMode );
        if ( data.ResolutionMode == GenericSweepData.ResolutionModes.DegreesPerPoint ) {
          data.HorizontalResolution = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.HorizontalResolution ) ), data.HorizontalResolution );
          data.VerticalResolution   = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.VerticalResolution ) ), data.VerticalResolution );
        }
        else if ( data.ResolutionMode == GenericSweepData.ResolutionModes.TotalPoints ) {
          data.HorizontalResolutionTotal = EditorGUILayout.IntField( FindGUIContentFor( data.GetType(), nameof( data.HorizontalResolutionTotal ) ), data.HorizontalResolutionTotal );
          data.VerticalResolutionTotal   = EditorGUILayout.IntField( FindGUIContentFor( data.GetType(), nameof( data.VerticalResolutionTotal ) ), data.VerticalResolutionTotal );
        }

      }
      var result = InspectorGUI.RangeRealField( FindGUIContentFor( data.GetType(), nameof( data.Range ) ), data.Range );
      if ( result.MaxChanged || result.MinChanged )
        data.Range = new RangeReal( result.Min, result.Max );
      data.BeamDivergence = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.BeamDivergence ) ), data.BeamDivergence );
      data.BeamExitRadius = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.BeamExitRadius ) ), data.BeamExitRadius );
    }

    public static void DrawReadFromFileModelData( AGXUnity.Sensor.ReadFromFileData data )
    {
      data.Frequency     = Mathf.Max( 1, EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "Frequency" ), data.Frequency ) );
      data.FrameSize     = (uint)Mathf.Max( 1, EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "FrameSize" ), data.FrameSize ) );
      data.FilePath      = EditorGUILayout.TextField( FindGUIContentFor( data.GetType(), "FilePath" ), data.FilePath );
      data.TwoColumns    = EditorGUILayout.Toggle( FindGUIContentFor( data.GetType(), "TwoColumns" ), data.TwoColumns );
      data.AnglesInDegrees = EditorGUILayout.Toggle( FindGUIContentFor( data.GetType(), "AnglesInDegrees" ), data.AnglesInDegrees );
      data.FirstLineIsHeader   = EditorGUILayout.Toggle( FindGUIContentFor( data.GetType(), "FirstLineIsHeader" ), data.FirstLineIsHeader );
      var delimiterText = EditorGUILayout.TextField( FindGUIContentFor(data.GetType(), "Delimiter"),  data.Delimiter.ToString() );
      if ( char.TryParse( delimiterText, out var delimiter ) )
        data.Delimiter = delimiter;
      var result = InspectorGUI.RangeRealField( FindGUIContentFor( data.GetType(), nameof( data.Range ) ), data.Range );
      if ( result.MaxChanged || result.MinChanged )
        data.Range = new RangeReal( result.Min, result.Max );
      data.BeamDivergence = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.BeamDivergence ) ), data.BeamDivergence );
      data.BeamExitRadius = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), nameof( data.BeamExitRadius ) ), data.BeamExitRadius );
    }

    public static void DrawLivoxModelData( AGXUnity.Sensor.LivoxData data )
    {
      data.Downsample = (uint)EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "Downsample" ), data.Downsample );
    }

    [InspectorDrawer( typeof( AGXUnity.Sensor.IModelData ) )]
    public static object ModelDataDrawer( object[] objects, InvokeWrapper wrapper )
    {
      if ( objects.Length != 1 ) {
        InspectorGUI.WarningLabel( "Multi-select of ModelData Elements isn't supported." );
        return null;
      }

      var data = wrapper.Get<AGXUnity.Sensor.IModelData>( objects[0] );
      using ( new InspectorGUI.IndentScope() ) {
        switch ( data ) {
          case AGXUnity.Sensor.OusterData ousterData:
            DrawOusterModelData( ousterData );
            break;
          case AGXUnity.Sensor.GenericSweepData sweepData:
            DrawGenericSweepModelData( sweepData );
            break;
          case AGXUnity.Sensor.LivoxData livoxData:
            DrawLivoxModelData( livoxData );
            break;
          case AGXUnity.Sensor.ReadFromFileData readFromFileData:
            DrawReadFromFileModelData( readFromFileData );
            break;
        }
      }

      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.Sensor.LidarDistanceGaussianNoise ) )]
    public static object LidarDistanceGaussianNoiseDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var data = wrapper.Get<AGXUnity.Sensor.LidarDistanceGaussianNoise>( objects[0] );
      data.Enable = EditorGUILayout.Toggle( FindGUIContentFor( data.GetType(), "Enable", " Distance Gaussian Noise" ), data.Enable );
      if ( !data.Enable )
        return null;

      using ( new InspectorGUI.IndentScope() ) {
        data.Mean                   = EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "Mean" ), data.Mean );
        data.StandardDeviationBase  = Mathf.Max( EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "StandardDeviationBase" ), data.StandardDeviationBase ), 0.0f );
        data.StandardDeviationSlope = Mathf.Max( EditorGUILayout.FloatField( FindGUIContentFor( data.GetType(), "StandardDeviationSlope" ), data.StandardDeviationSlope ), 0.0f );
      }

      return null;
    }

    [InspectorDrawer( typeof( AGXUnity.Sensor.ImuAttachment ) )]
    public static object ImuAttachmentDrawer( object[] objects, InvokeWrapper wrapper )
    {
      var target = objects[ 0 ] as Object;

      if ( objects.Length != 1 ) {
        InspectorGUI.WarningLabel( "Multi-select of ImuAttachment Elements isn't supported." );
        return null;
      }

      var data = wrapper.Get<AGXUnity.Sensor.ImuAttachment>( objects[0] );
      using ( new InspectorGUI.IndentScope() ) {
        data.TriaxialRange = TriaxialRangeDataGUI( data.TriaxialRange );
        data.CrossAxisSensitivity = EditorGUILayout.FloatField( "Cross Axis Sensitivity", data.CrossAxisSensitivity );
        data.ZeroBias = EditorGUILayout.Vector3Field( "Zero Rate Bias", data.ZeroBias );
        EditorGUI.BeginChangeCheck();
        data.OutputFlags = OutputXYZGUI( data.OutputFlags );
        if ( EditorGUI.EndChangeCheck() )
          EditorUtility.SetDirty( target );

        if ( InspectorGUI.Foldout( EditorData.Instance.GetData( target, wrapper.Member.Name ),
            GUI.MakeLabel( "Modifiers", true, "Optional signal output modifiers" ) ) ) {
          using ( new InspectorGUI.IndentScope() ) {
            (data.EnableTotalGaussianNoise, data.TotalGaussianNoise) = OptionalVector3GUI(
              data.EnableTotalGaussianNoise,
              data.TotalGaussianNoise,
              "Total Gaussian Noise",
              "" );
            (data.EnableSignalScaling, data.SignalScaling) = OptionalVector3GUI(
              data.EnableSignalScaling,
              data.SignalScaling,
              "Signal Scaling",
              "" );
            (data.EnableGaussianSpectralNoise, data.GaussianSpectralNoise) = OptionalVector3GUI(
              data.EnableGaussianSpectralNoise,
              data.GaussianSpectralNoise,
              "Gaussian Spectral Noise",
              "" );
            if ( data.Type == ImuAttachment.ImuAttachmentType.Gyroscope ) {
              (data.EnableLinearAccelerationEffects, data.LinearAccelerationEffects) = OptionalVector3GUI(
              data.EnableLinearAccelerationEffects,
              data.LinearAccelerationEffects,
              "Linear Acceleration Effects",
              "" );
            }
          }
        }
      }

      InspectorGUI.Separator();

      return null;
    }

    private static (bool, Vector3) OptionalVector3GUI( bool toggle, Vector3 value, string label, string tooltip )
    {
      using ( new GUILayout.HorizontalScope() ) {
        var rect = EditorGUILayout.GetControlRect();
        var xMaxOriginal = rect.xMax;
        rect.xMax = EditorGUIUtility.labelWidth + 20;
        //InspectorGUI.MakeLabel( wrapper.Member );
        toggle = EditorGUI.ToggleLeft( rect, GUI.MakeLabel( label, false, tooltip ), toggle );
        using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && toggle ) ) {
          rect.x = rect.xMax - 30;
          rect.xMax = xMaxOriginal;
          value = EditorGUI.Vector3Field( rect, "", value );
        }
      }
      return (toggle, value);
    }

    private static TriaxialRangeData TriaxialRangeDataGUI( TriaxialRangeData data )
    {
      data.Mode = (TriaxialRangeData.ConfigurationMode)EditorGUILayout.EnumPopup( "Sensor Measurement Range", data.Mode );

      using ( new InspectorGUI.IndentScope() ) {
        switch ( data.Mode ) {
          case TriaxialRangeData.ConfigurationMode.MaxRange:
            break;
          case TriaxialRangeData.ConfigurationMode.EqualAxisRanges:
            data.EqualAxesRange = EditorGUILayout.Vector2Field( "XYZ range", data.EqualAxesRange );
            break;
          case TriaxialRangeData.ConfigurationMode.IndividualAxisRanges:
            data.RangeX = EditorGUILayout.Vector2Field( "X axis range", data.RangeX );
            data.RangeY = EditorGUILayout.Vector2Field( "Y axis range", data.RangeY );
            data.RangeZ = EditorGUILayout.Vector2Field( "Z axis range", data.RangeZ );
            break;
        }
      }
      return data;
    }

    private static OutputXYZ OutputXYZGUI( OutputXYZ state )
    {
      var skin = InspectorEditor.Skin;

      using ( new EditorGUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( GUI.MakeLabel( "Output values", false ),
                                      InspectorEditor.Skin.LabelMiddleLeft );

        var xEnabled = state.HasFlag(OutputXYZ.X);
        var yEnabled = state.HasFlag(OutputXYZ.Y);
        var zEnabled = state.HasFlag(OutputXYZ.Z);

        if ( GUILayout.Toggle( xEnabled,
                               GUI.MakeLabel( "X",
                                              xEnabled,
                                              "Use sensor X value in output" ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Left ),
                               GUILayout.Width( 76 ) ) != xEnabled )
          state ^= OutputXYZ.X;
        if ( GUILayout.Toggle( yEnabled,
                               GUI.MakeLabel( "Y",
                                              yEnabled,
                                              "Use sensor X value in output" ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Middle ),
                               GUILayout.Width( 76 ) ) != yEnabled )
          state ^= OutputXYZ.Y;
        if ( GUILayout.Toggle( zEnabled,
                               GUI.MakeLabel( "Z",
                                              yEnabled,
                                              "Use sensor Z value in output" ),
                               skin.GetButton( InspectorGUISkin.ButtonType.Right ),
                               GUILayout.Width( 76 ) ) != zEnabled )
          state ^= OutputXYZ.Z;
      }

      return state;
    }


  }
}

