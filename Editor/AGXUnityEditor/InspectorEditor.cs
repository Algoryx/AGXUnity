using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    private static GUISkin m_skin = null;

    public static GUISkin Skin
    {
      get
      {
        if ( m_skin == null ) {
          m_skin                    = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
          m_skin.label.richText     = true;
          m_skin.toggle.richText    = true;
          m_skin.button.richText    = true;
          m_skin.textArea.richText  = true;
          m_skin.textField.richText = true;

          if ( EditorGUIUtility.isProSkin )
            m_skin.label.normal.textColor = 204.0f / 255.0f * Color.white;
        }
        return m_skin;
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
    public static void DrawMembersGUI( Object[] targets, Func<Object, object> getChildCallback = null )
    {
      targets = targets.Where( obj => obj != null ).ToArray();

      if ( targets.Length == 0 )
        return;

      var objects = targets.Select( target => getChildCallback == null ?
                                      target :
                                      getChildCallback( target ) )
                           .Where( obj => obj != null ).ToArray();
      if ( objects.Length == 0 )
        return;

      Undo.RecordObjects( targets, "Inspector" );

      var hasChanges = false;
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects[ 0 ].GetType() );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        hasChanges = HandleType( wrapper, objects ) || hasChanges;
      }

      if ( hasChanges ) {
        foreach ( var obj in targets )
          EditorUtility.SetDirty( obj );
      }
    }

    public static bool ShouldBeShownInInspector( MemberInfo memberInfo )
    {
      if ( memberInfo == null )
        return false;

      // Override hidden in inspector.
      if ( memberInfo.IsDefined( typeof( HideInInspector ), true ) )
        return false;

      // In general, don't show UnityEngine objects unless ShowInInspector is set.
      bool show = memberInfo.IsDefined( typeof( ShowInInspector ), true ) ||
                  !( memberInfo.DeclaringType.Namespace != null &&
                     memberInfo.DeclaringType.Namespace.Contains( "UnityEngine" ) );

      return show;
    }

    public sealed override void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( this.targets, Event.current ) )
        return;

      GUILayout.BeginVertical();

      ToolManager.OnPreTargetMembers( this.targets );

      DrawMembersGUI( this.targets );

      ToolManager.OnPostTargetMembers( this.targets );

      GUILayout.EndHorizontal();
    }

    private void OnEnable()
    {
      if ( this.target == null )
        return;

      // Entire class/component marked as hidden - enable "hide in inspector".
      if ( this.target.GetType().GetCustomAttributes( typeof( HideInInspector ), false ).Length > 0 )
        this.target.hideFlags |= HideFlags.HideInInspector;

      ToolManager.OnTargetEditorEnable( this.targets );
    }

    private void OnDisable()
    {
      ToolManager.OnTargetEditorDisable( this.targets );
    }

    public static bool HandleType( InvokeWrapper wrapper, object[] objects )
    {
      if ( !wrapper.CanRead() )
        return false;

      var drawerInfo = InspectorGUI.GetDrawerMethod( wrapper.GetContainingType() );
      if ( !drawerInfo.IsValid )
        return false;

      if ( wrapper.HasAttribute<InspectorSeparatorAttribute>() )
        Utils.GUI.Separator();

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( objects );

      var value   = drawerInfo.Drawer.Invoke( null, new object[] { objects[ 0 ], wrapper, Skin } );
      var changed = UnityEngine.GUI.changed &&
                    ( drawerInfo.IsNullable || value != null );

      // Reset changed state so that non-edited values
      // are propagated to other properties.
      UnityEngine.GUI.changed = false;

      EditorGUI.showMixedValue = false;

      if ( !changed )
        return false;

      foreach ( var obj in objects ) {
        object newValue = value;
        if ( drawerInfo.CopyOp != null ) {
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
