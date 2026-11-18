using AGXUnity.IO.OpenPLX;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AGXUnityTesting.Runtime
{
  public class SignalWrapperTests
  {
    private sealed class NativeLidarFixture : IDisposable
    {
      private readonly List<IDisposable> m_owned = new List<IDisposable>();
      public openplx.Core.Object Root;
      public agxSensor.Lidar Sensor;
      public openplx.HeapControlInterface Heap;
      public LidarSignalBindings Bindings;
      public agxopenplx.AgxMetadata Metadata;

      public NativeLidarFixture( string model = "LidarSignalWrapperTestScene" )
      {
        Own( new agx.AutoInit() );
        var bundles = Own( new std.StringVector( new[] { System.IO.Path.GetFullPath( "Assets/AGXUnity/OpenPLX" ) } ) );
        var context = Own( new openplx.Core.Api.OpenPlxContext( bundles ) );
        agxOpenPLXSWIG.register_bundles( context );
        agxOpenPLXSWIG.register_plugins( context, null );
        var file = model == "LidarSignalWrapperTestScene" ? "lidar_signal_wrappers_test.openplx" : "signal_wrappers_test.openplx";
        Root = Own( CoreSWIG.loadModelFromFile( System.IO.Path.GetFullPath( "Assets/AGXUnity/" + file ), model, context ) );
        Assert.That( context.hasErrors(), Is.False );
        Assert.That( Root, Is.Not.Null );
        var frame = Own( new agx.Frame() );
        var pattern = Own( new agxSensor.LidarRayPatternHorizontalSweep() );
        var range = Own( new agx.RangeReal32( 0.1f, 100.0f ) );
        var nativeModel = Own( new agxSensor.LidarModel( pattern, range ) );
        Sensor = Own( new agxSensor.Lidar( frame, nativeModel ) );
        Metadata = Own( new agxopenplx.AgxMetadata() );
        var entries = Own( new std.StringReferenceLookup() );
        entries.Add( new KeyValuePair<string, agx.Referenced>( Root.getObject( "lidar" ).getName(), Sensor ) );
        var mapper = Own( agxopenplx.AgxObjectMap.createPreMapped( entries, agxopenplx.AgxObjectMapMode.Name ) );
        var dispatch = Own( new openplx.ControlDispatch() );
        agxOpenPLXSWIG.register_control_handlers( dispatch, mapper, Metadata );
        var control = Own( new openplx.ControlInterface( dispatch ) );
        control.add_controls_from_object( Root );
        control.prepare_controls();
        Heap = Own( new openplx.HeapControlInterface( control ) );
        Bindings = Own( new LidarSignalBindings( Metadata ) );
      }

      private T Own<T>( T value ) where T : class, IDisposable
      {
        if ( value != null )
          m_owned.Add( value );
        return value;
      }

      public void Dispose()
      {
        for ( int i = m_owned.Count - 1; i >= 0; --i )
          m_owned[ i ].Dispose();
      }
    }

    [Test, Category( "Native" )]
    public void LidarBindingsShareInterfaceAliasesAndReadAnEmptyFirstScan()
    {
      using ( var fixture = new NativeLidarFixture() ) {
        var definition = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "samples_output" );
        var alias = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "controls.samples" );
        Assert.That( fixture.Heap.prepare_read( alias ), Is.Null );
        var native = fixture.Bindings.Register( definition, fixture.Sensor, 1 );
        Assert.That( fixture.Bindings.Register( alias, fixture.Sensor, 2 ), Is.SameAs( native ) );
        Assert.That( fixture.Bindings.Count, Is.EqualTo( 1 ) );
        Assert.That( fixture.Sensor.getOutputHandler().get( 2 ), Is.Null );
        using ( var marshalling = fixture.Heap.prepare_read( alias ) ) {
          Assert.That( marshalling, Is.Not.Null );
          var transfer = new SignalTransfer<LidarSample[]>( marshalling, true );
          Assert.That( transfer.Read( marshalling ), Is.Empty );
          Assert.That( transfer.ReadInto( marshalling, new LidarSample[ 3 ] ), Is.Zero );
        }
        // Retain a separate native reference so the detached graph can be
        // inspected after the registration owner disposes its proxy.
        using ( var retained = new agxSensor.RtOutput( agxSensor.RtOutput.getCPtr( native ).Handle, true ) ) {
          fixture.Bindings.Dispose();
          Assert.That( retained.getParent(), Is.Null );
          Assert.That( fixture.Bindings.Count, Is.Zero );
          Assert.That( fixture.Heap.prepare_read( alias ), Is.Null );
          Assert.Throws<ObjectDisposedException>( () => fixture.Bindings.Register( alias, fixture.Sensor, 3 ) );
        }
      }
    }

    [Test, Category( "Native" )]
    public void SignalInterfaceLooksUpExactMemberNames()
    {
      using ( var fixture = new NativeLidarFixture( "LidarWrapperFixture" ) ) {
        var native = (openplx.Physics.Signals.Output)fixture.Root.getObject( "distances" );
        var longer = new OutputSource( "Scene.controls.long_samples", native );
        var exact = new OutputSource( "Scene.controls.samples", native );
        var signalInterface = new SignalInterface {
          Outputs = new List<OutputSource> { longer, exact },
          Inputs = new List<InputTarget>()
        };
        Assert.That( signalInterface.FindOutput( "samples" ), Is.SameAs( exact ) );
        Assert.That( signalInterface.FindOutput( "mples" ), Is.Null );
      }
    }

    [Test]
    public void SerializedInterfaceNamesRecoverTheirRootRelativePath()
    {
      Assert.That( OpenPLXSignals.RootRelativeInterfaceName( "controls", "Test.Scene.vehicle", "Test.Scene" ),
                   Is.EqualTo( "vehicle.controls" ) );
      Assert.That( OpenPLXSignals.RootRelativeInterfaceName( "controls", "Scene.vehicle", "Test.Scene" ),
                   Is.EqualTo( "vehicle.controls" ) );
      Assert.That( OpenPLXSignals.RootRelativeInterfaceName( "vehicle.controls", "Test.Scene.vehicle", "Test.Scene" ),
                   Is.EqualTo( "vehicle.controls" ) );
      Assert.That( OpenPLXSignals.RootRelativeInterfaceName( "controls", "Test.Scene", "Test.Scene" ),
                   Is.EqualTo( "controls" ) );
    }

    [Test, Category( "Native" )]
    public void LidarBindingsKeepSeparateOutputLayoutsOnTheSameSensor()
    {
      using ( var fixture = new NativeLidarFixture( "LidarWrapperFixture" ) ) {
        var distances = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "distances" );
        var samples = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "samples" );
        Assert.That( fixture.Bindings.Register( distances, fixture.Sensor, 1 ).getElementSize(), Is.EqualTo( 4 ) );
        Assert.That( fixture.Bindings.Register( samples, fixture.Sensor, 2 ).getElementSize(), Is.EqualTo( 28 ) );
        Assert.That( fixture.Bindings.Count, Is.EqualTo( 2 ) );
        using ( var marshalling = fixture.Heap.prepare_read( distances ) )
          Assert.That( new SignalTransfer<float[]>( marshalling, true ).Read( marshalling ), Is.Empty );
        using ( var marshalling = fixture.Heap.prepare_read( samples ) )
          Assert.That( new SignalTransfer<LidarSample[]>( marshalling, true ).Read( marshalling ), Is.Empty );
      }
    }

    [Test, Category( "Native" )]
    public void LidarBindingsRejectUnknownFieldsBeforeAttachingAnOutput()
    {
      using ( var fixture = new NativeLidarFixture() )
      using ( var fields = new std.AnyVector() )
      using ( var value = new openplx.Core.Any( -123L ) ) {
        var definition = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "samples_output" );
        fields.Add( value );
        using ( var array = openplx.Core.Any.fromArray( fields ) )
          definition.setDynamic( "fields", array );
        var error = Assert.Throws<NotSupportedException>( () => fixture.Bindings.Register( definition, fixture.Sensor, 1 ) );
        Assert.That( error.Message, Does.Contain( "-123" ) );
        Assert.That( fixture.Bindings.Count, Is.Zero );
        Assert.That( fixture.Sensor.getOutputHandler().get( 1 ), Is.Null );
      }
    }

    [Test, Category( "Native" )]
    public void FailedLidarRegistrationDetachesItsOutputAndPreservesTheExistingBinding()
    {
      using ( var fixture = new NativeLidarFixture() )
      using ( var other = new LidarSignalBindings( fixture.Metadata ) ) {
        var definition = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "samples_output" );
        var original = fixture.Bindings.Register( definition, fixture.Sensor, 1 );
        Assert.Throws<InvalidOperationException>( () => other.Register( definition, fixture.Sensor, 2 ) );
        Assert.That( other.Count, Is.Zero );
        using ( var rejected = fixture.Sensor.getOutputHandler().get( 2 ) )
          Assert.That( rejected.getParent(), Is.Null, "A rejected output must not remain in the processing graph." );
        Assert.That( original.getParent(), Is.Not.Null );
        using ( var marshalling = fixture.Heap.prepare_read( definition ) )
          Assert.That( marshalling, Is.Not.Null );
      }
    }

    [Test, Category( "Native" )]
    public void LidarBindingsCanUnregisterAfterTheOriginalSensorProxyIsDisposed()
    {
      using ( var fixture = new NativeLidarFixture() ) {
        var definition = (openplx.Sensors.Signals.LidarOutput)fixture.Root.getObject( "samples_output" );
        fixture.Bindings.Register( definition, fixture.Sensor, 1 );
        fixture.Sensor.Dispose();
        Assert.DoesNotThrow( () => fixture.Bindings.Dispose() );
        Assert.That( fixture.Bindings.Count, Is.Zero );
      }
    }

    [TestCase( "Test.SignalScene.motor" )]
    [TestCase( "SignalScene.motor" )]
    [TestCase( "motor" )]
    [Category( "Native" )]
    public void BundledMotorDeclarationsResolveAndFlushToTheNativeController( string declaration )
    {
      using ( var initialization = new agx.AutoInit() )
      using ( var bundles = new std.StringVector( new[] { System.IO.Path.GetFullPath( "Assets/AGXUnity/OpenPLX" ) } ) )
      using ( var context = new openplx.Core.Api.OpenPlxContext( bundles ) ) {
        agxOpenPLXSWIG.register_bundles( context );
        agxOpenPLXSWIG.register_plugins( context, null );
        using ( var root = CoreSWIG.loadModelFromFile(
          System.IO.Path.GetFullPath( "Assets/AGXUnity/Tests/Runtime/Test Resources/TestBundle/motor_signal.openplx" ), "SignalScene", context ) ) {
          Assert.That( context.hasErrors(), Is.False );
          Assert.That( root, Is.Not.Null );
          var objectMap = new Dictionary<string, openplx.Core.Object>();
          foreach ( var obj in openplx.Core.Api.OpenPlxContextInternal.fromContext( context ).getRegisteredObjects() )
            objectMap[ obj.getName() ] = obj;
          using ( var resolved = OpenPLXRoot.ResolveNativeDeclaration( root, objectMap, declaration ) ) {
            Assert.That( resolved, Is.Not.Null, declaration );
            Assert.That( resolved.getName(), Is.EqualTo( "Test.SignalScene.motor" ) );
            using ( var hinge = new agx.Hinge() )
            using ( var motor = hinge.getMotor1D() )
            using ( var entries = new std.StringReferenceLookup() ) {
              entries.Add( new KeyValuePair<string, agx.Referenced>( resolved.getName(), motor ) );
              using ( var mapper = agxopenplx.AgxObjectMap.createPreMapped( entries, agxopenplx.AgxObjectMapMode.Name ) )
              using ( var metadata = new agxopenplx.AgxMetadata() )
              using ( var dispatch = new openplx.ControlDispatch() ) {
                agxOpenPLXSWIG.register_control_handlers( dispatch, mapper, metadata );
                using ( var control = new openplx.ControlInterface( dispatch ) ) {
                  control.add_controls_from_object( root );
                  control.prepare_controls();
                  using ( var heap = new openplx.HeapControlInterface( control ) )
                  using ( var input = root.getObject( "motor_speed" ) as openplx.Physics.Signals.Input ) {
                    foreach ( var speed in new[] { 2.75, -1.5 } )
                      using ( var marshalling = heap.prepare_write( input ) ) {
                        new SignalTransfer<double>( marshalling, false ).Write( marshalling, speed );
                        Assert.That( heap.flush(), Is.True );
                        Assert.That( motor.getSpeed(), Is.EqualTo( speed ).Within( 1e-10 ) );
                      }
                  }
                }
              }
            }
          }
        }
      }
    }

    [Test, Category( "Native" )]
    public void ImportedInterfaceAliasesResolveToTheirNativeSignals()
    {
      using ( var initialization = new agx.AutoInit() )
      using ( var bundles = new std.StringVector( new[] { System.IO.Path.GetFullPath( "Assets/AGXUnity/OpenPLX" ) } ) )
      using ( var context = new openplx.Core.Api.OpenPlxContext( bundles ) ) {
        agxOpenPLXSWIG.register_bundles( context );
        agxOpenPLXSWIG.register_plugins( context, null );
        var root = CoreSWIG.loadModelFromFile(
          System.IO.Path.GetFullPath( "Assets/AGXUnity/signal_wrappers_test.openplx" ), "SignalWrapperTestScene", context );
        Assert.That( context.hasErrors(), Is.False );
        Assert.That( root, Is.Not.Null );

        // The importer indexes registered objects by their canonical names.
        // Interface references have declaration paths but aren't new objects.
        var objectMap = new Dictionary<string, openplx.Core.Object>();
        foreach ( var obj in openplx.Core.Api.OpenPlxContextInternal.fromContext( context ).getRegisteredObjects() )
          objectMap[ obj.getName() ] = obj;

        var controls = root.getObject( "controls" );
        int count = 0;
        foreach ( var (name, signal) in controls.getEntries<openplx.Physics.Signals.Input>() ) {
          AssertAlias( name, signal );
          ++count;
        }
        foreach ( var (name, signal) in controls.getEntries<openplx.Physics.Signals.Output>() ) {
          AssertAlias( name, signal );
          ++count;
        }
        Assert.That( count, Is.EqualTo( 9 ) );
        Assert.That( OpenPLXRoot.ResolveNativeDeclaration( root, objectMap, "controls.missing" ), Is.Null );

        void AssertAlias( string name, openplx.Core.Object signal )
        {
          foreach ( var declaration in new[] { "controls." + name, root.getName() + ".controls." + name, signal.getName() } ) {
            var resolved = OpenPLXRoot.ResolveNativeDeclaration( root, objectMap, declaration );
            Assert.That( resolved, Is.Not.Null, declaration );
            Assert.That( resolved.getUuid(), Is.EqualTo( signal.getUuid() ), declaration );
            Assert.That( resolved.GetType(), Is.EqualTo( signal.GetType() ), declaration );
          }
        }
      }
    }

    [Test, Category( "Native" )]
    public void LidarSceneExposesTheConfiguredPackedSamplesThroughItsInterface()
    {
      using ( var initialization = new agx.AutoInit() )
      using ( var bundles = new std.StringVector( new[] { System.IO.Path.GetFullPath( "Assets/AGXUnity/OpenPLX" ) } ) )
      using ( var context = new openplx.Core.Api.OpenPlxContext( bundles ) ) {
        agxOpenPLXSWIG.register_bundles( context );
        agxOpenPLXSWIG.register_plugins( context, null );
        var root = CoreSWIG.loadModelFromFile(
          System.IO.Path.GetFullPath( "Assets/AGXUnity/lidar_signal_wrappers_test.openplx" ), "LidarSignalWrapperTestScene", context );
        Assert.That( context.hasErrors(), Is.False );
        Assert.That( root, Is.Not.Null );
        var lidar = root.getObject( "lidar" ) as openplx.Sensors.PulsedLidarLogic;
        Assert.That( lidar, Is.Not.Null );
        var sweep = lidar.ray_source() as openplx.Sensors.Optics.HorizontalSweepRaySource;
        Assert.That( sweep, Is.Not.Null );
        Assert.That( sweep.horizontal_resolution(), Is.EqualTo( 180 ) );
        Assert.That( sweep.vertical_resolution(), Is.EqualTo( 5 ) );

        foreach ( var name in new[] { "floor", "near_target", "left_target", "right_target", "back_wall" } ) {
          var body = root.getObject( name ) as openplx.Physics3D.Bodies.RigidBody;
          Assert.That( body, Is.Not.Null, name );
          Assert.That( body.is_dynamic(), Is.False, name );
        }

        var controls = root.getObject( "controls" ) as openplx.Physics.Signals.SignalInterface;
        Assert.That( controls, Is.Not.Null );
        Assert.That( controls.enable(), Is.True );
        var samples = controls.getObject( "samples" ) as openplx.Sensors.Signals.LidarOutput;
        Assert.That( samples, Is.Not.Null );
        Assert.That( samples.getUuid(), Is.EqualTo( root.getObject( "samples_output" ).getUuid() ) );
        Assert.That( samples.source().getUuid(), Is.EqualTo( lidar.getUuid() ) );
        using ( var fields = new openplx.Sensors.Signals.LidarOutputField() )
          CollectionAssert.AreEqual(
            new[] { fields.Position3D_F32(), fields.TimeStamp_F64(), fields.Distance_F32(), fields.IsHit_I32() }, samples.fields() );

        var layout = SensorLayout( samples.marshalling(), 2 );
        Assert.That( layout.BufferSize, Is.EqualTo( 56 ), "Two packed 28-byte samples" );
        var bytes = new byte[56];
        Buffer.BlockCopy( new[] { 1.0f, 2.0f, 3.0f }, 0, bytes, 0, 12 );
        Buffer.BlockCopy( BitConverter.GetBytes( 1.25 ), 0, bytes, 12, 8 );
        Buffer.BlockCopy( BitConverter.GetBytes( 7.0f ), 0, bytes, 20, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 1 ), 0, bytes, 24, 4 );
        var result = new SignalMapping<LidarSample[]>( layout, true ).Read( bytes, layout );
        Assert.That( result.Length, Is.EqualTo( 2 ) );
        Assert.That( result[ 0 ].position3d.z, Is.EqualTo( 3 ) );
        Assert.That( result[ 0 ].timestamp, Is.EqualTo( 1.25 ) );
        Assert.That( result[ 0 ].distance, Is.EqualTo( 7 ) );
        Assert.That( result[ 0 ].is_hit, Is.EqualTo( 1 ) );

        // A missing handler is a failed read, not a valid empty first scan.
        using ( var dispatch = new openplx.ControlDispatch() )
        using ( var control = new openplx.ControlInterface( dispatch ) ) {
          control.add_controls_from_signal_interface( controls );
          control.prepare_controls();
          using ( var heap = new openplx.HeapControlInterface( control ) )
          using ( var marshalling = heap.prepare_read( samples ) ) {
            Assert.That( marshalling, Is.Null );
            var name = root.getName() + ".controls.samples";
            var error = Assert.Throws<InvalidOperationException>( () => new SignalTransfer<LidarSample[]>( marshalling, true, name ) );
            Assert.That( error.Message, Does.Contain( name ) );
            Assert.That( error.Message, Does.Contain( "handler" ) );
          }
        }
      }
    }

    [Test, Category( "Native" )]
    public void AgxAndOpenPlxNativeRuntimesCanInitializeTogether()
    {
      // Buffer-only tests load openplxDotNetRuntime but do not necessarily load
      // agxDotNetRuntime or its agxOpenPLX dependency, as editor startup does.
      using ( var initialization = new agx.AutoInit() )
      using ( var body = new agx.RigidBody() )
      using ( var marshalling = new openplx.Marshalling() ) {
        body.setVelocity( new agx.Vec3( 1, 2, 3 ) );
        Assert.That( body.getVelocity(), Is.EqualTo( new agx.Vec3( 1, 2, 3 ) ) );
        Assert.That( marshalling.CopyBuffer(), Is.Empty );
        Assert.That( marshalling.get_nested_map(), Is.Empty );
      }
    }

    public struct Pair
    {
      public float x;
      public float y;
    }

    public class Properties
    {
      public decimal x { get; set; }
      public byte y { get; set; }
    }

    public struct Mixed
    {
      public int tag;
      public Pair value;
    }

    private static SignalLayout Scalar( openplx.FieldType type, int size, string name = "value" )
      => new SignalLayout { BufferSize = size, Stride = size, Fields = new[] { new SignalField( name, type, size ) } };

    private static SignalLayout PairLayout()
      => new SignalLayout
      {
        BufferSize = 8,
        Stride = 8,
        Fields = new[] {
        new SignalField( "x", openplx.FieldType.Real, 4 ), new SignalField( "y", openplx.FieldType.Real, 4, 4 ) }
      };

    private static SignalLayout Wrap( SignalLayout child )
      => new SignalLayout
      {
        BufferSize = child.Offset + child.BufferSize,
        Stride = child.Stride,
        Children = new Dictionary<string, SignalLayout> { { "wrapper", child } }
      };

    [Test]
    public void ArrayTypesCanBeUsedWithBothWrappers()
    {
      Assert.DoesNotThrow( () => typeof( OutputWrapper<> ).MakeGenericType( typeof( float[] ) ) );
      Assert.DoesNotThrow( () => typeof( InputWrapper<> ).MakeGenericType( typeof( float[] ) ) );
    }

    [TestCase( typeof( float ), openplx.FieldType.Int, true, true )]
    [TestCase( typeof( int ), openplx.FieldType.Real, false, true )]
    [TestCase( typeof( int ), openplx.FieldType.Real, true, false )]
    [TestCase( typeof( float ), openplx.FieldType.Int, false, false )]
    [TestCase( typeof( bool ), openplx.FieldType.Bool, true, true )]
    public void ScalarCompatibilityRespectsTransferDirection( Type type, openplx.FieldType nativeType, bool read, bool expected )
    {
      Assert.That( SignalUtils.IsValueTypeCompatible( type, nativeType, read ), Is.EqualTo( expected ) );
    }

    [Test]
    public void ReadsIntegerAsRealAndWritesIntegerToReal()
    {
      var integer = Scalar( openplx.FieldType.Int, 4 );
      Assert.That( new SignalMapping<double>( integer, true ).Read( BitConverter.GetBytes( 37 ), integer ), Is.EqualTo( 37.0 ) );
      var real = Scalar( openplx.FieldType.Real, 8 );
      var bytes = new byte[8];
      new SignalMapping<int>( real, false ).Write( bytes, real, -19 );
      Assert.That( BitConverter.ToDouble( bytes, 0 ), Is.EqualTo( -19.0 ) );
      Assert.Throws<ArgumentException>( () => new SignalMapping<int>( real, true ) );
      Assert.Throws<ArgumentException>( () => new SignalMapping<float>( integer, false ) );
    }

    [Test]
    public void SupportsSmallIntegersAndDecimalProperties()
    {
      var layout = new SignalLayout
      {
        BufferSize = 9,
        Fields = new[] {
        new SignalField( "x", openplx.FieldType.Real, 8 ), new SignalField( "y", openplx.FieldType.UInt, 1, 8 ) }
      };
      var bytes = new byte[9];
      new SignalMapping<Properties>( layout, false ).Write( bytes, layout, new Properties { x = 1.25m, y = 249 } );
      Assert.That( BitConverter.ToDouble( bytes, 0 ), Is.EqualTo( 1.25 ) );
      Assert.That( bytes[ 8 ], Is.EqualTo( 249 ) );
      var actual = new SignalMapping<Properties>( layout, true ).Read( bytes, layout );
      Assert.That( actual.x, Is.EqualTo( 1.25m ) );
      Assert.That( actual.y, Is.EqualTo( 249 ) );
      var signed = Scalar( openplx.FieldType.Int, 1 );
      Assert.That( new SignalMapping<sbyte>( signed, true ).Read( new byte[] { 253 }, signed ), Is.EqualTo( -3 ) );
    }

    [Test]
    public void UnwrapsSeveralLevelsAndMapsMixedNestedStructs()
    {
      var pair = PairLayout();
      pair.Offset = 4;
      var mixed = new SignalLayout
      {
        BufferSize = 12,
        Stride = 12,
        Fields = new[] { new SignalField( "tag", openplx.FieldType.Int, 4 ) },
        Children = new Dictionary<string, SignalLayout> { { "value", pair } }
      };
      var layout = Wrap( Wrap( mixed ) );
      var bytes = new byte[12];
      new SignalMapping<Mixed>( layout, false ).Write( bytes, layout, new Mixed { tag = 17, value = new Pair { x = 2.5f, y = -7.0f } } );
      Assert.That( BitConverter.ToInt32( bytes, 0 ), Is.EqualTo( 17 ) );
      Assert.That( BitConverter.ToSingle( bytes, 4 ), Is.EqualTo( 2.5f ) );
      Assert.That( BitConverter.ToSingle( bytes, 8 ), Is.EqualTo( -7.0f ) );
      var actual = new SignalMapping<Mixed>( layout, true ).Read( bytes, layout );
      Assert.That( actual.tag, Is.EqualTo( 17 ) );
      Assert.That( actual.value.y, Is.EqualTo( -7.0f ) );
    }

    [Test]
    public void CopiesStructArraysAndReusesOversizedDestination()
    {
      var array = PairLayout();
      array.ArrayType = openplx.FieldArrayType.Dynamic;
      array.Count = 2;
      array.BufferSize = 16;
      var layout = Wrap( array );
      var bytes = new byte[16];
      Buffer.BlockCopy( new[] { 1.0f, 2.0f, 3.0f, 5.0f }, 0, bytes, 0, bytes.Length );
      var reader = new SignalMapping<Pair[]>( layout, true );
      var destination = new Pair[3];
      destination[ 2 ].x = 99;
      Assert.That( reader.ReadInto( bytes, layout, destination ), Is.EqualTo( 2 ) );
      Assert.That( destination[ 0 ].y, Is.EqualTo( 2 ) );
      Assert.That( destination[ 1 ].x, Is.EqualTo( 3 ) );
      Assert.That( destination[ 2 ].x, Is.EqualTo( 99 ) );
      Assert.Throws<ArgumentException>( () => reader.ReadInto( bytes, layout, new Pair[ 1 ] ) );
      var written = new byte[16];
      new SignalMapping<Pair[]>( layout, false ).Write( written, layout, new[] { new Pair { x = -1, y = 4 }, new Pair { x = 6, y = 8 } } );
      Assert.That( BitConverter.ToSingle( written, 8 ), Is.EqualTo( 6 ) );
      Assert.That( BitConverter.ToSingle( written, 12 ), Is.EqualTo( 8 ) );
    }

    [Test]
    public void PrimitiveArrayReadsOwnTheirDataAndAcceptChangingLengths()
    {
      var layout = Scalar( openplx.FieldType.Real, 4 );
      layout.ArrayType = openplx.FieldArrayType.Dynamic;
      layout.Count = 2;
      layout.BufferSize = 8;
      var bytes = new byte[8];
      Buffer.BlockCopy( new[] { 3.0f, 7.0f }, 0, bytes, 0, 8 );
      var reader = new SignalMapping<float[]>( layout, true );
      var result = reader.Read( bytes, layout );
      Array.Clear( bytes, 0, bytes.Length );
      Assert.That( result, Is.EqualTo( new[] { 3.0f, 7.0f } ) );
      var empty = Scalar( openplx.FieldType.Real, 4 );
      empty.ArrayType = openplx.FieldArrayType.Dynamic;
      empty.Count = 0;
      empty.BufferSize = 0;
      Assert.That( reader.Read( Array.Empty<byte>(), empty ), Is.Empty );
    }

    [Test]
    public void RejectsArrayConversionsAndIncorrectLayouts()
    {
      var layout = PairLayout();
      layout.ArrayType = openplx.FieldArrayType.Dynamic;
      layout.Count = 2;
      layout.BufferSize = 16;
      Assert.Throws<ArgumentException>( () => new SignalMapping<double[]>( layout, true ) );
      layout.Stride = 12;
      Assert.Throws<ArgumentException>( () => new SignalMapping<Pair[]>( layout, true ) );
      Assert.That( Marshal.SizeOf<Pair>(), Is.EqualTo( 8 ) );
    }

    [Test]
    public void ArrayAnywhereDisablesConversionsForAllMembers()
    {
      var layout = Scalar( openplx.FieldType.Real, 8 );
      layout.Fields[ 0 ].ArrayType = openplx.FieldArrayType.Static;
      Assert.Throws<ArgumentException>( () => new SignalMapping<float[]>( layout, true ) );
    }

    [Test]
    public void RejectsMissingMembersAndOutOfBoundsBuffers()
    {
      var layout = PairLayout();
      Assert.Throws<ArgumentException>( () => new SignalMapping<int>( layout, true ) );
      var reader = new SignalMapping<Pair>( layout, true );
      Assert.Throws<ArgumentException>( () => reader.Read( new byte[ 7 ], layout ) );
    }

    [Test]
    public void NumericOverflowDoesNotSilentlyTruncate()
    {
      var layout = Scalar( openplx.FieldType.Int, 4 );
      var reader = new SignalMapping<byte>( layout, true );
      Assert.Throws<OverflowException>( () => reader.Read( BitConverter.GetBytes( 256 ), layout ) );
    }

    [Test]
    public void NestedOffsetsAndArrayPrefixesArePreserved()
    {
      var array = Scalar( openplx.FieldType.UInt, 2 );
      array.Offset = 8;
      array.ArrayType = openplx.FieldArrayType.Dynamic;
      array.Count = 3;
      array.BufferSize = 6;
      var root = Wrap( array );
      var bytes = new byte[14];
      bytes[ 0 ] = 99;
      new SignalMapping<ushort[]>( root, false ).Write( bytes, root, new ushort[] { 7, 11, 13 } );
      Assert.That( bytes[ 0 ], Is.EqualTo( 99 ) );
      Assert.That( BitConverter.ToUInt16( bytes, 8 ), Is.EqualTo( 7 ) );
      Assert.That( new SignalMapping<ushort[]>( root, true ).Read( bytes, root ), Is.EqualTo( new ushort[] { 7, 11, 13 } ) );
    }

    [Test]
    public void RejectsSchemaChangesAndWrongArrayWriteLengths()
    {
      var layout = Scalar( openplx.FieldType.Real, 4 );
      var changed = Scalar( openplx.FieldType.Int, 4 );
      var reader = new SignalMapping<float>( layout, true );
      Assert.Throws<ArgumentException>( () => reader.Read( new byte[ 4 ], changed ) );
      layout.ArrayType = openplx.FieldArrayType.Static;
      layout.Count = 2;
      layout.BufferSize = 8;
      var writer = new SignalMapping<float[]>( layout, false );
      Assert.Throws<ArgumentException>( () => writer.Write( new byte[ 8 ], layout, new float[ 1 ] ) );
      Assert.Throws<ArgumentException>( () => writer.Write( new byte[ 8 ], layout, new float[ 3 ] ) );
      Assert.Throws<ArgumentException>( () => new SignalMapping<float[,]>( layout, true ) );
    }

    public struct ReferencedSample
    {
      public string x;
      public float y;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct PackedSample
    {
      public byte x;
      public float y;
    }

    [Test]
    public void RejectsManagedReferencesAndAcceptsMatchingPackedStructs()
    {
      var layout = new SignalLayout
      {
        ArrayType = openplx.FieldArrayType.Static,
        Stride = 5,
        Count = 2,
        BufferSize = 10,
        Fields = new[] { new SignalField( "x", openplx.FieldType.UInt, 1 ), new SignalField( "y", openplx.FieldType.Real, 4, 1 ) }
      };
      Assert.Throws<ArgumentException>( () => new SignalMapping<ReferencedSample[]>( layout, true ) );
      var bytes = new byte[10];
      bytes[ 0 ] = 17;
      Buffer.BlockCopy( BitConverter.GetBytes( 2.5f ), 0, bytes, 1, 4 );
      var value = new SignalMapping<PackedSample[]>( layout, true ).Read( bytes, layout );
      Assert.That( value[ 0 ].x, Is.EqualTo( 17 ) );
      Assert.That( value[ 0 ].y, Is.EqualTo( 2.5f ) );
    }

    public class ReadonlyProperty
    {
      public float value => 1;
    }

    [Test]
    public void OutputRequiresWritableMembersButInputCanReadReadonlyProperties()
    {
      var layout = Scalar( openplx.FieldType.Real, 4 );
      Assert.Throws<ArgumentException>( () => new SignalMapping<ReadonlyProperty>( layout, true ) );
      var bytes = new byte[4];
      new SignalMapping<ReadonlyProperty>( layout, false ).Write( bytes, layout, new ReadonlyProperty() );
      Assert.That( BitConverter.ToSingle( bytes, 0 ), Is.EqualTo( 1 ) );
    }

    // Detached controls supply real heap-owned buffers without a scene or a
    // simulation. Scalar native reads provide an independent check of writes.
    private static string Controls( string layout ) => "{\"controls\":[{\"key\":1,\"name\":\"values\",\"uuid\":\"signal-wrapper-test\",\"control_type\":\"input\",\"model_type\":\"Values\",\"marshalling_key\":\"Values\"}],\"marshalling\":{\"Values\":" + layout + "}}";

    private const string NestedLayout = "{\"size\":16,\"fields\":{\"tag\":{\"field_type\":2,\"size\":4,\"offset\":0}},\"nested\":{\"value\":{\"size\":8,\"offset\":8,\"fields\":{\"x\":{\"field_type\":3,\"size\":4,\"offset\":0},\"y\":{\"field_type\":3,\"size\":4,\"offset\":4}}}}}";

    private static void ConfigureNested( openplx.ControlInterface control, bool flexible = false )
    {
      // The detached JSON reader currently restores fields and sizes but not
      // node offsets/array modes. Set those explicitly on the fixture layout.
      using ( var detached = control.lookup_detached_control( 1 ) )
      using ( var layout = detached.get_marshalling() ) {
        layout.set_buffer_size( flexible ? 0U : 16U );
        using ( var child = layout.get_nested_map()[ flexible ? "window" : "value" ] ) {
          child.set_offset( flexible ? 0U : 8U );
          child.set_array_type( flexible ? openplx.FieldArrayType.Flexible : openplx.FieldArrayType.None );
        }
      }
    }

    [Test, Category( "Native" )]
    public void NativeNestedWritesAndReadsRespectParentRelativeOffsets()
    {
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( NestedLayout ) );
        ConfigureNested( control );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.prepare_write( "values" ) ) {
          var writer = new SignalTransfer<Mixed>( marshalling, false );
          writer.Write( marshalling, new Mixed { tag = 23, value = new Pair { x = 2.5f, y = -9.0f } } );
          Assert.That( marshalling.readInt32( "tag" ), Is.EqualTo( 23 ) );
          using ( var child = marshalling.get_nested_map()[ "value" ] ) {
            Assert.That( child.readFloat( "x" ), Is.EqualTo( 2.5f ) );
            Assert.That( child.readFloat( "y" ), Is.EqualTo( -9.0f ) );
          }
          var actual = new SignalTransfer<Mixed>( marshalling, true ).Read( marshalling );
          Assert.That( actual.tag, Is.EqualTo( 23 ) );
          Assert.That( actual.value.y, Is.EqualTo( -9.0f ) );
        }
      }
    }

    private const string FlexibleLayout = "{\"size\":0,\"nested\":{\"window\":{\"size\":8,\"offset\":0,\"array_type\":3,\"fields\":{\"x\":{\"field_type\":3,\"size\":4,\"offset\":0},\"y\":{\"field_type\":3,\"size\":4,\"offset\":4}}}}}";

    [Test, Category( "Native" )]
    public void SizedNativeWritesSupportChangingArrayLengthsAndDetachedOwnership()
    {
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( FlexibleLayout ) );
        ConfigureNested( control, true );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) ) {
          SignalTransfer<Pair[]> writer;
          SignalTransfer<Pair[]> reader;
          using ( var initial = heap.PrepareWrite( "values", 0 ) ) {
            writer = new SignalTransfer<Pair[]>( initial, false );
            reader = new SignalTransfer<Pair[]>( initial, true );
          }
          var values = new[] { new Pair { x = 1, y = 2 }, new Pair { x = 3, y = 5 } };
          using ( var marshalling = writer.PrepareArrayWrite( heap, "signal-wrapper-test", values ) ) {
            writer.Write( marshalling, values );
            var bytes = marshalling.CopyBuffer();
            Assert.That( BitConverter.ToSingle( bytes, 12 ), Is.EqualTo( 5 ) );
            using ( var detached = marshalling.detach_copy() ) {
              var destination = new Pair[3];
              Assert.That( reader.ReadInto( detached, destination ), Is.EqualTo( 2 ) );
              Assert.That( destination[ 1 ].x, Is.EqualTo( 3 ) );
              using ( var next = heap.PrepareWrite( "values", 8 ) )
                writer.Write( next, new[] { new Pair { x = 7, y = 11 } } );
              Assert.That( reader.Read( detached )[ 1 ].y, Is.EqualTo( 5 ) );
            }
          }
          using ( var empty = heap.PrepareWrite( "values", 0 ) )
            Assert.That( reader.Read( empty ), Is.Empty );
        }
      }
    }

    [Test, Category( "Native" )]
    public void NativeCopyBoundsRejectInvalidTransfersBeforeModifyingStorage()
    {
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( NestedLayout ) );
        ConfigureNested( control );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.prepare_write( "values" ) ) {
          marshalling.CopyBufferFrom( new byte[ 16 ], 0 );
          var original = marshalling.CopyBuffer();
          Assert.Throws<ArgumentException>( () => marshalling.CopyBufferTo( new byte[ 15 ], 0 ) );
          Assert.Throws<ArgumentException>( () => marshalling.CopyBufferFrom( new byte[ 17 ], 0 ) );
          Assert.That( marshalling.CopyBuffer(), Is.EqualTo( original ) );
          using ( var empty = new openplx.Marshalling() ) {
            Assert.That( empty.CopyBuffer(), Is.Empty );
            empty.set_buffer_size( 1 );
            Assert.Throws<InvalidOperationException>( () => empty.CopyBuffer() );
          }
        }
      }
    }

    public struct Double3
    {
      public double x;
      public double y;
      public double z;
    }

    public struct Float3
    {
      public float x;
      public float y;
      public float z;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct LidarSample
    {
      public Float3 position3d;
      public double timestamp;
      public float distance;
      public int is_hit;
    }

    private static SignalLayout SensorLayout( openplx.Core.Object schema, int count )
    {
      using ( var marshalling = new openplx.Marshalling() ) {
        Assert.That( marshalling.add_fields_from_object( schema ), Is.True );
        int elementSize;
        using ( var window = marshalling.get_nested_map()[ "window" ] )
          elementSize = checked((int)window.get_size());
        marshalling.set_buffer_size( checked((uint)( count * elementSize )) );
        marshalling.calculate_nested_buffer_sizes();
        return SignalLayout.Capture( marshalling );
      }
    }

    [Test, Category( "Native" )]
    public void LidarMappingUsesTheActualConfiguredNativeSchema()
    {
      using ( var fields = new openplx.Sensors.Signals.LidarOutputField() )
      using ( var selected = new std.IntVector( new[] { fields.Position3D_F32(), fields.TimeStamp_F64(), fields.Distance_F32(), fields.IsHit_I32() } ) )
      using ( var schema = openplx.Sensors.Signals.LidarOutput.marshalling_from_fields( selected ) ) {
        var layout = SensorLayout( schema, 2 );
        var bytes = new byte[56];
        Buffer.BlockCopy( new[] { 1.0f, 2.0f, 3.0f }, 0, bytes, 0, 12 );
        Buffer.BlockCopy( BitConverter.GetBytes( 1.25 ), 0, bytes, 12, 8 );
        Buffer.BlockCopy( BitConverter.GetBytes( 7.0f ), 0, bytes, 20, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 1 ), 0, bytes, 24, 4 );
        Buffer.BlockCopy( BitConverter.GetBytes( 2.5 ), 0, bytes, 40, 8 );
        var result = new SignalMapping<LidarSample[]>( layout, true ).Read( bytes, layout );
        Assert.That( result.Length, Is.EqualTo( 2 ) );
        Assert.That( result[ 0 ].position3d.z, Is.EqualTo( 3 ) );
        Assert.That( result[ 0 ].timestamp, Is.EqualTo( 1.25 ) );
        Assert.That( result[ 0 ].distance, Is.EqualTo( 7 ) );
        Assert.That( result[ 0 ].is_hit, Is.EqualTo( 1 ) );
        Assert.That( result[ 1 ].timestamp, Is.EqualTo( 2.5 ) );
      }
    }

    [Test, Category( "Native" )]
    public void ImuMappingUnwrapsTheActualNativeSensorGrouping()
    {
      using ( var sensor = new openplx.Sensors.SensorLogic() )
      using ( var constants = new openplx.Sensors.Signals.IMUOutputFieldConstant() )
      using ( var fields = new std.SensorsSignalsIMUOutputFieldVector() ) {
        foreach ( var axis in new[] { constants.X_F64(), constants.Y_F64(), constants.Z_F64() } ) {
          using ( var field = new openplx.Sensors.Signals.IMUOutputField() )
          using ( var value = new openplx.Core.Any( axis ) )
          using ( var source = openplx.Core.Any.fromObject( sensor ) ) {
            field.setDynamic( "field", value );
            field.setDynamic( "sensor", source );
            fields.Add( field );
          }
        }
        using ( var schema = openplx.Sensors.Signals.IMUOutput.marshalling_from_fields( fields ) ) {
          var layout = SensorLayout( schema, 2 );
          var bytes = new byte[48];
          Buffer.BlockCopy( new[] { 1.25, 2.5, 3.75, 4.25, 5.5, 6.75 }, 0, bytes, 0, 48 );
          var result = new SignalMapping<Double3[]>( layout, true ).Read( bytes, layout );
          Assert.That( result.Length, Is.EqualTo( 2 ) );
          Assert.That( result[ 0 ].x, Is.EqualTo( 1.25 ) );
          Assert.That( result[ 1 ].z, Is.EqualTo( 6.75 ) );
          Assert.Throws<ArgumentException>( () => new SignalMapping<Float3[]>( layout, true ) );
        }
      }
    }

    [Test, Category( "Native" )]
    public void ExistingAgxVectorMembersRemainSupported()
    {
      var layout = new SignalLayout
      {
        BufferSize = 24,
        Fields = new[] {
        new SignalField( "x", openplx.FieldType.Real, 8 ), new SignalField( "y", openplx.FieldType.Real, 8, 8 ),
        new SignalField( "z", openplx.FieldType.Real, 8, 16 ) }
      };
      var value = new agx.Vec3( 1, 2, 3 );
      var bytes = new byte[24];
      new SignalMapping<agx.Vec3>( layout, false ).Write( bytes, layout, value );
      Assert.That( BitConverter.ToDouble( bytes, 16 ), Is.EqualTo( 3 ) );
      var result = new SignalMapping<agx.Vec3>( layout, true ).Read( bytes, layout );
      Assert.That( result.x, Is.EqualTo( 1 ) );
      Assert.That( result.y, Is.EqualTo( 2 ) );
      Assert.That( result.z, Is.EqualTo( 3 ) );
    }

    public class NestedPropertySource
    {
      public int tag = 1;
      public int Reads;
      public Pair value { get { ++Reads; return new Pair { x = 2, y = 3 }; } }
    }

    [Test]
    public void NestedInputPropertiesAreEvaluatedOncePerWrite()
    {
      var pair = PairLayout();
      pair.Offset = 4;
      var layout = new SignalLayout
      {
        BufferSize = 12,
        Fields = new[] { new SignalField( "tag", openplx.FieldType.Int, 4 ) },
        Children = new Dictionary<string, SignalLayout> { { "value", pair } }
      };
      var value = new NestedPropertySource();
      new SignalMapping<NestedPropertySource>( layout, false ).Write( new byte[ 12 ], layout, value );
      Assert.That( value.Reads, Is.EqualTo( 1 ) );
    }

    [Test]
    public void RejectsUnsupportedArrayElementTypesAndTruncatedNestedViews()
    {
      var layout = Scalar( openplx.FieldType.UInt, 2 );
      layout.ArrayType = openplx.FieldArrayType.Flexible;
      Assert.Throws<ArgumentException>( () => new SignalMapping<char[]>( layout, true ) );
      Assert.Throws<ArgumentException>( () => new SignalMapping<IntPtr[]>( layout, true ) );
      var pair = PairLayout();
      pair.BufferSize = 4;
      Assert.Throws<ArgumentException>( () => new SignalMapping<Pair>( Wrap( pair ), true ) );
    }

    [Test, Category( "Native" )]
    public void FlexiblePrimitiveFieldSupportsNativeBulkTransfers()
    {
      const string schema = "{\"size\":0,\"fields\":{\"values\":{\"field_type\":3,\"size\":4,\"offset\":0,\"field_array_type\":3}}}";
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( schema ) );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.PrepareWrite( "values", 12 ) ) {
          new SignalTransfer<float[]>( marshalling, false ).Write( marshalling, new[] { 2.0f, 3.0f, 5.0f } );
          Assert.That( BitConverter.ToSingle( marshalling.CopyBuffer(), 8 ), Is.EqualTo( 5 ) );
          var result = new SignalTransfer<float[]>( marshalling, true ).Read( marshalling );
          Assert.That( result, Is.EqualTo( new[] { 2.0f, 3.0f, 5.0f } ) );
        }
      }
    }

    public class OverflowSource
    {
      public long tag;
      public Pair value;
    }

    [Test, Category( "Native" )]
    public void FailedMappingLeavesTheNativeInputBufferUnchanged()
    {
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( NestedLayout ) );
        ConfigureNested( control );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.prepare_write( "values" ) ) {
          Assert.That( marshalling.writeInt32( "tag", 13 ), Is.True );
          var before = marshalling.CopyBuffer();
          var writer = new SignalTransfer<OverflowSource>( marshalling, false );
          Assert.Throws<OverflowException>( () => writer.Write( marshalling,
            new OverflowSource { tag = long.MaxValue, value = new Pair { x = 2, y = 3 } } ) );
          Assert.That( marshalling.CopyBuffer(), Is.EqualTo( before ) );
        }
      }
    }

    [Test, Category( "Native" )]
    public void DetachedChildViewRetainsTheOwningTree()
    {
      openplx.Marshalling retained;
      using ( var control = new openplx.ControlInterface( null ) ) {
        control.add_controls_from_json( Controls( NestedLayout ) );
        ConfigureNested( control );
        control.prepare_controls();
        using ( var heap = new openplx.HeapControlInterface( control ) )
        using ( var marshalling = heap.prepare_write( "values" ) ) {
          new SignalTransfer<Mixed>( marshalling, false ).Write( marshalling,
            new Mixed { tag = 17, value = new Pair { x = 3, y = 5 } } );
          using ( var detached = marshalling.detach_copy() )
            retained = detached.get_nested_map()[ "value" ];
        }
      }
      using ( retained ) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.That( retained.readFloat( "x" ), Is.EqualTo( 3 ) );
        Assert.That( retained.readFloat( "y" ), Is.EqualTo( 5 ) );
      }
    }
  }
}
