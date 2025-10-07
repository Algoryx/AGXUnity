using openplx.Physics.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Input = openplx.Physics.Signals.Input;
using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public struct SignalInterface
  {
    [SerializeField]
    public string Name;

    [SerializeField]
    public string Path;

    [SerializeField]
    public bool Enabled;

    [SerializeField]
    public List<OutputSource> Outputs;

    [SerializeField]
    public List<InputTarget> Inputs;

    public OutputSource FindOutput( string name ) => Outputs.FirstOrDefault( s => s.Name.EndsWith( name ) );
    public InputTarget FindInput( string name ) => Inputs.FirstOrDefault( s => s.Name.EndsWith( name ) );
  }

  [RequireComponent( typeof( OpenPLXRoot ) )]
  [AddComponentMenu( "" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#openplx-signals" )]
  public class OpenPLXSignals : ScriptComponent
  {
    [SerializeField]
    private List<OutputSource> m_outputs = new List<OutputSource>();
    [SerializeField]
    private List<InputTarget> m_inputs = new List<InputTarget>();
    [SerializeField]
    private List<SignalInterface> m_interfaces = new List<SignalInterface>();

    public OutputSource[] Outputs => m_outputs.ToArray();
    public InputTarget[] Inputs => m_inputs.ToArray();
    public SignalInterface[] Interfaces => m_interfaces.ToArray();

    private List<OutputSignal> m_outputSignalList = new List<OutputSignal>();

    [HideInInspector]
    public List<OutputSignal> OutputSignals => m_outputSignalList;

    [HideInInspector]
    public OpenPLXRoot Root => GetComponent<OpenPLXRoot>();

    private Dictionary<string, SignalEndpoint> m_declaredNameEndpointMap = new Dictionary<string, SignalEndpoint>();

    private Dictionary<Output, OutputSignal> m_outputSignalCache = new Dictionary<Output, OutputSignal>();

    private std.StringReferenceLookup m_nativeMap;
    private agxopenplx.InputSignalQueue NativeInputQueue;
    private agxopenplx.AgxObjectMap NativeMapper;
    private agxopenplx.InputSignalListener NativeInputListener;
    private agxopenplx.OutputSignalQueue NativeOutputQueue;
    private agxopenplx.OutputSignalListener NativeOutputListener;

    public InputTarget FindInputTarget( string name ) => m_inputs.Find( it => it.Name == name );

    public OutputSource FindOutputSource( string name ) => m_outputs.Find( os => os.Name == name );

    public void SendInputSignal( InputSignal input ) => NativeInputQueue.send( input );

    protected override bool Initialize()
    {
      Root.GetInitialized();

      var ok = true;
      foreach ( var input in m_inputs ) {
        ok &= input.Initialize( this );
        m_declaredNameEndpointMap[ input.Name ] = input;
      }
      foreach ( var output in m_outputs ) {
        ok &= output.Initialize( this );
        m_declaredNameEndpointMap[ output.Name ] = output;
      }

      foreach ( var sigInt in m_interfaces ) {
        foreach ( var input in sigInt.Inputs ) {
          ok &= input.Initialize( this );
          m_declaredNameEndpointMap[ input.Name ] = input;
        }
        foreach ( var output in sigInt.Outputs ) {
          ok &= output.Initialize( this );
          m_declaredNameEndpointMap[ output.Name ] = output;
        }
      }

      Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync += Post;

      m_nativeMap = new std.StringReferenceLookup();
      foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() ) {
        foreach ( var decl in openPLXObj.SourceDeclarations ) {
          var relative = decl.Replace( Root.PrunedNativeName + ".", "" ).Trim();
          var obj = Root.Native.getObject( relative);
          if ( obj == null )
            continue;
          var native = openPLXObj.FindCorrespondingNative( Root, obj );
          if ( native != null )
            m_nativeMap.Add( new( decl, native ) );
        }
      }

      foreach ( var (k, v) in Root.RuntimeMapped )
        m_nativeMap.Add( new( k, v ) );

      NativeInputQueue = agxopenplx.InputSignalQueue.create();
      NativeOutputQueue = agxopenplx.OutputSignalQueue.create();

      NativeMapper = agxopenplx.AgxObjectMap.createPreMapped( m_nativeMap, agxopenplx.AgxObjectMapMode.Name );

      NativeInputListener = new agxopenplx.InputSignalListener( NativeInputQueue, NativeMapper );
      NativeOutputListener = new agxopenplx.OutputSignalListener( Root.Native, NativeOutputQueue, NativeMapper );

      Simulation.Instance.Native.add( NativeInputListener );
      Simulation.Instance.Native.add( NativeOutputListener );

      return ok;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync -= Post;

        Simulation.Instance.Native.remove( NativeInputListener );
        Simulation.Instance.Native.remove( NativeOutputListener );
      }
    }

    void Post()
    {
      m_outputSignalList.Clear();
      if ( !isActiveAndEnabled )
        return;
      foreach ( var signal in NativeOutputQueue.getSignals() ) {
        if ( signal != null ) {
          m_outputSignalList.Add( signal );
          m_outputSignalCache[ signal.source() ] = signal;
        }
      }
    }

    internal void RegisterSignal( string signal, Input openPLXSignal ) => m_inputs.Add( new InputTarget( signal, openPLXSignal ) );
    internal void RegisterSignal( string signal, Output openPLXSignal ) => m_outputs.Add( new OutputSource( signal, openPLXSignal ) );
    internal void RegisterInterface( SignalInterface sigInterface ) => m_interfaces.Add( sigInterface );

    internal Object InitializeNativeEndpoint( string endpoint )
    {
      var rootName = Root.Native.getName() + ".";
      if ( !endpoint.StartsWith( rootName ) )
        rootName = rootName[ ( rootName.IndexOf( "." ) + 1 ).. ];
      var relSigName = endpoint.Replace( rootName, "" ).Trim();
      var signalObj = Root.Native.getObject(relSigName);
      if ( signalObj != null )
        return signalObj;
      else {
        Debug.LogError( $"{endpoint} does not exist!" );
        return null;
      }
    }

    #region Output Signal Helpers

    public bool HasCachedSignal( Output output ) => m_outputSignalCache.ContainsKey( output );

    public Value GetValue( string outputName )
    {
      if ( !m_declaredNameEndpointMap.TryGetValue( outputName, out var endpoint ) )
        return null;

      if ( endpoint is not OutputSource os )
        return null;

      return os.GetValue();
    }

    public Value GetValue( Output output )
    {
      if ( !m_outputSignalCache.TryGetValue( output, out var signal ) )
        return null;

      if ( signal == null )
        return null;

      if ( signal is not ValueOutputSignal vos )
        return null;

      return vos.value();
    }

    public T GetValue<T>( Output output )
    {
      if ( !m_outputSignalCache.TryGetValue( output, out var signal ) )
        throw new ArgumentException( $"Output '{output.getName()}' does not have a cached value", "output" );

      if ( !output.enabled() )
        throw new ArgumentException( $"Output '{output.getName()}' is not enabled" );

      if ( signal is not ValueOutputSignal vos )
        throw new ArgumentException( $"Output '{output.getName()}' did not send a ValueOutputSignal" );

      if ( !IsValueTypeCompatible<T>( signal.source().type(), true ) )
        throw new InvalidCastException( $"Cannot convert signal value of type '{signal.source().GetType().Name}' to provided type '{typeof( T ).Name}'" );

      if ( !TryConvertOutputSignal<T>( vos, out T converted ) )
        throw new InvalidCastException( "Could not map signal type to requested type" );

      return converted;
    }

    public bool TryGetValue<T>( Output output, out T value )
    {
      value = default;
      if ( !m_outputSignalCache.TryGetValue( output, out var signal ) )
        return false;

      if ( !output.enabled() ||
           signal == null ||
           signal is not ValueOutputSignal vos ||
           !IsValueTypeCompatible<T>( signal.source().type(), true ) ||
           !TryConvertOutputSignal( vos, out value ) )
        return false;

      return true;
    }

    public T GetValue<T>( string outputName )
    {
      if ( !m_declaredNameEndpointMap.TryGetValue( outputName, out var output ) )
        throw new ArgumentException( $"Specified output '{outputName}' does not exist", "outputName" );
      if ( output is not OutputSource os )
        throw new ArgumentException( $"Specified output '{outputName}' is not an output signal", "outputName" );

      return os.GetValue<T>();
    }

    public bool TryGetValue<T>( string outputName, out T value )
    {
      value = default;
      if ( !m_declaredNameEndpointMap.TryGetValue( outputName, out var output ) ||
           output is not OutputSource os )
        return false;

      return os.TryGetValue<T>( out value );
    }
    #endregion

    #region Signal Type Handling
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

    public static ValueType GetTypeEnum<T>()
    {
      switch ( Type.GetTypeCode( typeof( T ) ) ) {
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
      };

      if ( typeof( T ) == typeof( Vector3 )
        || typeof( T ) == typeof( agx.Vec3 )
        || typeof( T ) == typeof( agx.Vec3f )
        || typeof( T ) == typeof( openplx.Math.Vec3 ) )
        return ValueType.Vec3;

      if ( typeof( T ) == typeof( Vector2 )
        || typeof( T ) == typeof( agx.Vec2 )
        || typeof( T ) == typeof( agx.Vec2f )
        || typeof( T ) == typeof( openplx.Math.Vec2 ) )
        return ValueType.Vec2;

      return ValueType.Unknown;
    }

    private static ValueType[] s_typeCache;

    public static ValueType GetOpenPLXTypeEnum( long type )
    {
      if ( s_typeCache == null ) {

        var tempIO = new InputOutputType();

        var maxVal = 0L;
        var methods = typeof( InputOutputType ).GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        foreach ( var method in methods ) {
          if ( method.ReturnType == typeof( long ) ) {
            var val = (long)method.Invoke( tempIO, null );
            maxVal = System.Math.Max( maxVal, val );
          }
        }
        s_typeCache = new ValueType[ maxVal ];
        for ( var i = 0; i < maxVal; i++ )
          s_typeCache[ i ] = ValueType.Unknown;

        s_typeCache[ tempIO.Position1D() - 1 ]            = ValueType.Real;
        s_typeCache[ tempIO.Position3D() - 1 ]            = ValueType.Vec3;
        s_typeCache[ tempIO.RPY() - 1 ]                   = ValueType.Vec3;
        s_typeCache[ tempIO.Angle() - 1 ]                 = ValueType.Real;
        s_typeCache[ tempIO.Velocity1D() - 1 ]            = ValueType.Real;
        s_typeCache[ tempIO.Velocity3D() - 1 ]            = ValueType.Vec3;
        s_typeCache[ tempIO.AngularVelocity1D() - 1 ]     = ValueType.Real;
        s_typeCache[ tempIO.AngularVelocity3D() - 1 ]     = ValueType.Vec3;
        s_typeCache[ tempIO.Torque1D() - 1 ]              = ValueType.Real;
        s_typeCache[ tempIO.Torque3D() - 1 ]              = ValueType.Vec3;
        s_typeCache[ tempIO.Force1D() - 1 ]               = ValueType.Real;
        s_typeCache[ tempIO.Force3D() - 1 ]               = ValueType.Vec3;
        s_typeCache[ tempIO.Acceleration3D() - 1 ]        = ValueType.Vec3;
        s_typeCache[ tempIO.AngularAcceleration3D() - 1 ] = ValueType.Vec3;
        s_typeCache[ tempIO.Boolean() - 1 ]               = ValueType.Boolean;
        s_typeCache[ tempIO.Percentage() - 1 ]            = ValueType.Real;
        s_typeCache[ tempIO.Composite() - 1 ]             = ValueType.Ignored;
        s_typeCache[ tempIO.Integer() - 1 ]               = ValueType.Integer;
        s_typeCache[ tempIO.Duration() - 1 ]              = ValueType.Real;
        s_typeCache[ tempIO.TorqueRange() - 1 ]           = ValueType.Vec2;
        s_typeCache[ tempIO.ForceRange() - 1 ]            = ValueType.Vec2;
        s_typeCache[ tempIO.Rpm() - 1 ]                   = ValueType.Real;
        s_typeCache[ tempIO.Ratio() - 1 ]                 = ValueType.Real;

        if ( s_typeCache.Contains( ValueType.Unknown ) )
          Debug.LogWarning( "OpenPLX value type mapping contains unhandled value type(s)" );
      }

      if ( s_typeCache.Length < type )
        return ValueType.Unknown;
      return s_typeCache[ type - 1 ];
    }

    public static bool IsValueTypeCompatible<T>( long typeCode, bool toCSType )
    {
      var requestedType = GetTypeEnum<T>();
      var endpointType = GetOpenPLXTypeEnum(typeCode);

      if ( requestedType == ValueType.Unknown || requestedType == ValueType.Ignored ) {
        Debug.LogWarning( $"The requested type {typeof( T ).Name} is not supported" );
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

    private static bool TryConvertOutputSignal<T>( ValueOutputSignal vos, out T converted )
    {
      var value = vos.value();
      converted = default;

      if ( value is RealValue realVal ) {
        converted = (T)Convert.ChangeType( realVal.value(), typeof( T ) );
        return true;
      }
      else if ( value is IntValue intVal ) {
        converted = (T)Convert.ChangeType( intVal.value(), typeof( T ) );
        return true;
      }
      else if ( value is BoolValue boolVal ) {
        converted = (T)Convert.ChangeType( boolVal.value(), typeof( T ) );
        return true;
      }
      else if ( value is Vec3Value v3val ) {
        if ( typeof( T ) == typeof( UnityEngine.Vector3 ) )
          converted = (T)(object)v3val.value().ToVector3();
        else if ( typeof( T ) == typeof( agx.Vec3 ) )
          converted = (T)(object)v3val.value().ToVec3();
        else if ( typeof( T ) == typeof( agx.Vec3f ) )
          converted = (T)(object)v3val.value().ToVec3f();
        else if ( typeof( T ) == typeof( openplx.Math.Vec3 ) )
          converted = (T)(object)v3val.value();
        else
          return false;
        return true;
      }
      else if ( value is Vec2Value v2val ) {
        if ( typeof( T ) == typeof( UnityEngine.Vector2 ) )
          converted = (T)(object)v2val.value().ToVector2();
        else if ( typeof( T ) == typeof( agx.Vec2 ) )
          converted = (T)(object)v2val.value().ToVec2();
        else if ( typeof( T ) == typeof( agx.Vec2f ) )
          converted = (T)(object)v2val.value().ToVec2f();
        else if ( typeof( T ) == typeof( openplx.Math.Vec2 ) )
          converted = (T)(object)v2val.value();
        else
          return false;
        return true;
      }

      return false;
    }
    #endregion
  }
}
