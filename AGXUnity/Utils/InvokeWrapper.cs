using System;
using System.Linq;
using System.Reflection;

namespace AGXUnity.Utils
{
  public abstract class InvokeWrapper
  {
    public static InvokeWrapper[] FindFieldsAndProperties( object obj,
                                                           BindingFlags fieldBindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                                                           BindingFlags propertyBindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      if ( obj == null )
        return new InvokeWrapper[] { };

      return FindFieldsAndProperties( obj, obj.GetType(), fieldBindFlags, propertyBindFlags );
    }

    public static InvokeWrapper[] FindFieldsAndProperties( object obj,
                                                           Type type,
                                                           BindingFlags fieldBindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                                                           BindingFlags propertyBindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      FieldWrapper[] fields = FieldWrapper.FindFields( obj, type, fieldBindFlags );
      PropertyWrapper[] properties = PropertyWrapper.FindProperties( obj, type, propertyBindFlags );
      return fields.Concat<InvokeWrapper>( properties ).ToArray();
    }

    protected InvokeWrapper( object obj, MemberInfo member )
    {
      Object = obj;
      Member = member;
    }

    public object Object { get; set; }

    public MemberInfo Member { get; private set; }

    public U GetAttribute<U>() where U : Attribute
    {
      object[] attributes = Member.GetCustomAttributes( typeof( U ), false );
      return attributes.Length > 0 ? attributes[ 0 ] as U : null;
    }

    public bool HasAttribute<U>() where U : Attribute
    {
      return GetAttribute<U>() != null;
    }

    public bool IsValid( object value )
    {
      object[] clampAboveZeroAttributes = Member.GetCustomAttributes( typeof( ClampAboveZeroInInspector ), false );
      if ( clampAboveZeroAttributes.Length > 0 )
        return ( clampAboveZeroAttributes[ 0 ] as ClampAboveZeroInInspector ).IsValid( value );
      return true;
    }

    /// <summary>
    /// Return true if type parameter matches type of the field/property.
    /// </summary>
    /// <typeparam name="U">Type.</typeparam>
    /// <returns>True if type matches the field/property type.</returns>
    public bool IsType<U>() { return typeof( U ) == GetContainingType(); }

    /// <summary>
    /// Returns the current value given any object of matching type.
    /// </summary>
    /// <remarks>
    /// If <paramref name="obj"/> isn't the object with this field/property
    /// an exception will be thrown by the reflection core.
    /// </remarks>
    /// <typeparam name="U">Return type.</typeparam>
    /// <param name="obj">Object with this field/property. Valid if null if this field/property is static.</param>
    /// <returns>The value.</returns>
    public U Get<U>( object obj )
    {
      var prevObj = Object;
      Object      = obj;
      U value     = Get<U>();
      Object      = prevObj;
      return value;
    }

    public object GetValue( object obj )
    {
      var prevObj = Object;
      Object = obj;
      var value = GetValue();
      Object = prevObj;
      return value;
    }

    /// <summary>
    /// Invoke set method in <paramref name="obj"/> given value.
    /// </summary>
    /// <param name="obj">Object with this field/property.</param>
    /// <param name="value">Value.</param>
    /// <returns>True if value was assigned.</returns>
    public bool ConditionalSet( object obj, object value )
    {
      var prevObj = Object;
      Object = obj;
      bool ret = ConditionalSet( value );
      Object = prevObj;
      return ret;
    }

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
    /// Get current value. Note that this will throw if e.g., a property only
    /// has a setter.
    /// </summary>
    /// <typeparam name="U">Type, e.g., bool, Vector3, etc..</typeparam>
    /// <returns>Current value.</returns>
    public abstract U Get<U>();

    /// <summary>
    /// Get current value without explicit type information.
    /// </summary>
    /// <returns>Current value.</returns>
    public abstract object GetValue();

    /// <summary>
    /// Invoke set method if exist and the input is valid.
    /// </summary>
    /// <param name="value">Value to set.</param>
    /// <returns>true if set method was called with new value.</returns>
    public abstract bool ConditionalSet( object value );

