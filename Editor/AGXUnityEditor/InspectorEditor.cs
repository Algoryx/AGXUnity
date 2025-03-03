﻿using AGXUnity;
using AGXUnity.Utils;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static InspectorGUISkin Skin
    {
      get
      {
        return InspectorGUISkin.Instance;
      }
    }

    /// <summary>
    /// Create editor to be rendered form within another editor, e.g.,
    /// dropdown showing content with tools of another object.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <returns>Editor instance.</returns>
    public static Editor CreateRecursive( Object target )
    {
      var editor = CreateEditor( target );
      if ( editor is InspectorEditor )
        ( editor as InspectorEditor ).IsMainEditor = false;
      return editor;
    }

    /// <summary>
    /// True to force repaint of all InspectorEditor editors.
    /// </summary>
    public static bool RequestConstantRepaint = false;

    /// <summary>
    /// Draw supported member GUI for given targets. This method supports
    /// non-UnityEngine.Object instances, such as pure Serializable classes,
    /// that are part of <paramref name="targets"/>. <paramref name="getChildCallback"/>
    /// is called to access these serializable objects. If <paramref name="getChildCallback"/>
    /// is null, targets will be rendered.
    /// </summary>
    /// <param name="targets">Target UnityEngine.Object instances (used for Undo and SetDirty).</param>
    /// <param name="getChildCallback">Null and targets will be rendered, otherwise the returned
    ///                                instance from this callback.</param>
    public static void DrawMembersGUI( Object[] targets,
                                       Func<Object, object> getChildCallback = null,
                                       SerializedObject fallback = null )
    {
      targets = targets.Where( obj => obj != null ).ToArray();

      using ( new GUI.EnabledBlock( targets.All( o => ( o.hideFlags & HideFlags.NotEditable ) == 0 ) ) ) {
        if ( targets.Length == 0 )
          return;

        var objects = targets.Select( target => getChildCallback == null ?
                                        target :
                                        getChildCallback( target ) )
                             .Where( obj => obj != null ).ToArray();
        if ( objects.Length == 0 )
          return;

        Undo.RecordObjects( targets, "Inspector" );

        InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects[ 0 ].GetType() );
        var group = InspectorGroupHandler.Create();
        foreach ( var wrapper in fieldsAndProperties ) {
          if ( !ShouldBeShownInInspector( wrapper.Member, objects ) )
            continue;

          group.Update( wrapper, objects[ 0 ] );

          if ( group.IsHidden )
            continue;

          var runtimeDisabled = EditorApplication.isPlayingOrWillChangePlaymode &&
                                wrapper.Member.IsDefined( typeof( DisableInRuntimeInspectorAttribute ), true );
          using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && !runtimeDisabled ) )
            HandleType( wrapper, objects, fallback );
        }
        group.Dispose();
      }
    }

    /// <summary>
    /// Draw supported member GUI for given targets. This method supports
    /// non-UnityEngine.Object instances, such as pure Serializable classes,
    /// that are part of <paramref name="targets"/>. <paramref name="getChildCallback"/>
    /// is called to access these serializable objects. If <paramref name="getChildCallback"/>
    /// is null, targets will be rendered.
    /// </summary>
    /// <param name="targets">Target UnityEngine.Object instances (used for Undo and SetDirty).</param>
    /// <param name="getChildCallback">Null and targets will be rendered, otherwise the returned
    ///                                instance from this callback.</param>
    public static void DrawMembersGUI( object[] targets,
                                       Object[] undoObjects,
                                       Func<object, object> getChildCallback = null,
                                       bool enabled = true )
    {
      targets = targets.Where( obj => obj != null ).ToArray();

      var objects = targets.Select( target => getChildCallback == null ?
                                        target :
                                        getChildCallback( target ) )
                             .Where( obj => obj != null ).ToArray();
      if ( objects.Length == 0 )
        return;

      using ( new GUI.EnabledBlock( enabled ) ) {
        Undo.RecordObjects( undoObjects, "Inspector" );

        InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects[ 0 ].GetType() );
        var group = InspectorGroupHandler.Create();
        foreach ( var wrapper in fieldsAndProperties ) {
          if ( !ShouldBeShownInInspector( wrapper.Member, objects ) )
            continue;

          group.Update( wrapper, objects[ 0 ] );

          if ( group.IsHidden )
            continue;

          var runtimeDisabled = EditorApplication.isPlayingOrWillChangePlaymode &&
                                wrapper.Member.IsDefined( typeof( DisableInRuntimeInspectorAttribute ), true );
          using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && !runtimeDisabled ) )
            HandleType( wrapper, objects, null );
        }
        group.Dispose();
      }
    }

    public static bool ShouldBeShownInInspector( MemberInfo memberInfo, object[] targets )
    {
      if ( memberInfo == null )
        return false;

      // Override hidden in inspector.
      var runtimeHide = EditorApplication.isPlayingOrWillChangePlaymode &&
                        memberInfo.IsDefined( typeof( HideInRuntimeInspectorAttribute ), true );

      if ( memberInfo.IsDefined( typeof( HideInInspector ), true ) || runtimeHide )
        return false;

      if ( targets != null && memberInfo.IsDefined( typeof( DynamicallyShowInInspector ), true ) ) {
        var t = memberInfo.DeclaringType;
        var showInfo = memberInfo.GetCustomAttribute<DynamicallyShowInInspector>();
        var bindings =  BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic;
        if ( showInfo.IsMethod )
          bindings |=   BindingFlags.InvokeMethod;
        else
          bindings |=   BindingFlags.GetField |
                        BindingFlags.GetProperty;

        var members = t.GetMember( showInfo.Name, bindings );
        if ( members.Length == 0 ) {
          Debug.LogWarning( $"No member '{showInfo.Name}' found to determine dynamic inspector status for member '{memberInfo.Name}', skipping" );
          return false;
        }
        else if ( members.Length > 1 ) {
          Debug.LogWarning( $"Multiple members '{showInfo.Name}' found to determine dynamic inspector status for member '{memberInfo.Name}', skipping" );
          return false;
        }

        var member = members[ 0 ];
        if ( member.MemberType == MemberTypes.Method ) {
          var method = (MethodInfo)member;
          if ( method.GetParameters().Length != 0 ) {
            Debug.LogWarning( $"Method '{method.Name}', used to dynamically show '{memberInfo.Name}', requires parameters, this is not supported, skipping" );
            return false;
          }
          if ( method.ContainsGenericParameters ) {
            Debug.LogWarning( $"Method '{method.Name}', used to dynamically show '{memberInfo.Name}', requires type parameters, sthis is not supported, skipping" );
            return false;
          }
          if ( !method.IsStatic )
            return targets.All( t => (bool)method.Invoke( t, new object[] { } ) );
          return (bool)method.Invoke( null, new object[] { } );
        }
        else if ( member.MemberType == MemberTypes.Property ) {
          var property = (PropertyInfo)member;

          return targets.All( t => (bool)property.GetValue( t ) );
        }
      }

      // In general, don't show UnityEngine objects unless ShowInInspector is set.
      bool show = memberInfo.IsDefined( typeof( ShowInInspector ), true ) ||
                  !( memberInfo.DeclaringType.Namespace != null &&
                     memberInfo.DeclaringType.Namespace.Contains( "UnityEngine" ) );

      return show;
    }

    /// <summary>
    /// True if this editor is main in Inspector, i.e., not rendered
    /// inside another editor.
    /// </summary>
    public bool IsMainEditor { get; private set; } = true;

    public sealed override void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( this.targets, Event.current ) )
        return;

      if ( IsMainEditor && !typeof( ScriptableObject ).IsAssignableFrom( target.GetType() ) ) {
        InspectorGUI.BrandSeparator();
      }

      GUILayout.BeginVertical();

      EditorGUI.BeginChangeCheck();

      ToolManager.OnPreTargetMembers( this.targets );

      DrawMembersGUI( this.targets, null, serializedObject );

      ToolManager.OnPostTargetMembers( this.targets );

      // If any changes occured during the editor draw we have to tell unity that the component has changes.
      // Additionally, some components (such as the Constraint component) modifies other components on the same GameObject.
      // In this case all compoments have to be manually dirtied. Here, we blanket dirty all compoments on the same GameObject
      // as the currently edited component.
      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var t in this.targets ) {
          EditorUtility.SetDirty( t );
          if ( t is MonoBehaviour root )
            foreach ( var comp in root.GetComponents<MonoBehaviour>() )
              EditorUtility.SetDirty( comp );
        }
      }

      GUILayout.EndVertical();
    }

    public override bool RequiresConstantRepaint()
    {
      return base.RequiresConstantRepaint() || RequestConstantRepaint;
    }

    protected virtual void OnTargetsDeleted() { }

    private Type m_targetType = null;
    private GameObject[] m_targetGameObjects = null;
    private int m_numTargetGameObjectsTargetComponents = 0;

    private void OnEnable()
    {
      if ( this.target == null )
        return;

      m_targetType = this.target.GetType();
      m_targetGameObjects = this.targets.Where( obj => obj is Component )
                                        .Select( obj => ( obj as Component ).gameObject ).ToArray();
      m_numTargetGameObjectsTargetComponents = m_targetGameObjects.Sum( go => go.GetComponents( m_targetType ).Length );

      // Entire class/component marked as hidden - enable "hide in inspector".
      // NOTE: This will break Inspector rendering in 2022.1 and later because changing
      //       hideFlags here results in a destroy of all editors and the editors that
      //       should be visible are enabled again but never rendered by Unity.
      // SOLUTION: Add hideFlags |= HideFlags.HideInInspector in Reset method of the class
      //           that shouldn't be rendered in the Inspector.
      // NOTE 2: The above solution does not work when importing prefabs as the act of 
      //         saving a GameObject to a prefab clears the hideFlags.
      // SOLUTION: Set the hideFlags on affected components when adding a prefab to the scene
      //           in AGXUnityEditor.AssetPostprocessorHandler.OnAGXPrefabAdddedToScene
      if ( this.targets.Any( t => !t.hideFlags.HasFlag( HideFlags.HideInInspector ) ) && m_targetType.GetCustomAttribute<HideInInspector>( false ) != null ) {
        foreach ( var t in this.targets )
          t.hideFlags |= HideFlags.HideInInspector;
      }

      ToolManager.OnTargetEditorEnable( this.targets, this );

      var ctx = ContextManager.GetCustomContextForType(m_targetType);
      if ( ctx != null )
        UnityEditor.EditorTools.ToolManager.SetActiveContext( ctx );
    }

    private void OnDisable()
    {
      if ( this.target == null ) {
        if ( IsTargetMostProbablyDeleted() ) {
          Manager.OnEditorTargetsDeleted();
          OnTargetsDeleted();
        }
      }

      ToolManager.OnTargetEditorDisable( this.targets );

      m_targetType = null;
      m_targetGameObjects = null;
      m_numTargetGameObjectsTargetComponents = 0;

      UnityEditor.EditorTools.ToolManager.SetActiveContext( null );
    }

    private bool IsTargetMostProbablyDeleted()
    {
      // Nothing selected so the user probably hit 'Delete'.
      if ( Selection.activeGameObject == null || m_targetGameObjects == null )
        return true;

      // Any of the previously (OnEnable) collected game objects
      // is "null".
      if ( m_targetGameObjects.Any( go => go == null ) )
        return true;

      var targetComponents = m_targetGameObjects.SelectMany( go => go.GetComponents( m_targetType ) )
                                                .Select( component => component ).ToArray();
      // Current number of components doesn't match the amount of
      // components collected in OnEnable.
      return targetComponents.Length != m_numTargetGameObjectsTargetComponents;
    }

    private static bool HandleType( InvokeWrapper wrapper,
                                    object[] objects,
                                    SerializedObject fallback )
    {
      if ( !wrapper.CanRead() )
        return false;

      var drawerInfo = InvokeWrapperInspectorDrawer.GetDrawerMethod( wrapper.GetContainingType() );

      if ( wrapper.HasAttribute<InspectorSeparatorAttribute>() )
        InspectorGUI.BrandSeparator();

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( objects );

      object value = null;
      bool changed = false;
      if ( drawerInfo.IsValid ) {
        EditorGUI.BeginChangeCheck();
        value   = drawerInfo.Drawer.Invoke( null, new object[] { objects, wrapper } );
        changed = EditorGUI.EndChangeCheck() &&
                  ( drawerInfo.IsNullable || value != null );
      }
      // Fallback to Unity types rendered with property drawers.
      else if ( fallback != null && !wrapper.GetContainingType().FullName.StartsWith( "AGXUnity." ) ) {
        var serializedProperty = fallback.FindProperty( wrapper.Member.Name );

        // This is currently only tested on:
        //     private InputActionAssert m_inputAsset = null;
        //     public InputActionAsset InputAsset { get ... set ... }
        //     public InputActionMap InputMap = null;
        // And serializedProperty.objectReferenceValue prints error:
        //     type is not a supported pptr value
        // for 'InputMap' when changed (not assigned, just manipulated).
        // When we catch the 'm_inputAsset' we may do objectReferenceValue and
        // can propagate the value to the C# property.
        var assignSupported = false;
        if ( serializedProperty == null && wrapper.Member.Name.Length > 2 ) {
          var fieldName = "m_" + char.ToLower( wrapper.Member.Name[ 0 ] ) + wrapper.Member.Name.Substring( 1 );
          serializedProperty = fallback.FindProperty( fieldName );
          assignSupported = serializedProperty != null;
        }

        if ( serializedProperty != null ) {
          EditorGUI.BeginChangeCheck();
          EditorGUILayout.PropertyField( serializedProperty );
          if ( serializedProperty.isArray )
            Debug.LogWarning( "The AGXUnity Inspector wrapper currently does not support editable array types. Consider using List<T> as an alternative." );
          if ( EditorGUI.EndChangeCheck() && assignSupported ) {
            changed = true;
            value = serializedProperty.boxedValue;
          }
        }
      }

      EditorGUI.showMixedValue = false;

      if ( !changed )
        return false;

      foreach ( var obj in objects ) {
        object newValue = value;
        if ( drawerInfo.IsValid && drawerInfo.CopyOp != null ) {
          newValue = wrapper.GetValue( obj );
          // CopyOp returns the new value for value types.
          var ret = drawerInfo.CopyOp.Invoke( null, new object[] { value, newValue } );
          if ( ret != null )
            newValue = ret;
        }
        wrapper.ConditionalSet( obj, newValue );
      }

      return true;
    }
  }
}
