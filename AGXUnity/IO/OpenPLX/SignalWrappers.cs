using openplx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

using Expression = System.Linq.Expressions.Expression;
using Marshalling = openplx.Marshalling;

namespace AGXUnity.IO.OpenPLX
{
  public enum ValueType
  {
    Integer,
    Real,
    Vec3,
    Vec2,
    Boolean,
    Ignored,
    Unknown
  }

  public static class SignalUtils
  {

    public static ValueType GetTypeEnum<U>() => GetTypeEnum( typeof( U ) );
    public static ValueType GetTypeEnum( System.Type type )
    {
      switch ( System.Type.GetTypeCode( type ) ) {
        case TypeCode.Byte: return ValueType.Integer;
        case TypeCode.SByte: return ValueType.Integer;
        case TypeCode.UInt16: return ValueType.Integer;
        case TypeCode.UInt32: return ValueType.Integer;
        case TypeCode.UInt64: return ValueType.Integer;
        case TypeCode.Int16: return ValueType.Integer;
        case TypeCode.Int32: return ValueType.Integer;
        case TypeCode.Int64: return ValueType.Integer;
        case TypeCode.Decimal: return ValueType.Real;
        case TypeCode.Double: return ValueType.Real;
        case TypeCode.Single: return ValueType.Real;
        case TypeCode.Boolean: return ValueType.Boolean;
        default: break;
      }

      if ( type == typeof( Vector3 )
        || type == typeof( agx.Vec3 )
        || type == typeof( agx.Vec3f )
        || type == typeof( openplx.Math.Vec3 ) )
        return ValueType.Vec3;

      if ( type == typeof( Vector2 )
        || type == typeof( agx.Vec2 )
        || type == typeof( agx.Vec2f )
        || type == typeof( openplx.Math.Vec2 ) )
        return ValueType.Vec2;

      return ValueType.Unknown;
    }

    public static ValueType GetOpenPLXTypeEnum( FieldType type )
    {
      switch ( type ) {
        case FieldType.Int: return ValueType.Integer;
        case FieldType.UInt: return ValueType.Integer;
        case FieldType.Bool: return ValueType.Boolean;
        case FieldType.Real: return ValueType.Real;
        default: return ValueType.Unknown;
      }
    }

    public static bool IsValueTypeCompatible<U>( FieldType typeCode, bool toCSType ) => IsValueTypeCompatible( typeof( U ), typeCode, toCSType );
    public static bool IsValueTypeCompatible( System.Type type, FieldType typeCode, bool toCSType )
    {
      var requestedType = GetTypeEnum(type);
      var endpointType = GetOpenPLXTypeEnum(typeCode);

      if ( requestedType == ValueType.Unknown || requestedType == ValueType.Ignored ) {
        Debug.LogWarning( $"The requested type {type.Name} is not supported" );
        return false;
      }

      if ( endpointType == ValueType.Unknown || endpointType == ValueType.Ignored ) {
        Debug.LogWarning( $"The endpoint value type ({typeCode}) is not handled" );
        return false;
      }

      if ( toCSType && requestedType == ValueType.Real )
        return endpointType == ValueType.Real || endpointType == ValueType.Integer;
      else if ( !toCSType && endpointType == ValueType.Real )
        return requestedType == ValueType.Real || requestedType == ValueType.Integer;

      return requestedType == endpointType;
    }
  }

