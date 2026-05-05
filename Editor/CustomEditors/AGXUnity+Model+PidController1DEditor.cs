using AGXUnity.Model;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( PidController1D ) )]
  [CanEditMultipleObjects]
  public class AGXUnityModelPidController1DEditor : InspectorEditor
  { }

  [CustomPropertyDrawer( typeof( PidController1D.ComponentFloatProperty ) )]
  public class ComponentFloatPropertyDrawer : PropertyDrawer
  {
    private const float Spacing = 2f;

    public override float GetPropertyHeight( SerializedProperty property, GUIContent label )
    {
      return 2f * EditorGUIUtility.singleLineHeight + Spacing;
    }

    public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
    {
      var targetProp = property.FindPropertyRelative( "Target" );
      var memberProp = property.FindPropertyRelative( "MemberName" );

      float lineH    = EditorGUIUtility.singleLineHeight;
      var targetRect = new Rect( position.x, position.y, position.width, lineH );
      var memberRect = new Rect( position.x, position.y + lineH + Spacing, position.width, lineH );

      EditorGUI.BeginChangeCheck();
      EditorGUI.PropertyField( targetRect, targetProp, new GUIContent( "Target" ) );
      if ( EditorGUI.EndChangeCheck() )
        memberProp.stringValue = string.Empty;

      var target = targetProp.objectReferenceValue as Component;
      if ( target == null ) {
        EditorGUI.BeginDisabledGroup( true );
        EditorGUI.Popup( memberRect, "Member", 0, new[] { "— select Target first —" } );
        EditorGUI.EndDisabledGroup();
        return;
      }

      var members = GetWritableFloatMembers( target.GetType() );
      if ( members.Length == 0 ) {
        EditorGUI.BeginDisabledGroup( true );
        EditorGUI.Popup( memberRect, "Member", 0, new[] { "— no writable float members —" } );
        EditorGUI.EndDisabledGroup();
        return;
      }

      var names        = members.Select( m => m.Name ).ToArray();
      int currentIndex = Array.IndexOf( names, memberProp.stringValue );
      if ( currentIndex < 0 )
        currentIndex = 0;

      int newIndex = EditorGUI.Popup( memberRect, "Member", currentIndex, names );
      memberProp.stringValue = names[ newIndex ];
    }

    private static MemberInfo[] GetWritableFloatMembers( Type type )
    {
      const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
      var props  = type.GetProperties( flags )
                       .Where( p => p.CanWrite && p.PropertyType == typeof( float ) )
                       .Cast<MemberInfo>();
      var fields = type.GetFields( flags )
                       .Where( f => f.FieldType == typeof( float ) )
                       .Cast<MemberInfo>();
      return props.Concat( fields ).OrderBy( m => m.Name ).ToArray();
    }
  }
}
