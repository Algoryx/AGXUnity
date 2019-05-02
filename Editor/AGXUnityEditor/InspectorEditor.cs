using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static GUISkin Skin = null;

    private struct Result
    {
      public bool Changed;
      public object Value;
    }

    private void OnEnable()
    {
      if ( Skin == null ) {
        Skin                    = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
        Skin.label.richText     = true;
        Skin.toggle.richText    = true;
        Skin.button.richText    = true;
        Skin.textArea.richText  = true;
        Skin.textField.richText = true;

        if ( EditorGUIUtility.isProSkin )
          Skin.label.normal.textColor = 204.0f / 255.0f * Color.white;
      }

      // Entire class/component marked as hidden - enable "hide in inspector".
      if ( target.GetType().GetCustomAttributes( typeof( HideInInspector ), false ).Length > 0 )
        target.hideFlags |= HideFlags.HideInInspector;

      if ( targets.Length == 1 )
        ToolManager.OnTargetEditorEnable( target );
    }

    private void OnDisable()
    {
      ToolManager.OnTargetEditorDisable( target );
    }

    public sealed override void OnInspectorGUI()
    {
      // Disable tool when doing multi-edit until they can indicate
      // support for it.
      if ( targets.Length > 1 && ToolManager.FindActive( target ) != null )
        ToolManager.OnTargetEditorDisable( target );

      ToolManager.OnPreTargetMembers( target, Skin );

      DrawMembersGUI( target );

      ToolManager.OnPostTargetMembers( target, Skin );
    }

    private void DrawMembersGUI( object obj )
    {
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( obj );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        var result = HandleType( wrapper );
        if ( !result.Changed )
          continue;

        Undo.RecordObjects( targets, "Modification: " + wrapper.Member.Name.SplitCamelCase() );
        foreach ( var targetObject in targets ) {
          wrapper.ConditionalSet( targetObject, result.Value );
          EditorUtility.SetDirty( targetObject );
        }
      }
    }

    private Result HandleType( InvokeWrapper wrapper )
    {
      var result = new Result() { Changed = false, Value = null };
      if ( !wrapper.CanRead() )
        return result;
      var isNullable = false;
      var drawer     = InspectorGUI.GetDrawerMethod( wrapper.GetContainingType(),
                                                     out isNullable );
      if ( drawer == null )
        return result;

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( targets );

      result.Value   = drawer.Invoke( null, new object[] { wrapper, Skin } );
      result.Changed = UnityEngine.GUI.changed &&
                       ( isNullable || result.Value != null );

      EditorGUI.showMixedValue = false;

      return result;
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
