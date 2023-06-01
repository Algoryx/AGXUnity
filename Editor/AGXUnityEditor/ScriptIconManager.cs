using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  class ScriptIconManager
  {
    static ScriptIconManager()
    {
      foreach ( var t in Assembly.GetAssembly( typeof( AGXUnity.ScriptComponent ) ).DefinedTypes ) {
        if ( t.IsSubclassOf( typeof( AGXUnity.ScriptComponent ) ) )
          SetGizmoIconEnabled( t.AsType(), false );
      }
    }

    // Script adapted based on: https://answers.unity.com/questions/851470/how-to-hide-gizmos-by-script.html
    private static void SetGizmoIconEnabled( Type type, bool on )
    {
      var annotations = Assembly.GetAssembly(typeof(Editor))?.GetType("UnityEditor.AnnotationUtility");
      const int MONO_BEHAVIOR_CLASS_ID = 114; // https://docs.unity3d.com/Manual/ClassIDReference.html

      if ( type != null && annotations != null ) {
        MethodInfo getAnnotations = annotations.GetMethod("GetAnnotations", BindingFlags.Static | BindingFlags.NonPublic);
        if ( getAnnotations == null ) Debug.Log( "Cannot find annotation method" );

        bool hasAnnotation = false;
        var allA = getAnnotations.Invoke(null, null);
        foreach ( object annotation in (System.Collections.IEnumerable)allA ) {
          Type annotationType = annotation.GetType();
          FieldInfo scriptClassField = annotationType.GetField("scriptClass", BindingFlags.Public | BindingFlags.Instance);
          if ( (string)( scriptClassField.GetValue( annotation ) ) == type.Name ) 
            hasAnnotation = true;
        }
        if ( !hasAnnotation ) return;

        MethodInfo setGizmoEnabled = annotations.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo setIconEnabled = annotations.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

        if ( setIconEnabled == null ) return;
        setIconEnabled.Invoke( null, new object[] { MONO_BEHAVIOR_CLASS_ID, type.Name, on ? 1 : 0 } );
        setGizmoEnabled.Invoke( null, new object[] { MONO_BEHAVIOR_CLASS_ID, type.Name, on ? 1 : 0, false } );
      }
    }
  }
}
