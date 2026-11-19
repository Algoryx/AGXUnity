using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Array = System.Array;
using FieldType = openplx.FieldType;
using FieldArrayType = openplx.FieldArrayType;

[assembly: InternalsVisibleTo( "AGXUnity.Tests" )]

namespace AGXUnity.IO.OpenPLX
{
  // A managed description of a prepared buffer. No native handles are retained.
  internal class SignalLayout
  {
    public int Offset;
    public int Stride;
    public int Count = 1;
    public int BufferSize;
    public FieldArrayType ArrayType;
    public SignalField[] Fields = Array.Empty<SignalField>();
    public Dictionary<string, SignalLayout> Children = new Dictionary<string, SignalLayout>();

    public static SignalLayout Capture( openplx.Marshalling marshalling, bool root = true )
    {
      if ( marshalling == null )
        throw new InvalidOperationException( "OpenPLX could not prepare the signal buffer." );
      var result = new SignalLayout
      {
        Offset = root ? 0 : checked( (int)marshalling.get_offset() ),
        BufferSize = checked( (int)marshalling.get_buffer_size() ),
        Stride = checked( (int)marshalling.get_stride() ),
        ArrayType = marshalling.get_array_type()
      };
      if ( result.ArrayType == FieldArrayType.Flexible ) {
        result.Stride = checked((int)marshalling.get_size());
        if ( result.Stride <= 0 || result.BufferSize % result.Stride != 0 )
          throw new ArgumentException( "A flexible signal array must contain whole, nonzero-size elements." );
        result.Count = result.BufferSize / result.Stride;
      }
      else if ( result.ArrayType != FieldArrayType.None )
        throw new NotSupportedException( $"OpenPLX array mode {result.ArrayType} is not supported." );

      var fields = new List<SignalField>();
      using ( var nativeFields = marshalling.get_field_map() ) {
        foreach ( var entry in nativeFields ) {
          using ( var field = entry.Value ) {
            var value = new SignalField( entry.Key, field.field_type, checked( (int)field.size ), checked( (int)field.offset ) );
            if ( field.field_array_type == FieldArrayType.None )
              fields.Add( value );
            else if ( field.field_array_type == FieldArrayType.Flexible ) {
              if ( result.ArrayType != FieldArrayType.None || value.Size <= 0 || value.Offset > result.BufferSize ||
                   ( result.BufferSize - value.Offset ) % value.Size != 0 )
                throw new ArgumentException( "A flexible primitive array must occupy a whole trailing buffer region." );
              result.Children.Add( value.Name, new SignalLayout
              {
                Offset = value.Offset,
                Stride = value.Size,
                ArrayType = FieldArrayType.Flexible,
                BufferSize = result.BufferSize - value.Offset,
                Count = ( result.BufferSize - value.Offset ) / value.Size,
                Fields = new[] { new SignalField( value.Name, value.Type, value.Size ) }
              } );
            }
            else
              throw new NotSupportedException( $"OpenPLX field array mode {field.field_array_type} is not supported." );
          }
        }
      }
      result.Fields = fields.ToArray();
      var children = marshalling.get_nested_map();
      try {
        foreach ( var child in children )
          result.Children.Add( child.Key, Capture( child.Value, false ) );
      }
      finally {
        foreach ( var child in children.Values )
          child?.Dispose();
      }
      return result;
    }
  }

  internal class SignalField
  {
    public string Name;
    public FieldType Type;
    public int Size;
    public int Offset;
    public FieldArrayType ArrayType;

    public SignalField( string name, FieldType type, int size, int offset = 0 )
    {
      Name = name;
      Type = type;
      Size = size;
      Offset = offset;
    }
  }

  internal class SignalMapping<T>
  {
    private readonly SignalLayout m_schema;
    private readonly bool m_read;
    private readonly Func<byte[], T> m_reader;
    private readonly Action<byte[], T> m_writer;
    private readonly Type m_elementType;
    private readonly int m_elementSize;

