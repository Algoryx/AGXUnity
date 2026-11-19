using AGXUnity.IO.OpenPLX;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGXUnityTesting.Runtime
{
  public class LidarSignalFrameTests
  {
    private static SignalLayout ArrayLayout( int count, int stride, params SignalField[] fields )
      => new SignalLayout
      {
        BufferSize = count * stride,
        Children = new Dictionary<string, SignalLayout> {
          { "window", new SignalLayout {
            ArrayType = openplx.FieldArrayType.Flexible, Count = count,
            Stride = stride, BufferSize = count * stride, Fields = fields
          } }
        }
      };

    [Test]
    public void ReadsNestedFieldsWithHeaderOffsetsAndPaddedRecords()
    {
      var layout = ArrayLayout( 2, 24, new SignalField( "timestamp", openplx.FieldType.Real, 8 ),
                                      new SignalField( "is_hit", openplx.FieldType.Int, 4, 20 ) );
      var window = layout.Children[ "window" ];
      window.Offset = 8;
      layout.BufferSize += 8;
      window.Children.Add( "position3d", new SignalLayout
      {
        Offset = 8,
        BufferSize = 12,
        Stride = 24,
        Fields = new[] { new SignalField( "x", openplx.FieldType.Real, 4 ),
                         new SignalField( "y", openplx.FieldType.Real, 4, 4 ),
                         new SignalField( "z", openplx.FieldType.Real, 4, 8 ) }
      } );
      var bytes = new byte[56];
      Buffer.BlockCopy( BitConverter.GetBytes( 12.5 ), 0, bytes, 32, 8 );
      Buffer.BlockCopy( BitConverter.GetBytes( 3.25f ), 0, bytes, 40, 4 );
      Buffer.BlockCopy( BitConverter.GetBytes( 1 ), 0, bytes, 52, 4 );
      var frame = new LidarSignalFrame();
      frame.Update( layout, bytes );
      Array.Clear( bytes, 0, bytes.Length );

      Assert.That( frame.SampleCount, Is.EqualTo( 2 ) );
      Assert.That( frame.RecordSize, Is.EqualTo( 24 ) );
      Assert.That( frame.ArrayPath, Is.EqualTo( "window" ) );
      Assert.That( frame.FindPositionField(), Is.EqualTo( "position3d" ) );
      Assert.That( frame.GetValue( 1, "timestamp" ), Is.EqualTo( 12.5 ) );
      Assert.That( frame.GetNumber( 1, frame.FindField( "position3d.x" ) ), Is.EqualTo( 3.25 ) );
      Assert.That( frame.GetValue( 1, "is_hit" ), Is.EqualTo( 1L ) );
      Assert.Throws<ArgumentOutOfRangeException>( () => frame.GetValue( 2, "timestamp" ) );
      Assert.Throws<ArgumentException>( () => frame.GetValue( 0, "missing" ) );
    }

    [Test]
    public void FrozenWindowRetainsValuesAndFieldsAfterTheLiveLayoutChanges()
    {
      var frame = new LidarSignalFrame();
      frame.Update( ArrayLayout( 1, 4, new SignalField( "distance", openplx.FieldType.Real, 4 ) ), BitConverter.GetBytes( 3.5f ) );
      var snapshot = frame.Copy();
      frame.Update( ArrayLayout( 1, 4, new SignalField( "distance", openplx.FieldType.Real, 4 ) ), BitConverter.GetBytes( 8.0f ) );
      Assert.That( snapshot.GetValue( 0, "distance" ), Is.EqualTo( 3.5f ), "A snapshot must survive reuse of the live byte buffer." );
      frame.Update( ArrayLayout( 1, 8, new SignalField( "timestamp", openplx.FieldType.Real, 8 ) ), BitConverter.GetBytes( 99.0 ) );
      Assert.That( snapshot.GetValue( 0, "distance" ), Is.EqualTo( 3.5f ) );
      Assert.That( snapshot.FindField( "timestamp" ), Is.Null );
      Assert.That( snapshot.RecordSize, Is.EqualTo( 4 ) );
      Assert.That( frame.GetValue( 0, "timestamp" ), Is.EqualTo( 99.0 ) );
      Assert.That( snapshot.FindPositionField(), Is.Null );
    }

    [Test]
    public void AutomaticallyFindsNestedPositionsButDoesNotTreatOtherVectorsAsPositions()
    {
      var layout = ArrayLayout( 1, 12, new SignalField( "normal.x", openplx.FieldType.Real, 4 ),
                                      new SignalField( "normal.y", openplx.FieldType.Real, 4, 4 ),
                                      new SignalField( "normal.z", openplx.FieldType.Real, 4, 8 ) );
      var frame = new LidarSignalFrame();
      frame.Update( layout, new byte[ 12 ] );
      Assert.That( frame.FindPositionField(), Is.Null );
      layout.Children[ "window" ].Fields = new[] {
        new SignalField( "hit.position3d.z", openplx.FieldType.Real, 4, 8 ),
        new SignalField( "hit.position3d.x", openplx.FieldType.Real, 4 ),
        new SignalField( "hit.position3d.y", openplx.FieldType.Real, 4, 4 ) };
      frame.Update( layout, new byte[ 12 ] );
      Assert.That( frame.FindPositionField(), Is.EqualTo( "hit.position3d" ) );
    }

    [Test]
    public void AdaptsToEmptyAndChangingScanLengthsWithoutChangingTheSchema()
    {
      var frame = new LidarSignalFrame();
      var field = new SignalField( "distance", openplx.FieldType.Real, 4 );
      frame.Update( ArrayLayout( 2, 4, field ), new byte[ 8 ] );
      var version = frame.SchemaVersion;
      var descriptor = frame.FindField( "distance" );
      frame.Update( ArrayLayout( 0, 4, field ), Array.Empty<byte>() );
      Assert.That( frame.SampleCount, Is.Zero );
      Assert.That( frame.ByteCount, Is.Zero );
      Assert.That( frame.Fields, Has.Count.EqualTo( 1 ) );
      Assert.That( frame.SchemaVersion, Is.EqualTo( version ) );
      Assert.That( frame.FindField( "distance" ), Is.SameAs( descriptor ) );
      Assert.Throws<ArgumentOutOfRangeException>( () => frame.GetValue( 0, "distance" ) );
      frame.Update( ArrayLayout( 1, 4, field ), BitConverter.GetBytes( 7.5f ) );
      Assert.That( frame.GetValue( 0, "distance" ), Is.EqualTo( 7.5f ) );
    }

    [Test]
    public void AdaptsToAReorderedLayoutAndRejectsOldFieldDescriptors()
    {
      var frame = new LidarSignalFrame();
      frame.Update( ArrayLayout( 1, 4, new SignalField( "distance", openplx.FieldType.Real, 4 ) ), new byte[ 4 ] );
      var previous = frame.FindField( "distance" );
      var version = frame.SchemaVersion;
      var bytes = new byte[12];
      Buffer.BlockCopy( BitConverter.GetBytes( 0.75f ), 0, bytes, 0, 4 );
      Buffer.BlockCopy( BitConverter.GetBytes( 42.5 ), 0, bytes, 4, 8 );
      frame.Update( ArrayLayout( 1, 12, new SignalField( "timestamp", openplx.FieldType.Real, 8, 4 ),
                                      new SignalField( "intensity", openplx.FieldType.Real, 4 ) ), bytes );
      Assert.That( frame.SchemaVersion, Is.GreaterThan( version ) );
      Assert.That( frame.Fields.Select( value => value.Path ), Is.EqualTo( new[] { "intensity", "timestamp" } ) );
      Assert.That( frame.GetValue( 0, "timestamp" ), Is.EqualTo( 42.5 ) );
      Assert.That( frame.GetValue( 0, "intensity" ), Is.EqualTo( 0.75f ) );
      Assert.That( frame.FindField( "distance" ), Is.Null );
      Assert.Throws<ArgumentException>( () => frame.GetNumber( 0, previous ) );
    }

    [Test]
    public void PreservesLargeIntegersAndBooleanValues()
    {
      var bytes = new byte[17];
      Buffer.BlockCopy( BitConverter.GetBytes( ulong.MaxValue ), 0, bytes, 0, 8 );
      Buffer.BlockCopy( BitConverter.GetBytes( long.MinValue ), 0, bytes, 8, 8 );
      bytes[ 16 ] = 1;
      var frame = new LidarSignalFrame();
      frame.Update( ArrayLayout( 1, 17, new SignalField( "id", openplx.FieldType.UInt, 8 ),
                                      new SignalField( "signed", openplx.FieldType.Int, 8, 8 ),
                                      new SignalField( "hit", openplx.FieldType.Bool, 1, 16 ) ), bytes );
      Assert.That( frame.GetValue( 0, "id" ), Is.EqualTo( ulong.MaxValue ) );
      Assert.That( frame.GetValue( 0, "signed" ), Is.EqualTo( long.MinValue ) );
      Assert.That( frame.GetValue( 0, "hit" ), Is.EqualTo( true ) );
    }

    [Test]
    public void RejectsInvalidBoundsAndUnsupportedFieldSizes()
    {
      var frame = new LidarSignalFrame();
      Assert.Throws<ArgumentException>( () => frame.Update(
        ArrayLayout( 1, 4, new SignalField( "distance", openplx.FieldType.Real, 4, 1 ) ), new byte[ 4 ] ) );
      Assert.Throws<NotSupportedException>( () => frame.Update(
        ArrayLayout( 1, 3, new SignalField( "distance", openplx.FieldType.Real, 3 ) ), new byte[ 3 ] ) );
      Assert.Throws<ArgumentException>( () => frame.Update(
        ArrayLayout( 2, 4, new SignalField( "distance", openplx.FieldType.Real, 4 ) ), new byte[ 4 ] ) );
      var multiple = ArrayLayout( 1, 4, new SignalField( "x", openplx.FieldType.Real, 4 ) );
      multiple.Children.Add( "other", multiple.Children[ "window" ] );
      Assert.Throws<NotSupportedException>( () => frame.Update( multiple, new byte[ 4 ] ) );
    }

    [Test, Category( "Native" )]
    public void FailedNativeCopyPreservesThePreviousFrame()
    {
      var frame = new LidarSignalFrame();
      frame.Update( ArrayLayout( 1, 4, new SignalField( "distance", openplx.FieldType.Real, 4 ) ), BitConverter.GetBytes( 3.5f ) );
      using ( var marshalling = new openplx.Marshalling() ) {
        marshalling.add_field( "distance", openplx.FieldType.Real, 4, 0, openplx.FieldArrayType.Flexible );
        // Declaring an extent does not allocate a native buffer. This forces a
        // checked copy failure after validating the larger, otherwise valid schema.
        marshalling.set_buffer_size( 8 );
        Assert.Throws<InvalidOperationException>( () => frame.ReadFrom( marshalling ) );
      }
      Assert.That( frame.SampleCount, Is.EqualTo( 1 ) );
      Assert.That( frame.GetValue( 0, "distance" ), Is.EqualTo( 3.5f ) );
    }

    [Test, Category( "Native" )]
    public void BulkReadOwnsItsBytesAfterNativeStorageIsOverwrittenAndDisposed()
    {
      const string controls = "{\"controls\":[{\"key\":1,\"name\":\"values\",\"uuid\":\"lidar-frame-test\",\"control_type\":\"input\",\"model_type\":\"Values\",\"marshalling_key\":\"Values\"}],\"marshalling\":{\"Values\":{\"size\":0,\"fields\":{\"distance\":{\"field_type\":3,\"size\":4,\"offset\":0,\"field_array_type\":3}}}}}";
      var frame = new LidarSignalFrame();
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( controls );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.PrepareWrite( "values", 12 ) ) {
          var bytes = new byte[12];
          Buffer.BlockCopy( new[] { 2.0f, 3.0f, 5.0f }, 0, bytes, 0, 12 );
          marshalling.CopyBufferFrom( bytes, 0 );
          frame.ReadFrom( marshalling );
          marshalling.CopyBufferFrom( new byte[ 12 ], 0 );
        }
      }
      Assert.That( frame.SampleCount, Is.EqualTo( 3 ) );
      Assert.That( frame.GetValue( 2, "distance" ), Is.EqualTo( 5.0f ) );
    }

    [Test, Category( "Native" )]
    public void DecodesADistanceOnlyNativeLidarSchema()
    {
      using ( var fields = new openplx.Sensors.Signals.LidarOutputField() )
      using ( var selected = new std.IntVector( new[] { fields.Distance_F32() } ) )
      using ( var schema = openplx.Sensors.Signals.LidarOutput.marshalling_from_fields( selected ) )
      using ( var marshalling = new openplx.Marshalling() ) {
        Assert.That( marshalling.add_fields_from_object( schema ), Is.True );
        marshalling.set_buffer_size( 8 );
        marshalling.calculate_nested_buffer_sizes();
        var bytes = new byte[8];
        Buffer.BlockCopy( BitConverter.GetBytes( 4.5f ), 0, bytes, 4, 4 );
        var frame = new LidarSignalFrame();
        frame.Update( SignalLayout.Capture( marshalling ), bytes );
        Assert.That( frame.SampleCount, Is.EqualTo( 2 ) );
        Assert.That( frame.Fields, Has.Count.EqualTo( 1 ) );
        Assert.That( frame.FindField( "position3d.x" ), Is.Null );
        Assert.That( frame.GetValue( 1, "distance" ), Is.EqualTo( 4.5f ) );
      }
    }

    [Test, Category( "Native" )]
    public void DecodesAllNativeLidarFieldsInAnArbitraryOrder()
    {
      using ( var fields = new openplx.Sensors.Signals.LidarOutputField() )
      using ( var selected = new std.IntVector( new[] {
        fields.EntityId_I32(), fields.Intensity_F32(), fields.Position3D_F32(), fields.TimeStamp_F64(),
        fields.RayPose3x4_F32(), fields.Distance_F32(), fields.IsHit_I32() } ) )
      using ( var schema = openplx.Sensors.Signals.LidarOutput.marshalling_from_fields( selected ) )
      using ( var marshalling = new openplx.Marshalling() ) {
        Assert.That( marshalling.add_fields_from_object( schema ), Is.True );
        using ( var window = marshalling.get_nested_map()[ "window" ] )
          Assert.That( window.get_size(), Is.EqualTo( 84 ) );
        marshalling.set_buffer_size( 84 );
        marshalling.calculate_nested_buffer_sizes();
        var bytes = new byte[84];
        Buffer.BlockCopy( BitConverter.GetBytes( -17 ), 0, bytes, 0, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 0.75f ), 0, bytes, 4, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 3.25f ), 0, bytes, 8, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 12.5 ), 0, bytes, 20, 8 );
        Buffer.BlockCopy( BitConverter.GetBytes( 6.5f ), 0, bytes, 76, 4 );
        var frame = new LidarSignalFrame();
        frame.Update( SignalLayout.Capture( marshalling ), bytes );
        Assert.That( frame.Fields, Has.Count.EqualTo( 20 ) );
        Assert.That( frame.GetValue( 0, "position3d.x" ), Is.EqualTo( 3.25f ) );
        Assert.That( frame.GetValue( 0, "timestamp" ), Is.EqualTo( 12.5 ) );
        Assert.That( frame.GetValue( 0, "distance" ), Is.EqualTo( 6.5f ) );
        Assert.That( frame.GetValue( 0, "intensity" ), Is.EqualTo( 0.75f ) );
      }
    }
  }
}
