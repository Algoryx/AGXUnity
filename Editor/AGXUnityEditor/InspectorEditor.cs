using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static GUISkin Skin = null;

    public static void DrawMembersGUI( Object[] objects )
    {
      if ( objects.Length == 0 )
        return;

      Undo.RecordObjects( objects, "Inspector" );

      var hasChanges = false;
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects[ 0 ] );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        hasChanges = HandleType( wrapper, objects ) || hasChanges;
      }

      if ( hasChanges ) {
        foreach ( var obj in objects )
          EditorUtility.SetDirty( obj );
      }
    }

    public bool IsMultiSelect { get { return targets.Length > 1; } }

    public sealed override void OnInspectorGUI()
    {
      ToolManager.OnPreTargetMembers( target, this );

      DrawMembersGUI( targets );

      ToolManager.OnPostTargetMembers( target, this );
    }

    private void OnEnable()
    {
      if ( Skin == null ) {
        Skin = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
        Skin.label.richText = true;
        Skin.toggle.richText = true;
        Skin.button.richText = true;
        Skin.textArea.richText = true;
        Skin.textField.richText = true;

        if ( EditorGUIUtility.isProSkin )
          Skin.label.normal.textColor = 204.0f / 255.0f * Color.white;
      }

      // Entire class/component marked as hidden - enable "hide in inspector".
      if ( target.GetType().GetCustomAttributes( typeof( HideInInspector ), false ).Length > 0 )
        target.hideFlags |= HideFlags.HideInInspector;

      ToolManager.OnTargetEditorEnable( target );
    }

    private void OnDisable()
    {
      ToolManager.OnTargetEditorDisable( target );
    }

    private static bool HandleType( InvokeWrapper wrapper, Object[] objects )
    {
      if ( !wrapper.CanRead() )
        return false;

      var drawerInfo = InspectorGUI.GetDrawerMethod( wrapper.GetContainingType() );
      if ( !drawerInfo.IsValid )
        return false;

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( objects );

      var value   = drawerInfo.Drawer.Invoke( null, new object[] { wrapper, Skin } );
      var changed = UnityEngine.GUI.changed &&
                    ( drawerInfo.IsNullable || value != null );

      EditorGUI.showMixedValue = false;

      if ( !changed )
        return false;

      foreach ( var obj in objects ) {
        object newValue = value;
        if ( drawerInfo.CopyOp != null ) {
          newValue = wrapper.GetValue( obj );
          drawerInfo.CopyOp.Invoke( null, new object[] { value, newValue } );
        }
        wrapper.ConditionalSet( obj, newValue );
      }

      return true;
    }

    private static bool ShouldBeShownInInspector( MemberInfo memberInfo )
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
  }
}