    public SignalMapping( SignalLayout layout, bool read )
    {
      m_schema = layout ?? throw new ArgumentNullException( nameof( layout ) );
      m_read = read;
      if ( !BitConverter.IsLittleEndian )
        throw new PlatformNotSupportedException( "OpenPLX signal buffer mapping requires a little-endian host." );

      var containsArray = ContainsArray( layout );
      if ( typeof( T ).IsArray ) {
        m_elementType = typeof( T ).GetElementType();
        if ( typeof( T ) != m_elementType.MakeArrayType() )
          throw new ArgumentException( "Only one-dimensional, zero-based signal arrays are supported." );
        var array = FindArray( layout, out _ );
        m_elementSize = BlittableSize( m_elementType );
        if ( array.Stride != m_elementSize )
          throw new ArgumentException( $"Native array stride {array.Stride} does not match {m_elementType.FullName} size {m_elementSize}." );
        ValidateElement( m_elementType, array, 0 );
      }
      else {
        if ( containsArray )
          throw new ArgumentException( "Payloads containing arrays must map to a primitive or fixed-layout struct array." );
        var buffer = Expression.Parameter( typeof( byte[] ), "buffer" );
        if ( read )
          m_reader = Expression.Lambda<Func<byte[], T>>( ReadValue( typeof( T ), layout, buffer, 0 ), buffer ).Compile();
        else {
          var value = Expression.Parameter( typeof( T ), "value" );
          var calls = new List<Expression>();
          WriteValue( value, layout, buffer, 0, calls );
          m_writer = Expression.Lambda<Action<byte[], T>>( Expression.Block( calls ), buffer, value ).Compile();
        }
      }
      ValidateBounds( layout, layout.BufferSize, 0 );
    }

    public T Read( byte[] buffer, SignalLayout layout )
    {
      Validate( buffer, layout, true );
      if ( m_elementType == null )
        return m_reader( buffer );
      var array = FindArray( layout, out _ );
      var result = (T)(object)Array.CreateInstance( m_elementType, array.Count );
      ReadInto( buffer, layout, result );
      return result;
    }

    public int ReadInto( byte[] buffer, SignalLayout layout, T destination )
    {
      Validate( buffer, layout, true );
      if ( m_elementType == null )
        throw new InvalidOperationException( "ReadInto is only supported for array wrappers." );
      if ( destination == null )
        throw new ArgumentNullException( nameof( destination ) );
      var array = FindArray( layout, out var offset );
      var target = (Array)(object)destination;
      if ( target.Length < array.Count )
        throw new ArgumentException( $"Destination has {target.Length} elements; {array.Count} are required.", nameof( destination ) );
      CopyArray( target, buffer, offset, checked(array.Count * m_elementSize), true );
      return array.Count;
    }

    public void Write( byte[] buffer, SignalLayout layout, T value )
    {
      Validate( buffer, layout, false );
      if ( value == null )
        throw new ArgumentNullException( nameof( value ) );
      if ( m_elementType == null )
        m_writer( buffer, value );
      else {
        var array = FindArray( layout, out var offset );
        var source = (Array)(object)value;
        if ( source.Length != array.Count )
          throw new ArgumentException( $"Source has {source.Length} elements; the prepared native array requires {array.Count}.", nameof( value ) );
        CopyArray( source, buffer, offset, checked(array.Count * m_elementSize), false );
      }
    }

    public int WriteCapacity( T value )
    {
      if ( m_elementType == null )
        return m_schema.BufferSize;
      if ( value == null )
        throw new ArgumentNullException( nameof( value ) );
      var array = FindArray( m_schema, out var offset );
      var count = ( (Array)(object)value ).Length;
      if ( array.ArrayType == FieldArrayType.Static && count != array.Count )
        throw new ArgumentException( "A fixed array requires exactly the declared element count." );
      return checked(offset + count * m_elementSize);
    }

