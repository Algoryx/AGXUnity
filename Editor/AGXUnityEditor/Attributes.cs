using System;

namespace AGXUnityEditor
{
  /// <summary>
  /// Inspector Editor Tool extension attribute. Class with this
  /// attribute may act as tool for given type. When InspectorEditor
  /// is invoked the tool receives the same targets as the editor
  /// and may draw inspector GUI before and after members are drawn.
  /// <seealso cref="Tools.CustomTargetTool"/>
  /// </summary>
  [AttributeUsage( AttributeTargets.Class,
                   AllowMultiple = false,
                   Inherited = false )]
  public class CustomToolAttribute : Attribute
  {
    /// <summary>
    /// Instance type of the tool is implemented for.
    /// </summary>
    public Type Type = null;

    /// <summary>
    /// Construct given instance type the tool is implemented for.
    /// </summary>
    /// <param name="type">Instance target type.</param>
    public CustomToolAttribute( Type type )
    {
      Type = type;
    }
  }

  /// <summary>
  /// Inspector type drawer attribute for GUI draw methods
  /// handling specific types.
  /// </summary>
  [AttributeUsage( AttributeTargets.Method,
                   AllowMultiple = true,
                   Inherited = false )]
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

    /// <summary>
    /// Set to o true if type is generic and:
    ///     givenType.IsGenericType && givenType.GetGenericTypeDefinition() == this.Type
    /// </summary>
    /// <example>
    /// [InspectorDrawer( typeof( List<> ), IsGeneric = true )]
    /// </example>
    public bool IsGeneric { get; set; } = false;

    /// <summary>
    /// Construct given type the inspector drawer handles.
    /// </summary>
    /// <param name="type">Handling type of the inspector drawer.</param>
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
      return ( type == Type ) ||
             ( AssignableFrom && Type.IsAssignableFrom( type ) ) ||
             ( IsBaseType && type.BaseType == Type ) ||
             ( IsGeneric && type.IsGenericType && type.GetGenericTypeDefinition() == Type );
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

    /// <summary>
    /// True if drawer implements a copy operator for nullable types.
    /// Name of copy method is method of this attribute + CopyOp.
    /// </summary>
    public bool HasCopyOp { get; set; } = false;
  }
}
