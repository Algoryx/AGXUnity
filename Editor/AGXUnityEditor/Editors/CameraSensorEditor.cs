using System.Reflection;
using UnityEditor;

public static class SerializedPropertyExtensions
{
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
