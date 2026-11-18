using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AGXUnity.IO.OpenPLX
{
  /// <summary>A scalar field within one lidar record. Paths are relative to the sample array.</summary>
  public sealed class LidarSignalField
  {
    public string Path { get; }
    public openplx.FieldType Type { get; }
    public int ByteSize { get; }
    public int Offset { get; }

    internal LidarSignalField( string path, openplx.FieldType type, int size, int offset )
    {
      Path = path;
      Type = type;
      ByteSize = size;
      Offset = offset;
    }

    public override string ToString() => $"{Path}: {Type}, {ByteSize} bytes, offset {Offset}";
  }

  /// <summary>
  /// Latest managed lidar payload, decoded using its native schema instead of a C# struct.
  /// Storage is reused: its contents remain valid until the reader's next successful read.
  /// Nested vectors/matrices appear as scalar paths such as position3d.x.
  /// </summary>
  public sealed class LidarSignalFrame
  {
    public int SampleCount { get; private set; }
    public int ByteCount { get; private set; }
    public int RecordSize { get; private set; }
    public string ArrayPath { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public IReadOnlyList<LidarSignalField> Fields => m_fields;

    private byte[] m_buffer = Array.Empty<byte>();
    private int m_arrayOffset;
    private ReadOnlyCollection<LidarSignalField> m_fields = Array.AsReadOnly( Array.Empty<LidarSignalField>() );
    private readonly Dictionary<string, LidarSignalField> m_lookup = new Dictionary<string, LidarSignalField>( StringComparer.Ordinal );

    public LidarSignalField FindField( string path )
      => path != null && m_lookup.TryGetValue( path, out var field ) ? field : null;

    /// <summary>Finds an unambiguous position vector, including one nested inside a record.</summary>
    public string FindPositionField()
    {
      foreach ( var name in new[] { "position3d", "position", "xyz" } ) {
        var candidates = m_fields.Where( field => field.Path.EndsWith( ".x", StringComparison.Ordinal ) )
          .Select( field => field.Path.Substring( 0, field.Path.Length - 2 ) )
          .Where( prefix => ( prefix == name || prefix.EndsWith( "." + name, StringComparison.Ordinal ) ) &&
                           FindField( prefix + ".y" ) != null && FindField( prefix + ".z" ) != null ).ToArray();
        if ( candidates.Length > 0 )
          return candidates.Length == 1 ? candidates[ 0 ] : null;
      }
      return null;
    }

    /// <summary>Returns an independent snapshot for inspecting a window while live sampling continues.</summary>
    public LidarSignalFrame Copy()
    {
      var snapshot = new LidarSignalFrame {
        SampleCount = SampleCount, ByteCount = ByteCount, RecordSize = RecordSize,
        ArrayPath = ArrayPath, SchemaVersion = SchemaVersion,
        m_arrayOffset = m_arrayOffset, m_fields = m_fields, m_buffer = new byte[ByteCount]
      };
      Buffer.BlockCopy( m_buffer, 0, snapshot.m_buffer, 0, ByteCount );
      // Field descriptions and their read-only collection are immutable.
      foreach ( var field in m_fields )
        snapshot.m_lookup.Add( field.Path, field );
      return snapshot;
    }

    /// <summary>Integers return Int64/UInt64 without loss of precision; reals return Single/Double.</summary>
    public object GetValue( int sampleIndex, string path )
    {
      var field = FindField( path ) ?? throw new ArgumentException( $"The lidar layout has no field '{path}'.", nameof( path ) );
      var offset = ValueOffset( sampleIndex, field );
      switch ( field.Type ) {
        case openplx.FieldType.Real:
          if ( field.ByteSize == 4 )
            return BitConverter.ToSingle( m_buffer, offset );
          return BitConverter.ToDouble( m_buffer, offset );
        case openplx.FieldType.Int: return Signed( offset, field.ByteSize );
        case openplx.FieldType.UInt: return Unsigned( offset, field.ByteSize );
        case openplx.FieldType.Bool: return Unsigned( offset, field.ByteSize ) != 0;
        default: throw new NotSupportedException( $"Unsupported lidar field type {field.Type}." );
      }
    }

    /// <summary>Numeric access for plotting. Use GetValue when an integer must retain all 64 bits.</summary>
    public double GetNumber( int sampleIndex, LidarSignalField field )
    {
      var offset = ValueOffset( sampleIndex, field );
      switch ( field.Type ) {
        case openplx.FieldType.Real:
          return field.ByteSize == 4 ? BitConverter.ToSingle( m_buffer, offset ) : BitConverter.ToDouble( m_buffer, offset );
        case openplx.FieldType.Int: return Signed( offset, field.ByteSize );
        case openplx.FieldType.UInt: return Unsigned( offset, field.ByteSize );
        case openplx.FieldType.Bool: return Unsigned( offset, field.ByteSize ) == 0 ? 0 : 1;
        default: throw new NotSupportedException( $"Unsupported lidar field type {field.Type}." );
      }
    }

    private int ValueOffset( int sampleIndex, LidarSignalField field )
    {
      if ( sampleIndex < 0 || sampleIndex >= SampleCount )
        throw new ArgumentOutOfRangeException( nameof( sampleIndex ) );
      if ( field == null || !m_lookup.TryGetValue( field.Path, out var current ) || !ReferenceEquals( field, current ) )
        throw new ArgumentException( "The field must belong to the current lidar layout.", nameof( field ) );
      return checked(m_arrayOffset + sampleIndex * RecordSize + field.Offset);
    }

    private long Signed( int offset, int size ) => size switch {
      1 => (sbyte)m_buffer[ offset ],
      2 => BitConverter.ToInt16( m_buffer, offset ),
      4 => BitConverter.ToInt32( m_buffer, offset ),
      8 => BitConverter.ToInt64( m_buffer, offset ),
      _ => throw new NotSupportedException( $"Unsupported integer size {size}." )
    };

    private ulong Unsigned( int offset, int size ) => size switch {
      1 => m_buffer[ offset ],
      2 => BitConverter.ToUInt16( m_buffer, offset ),
      4 => BitConverter.ToUInt32( m_buffer, offset ),
      8 => BitConverter.ToUInt64( m_buffer, offset ),
      _ => throw new NotSupportedException( $"Unsupported integer size {size}." )
    };

    internal void ReadFrom( openplx.Marshalling marshalling )
    {
      if ( marshalling == null )
        throw new InvalidOperationException( "OpenPLX could not prepare the lidar signal. Check its source and output registration." );
      Update( SignalLayout.Capture( marshalling ), null, marshalling );
    }

    internal void Update( SignalLayout layout, byte[] buffer ) => Update( layout, buffer, null );

    private void Update( SignalLayout layout, byte[] buffer, openplx.Marshalling marshalling )
    {
      if ( !BitConverter.IsLittleEndian )
        throw new PlatformNotSupportedException( "Lidar payload decoding requires a little-endian host." );
      if ( layout == null || layout.BufferSize < 0 )
        throw new ArgumentException( "A valid lidar layout is required.", nameof( layout ) );
      if ( marshalling == null && ( buffer == null || buffer.Length < layout.BufferSize ) )
        throw new ArgumentException( "The source buffer is smaller than the lidar payload.", nameof( buffer ) );

      SignalLayout array = null;
      var offset = 0;
      var path = string.Empty;
      FindArray( layout, 0, string.Empty, ref array, ref offset, ref path );
      if ( array == null )
        throw new NotSupportedException( "The lidar signal must contain one flexible sample array." );
      if ( array.Stride <= 0 || array.Count < 0 || array.BufferSize != checked(array.Count * array.Stride) ||
           offset < 0 || checked(offset + array.BufferSize) > layout.BufferSize )
        throw new ArgumentException( "The lidar array exceeds its payload or does not contain whole records." );

      var fields = new List<LidarSignalField>();
      Flatten( array, 0, string.Empty, array.Stride, fields );
      if ( fields.Count == 0 )
        throw new NotSupportedException( "The lidar record contains no supported scalar fields." );
      fields = fields.OrderBy( value => value.Offset ).ThenBy( value => value.Path, StringComparer.Ordinal ).ToList();
      if ( fields.Select( value => value.Path ).Distinct( StringComparer.Ordinal ).Count() != fields.Count )
        throw new ArgumentException( "The lidar layout contains duplicate field paths." );

      var sameSchema = RecordSize == array.Stride && m_arrayOffset == offset && ArrayPath == path && m_fields.Count == fields.Count;
      for ( int i = 0; sameSchema && i < fields.Count; ++i ) {
        var left = m_fields[ i ];
        var right = fields[ i ];
        sameSchema = left.Path == right.Path && left.Type == right.Type && left.ByteSize == right.ByteSize && left.Offset == right.Offset;
      }
      var destination = m_buffer.Length < layout.BufferSize ? new byte[layout.BufferSize] : m_buffer;
      if ( marshalling != null )
        marshalling.CopyBufferTo( destination, 0 );
      else
        Buffer.BlockCopy( buffer, 0, destination, 0, layout.BufferSize );

      m_buffer = destination;
      if ( !sameSchema ) {
        m_fields = fields.AsReadOnly();
        m_lookup.Clear();
        foreach ( var field in fields )
          m_lookup.Add( field.Path, field );
        ++SchemaVersion;
      }
      m_arrayOffset = offset;
      ArrayPath = path;
      RecordSize = array.Stride;
      ByteCount = layout.BufferSize;
      SampleCount = array.Count;
    }

    private static void FindArray( SignalLayout layout, int offset, string path,
                                   ref SignalLayout array, ref int arrayOffset, ref string arrayPath )
    {
      if ( layout.ArrayType != openplx.FieldArrayType.None ) {
        if ( array != null || layout.ArrayType != openplx.FieldArrayType.Flexible )
          throw new NotSupportedException( "The lidar payload must contain exactly one flexible array." );
        array = layout;
        arrayOffset = offset;
        arrayPath = path;
        return;
      }
      foreach ( var child in layout.Children ) {
        if ( child.Value.Offset < 0 )
          throw new ArgumentException( "A lidar layout offset cannot be negative." );
        FindArray( child.Value, checked(offset + child.Value.Offset), Join( path, child.Key ), ref array, ref arrayOffset, ref arrayPath );
      }
    }

    private static void Flatten( SignalLayout layout, int offset, string path, int stride, List<LidarSignalField> fields )
    {
      foreach ( var field in layout.Fields ) {
        var fieldOffset = checked(offset + field.Offset);
        if ( field.ArrayType != openplx.FieldArrayType.None )
          throw new NotSupportedException( "Arrays nested within a lidar record are not supported." );
        if ( field.Offset < 0 || fieldOffset < 0 || field.Size <= 0 || checked(fieldOffset + field.Size) > stride )
          throw new ArgumentException( $"Lidar field '{Join( path, field.Name )}' exceeds the record stride." );
        var real = field.Type == openplx.FieldType.Real && ( field.Size == 4 || field.Size == 8 );
        var integer = ( field.Type == openplx.FieldType.Int || field.Type == openplx.FieldType.UInt || field.Type == openplx.FieldType.Bool ) &&
                      ( field.Size == 1 || field.Size == 2 || field.Size == 4 || field.Size == 8 );
        if ( !real && !integer )
          throw new NotSupportedException( $"Unsupported lidar field '{Join( path, field.Name )}': {field.Type}, {field.Size} bytes." );
        fields.Add( new LidarSignalField( Join( path, field.Name ), field.Type, field.Size, fieldOffset ) );
      }
      foreach ( var child in layout.Children ) {
        if ( child.Value.ArrayType != openplx.FieldArrayType.None )
          throw new NotSupportedException( "Arrays nested within a lidar record are not supported." );
        if ( child.Value.Offset < 0 )
          throw new ArgumentException( "A lidar record offset cannot be negative." );
        Flatten( child.Value, checked(offset + child.Value.Offset), Join( path, child.Key ), stride, fields );
      }
    }

    private static string Join( string parent, string child ) => string.IsNullOrEmpty( parent ) ? child : parent + "." + child;
  }

  /// <summary>Reads an existing, registered OpenPLX lidar output through the signal heap.</summary>
  public sealed class LidarSignalReader
  {
    private readonly OutputSource m_output;
    public LidarSignalFrame Frame { get; } = new LidarSignalFrame();

    public LidarSignalReader( OutputSource output )
    {
      m_output = output ?? throw new ArgumentNullException( nameof( output ) );
      if ( !output.IsValid || output.SignalRoot.HeapControlInterface == null )
        throw new ArgumentException( "The output must belong to initialized OpenPLX signals.", nameof( output ) );
      if ( !( output.Native is openplx.Sensors.Signals.LidarOutput ) )
        throw new ArgumentException( "Select an OpenPLX lidar output.", nameof( output ) );
    }

    /// <summary>Updates and returns Frame. No native buffer or marshalling handle escapes this call.</summary>
    public LidarSignalFrame Read()
    {
      if ( !m_output.IsValid || m_output.SignalRoot.HeapControlInterface == null )
        throw new InvalidOperationException( "The OpenPLX lidar output has been invalidated." );
      var heap = m_output.SignalRoot.HeapControlInterface;
      lock ( heap ) {
        using ( var marshalling = heap.prepare_read( m_output.Native ) )
          Frame.ReadFrom( marshalling );
      }
      return Frame;
    }
  }
}