    private void Validate( byte[] buffer, SignalLayout layout, bool read )
    {
      if ( m_read != read )
        throw new InvalidOperationException( "Signal mapping used in the wrong transfer direction." );
      if ( buffer == null )
        throw new ArgumentNullException( nameof( buffer ) );
      if ( layout == null || buffer.Length < layout.BufferSize )
        throw new ArgumentException( "Buffer is smaller than the prepared signal payload." );
      if ( !SameSchema( m_schema, layout ) )
        throw new ArgumentException( "The prepared signal schema changed after the wrapper was constructed." );
      ValidateBounds( layout, layout.BufferSize, 0 );
    }

    private static bool SameSchema( SignalLayout left, SignalLayout right )
    {
      if ( left.Offset != right.Offset || left.Stride != right.Stride || left.ArrayType != right.ArrayType ||
           left.Fields.Length != right.Fields.Length || left.Children.Count != right.Children.Count ||
           left.ArrayType == FieldArrayType.None && left.Count != right.Count ||
           left.ArrayType == FieldArrayType.Static && left.Count != right.Count )
        return false;
      foreach ( var field in left.Fields ) {
        var other = right.Fields.FirstOrDefault( f => f.Name == field.Name );
        if ( other == null || field.Type != other.Type || field.Size != other.Size ||
             field.Offset != other.Offset || field.ArrayType != other.ArrayType )
          return false;
      }
      foreach ( var child in left.Children )
        if ( !right.Children.TryGetValue( child.Key, out var other ) || !SameSchema( child.Value, other ) )
          return false;
      return true;
    }

    private static bool ContainsArray( SignalLayout node )
      => node.ArrayType != FieldArrayType.None || node.Fields.Any( f => f.ArrayType != FieldArrayType.None ) || node.Children.Values.Any( ContainsArray );

    private static SignalLayout FindArray( SignalLayout node, out int offset )
    {
      offset = node.Offset;
      while ( node.ArrayType == FieldArrayType.None && node.Fields.Length == 0 && node.Children.Count == 1 ) {
        node = node.Children.Values.First();
        offset = checked(offset + node.Offset);
      }
      if ( node.ArrayType == FieldArrayType.None )
        throw new ArgumentException( "The unwrapped payload is not an array." );
      return node;
    }

    private static void ValidateBounds( SignalLayout node, int capacity, int parentOffset )
    {
      if ( node.Offset < 0 || node.Stride < 0 || node.Count < 0 || capacity < 0 )
        throw new ArgumentException( "Negative signal layout dimensions are invalid." );
      var offset = checked( parentOffset + node.Offset );
      if ( offset > capacity )
        throw new ArgumentException( "Signal offset exceeds the buffer." );
      if ( node.BufferSize < 0 || node.BufferSize > capacity - offset )
        throw new ArgumentException( "A nested signal view exceeds its containing buffer." );
      capacity = checked(offset + node.BufferSize);
      if ( node.ArrayType != FieldArrayType.None ) {
        if ( checked((long)offset + (long)node.Stride * node.Count) > capacity )
          throw new ArgumentException( "Signal array exceeds the prepared buffer." );
        if ( node.Count == 0 )
          return;
        capacity = checked(offset + node.Stride);
      }
      foreach ( var field in node.Fields )
        if ( field.Offset < 0 || field.Size <= 0 || checked((long)offset + field.Offset + field.Size) > capacity )
          throw new ArgumentException( $"Field '{field.Name}' exceeds the prepared buffer." );
      foreach ( var child in node.Children.Values )
        ValidateBounds( child, capacity, offset );
    }

    private static Type PrimitiveType( SignalField field )
    {
      if ( field.Type == FieldType.Bool && field.Size == 1 ) return typeof( bool );
      if ( field.Type == FieldType.Real ) {
        if ( field.Size == 4 ) return typeof( float );
        if ( field.Size == 8 ) return typeof( double );
      }
      if ( field.Type == FieldType.Int ) {
        if ( field.Size == 1 ) return typeof( sbyte );
        if ( field.Size == 2 ) return typeof( short );
        if ( field.Size == 4 ) return typeof( int );
        if ( field.Size == 8 ) return typeof( long );
      }
      if ( field.Type == FieldType.UInt ) {
        if ( field.Size == 1 ) return typeof( byte );
        if ( field.Size == 2 ) return typeof( ushort );
        if ( field.Size == 4 ) return typeof( uint );
        if ( field.Size == 8 ) return typeof( ulong );
      }
      throw new ArgumentException( $"Unsupported native field '{field.Name}': {field.Type}, {field.Size} bytes." );
    }