  public class OutputWrapper<T>
  where T : new()
  {
    OutputSource m_output;

    Func<Marshalling, T> m_performRead;

    public OutputWrapper( OutputSource output )
    {
      this.m_output = output;

      BuildReadMethod();
    }

    private void BuildReadMethod()
    {
      var marshal = m_output.SignalRoot.HeapControlInterface.prepare_read( m_output.Native );
      var fields = marshal.get_field_map();

      // We want to compile a dynamic method that looks like follows:
      //  T PerformRead(Marshalling marshalling) {
      //    T result = new T();
      //    [foreach field in marshalling] [
      //      [if field in T && marshalling[field].type (is convertible to) field.type ] [
      //        result.[[field.name]] = marshalling.read[[field.type]]([[field.name]]);
      //      ]
      //    ]
      //    return result;
      //  }

      List<Expression> calls = new List<Expression>();

      // Function declaration: T <anonymous>(Marshalling marshalling)
      var returnTarget = Expression.Label(typeof(T));
      var marshalParam = Expression.Parameter(typeof(Marshalling), "marshalling");
      var parameters = new ParameterExpression[]{marshalParam};

      // T result = new T();
      var result = Expression.Variable( typeof( T ), "result" );
      calls.Add( Expression.Assign( result, Expression.New( typeof( T ) ) ) );

      // This is a special case:
      // If there is only a single field and T can be directly assigned from it (T is primitive),
      // then we do that instead of finding fields in T to assign to.
      if ( fields.Count == 1 && SignalUtils.IsValueTypeCompatible<T>( fields.Values.First().field_type, true ) ) {
        var (k, v) = fields.First();
        // result = marshalling.read[[fields[0].type]](fields[0].name);
        var assignment = EmitReadField( result, marshalParam, k, v, true );
        if ( assignment == null ) {
          Debug.LogError( $"Failed to initialize OutputWrapper<{typeof( T ).Name}> for output '{m_output.Name}'" );
          return;
        }
        calls.Add( assignment );
      }
      else { // Default case
        // [foreach field in marshalling]
        foreach ( var (k, v) in marshal.get_field_map() ) {
          var assignment = EmitReadField(result, marshalParam, k, v, false);
          if ( assignment != null )
            calls.Add( assignment );
        }
      }

      calls.Add( Expression.Return( returnTarget, result ) );
      calls.Add( Expression.Label( returnTarget, result ) ); // TODO: Is this needed?
      var block = Expression.Block(new ParameterExpression[]{result},calls);
      LambdaExpression lambda = Expression.Lambda<Func<Marshalling,T>>(block, parameters );
      m_performRead = (Func<Marshalling, T>)lambda.Compile();
    }

    private Expression EmitReadField( Expression target, Expression marshal, string fieldName, openplx.Field oField, bool direct )
    {
      System.Type targetType = null;

      if ( direct ) // Assign to 'target' directly
        targetType = target.Type;
      else { // Find field in 'target' matching the OpenPLX field name
        MemberInfo mInfo = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance );
        if ( mInfo == null )
          mInfo = typeof( T ).GetProperty( fieldName, BindingFlags.Public | BindingFlags.Instance );

        if ( mInfo is PropertyInfo pi )
          targetType = pi.PropertyType;
        else if ( mInfo is FieldInfo fi )
          targetType = fi.FieldType;
        else {
          Debug.LogWarning( $"Output '{m_output.Name}' has a field '{fieldName}' that could not be found in type '{typeof( T ).FullName}'. Ignoring..." );
          return null;
        }

        target = Expression.PropertyOrField( target, mInfo.Name );
      }

      // Check if target field can be assigned the OpenPLX field value
      if ( !SignalUtils.IsValueTypeCompatible( targetType, oField.field_type, true ) ) {
        Debug.LogWarning( $"Target field '{fieldName}' ({targetType.FullName}) on type '{typeof( T ).FullName}' cannot be assigned OpenPLX field type '{oField.field_type}'" );
        return null;
      }

      // The call that we wanna make on the marshalling object depends on the field type
      Expression nativeCall( string funcName ) =>
        Expression.Call( marshal,
                         funcName,
                         new System.Type[] { },
                         Expression.Constant( fieldName, typeof( string ) ) );

      var readCall = System.Type.GetTypeCode( targetType ) switch
      {
        TypeCode.UInt16   => nativeCall(nameof(Marshalling.readUInt16)),
        TypeCode.UInt32   => nativeCall(nameof(Marshalling.readUInt32)),
        TypeCode.UInt64   => nativeCall(nameof(Marshalling.readUInt64)),
        TypeCode.Int16    => nativeCall(nameof(Marshalling.readInt16)),
        TypeCode.Int32    => nativeCall(nameof(Marshalling.readInt32)),
        TypeCode.Int64    => nativeCall(nameof(Marshalling.readInt64)),
        TypeCode.Double   => nativeCall(nameof(Marshalling.readDouble)),
        TypeCode.Decimal  => nativeCall(nameof(Marshalling.readDouble)),
        TypeCode.Single   => nativeCall(nameof(Marshalling.readFloat)),
        TypeCode.Boolean  => nativeCall(nameof(Marshalling.read_bool)),
        _ => null
      };

      // Check that we found an appropriate read method to call
      if ( readCall == null ) {
        Debug.LogWarning( $"Failed to build read call for field '{fieldName}' on Output '{m_output.Name}', field type: '{oField.field_type}', target type: '{targetType.FullName}'. Ignoring..." );
        return null;
      }

      // TODO: Handle nullopt return
      return Expression.Assign( target, Expression.PropertyOrField( readCall, "Value" ) );
    }

