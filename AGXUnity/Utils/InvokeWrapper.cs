using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Abstraction of Field- and PropertyInfo enabling manipulation
  /// independently of if the member is a field or a property.
  /// </summary>
  public abstract class InvokeWrapper
  {
    public const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// Collects fields and properties of a given type.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <param name="fieldBindingFlags">Fields binding flags.</param>
    /// <param name="propertyBindingFlags">Properties binding flags.</param>
    /// <returns>Array of field/property wrappers.</returns>
    public static InvokeWrapper[] FindFieldsAndProperties<T>( BindingFlags fieldBindingFlags = DefaultBindingFlags,
                                                              BindingFlags propertyBindingFlags = DefaultBindingFlags )
      where T : class
    {
      return FindFieldsAndProperties( typeof( T ), fieldBindingFlags, propertyBindingFlags );
    }

    /// <summary>
    /// Collects fields and properties of a given type.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="fieldBindingFlags">Field binding flags.</param>
    /// <param name="propertyBindingFlags">Properties binding flags.</param>
    /// <returns>Array of field/property wrappers.</returns>
    public static InvokeWrapper[] FindFieldsAndProperties( Type type,
                                                           BindingFlags fieldBindingFlags = DefaultBindingFlags,
                                                           BindingFlags propertyBindingFlags = DefaultBindingFlags )
    {
      var isDefaultBindings = fieldBindingFlags == DefaultBindingFlags &&
                              propertyBindingFlags == DefaultBindingFlags;
      if ( isDefaultBindings &&
           s_defaultBindingFlagsFieldsPropertiesCache.TryGetValue( type, out var fieldsAndPropertiesCache ) )
        return fieldsAndPropertiesCache;

      var fields              = FieldWrapper.FindFields( type, fieldBindingFlags );
      var properties          = PropertyWrapper.FindProperties( type, propertyBindingFlags );
      var fieldsAndProperties = fields.Concat<InvokeWrapper>( properties ).OrderByDescending( wrapper => wrapper.Priority ).ToArray();
      if ( isDefaultBindings )
        s_defaultBindingFlagsFieldsPropertiesCache.Add( type, fieldsAndProperties );
      return fieldsAndProperties;
    }

    /// <summary>
    /// Construct given member (field or property).
    /// </summary>
    /// <param name="member">Member info of given field or property.</param>
    protected InvokeWrapper( MemberInfo member )
    {
      Member                = member;
      var priorityAttribute = GetAttribute<InspectorPriorityAttribute>();
      Priority              = priorityAttribute != null ?
                                priorityAttribute.Priority :
                                0;
    }

    /// <summary>
    /// Member info of either field or property.
    /// </summary>
    public MemberInfo Member { get; private set; }

    /// <summary>
    /// Priority of this member. Higher values are earlier in the resulting array.
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// Find attribute of given type.
    /// </summary>
    /// <typeparam name="U">Attribute type.</typeparam>
    /// <param name="inherit">Include inherited attributes.</param>
    /// <returns>Attribute if exists - otherwise null.</returns>
    public U GetAttribute<U>( bool inherit = false )
      where U : Attribute
    {
      return Member.GetCustomAttribute<U>( inherit );
    }

    /// <summary>
    /// Find attributes of given type.
    /// </summary>
    /// <typeparam name="U">Attribute type.</typeparam>
    /// <param name="inherit">Include inherited attributes.</param>
    /// <returns>Attributes of given type.</returns>
    public U[] GetAttributes<U>( bool inherit = false )
      where U : Attribute
    {
      return Member.GetCustomAttributes<U>( inherit ).ToArray();
    }

    /// <summary>
    /// Check if attribute exist on this member.
    /// </summary>
    /// <typeparam name="U">Attribute type.</typeparam>
    /// <returns>True if attribute exist, otherwise false.</returns>
    public bool HasAttribute<U>() where U : Attribute
    {
      return GetAttribute<U>() != null;
    }

    /// <summary>
    /// Checks if given value is valid for this member.
    /// </summary>
    /// <param name="value">New value to test.</param>
    /// <returns>True if the value is valid, otherwise false.</returns>
    public bool IsValid( object value )
    {
      // We haven't any accept-null-or-not attributes so for
      // now null is valid.
      if ( value == null )
        return true;

      var clampAttribute = GetAttribute<ClampAboveZeroInInspector>();
      return clampAttribute == null || clampAttribute.IsValid( value );
    }

    /// <summary>
    /// Return true if type parameter matches type of the field/property.
    /// </summary>
    /// <typeparam name="U">Type.</typeparam>
    /// <returns>True if type matches the field/property type.</returns>
    public bool IsType<U>() { return typeof( U ) == GetContainingType(); }

    /// <summary>
    /// Checks if the property can read values, i.e., has a getter. This is
    /// always true for public fields.
    /// </summary>
    /// <returns></returns>
    public virtual bool CanRead() { return true; }

    /// <summary>
    /// Checks if the property can write values, i.e., has a setter. This
    /// is always true for public fields.
    /// </summary>
    /// <returns></returns>
    public virtual bool CanWrite() { return true; }

    /// <summary>
    /// Returns the current value given any object of matching type.
    /// </summary>
    /// <remarks>
    /// If <paramref name="obj"/> isn't the object with this field/property
    /// an exception will be thrown by the reflection core.
    /// </remarks>
    /// <typeparam name="U">Return type.</typeparam>
    /// <param name="obj">Object with this field/property. Valid if null when the field/property is static.</param>
    /// <returns>The value.</returns>
    public abstract U Get<U>( object obj );

    /// <summary>
    /// Returns boxed object value given object instance.
    /// </summary>
    /// <param name="obj">Object instance. Valid if null when the field/property is static.</param>
    /// <returns>Boxed value.</returns>
    public abstract object GetValue( object obj );

    /// <summary>
    /// Invoke set method in <paramref name="obj"/> given value.
    /// </summary>
    /// <param name="obj">Object with this field/property.</param>
    /// <param name="value">Value.</param>
    /// <returns>True if value was assigned.</returns>
    public abstract bool ConditionalSet( object obj, object value );

    /// <summary>
    /// Returns true if the field or property is static and can handle Object == null.
    /// </summary>
    /// <returns>True if field or property is declared static.</returns>
    public abstract bool IsStatic();

    /// <summary>
    /// Returns the type of the field or property.
    /// </summary>
    /// <returns>Type of the field of property.</returns>
    public abstract Type GetContainingType();

    /// <summary>
    /// Checks Equals for each instance and returns true if all Equals
    /// calls return true - otherwise false.
    /// </summary>
    /// <param name="instances">Array of instances.</param>
    /// <returns>true if all values are equal - otherwise false.</returns>
    public abstract bool AreValuesEqual( object[] instances );

    private static Dictionary<Type, InvokeWrapper[]> s_defaultBindingFlagsFieldsPropertiesCache = new Dictionary<Type, InvokeWrapper[]>();
  }

  /// <summary>
  /// Wrapper class for editable fields.
  /// </summary>
  public class FieldWrapper : InvokeWrapper
  {
    /// <summary>
    /// Finds fields given type and binding flags.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <param name="bindingFlags">Binding flags.</param>
    /// <returns>Priority sorted array (higher priority earlier in array).</returns>
    public static FieldWrapper[] FindFilds<T>( BindingFlags bindingFlags = DefaultBindingFlags )
      where T : class
    {
      return FindFields( typeof( T ), bindingFlags );
    }

    /// <summary>
    /// Finds fields given type and binding flags.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="bindingFlags">Binding flags.</param>
    /// <returns>Priority sorted array (higher priority earlier in array).</returns>
    public static FieldWrapper[] FindFields( Type type, BindingFlags bindingFlags = DefaultBindingFlags )
    {
      if ( bindingFlags == DefaultBindingFlags && s_defaultBindingsCache.TryGetValue( type, out var cachedFields ) )
        return cachedFields;
      var fields = ( from fieldInfo
                     in type.GetFields( bindingFlags )
                     select new FieldWrapper( fieldInfo ) ).OrderByDescending( wrapper => wrapper.Priority ).ToArray();
      if ( bindingFlags == DefaultBindingFlags )
        s_defaultBindingsCache.Add( type, fields );
      return fields;
    }

    /// <summary>
    /// Member field info.
    /// </summary>
    public FieldInfo Field { get { return (FieldInfo)Member; } }

    /// <summary>
    /// Construct given field info.
    /// </summary>
    /// <param name="fieldInfo">Field info.</param>
    public FieldWrapper( FieldInfo fieldInfo )
      : base( fieldInfo )
    {
    }

    /// <returns>True if static - otherwise false.</returns>
    public override bool IsStatic()
    {
      return Field.IsStatic;
    }

    /// <returns>Type of this field.</returns>
    public override Type GetContainingType()
    {
      return Field.FieldType;
    }

    /// <summary>
    /// Finds current value of this field given instance. This
    /// method will throw exception if the types aren't matching.
    /// </summary>
    /// <typeparam name="U">Field type.</typeparam>
    /// <param name="obj">Object which type contains this field.</param>
    /// <returns>Current value of this field, default( U ) if obj == null and non-static field.</returns>
    public override U Get<U>( object obj )
    {
      return obj == null && !IsStatic() ?
               default( U ) :
               (U)Field.GetValue( obj );
    }

    /// <summary>
    /// Finds current boxed value of this field given instance.
    /// </summary>
    /// <param name="obj">Object which type contains this field.</param>
    /// <returns>Current value if valid - otherwise null.</returns>
    public override object GetValue( object obj )
    {
      return obj == null && !IsStatic() ?
               null :
               Field.GetValue( obj );
    }

    /// <summary>
    /// Set value if it's possible to assign value and the
    /// value is valid for this field.
    /// </summary>
    /// <param name="obj">Object which type contains this field (null is valid for static fields).</param>
    /// <param name="value">New value.</param>
    /// <returns>True if the value is set - otherwise false.</returns>
    public override bool ConditionalSet( object obj, object value )
    {
      if ( Field.IsLiteral || !IsValid( value ) || ( obj == null && !IsStatic() ) )
        return false;
      Field.SetValue( obj, value );
      return true;
    }

    /// <summary>
    /// Checks if the list of instances contains values that
    /// equals each other.
    /// </summary>
    /// <param name="instances">Array of instances containing this field.</param>
    /// <returns>True if the values are equal, otherwise false.</returns>
    public override bool AreValuesEqual( object[] instances )
    {
      if ( instances.Length <= 1 )
        return true;

      var refValue = Field.GetValue( instances[ 0 ] );

      // Reference value is null, check if all the rest are null.
      if ( refValue == null ) {
        for ( int i = 1; i < instances.Length; ++i )
          if ( Field.GetValue( instances[ i ] ) != null )
            return false;
      }
      // Using object1.Equals( object2 ) to compare the values.
      else {
        for ( int i = 1; i < instances.Length; ++i )
          if ( !refValue.Equals( Field.GetValue( instances[ i ] ) ) )
            return false;
      }

      return true;
    }

    private static Dictionary<Type, FieldWrapper[]> s_defaultBindingsCache = new Dictionary<Type, FieldWrapper[]>();
  }

  /// <summary>
  /// Wrapper class for editable properties.
  /// </summary>
  public class PropertyWrapper : InvokeWrapper
  {
    /// <summary>
    /// Finds properties given type and binding flags.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <param name="bindingFlags">Binding flags.</param>
    /// <returns>Priority sorted array (higher priority earlier in array).</returns>
    public static PropertyWrapper[] FindProperties<T>( BindingFlags bindingFlags = DefaultBindingFlags )
    {
      return FindProperties( typeof( T ), bindingFlags );
    }

    /// <summary>
    /// Finds properties given type and binding flags.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="bindingFlags">Binding flags.</param>
    /// <returns>Priority sorted array (higher priority earlier in array).</returns>
    public static PropertyWrapper[] FindProperties( Type type, BindingFlags bindingFlags = DefaultBindingFlags )
    {
      if ( bindingFlags == DefaultBindingFlags && s_defaultBindingsCache.TryGetValue( type, out var cachedProperties ) )
        return cachedProperties;
      var properties = ( from propertyInfo
                         in type.GetProperties( bindingFlags )
                         select new PropertyWrapper( propertyInfo ) ).OrderByDescending( wrapper => wrapper.Priority ).ToArray();
      if ( bindingFlags == DefaultBindingFlags )
        s_defaultBindingsCache.Add( type, properties );
      return properties;
    }

    /// <summary>
    /// Member property info.
    /// </summary>
    public PropertyInfo Property { get { return (PropertyInfo)Member; } }

    /// <summary>
    /// Construct given property info.
    /// </summary>
    /// <param name="propertyInfo">Property info.</param>
    public PropertyWrapper( PropertyInfo propertyInfo )
      : base( propertyInfo )
    {
    }

    /// <returns>True if static - otherwise false.</returns>
    public override bool IsStatic()
    {
      return CanRead() && Property.GetGetMethod().IsStatic;
    }

    /// <returns>True if this property has a get method, otherwise false.</returns>
    public override bool CanRead()
    {
      return Property.GetGetMethod() != null;
    }

    /// <returns>True if this property has a set method, otherwise false.</returns>
    public override bool CanWrite()
    {
      return Property.GetSetMethod() != null && Property.GetSetMethod().IsPublic;
    }

    /// <returns>Type of this property.</returns>
    public override Type GetContainingType()
    {
      return Property.PropertyType;
    }

    /// <summary>
    /// Finds current value of this property given instance. This
    /// method will throw exception if the types aren't matching.
    /// </summary>
    /// <typeparam name="U">Property type.</typeparam>
    /// <param name="obj">Object which type contains this property.</param>
    /// <returns>Current value of this property, default( U ) if obj == null and non-static field.</returns>
    public override U Get<U>( object obj )
    {
      return !CanRead() || ( obj == null && !IsStatic() ) ?
               default( U ) :
               (U)Property.GetValue( obj, null );
    }

    /// <summary>
    /// Finds current boxed value of this property given instance.
    /// </summary>
    /// <param name="obj">Object which type contains this property.</param>
    /// <returns>Current value if valid - otherwise null.</returns>
    public override object GetValue( object obj )
    {
      return !CanRead() || ( obj == null && !IsStatic() ) ?
               null :
               Property.GetValue( obj, null );
    }

    /// <summary>
    /// Set value if it's possible to assign value and the
    /// value is valid for this property.
    /// </summary>
    /// <param name="obj">Object which type contains this property (null is valid for static fields).</param>
    /// <param name="value">New value.</param>
    /// <returns>True if the value is set - otherwise false.</returns>
    public override bool ConditionalSet( object obj, object value )
    {
      if ( !CanWrite() || !IsValid( value ) || ( obj == null && !IsStatic() ) )
        return false;
      Property.SetValue( obj, value, null );
      return true;
    }

    /// <summary>
    /// Checks if the list of instances contains values that
    /// equals each other.
    /// </summary>
    /// <param name="instances">Array of instances containing this property.</param>
    /// <returns>True if the values are equal, otherwise false.</returns>
    public override bool AreValuesEqual( object[] instances )
    {
      if ( instances.Length <= 1 )
        return true;

      var refValue = Property.GetValue( instances[ 0 ], null );
      if ( refValue == null ) {
        for ( int i = 1; i < instances.Length; ++i )
          if ( Property.GetValue( instances[ i ], null ) != null )
            return false;
      }
      else {
        for ( int i = 1; i < instances.Length; ++i )
          if ( !refValue.Equals( Property.GetValue( instances[ i ], null ) ) )
            return false;
      }

      return true;
    }

    private static Dictionary<Type, PropertyWrapper[]> s_defaultBindingsCache = new Dictionary<Type, PropertyWrapper[]>();
  }
}