    /// <summary>
    /// Checks Equals for each instance and returns true if all Equals
    /// calls return true - otherwise false.
    /// </summary>
    /// <param name="instances">Array of instances.</param>
    /// <returns>true if all values are equal - otherwise false.</returns>
    public abstract bool AreValuesEqual( object[] instances );
  }

  /// <summary>
  /// Wrapper class for editable fields.
  /// </summary>
  public class FieldWrapper : InvokeWrapper
  {
    public static FieldWrapper[] FindFields( object obj, BindingFlags bindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      if ( obj == null )
        return new FieldWrapper[] { };
      return FindFields( obj, obj.GetType(), bindFlags );
    }

    public static FieldWrapper[] FindFields( object obj, Type type, BindingFlags bindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      return ( from fieldInfo in type.GetFields( bindFlags ) select new FieldWrapper( obj, fieldInfo ) ).ToArray();
    }

    public FieldInfo Field { get { return (FieldInfo)Member; } }

    public FieldWrapper( object obj, FieldInfo fieldInfo )
      : base( obj, fieldInfo )
    {
    }

    public override bool IsStatic()
    {
      return Field.IsStatic;
    }

    public override Type GetContainingType()
    {
      return Field.FieldType;
    }

    public override U Get<U>()
    {
      return Object == null && !IsStatic() ?
               default( U ) :
               (U)Field.GetValue( Object );
    }

    public override object GetValue()
    {
      return Object == null && !IsStatic() ?
               null :
               Field.GetValue( Object );
    }

    public override bool ConditionalSet( object value )
    {
      if ( Field.IsLiteral || !IsValid( value ) || ( Object == null && !IsStatic() ) )
        return false;
      Field.SetValue( Object, value );
      return true;
    }

    public override bool AreValuesEqual( object[] instances )
    {
      if ( instances.Length <= 1 )
        return true;

      var refValue = Field.GetValue( instances[ 0 ] );
      if ( refValue == null ) {
        for ( int i = 1; i < instances.Length; ++i )
          if ( Field.GetValue( instances[ i ] ) != null )
            return false;
      }
      else {
        for ( int i = 1; i < instances.Length; ++i )
          if ( !refValue.Equals( Field.GetValue( instances[ i ] ) ) )
            return false;
      }

      return true;
    }
  }

  /// <summary>
  /// Wrapper class for editable properties.
  /// </summary>
  public class PropertyWrapper : InvokeWrapper
  {
    public static PropertyWrapper[] FindProperties( object obj, BindingFlags bindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      if ( obj == null )
        return new PropertyWrapper[] { };

      return FindProperties( obj, obj.GetType(), bindFlags );
    }

    public static PropertyWrapper[] FindProperties( object obj, Type type, BindingFlags bindFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static )
    {
      return ( from propertyInfo in type.GetProperties( bindFlags ) select new PropertyWrapper( obj, propertyInfo ) ).ToArray();
    }

    public PropertyInfo Property { get { return (PropertyInfo)Member; } }

    public PropertyWrapper( object obj, PropertyInfo propertyInfo )
      : base( obj, propertyInfo )
    {
    }

    public override bool IsStatic()
    {
      return CanRead() && Property.GetGetMethod().IsStatic;
    }

    public override bool CanRead()
    {
      return Property.GetGetMethod() != null;
    }

    public override bool CanWrite()
    {
      return Property.GetSetMethod() != null;
    }

    public override Type GetContainingType()
    {
      return Property.PropertyType;
    }

    public override U Get<U>()
    {
      return Object == null && !IsStatic() ?
               default( U ) :
               (U)Property.GetValue( Object, null );
    }

    public override object GetValue()
    {
      return Object == null && !IsStatic() ?
               null :
               Property.GetValue( Object, null );
    }

    public override bool ConditionalSet( object value )
    {
      if ( Property.GetSetMethod() == null || !IsValid( value ) || ( Object == null && !IsStatic() ) )
        return false;
      Property.SetValue( Object, value, null );
      return true;
    }

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
  }
}
