using System;

namespace AGXUnityEditor
{
  [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
  public class CustomTool : Attribute
  {
    public Type Type = null;

    public CustomTool( Type type )
    {
      Type = type;
    }
  }

  /// <summary>
  /// Inspector type drawer attribute for GUI draw methods
  /// handling specific types.
  /// </summary>
  [AttributeUsage( AttributeTargets.Method, AllowMultiple = true, Inherited = false )]
  public class InspectorDrawerAttribute : Attribute
  {
    /// <summary>
    /// Type the drawing method can handle.
    /// </summary>
    public Type Type { get; private set; }

    /// <summary>
    /// Set to true if drawing method handles this.Type.IsAssignableFrom( givenType ),
    /// i.e., not only explicitly givenType == this.Type.
    /// </summary>
    public bool AssignableFrom { get; set; } = false;

    /// <summary>
    /// Set to true if drawing method handles givenType.BaseType == this.Type.
    /// </summary>
    public bool IsBaseType { get; set; } = false;

    public InspectorDrawerAttribute( Type type )
    {
      Type = type;
    }

    /// <summary>
    /// Match given type with current configuration.
    /// </summary>
    /// <param name="type">Given type.</param>
    /// <returns>True if given type is a match - otherwise false.</returns>
    public bool Match( Type type )
    {
      return type == Type ||
             ( AssignableFrom && Type.IsAssignableFrom( type ) ) ||
             ( IsBaseType && type.BaseType == Type );
    }
  }

  /// <summary>
  /// Rather explicit attribute stating if the result from a drawing
  /// method may be null. If IsNullable == false and the drawing method
  /// returns null, the result is ignored. If IsNullable == true and
  /// the method returns null AND UnityEngine.GUI.changed - null is
  /// propagated.
  /// </summary>
  [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
  public class InspectorDrawerResultAttribute : Attribute
  {
    /// <summary>
    /// True if result from the drawing method may be null, false and
    /// null results will be ignored.
    /// </summary>
    public bool IsNullable { get; set; } = false;

    public bool HasCopyOp { get; set; } = false;
  }
}