    public T Read()
    {
      if ( !m_output.IsValid ) {
        Debug.LogWarning( $"Read() called on OutputWrapper wrapping invalid Output '{m_output.Name}'. " +
                          $"This might be caused by a callback attempting to access the wrapper after the Root object has been destroyed." );
        return default( T );
      }
      var marshal = m_output.SignalRoot.HeapControlInterface.prepare_read( m_output.Native );
      return m_performRead( marshal );
    }
  }

  public class InputWrapper<T>
  where T : new()
  {
    InputTarget m_input;

    Action<Marshalling, T> m_performWrite;

    public InputWrapper( InputTarget input )
    {
      this.m_input = input;

      BuildWriteMethod();
    }

    private void BuildWriteMethod()
    {
      var marshal = m_input.SignalRoot.HeapControlInterface.prepare_write( m_input.Native );
      var fields = marshal.get_field_map();

      // We want to compile a dynamic method that looks like follows:
      //  void PerformWrite(Marshalling marshalling, T value) {
      //    [foreach field in marshalling] [
      //      [if field in T && marshalling[field].type (is convertible to) field.type ] [
      //        marshalling.write[[field.type]]([[field.name]], value.[[field.name]]);
      //      ]
      //    ]
      //  }

      List<Expression> calls = new List<Expression>();

      // Function declaration: void <anonymous>(Marshalling marshalling, T value)
      var marshalParam = Expression.Parameter(typeof(Marshalling), "marshalling");
      var valueParam = Expression.Parameter(typeof(T), "value");
      var parameters = new ParameterExpression[]{marshalParam, valueParam};

      // This is a special case:
      // If there is only a single field and T can be directly assigned from it (T is primitive),
      // then we do that instead of finding fields in T to assign to.
      if ( fields.Count == 1 && SignalUtils.IsValueTypeCompatible<T>( fields.Values.First().field_type, true ) ) {
        var (k, v) = fields.First();
        // marshalling.write[[fields[0].type]](fields[0].name, value.[[field.name]]);
        var assignment = EmitReadField( valueParam, marshalParam, k, v, true );
        if ( assignment == null ) {
          Debug.LogError( $"Failed to initialize InputWrapper<{typeof( T ).Name}> for input '{m_input.Name}'" );
          return;
        }
        calls.Add( assignment );
      }
      else { // Default case
        // [foreach field in marshalling]
        foreach ( var (k, v) in marshal.get_field_map() ) {
          var assignment = EmitReadField(valueParam, marshalParam, k, v, false);
          if ( assignment != null )
            calls.Add( assignment );
        }
      }

      var block = Expression.Block(calls);
      LambdaExpression lambda = Expression.Lambda<Action<Marshalling,T>>(block, parameters );
      m_performWrite = (Action<Marshalling, T>)lambda.Compile();
    }

    private Expression EmitReadField( Expression value, Expression marshal, string fieldName, openplx.Field oField, bool direct )
    {
      System.Type targetType = null;

      if ( direct ) // Assign to 'target' directly
        targetType = value.Type;
      else { // Find field in 'target' matching the OpenPLX field name
        MemberInfo mInfo = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance );
        if ( mInfo == null )
          mInfo = typeof( T ).GetProperty( fieldName, BindingFlags.Public | BindingFlags.Instance );

        if ( mInfo is PropertyInfo pi )
          targetType = pi.PropertyType;
        else if ( mInfo is FieldInfo fi )
          targetType = fi.FieldType;
        else {
          Debug.LogWarning( $"Output '{m_input.Name}' has a field '{fieldName}' that could not be found in type '{typeof( T ).FullName}'. Ignoring..." );
          return null;
        }

        value = Expression.PropertyOrField( value, mInfo.Name );
      }

      // Check if target field can be assigned the OpenPLX field value
      if ( !SignalUtils.IsValueTypeCompatible( targetType, oField.field_type, true ) ) {
        Debug.LogWarning( $"Target field '{fieldName}' ({targetType.FullName}) on type '{typeof( T ).FullName}' cannot be assigned OpenPLX field type '{oField.field_type}'" );
        return null;
      }

      // The call that we wanna make on the marshalling object depends on the field type
      Expression nativeCall( string funcName ) =>
        Expression.Call( marshal,
                         funcName,
                         new System.Type[] { },
                         Expression.Constant( fieldName, typeof( string ) ),
                         value );


      var writeCall = System.Type.GetTypeCode( targetType ) switch
      {
        TypeCode.UInt16   => nativeCall(nameof(Marshalling.writeUInt16)),
        TypeCode.UInt32   => nativeCall(nameof(Marshalling.writeUInt32)),
        TypeCode.UInt64   => nativeCall(nameof(Marshalling.writeUInt64)),
        TypeCode.Int16    => nativeCall(nameof(Marshalling.writeInt16)),
        TypeCode.Int32    => nativeCall(nameof(Marshalling.writeInt32)),
        TypeCode.Int64    => nativeCall(nameof(Marshalling.writeInt64)),
        TypeCode.Double   => nativeCall(nameof(Marshalling.writeDouble)),
        TypeCode.Decimal  => nativeCall(nameof(Marshalling.writeDouble)),
        TypeCode.Single   => nativeCall(nameof(Marshalling.writeFloat)),
        TypeCode.Boolean  => nativeCall(nameof(Marshalling.write_bool)),
        _ => null
      };

      // Check that we found an appropriate read method to call
      if ( writeCall == null ) {
        Debug.LogWarning( $"Failed to build read call for field '{fieldName}' on Output '{m_input.Name}', field type: '{oField.field_type}', target type: '{targetType.FullName}'. Ignoring..." );
        return null;
      }

      // TODO: Handle nullopt return
      return writeCall;
    }

    public void Write( T value )
    {
      if ( !m_input.IsValid ) {
        Debug.LogWarning( $"Write() called on InputWrapper wrapping invalid Input '{m_input.Name}'. " +
                          $"This might be caused by a callback attempting to access the wrapper after the Root object has been destroyed." );
        return;
      }
      var marshal = m_input.SignalRoot.HeapControlInterface.prepare_write( m_input.Native );
      m_performWrite( marshal, value );
      m_input.SignalRoot.HeapControlInterface.flush();
    }
  }
}