    private static bool IsScalar( Type type )
      => !type.IsEnum && ( type.IsPrimitive && type != typeof( char ) && type != typeof( IntPtr ) && type != typeof( UIntPtr ) || type == typeof( decimal ) );

    private static void ValidateScalar( Type type, SignalField field, bool read )
    {
      if ( field.ArrayType != FieldArrayType.None || !IsScalar( type ) || !SignalUtils.IsValueTypeCompatible( type, field.Type, read ) )
        throw new ArgumentException( $"Cannot {( read ? "read" : "write" )} field '{field.Name}' ({field.Type}) as {type.FullName}." );
      PrimitiveType( field );
    }

    private static Expression ReadField( Type type, SignalField field, Expression buffer, int offset )
    {
      ValidateScalar( type, field, true );
      var nativeType = PrimitiveType( field );
      var call = Expression.Call( typeof( SignalBuffer ), "Read" + nativeType.Name, null, buffer, Expression.Constant( checked( offset + field.Offset ) ) );
      return type == nativeType ? (Expression)call : Expression.ConvertChecked( call, type );
    }

    private static Expression ReadValue( Type type, SignalLayout node, Expression buffer, int parentOffset )
    {
      var offset = checked( parentOffset + node.Offset );
      if ( node.Fields.Length == 0 && node.Children.Count == 1 )
        return ReadValue( type, node.Children.Values.First(), buffer, offset );
      if ( IsScalar( type ) && node.Fields.Length == 1 && node.Children.Count == 0 )
        return ReadField( type, node.Fields[ 0 ], buffer, offset );
      if ( IsScalar( type ) || type.IsAbstract || type.IsInterface || type.IsArray ||
           !type.IsValueType && type.GetConstructor( Type.EmptyTypes ) == null )
        throw new ArgumentException( $"Type {type.FullName} cannot represent this signal object." );
      var result = Expression.Variable( type, "result" );
      var calls = new List<Expression> { Expression.Assign( result, Expression.New( type ) ) };
      foreach ( var field in node.Fields ) {
        var member = Member( result, field.Name, true );
        calls.Add( Expression.Assign( member, ReadField( member.Type, field, buffer, offset ) ) );
      }
      foreach ( var child in node.Children ) {
        var member = Member( result, child.Key, true );
        calls.Add( Expression.Assign( member, ReadValue( member.Type, child.Value, buffer, offset ) ) );
      }
      if ( calls.Count == 1 )
        throw new ArgumentException( "An empty signal object cannot be mapped." );
      calls.Add( result );
      return Expression.Block( new[] { result }, calls );
    }

    private static void WriteField( Expression value, SignalField field, Expression buffer, int offset, List<Expression> calls )
    {
      ValidateScalar( value.Type, field, false );
      var nativeType = PrimitiveType( field );
      calls.Add( Expression.Call( typeof( SignalBuffer ), "Write" + nativeType.Name, null, buffer,
        Expression.Constant( checked(offset + field.Offset) ), value.Type == nativeType ? value : Expression.ConvertChecked( value, nativeType ) ) );
    }

