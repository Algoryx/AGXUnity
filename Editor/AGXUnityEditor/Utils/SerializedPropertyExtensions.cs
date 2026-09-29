using System.Collections.Generic;
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
  }
}
