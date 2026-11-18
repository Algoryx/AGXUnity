using openplx;
using System;
using UnityEngine;

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

  /// <summary>
  /// Reads a signal as a scalar, an object with matching public members, or an
  /// array of primitives/fixed-layout structs. Array payloads require exact types.
  /// </summary>
  public class OutputWrapper<T>
  {
    private readonly OutputSource m_output;
    private readonly SignalTransfer<T> m_transfer;

    public OutputWrapper( OutputSource output )
    {
      m_output = output ?? throw new ArgumentNullException( nameof( output ) );
      if ( !output.IsValid || output.SignalRoot.HeapControlInterface == null )
        throw new ArgumentException( "The output must belong to initialized OpenPLX signals.", nameof( output ) );
      var heap = output.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        using ( var marshalling = heap.prepare_read( output.Native ) )
          m_transfer = new SignalTransfer<T>( marshalling, true, output.Name );
      }
    }

    /// <summary>Returns an independent managed snapshot of the current signal.</summary>
    public T Read()
    {
      if ( !IsValid() )
        return default;
      var heap = m_output.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        using ( var marshalling = heap.prepare_read( m_output.Native ) )
          return m_transfer.Read( marshalling );
      }
    }

    /// <summary>
    /// Reads into an existing array and returns the element count. The destination
    /// may be larger than the payload; remaining elements are preserved.
    /// </summary>
    public int ReadInto( T destination )
    {
      if ( !IsValid() )
        return 0;
      var heap = m_output.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        using ( var marshalling = heap.prepare_read( m_output.Native ) )
          return m_transfer.ReadInto( marshalling, destination );
      }
    }

    private bool IsValid()
    {
      if ( m_output.IsValid )
        return true;
      Debug.LogWarning( $"Read() called on OutputWrapper wrapping invalid Output '{m_output.Name}'. " +
                        "This might be caused by a callback attempting to access the wrapper after the Root object has been destroyed." );
      return false;
    }
  }

  /// <summary>Writes a signal using the same mapping rules as OutputWrapper.</summary>
  public class InputWrapper<T>
  {
    private readonly InputTarget m_input;
    private readonly SignalTransfer<T> m_transfer;

    public InputWrapper( InputTarget input )
    {
      m_input = input ?? throw new ArgumentNullException( nameof( input ) );
      if ( !input.IsValid || input.SignalRoot.HeapControlInterface == null )
        throw new ArgumentException( "The input must belong to initialized OpenPLX signals.", nameof( input ) );
      var heap = input.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        using ( var marshalling = heap.prepare_write( input.Native ) )
          m_transfer = new SignalTransfer<T>( marshalling, false, input.Name );
      }
    }

    public void Write( T value )
    {
      if ( !m_input.IsValid ) {
        Debug.LogWarning( $"Write() called on InputWrapper wrapping invalid Input '{m_input.Name}'. " +
                          "This might be caused by a callback attempting to access the wrapper after the Root object has been destroyed." );
        return;
      }
      var heap = m_input.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        // Sized preparation allocates heap-owned storage. Editing buffer-size
        // metadata on an existing marshalling would not allocate any storage.
        using ( var marshalling = typeof( T ).IsArray ? m_transfer.PrepareArrayWrite( heap, m_input.Native.getUuid(), value ) :
                                                       heap.prepare_write( m_input.Native ) ) {
          m_transfer.Write( marshalling, value );
          if ( !heap.flush() )
            throw new InvalidOperationException( $"OpenPLX failed to flush input '{m_input.Name}'." );
        }
      }
    }
  }

  internal class SignalTransfer<T>
  {
    private readonly SignalMapping<T> m_mapping;
    private readonly string m_preparationFailure;
    private byte[] m_buffer = System.Array.Empty<byte>();

    public SignalTransfer( Marshalling marshalling, bool read, string signalName = null )
    {
      m_preparationFailure = $"OpenPLX could not prepare {( read ? "output" : "input" )} '{signalName ?? "<unknown>"}'. " +
                             "Verify that its control handler and source/output mappings are initialized. " +
                             "A null marshalling is a failed preparation, not an empty payload.";
      m_mapping = new SignalMapping<T>( Capture( marshalling ), read );
    }

    public int WriteCapacity( T value ) => m_mapping.WriteCapacity( value );

    public Marshalling PrepareArrayWrite( openplx.HeapControlInterface heap, string inputUuid, T value )
    {
      // Endpoint names include the model prefix; control names are relative to
      // the root passed to add_controls_from_object. Resolve the actual name.
      using ( var controls = heap.get_control_interface() )
      using ( var input = controls.lookup_detached_control( controls.lookup_control_key_from_uuid( inputUuid ) ) ) {
        if ( input == null || !input.is_input() )
          throw new InvalidOperationException( "The input is not registered with the OpenPLX control interface." );
        return heap.PrepareWrite( input.get_name(), WriteCapacity( value ) );
      }
    }

    public T Read( Marshalling marshalling )
    {
      var layout = Prepare( marshalling );
      marshalling.CopyBufferTo( m_buffer, 0 );
      return m_mapping.Read( m_buffer, layout );
    }

    public int ReadInto( Marshalling marshalling, T destination )
    {
      var layout = Prepare( marshalling );
      marshalling.CopyBufferTo( m_buffer, 0 );
      return m_mapping.ReadInto( m_buffer, layout, destination );
    }

    public void Write( Marshalling marshalling, T value )
    {
      var layout = Prepare( marshalling );
      System.Array.Clear( m_buffer, 0, layout.BufferSize );
      m_mapping.Write( m_buffer, layout, value );
      marshalling.CopyBufferFrom( m_buffer, 0, layout.BufferSize );
    }

    private SignalLayout Prepare( Marshalling marshalling )
    {
      var layout = Capture( marshalling );
      if ( m_buffer.Length < layout.BufferSize )
        m_buffer = new byte[layout.BufferSize];
      return layout;
    }

    private SignalLayout Capture( Marshalling marshalling )
    {
      if ( marshalling == null )
        throw new InvalidOperationException( m_preparationFailure );
      return SignalLayout.Capture( marshalling );
    }
  }
}
