using Brick.Physics.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Input = Brick.Physics.Signals.Input;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  [RequireComponent( typeof( BrickRoot ) )]
  public class BrickSignals : ScriptComponent
  {
    [SerializeField]
    private List<OutputSource> m_outputs = new List<OutputSource>();
    [SerializeField]
    private List<InputTarget> m_inputs = new List<InputTarget>();

    public OutputSource[] Outputs => m_outputs.ToArray();
    public InputTarget[] Inputs => m_inputs.ToArray();

    private Queue<InputSignal> m_inputSignalQueue = new Queue<InputSignal>();
    private List<OutputSignal> m_outputSignalList = new List<OutputSignal>();

    [HideInInspector]
    public List<OutputSignal> OutputSignals => m_outputSignalList;

    [HideInInspector]
    public BrickRoot Root => GetComponent<BrickRoot>();

    private Dictionary<Output, OutputSignal> m_outputCache = new Dictionary<Output, OutputSignal>();
    private Dictionary<string, SignalEndpoint> m_declaredNameEndpointMap = new Dictionary<string, SignalEndpoint>();

    public void RegisterSignal<T>( string signal, T brickSignal )
      where T : Brick.Core.Object
    {
      if ( brickSignal is Output output )
        m_outputs.Add( new OutputSource( signal, output ) );
      else if ( brickSignal is Input input )
        m_inputs.Add( new InputTarget( signal, input ) );
      else {
        Debug.LogError( "Provided signal is neither an input nor an output" );
        return;
      }
    }

    public InputTarget FindInputTarget( string name ) => m_inputs.Find( it => it.Name == name );
    public OutputSource FindOutputSource( string name ) => m_outputs.Find( os => os.Name == name );

    internal Object InitializeNativeEndpoint( string endpoint )
    {
      var relativeSigName = endpoint.Replace(Root.Native.getName() + ".", "").Trim();
      var signalObj = Root.Native.getObject(relativeSigName);
      if ( signalObj != null )
        return signalObj;
      else {
        Debug.LogError( $"{endpoint} does not exist!" );
        return null;
      }
    }

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

      Simulation.Instance.StepCallbacks._Internal_BrickSignalPreSync += Pre;
      Simulation.Instance.StepCallbacks._Internal_BrickSignalPostSync += Post;

      return ok;
    }

    void Pre()
    {
      while ( m_inputSignalQueue.TryDequeue( out var inpSig ) ) {
        switch ( inpSig ) {
          case RealInputSignal realSig: InputSignalHandler.HandleRealInputSignal( realSig, Root ); break;
          case IntInputSignal intSig: InputSignalHandler.HandleIntInputSignal( intSig, Root ); break;
          default: Debug.LogWarning( $"Unhandled InputSignal type: '{inpSig.GetType().Name}'" ); break;
        }
      }
    }

    void Post()
    {
      m_outputSignalList.Clear();
      foreach ( var outputSource in m_outputs ) {
        OutputSignal signal = OutputSignalGenerator.GenerateSignalFrom( outputSource.Native, Root );

        if ( signal != null ) {
          m_outputSignalList.Add( signal );
          m_outputCache[ outputSource.Native ] = signal;
        }
      }
    }

    public void SendInputSignal( InputSignal input )
    {
      m_inputSignalQueue.Enqueue( input );
    }

    public Value GetOutputValue( Output output )
    {
      if ( output == null || !m_outputCache.TryGetValue( output, out var signal ) )
        return null;

      if ( signal is not ValueOutputSignal vos )
        return null;

      return vos.value();

    }

    public Value GetOutputValue( string outputName )
    {
      if ( !m_declaredNameEndpointMap.TryGetValue( outputName, out var endpoint ) )
        return null;

      if ( endpoint is not OutputSource os )
        return null;

      return GetOutputValue( os.Native );
    }

    public T GetConvertedOutputValue<T>( Output output )
    {
      if ( !m_outputCache.TryGetValue( output, out var signal ) )
        throw new ArgumentException( "Specified output does not have a cached value", "output" );

      if ( signal is not ValueOutputSignal vos )
        throw new ArgumentException( $"Given output '{output.getName()}' did not send a ValueOutputSignal" );

      if ( !IsValueTypeCompatible<T>( signal.source().type(), true ) )
        throw new InvalidCastException( $"Cannot convert signal value of type '{signal.source().GetType().Name}' to provided type '{typeof( T ).Name}'" );

      var value = vos.value();

      if ( value is RealValue realVal )
        return (T)Convert.ChangeType( realVal.value(), typeof( T ) );
      else if ( value is Vec3Value v3val ) {
        if ( typeof( T ) == typeof( Vector3 ) )
          return (T)(object)v3val.value().ToVector3();
        else if ( typeof( T ) == typeof( agx.Vec3 ) )
          return (T)(object)v3val.value().ToVec3();
        else if ( typeof( T ) == typeof( agx.Vec3f ) )
          return (T)(object)v3val.value().ToVec3f();
        else if ( typeof( T ) == typeof( Brick.Math.Vec3 ) )
          return (T)(object)v3val.value();
      }

      throw new InvalidCastException( "Could not map signal type to requested type" );
    }

    public T GetConvertedOutputValue<T>( string outputName )
    {
      if ( !m_declaredNameEndpointMap.TryGetValue( outputName, out var output ) )
        throw new ArgumentException( $"Specified output '{outputName}' does not exist", "outputName" );
      if ( output is not OutputSource os )
        throw new ArgumentException( $"Specified output '{outputName}' is not an output signal", "outputName" );

      return GetConvertedOutputValue<T>( os.Native );
    }

    public enum ValueType
    {
      Integer,
      Real,
      Vec3,
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
        default: break;
      };

      if ( typeof( T ) == typeof( Vector3 )
        || typeof( T ) == typeof( agx.Vec3 )
        || typeof( T ) == typeof( agx.Vec3f )
        || typeof( T ) == typeof( Brick.Math.Vec3 ) )
        return ValueType.Vec3;

      return ValueType.Unknown;
    }

    private static ValueType[] s_typeCache;

    internal static ValueType GetBrickTypeEnum( long type )
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
        s_typeCache[ tempIO.ControlEvent() - 1 ]          = ValueType.Ignored;
        s_typeCache[ tempIO.Percentage() - 1 ]            = ValueType.Real;
        s_typeCache[ tempIO.Composite() - 1 ]             = ValueType.Ignored;
        s_typeCache[ tempIO.Integer() - 1 ]               = ValueType.Integer;

        if ( s_typeCache.Contains( ValueType.Unknown ) )
          Debug.LogWarning( "Brick value type mapping contains unhandled value type(s)" );
      }

      if ( s_typeCache.Length < type )
        return ValueType.Unknown;
      return s_typeCache[ type - 1 ];
    }

    public static bool IsValueTypeCompatible<T>( long typeCode, bool toCSType )
    {
      var requestedType = GetTypeEnum<T>();
      var endpointType = GetBrickTypeEnum(typeCode);

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
  }
}