    private static void WriteValue( Expression value, SignalLayout node, Expression buffer, int parentOffset, List<Expression> calls )
    {
      var offset = checked( parentOffset + node.Offset );
      if ( node.Fields.Length == 0 && node.Children.Count == 1 ) {
        WriteValue( value, node.Children.Values.First(), buffer, offset, calls );
        return;
      }
      if ( IsScalar( value.Type ) && node.Fields.Length == 1 && node.Children.Count == 0 ) {
        WriteField( value, node.Fields[ 0 ], buffer, offset, calls );
        return;
      }
      if ( IsScalar( value.Type ) || value.Type.IsArray )
        throw new ArgumentException( $"Type {value.Type.FullName} cannot represent this signal object." );
      foreach ( var field in node.Fields )
        WriteField( Member( value, field.Name, false ), field, buffer, offset, calls );
      foreach ( var child in node.Children ) {
        var member = Member( value, child.Key, false );
        var local = Expression.Variable( member.Type, child.Key );
        var nestedCalls = new List<Expression> { Expression.Assign( local, member ) };
        if ( !member.Type.IsValueType )
          nestedCalls.Add( Expression.IfThen( Expression.Equal( local, Expression.Constant( null, member.Type ) ),
            Expression.Throw( Expression.New( typeof( ArgumentException ).GetConstructor( new[] { typeof( string ) } ),
              Expression.Constant( $"Nested input member '{child.Key}' cannot be null." ) ) ) ) );
        WriteValue( local, child.Value, buffer, offset, nestedCalls );
        calls.Add( Expression.Block( new[] { local }, nestedCalls ) );
      }
      if ( node.Fields.Length == 0 && node.Children.Count == 0 )
        throw new ArgumentException( "An empty signal object cannot be mapped." );
    }

    private static MemberExpression Member( Expression value, string name, bool read )
    {
      var field = value.Type.GetField( name, BindingFlags.Public | BindingFlags.Instance );
      if ( field != null ) {
        if ( read && field.IsInitOnly )
          throw new ArgumentException( $"Signal member {value.Type.FullName}.{name} is readonly." );
        return Expression.Field( value, field );
      }
      var property = value.Type.GetProperty( name, BindingFlags.Public | BindingFlags.Instance );
      if ( property == null || property.GetIndexParameters().Length != 0 ||
           ( read ? property.GetSetMethod() : property.GetGetMethod() ) == null )
        throw new ArgumentException( $"Signal member {value.Type.FullName}.{name} must be a public {( read ? "writable" : "readable" )} field or property." );
      return Expression.Property( value, property );
    }

    private static int BlittableSize( Type type )
    {
      if ( IsScalar( type ) ) {
        if ( type == typeof( decimal ) )
          throw new ArgumentException( "Decimal is not a native signal array element type." );
        return type == typeof( bool ) ? 1 : Marshal.SizeOf( type );
      }
      if ( type.IsPrimitive || !type.IsValueType || type.IsAutoLayout || type.IsEnum )
        throw new ArgumentException( $"Array element {type.FullName} must have a fixed value-type layout." );
      foreach ( var field in type.GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance ) ) {
        if ( field.FieldType == typeof( bool ) || field.GetCustomAttribute<MarshalAsAttribute>() != null )
          throw new ArgumentException( "Struct arrays cannot contain Boolean fields or custom marshalled fields." );
        BlittableSize( field.FieldType );
      }
      return Marshal.SizeOf( type );
    }

    private static void ValidateElement( Type type, SignalLayout node, int offset, int managedOffset = 0 )
    {
      if ( node.Fields.Length == 0 && node.Children.Count == 1 ) {
        var child = node.Children.Values.First();
        if ( child.ArrayType != FieldArrayType.None )
          throw new ArgumentException( "Nested arrays are not supported." );
        ValidateElement( type, child, checked(offset + child.Offset), managedOffset );
        return;
      }
      if ( IsScalar( type ) && node.Fields.Length == 1 && node.Children.Count == 0 ) {
        var field = node.Fields[0];
        if ( PrimitiveType( field ) != type || offset + field.Offset != managedOffset || field.ArrayType != FieldArrayType.None )
          throw new ArgumentException( $"Array element {type.FullName} does not exactly match the native layout." );
        return;
      }
      if ( IsScalar( type ) )
        throw new ArgumentException( "A structured array cannot be read as a primitive array." );
      var fields = type.GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
      if ( fields.Length != node.Fields.Length + node.Children.Count )
        throw new ArgumentException( $"Array element {type.FullName} has a different number of fields than the native layout." );
      foreach ( var field in node.Fields ) {
        var target = type.GetField( field.Name, BindingFlags.Public | BindingFlags.Instance );
        if ( target == null || target.FieldType != PrimitiveType( field ) || field.ArrayType != FieldArrayType.None ||
             managedOffset + Marshal.OffsetOf( type, field.Name ).ToInt64() != offset + field.Offset )
          throw new ArgumentException( $"Array field {type.FullName}.{field.Name} does not exactly match the native layout." );
      }
      foreach ( var child in node.Children ) {
        var target = type.GetField( child.Key, BindingFlags.Public | BindingFlags.Instance );
        if ( target == null || child.Value.ArrayType != FieldArrayType.None )
          throw new ArgumentException( $"Nested array field {type.FullName}.{child.Key} does not match the native layout." );
        ValidateElement( target.FieldType, child.Value, checked(offset + child.Value.Offset),
          checked(managedOffset + (int)Marshal.OffsetOf( type, child.Key )) );
      }
    }

