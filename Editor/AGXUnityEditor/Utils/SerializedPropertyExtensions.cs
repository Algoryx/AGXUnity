using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace AGXUnityEditor.Utils
{
  public static class SerializedPropertyExtensions
  {
    public static IEnumerable<SerializedProperty> FindChildren( this SerializedObject so )
    {
      var next = so.GetIterator();

      next.NextVisible( true );
      var hasNext = next.NextVisible( false );
      while ( hasNext ) {
        yield return next;
        hasNext = next.NextVisible( false );
      }
    }

    public static IEnumerable<SerializedProperty> FindChildren( this SerializedProperty property )
    {
      var cur = property.Copy();
      if ( cur.hasVisibleChildren ) {
        var next = property.Copy();
        next.NextVisible( false );

        var hasNext = cur.NextVisible( true ) && !SerializedProperty.EqualContents( cur, next );
        while ( hasNext ) {
          yield return cur;
          hasNext = cur.NextVisible( false ) && !SerializedProperty.EqualContents( cur, next );
        }
      }
    }

    public static SerializedProperty GetParent( this SerializedProperty property )
    {
      string path = property.propertyPath;

      int lastDot = path.LastIndexOf('.');
      if ( lastDot < 0 )
        return null; // Root-level property has no SerializedProperty parent

      string parentPath = path.Substring(0, lastDot);
      return property.serializedObject.FindProperty( parentPath );
    }

    public static FieldInfo GetFieldInfo( this SerializedProperty property )
    {
      System.Type type = property.serializedObject.targetObject.GetType();

      // Unity paths look like:
      // foo.bar.Array.data[0].baz
      string path = property.propertyPath
            .Replace(".Array.data[", "[");

      string[] parts = path.Split('.');

      FieldInfo field = null;

      foreach ( string part in parts ) {
        string fieldName = part;
        int bracketIndex = part.IndexOf('[');

        if ( bracketIndex >= 0 )
          fieldName = part.Substring( 0, bracketIndex );

        field = GetField( type, fieldName );

        if ( field == null )
          return null;

        type = field.FieldType;

        // If this segment indexes a collection, descend into its element type.
        if ( bracketIndex >= 0 ) {
          if ( type.IsArray ) {
            type = type.GetElementType();
          }
          else if ( type.IsGenericType ) {
            type = type.GetGenericArguments()[ 0 ];
          }
        }
      }

      return field;
    }

    private static FieldInfo GetField( System.Type type, string name )
    {
      while ( type != null ) {
        FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

        if ( field != null )
          return field;

        type = type.BaseType;
      }

      return null;
    }
  }
}
