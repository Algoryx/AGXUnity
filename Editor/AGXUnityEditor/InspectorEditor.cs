using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    private void OnEnable()
    {
    }

    private void OnDisable()
    {
    }

    public sealed override void OnInspectorGUI()
    {
      foreach ( var obj in targets )
        Debug.Log( obj.GetType().ToString() );
      Debug.Log( "" );
    }

    private void DrawMembersGUI( object target )
    {
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( target );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        object value = null;
        if ( wrapper.GetContainingType() == typeof( Vector3 ) && wrapper.CanRead() ) {
          Vector3 valInField = wrapper.Get<Vector3>();
          GUILayout.BeginHorizontal();
          {
            GUILayout.Label( GUI.MakeLabel( wrapper.Member.Name ) );
            value = EditorGUILayout.Vector3Field( "", valInField );
          }
          GUILayout.EndHorizontal();
        }

        if ( UnityEngine.GUI.changed && value != null )
          wrapper.ConditionalSet( value );
      }
    }

    public static GUIContent MakeLabel( MemberInfo field )
    {
      GUIContent guiContent = new GUIContent();

      guiContent.text = field.Name.SplitCamelCase();
      object[] descriptions = field.GetCustomAttributes( typeof( DescriptionAttribute ), true );
      if ( descriptions.Length > 0 )
        guiContent.tooltip = ( descriptions[ 0 ] as DescriptionAttribute ).Description;

      return guiContent;
    }
  }
}