    private static void CopyArray( Array array, byte[] bytes, int offset, int count, bool read )
    {
      if ( count == 0 )
        return;
      var handle = GCHandle.Alloc( array, GCHandleType.Pinned );
      try {
        if ( read )
          Marshal.Copy( bytes, offset, handle.AddrOfPinnedObject(), count );
        else
          Marshal.Copy( handle.AddrOfPinnedObject(), bytes, offset, count );
      }
      finally {
        handle.Free();
      }
    }
  }

  // Native payloads use their declared integer widths, including one-byte Boolean values.
  internal static class SignalBuffer
  {
    public static bool ReadBoolean( byte[] b, int i ) => b[ i ] != 0;
    public static byte ReadByte( byte[] b, int i ) => b[ i ];
    public static sbyte ReadSByte( byte[] b, int i ) => unchecked((sbyte)b[ i ]);
    public static short ReadInt16( byte[] b, int i ) => BitConverter.ToInt16( b, i );
    public static ushort ReadUInt16( byte[] b, int i ) => BitConverter.ToUInt16( b, i );
    public static int ReadInt32( byte[] b, int i ) => BitConverter.ToInt32( b, i );
    public static uint ReadUInt32( byte[] b, int i ) => BitConverter.ToUInt32( b, i );
    public static long ReadInt64( byte[] b, int i ) => BitConverter.ToInt64( b, i );
    public static ulong ReadUInt64( byte[] b, int i ) => BitConverter.ToUInt64( b, i );
    public static float ReadSingle( byte[] b, int i ) => BitConverter.ToSingle( b, i );
    public static double ReadDouble( byte[] b, int i ) => BitConverter.ToDouble( b, i );
    public static void WriteBoolean( byte[] b, int i, bool v ) => b[ i ] = v ? (byte)1 : (byte)0;
    public static void WriteByte( byte[] b, int i, byte v ) => b[ i ] = v;
    public static void WriteSByte( byte[] b, int i, sbyte v ) => b[ i ] = unchecked((byte)v);
    public static void WriteInt16( byte[] b, int i, short v ) => WriteUInt16( b, i, unchecked((ushort)v) );
    public static void WriteUInt16( byte[] b, int i, ushort v ) { b[ i ] = (byte)v; b[ i + 1 ] = (byte)( v >> 8 ); }
    public static void WriteInt32( byte[] b, int i, int v ) => WriteUInt32( b, i, unchecked((uint)v) );
    public static void WriteUInt32( byte[] b, int i, uint v ) { for ( int n = 0; n < 4; ++n ) b[ i + n ] = (byte)( v >> ( 8 * n ) ); }
    public static void WriteInt64( byte[] b, int i, long v ) => WriteUInt64( b, i, unchecked((ulong)v) );
    public static void WriteUInt64( byte[] b, int i, ulong v ) { for ( int n = 0; n < 8; ++n ) b[ i + n ] = (byte)( v >> ( 8 * n ) ); }
    public static void WriteSingle( byte[] b, int i, float v ) => WriteInt32( b, i, BitConverter.SingleToInt32Bits( v ) );
    public static void WriteDouble( byte[] b, int i, double v ) => WriteInt64( b, i, BitConverter.DoubleToInt64Bits( v ) );
  }
}
