using openplx.Physics.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using Input = openplx.Physics.Signals.Input;
using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  [RequireComponent( typeof( OpenPLXRoot ) )]
  public class OpenPLXSignals : ScriptComponent
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
    public OpenPLXRoot Root => GetComponent<OpenPLXRoot>();

    private Dictionary<Output, OutputSignal> m_outputCache = new Dictionary<Output, OutputSignal>();
    private Dictionary<string, SignalEndpoint> m_declaredNameEndpointMap = new Dictionary<string, SignalEndpoint>();

    public void RegisterSignal<T>( string signal, T openPLXSignal )
      where T : openplx.Core.Object
    {
      if ( openPLXSignal is Output output )
        m_outputs.Add( new OutputSource( signal, output ) );
      else if ( openPLXSignal is Input input )
        m_inputs.Add( new InputTarget( signal, input ) );
      else {
        Debug.LogError( "Provided endpoint is neither an input nor an output" );
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
        m_outputWrapperMap[ output.Native ] = output;
      }

      Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPreSync += Pre;
      Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync += Post;

      return ok;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPreSync -= Pre;
        Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync -= Post;
      }
    }

    void Pre()
    {
      if ( !isActiveAndEnabled ) {
        m_inputSignalQueue.Clear();
        return;
      }
      while ( m_inputSignalQueue.TryDequeue( out var inpSig ) ) {
        switch ( inpSig ) {
          case RealInputSignal realSig: InputSignalHandler.HandleRealInputSignal( realSig, Root ); break;
          case IntInputSignal intSig: InputSignalHandler.HandleIntInputSignal( intSig, Root ); break;
          case BoolInputSignal boolSig: InputSignalHandler.HandleBoolInputSignal( boolSig, Root ); break;
          default: Debug.LogWarning( $"Unhandled InputSignal type: '{inpSig.GetType().Name}'" ); break;
        }
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
          m_outputWrapperMap[ signal.source() ].CachedSignal = signal;
        }
        //  Profiler.EndSample();
      }
    }

    public void SendInputSignal( InputSignal input )
    {
    #region Output Signal Helpers

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
      if ( !m_outputWrapperMap.TryGetValue( output, out var endpoint ) )
        return null;

      return endpoint.GetValue();
    }

    public T GetValue<T>( Output output )
    {
      if ( !m_outputWrapperMap.TryGetValue( output, out var endpoint ) )
        throw new ArgumentException( $"Failed to find output '{output.getName()}' in OpenPLX Root '{Root.name}'", "output" );

      return endpoint.GetValue<T>();
    }

    public bool TryGetValue<T>( Output output, out T value )
    {
      value = default;
      if ( !m_outputWrapperMap.TryGetValue( output, out var endpoint ) )
        return false;

      return endpoint.TryGetValue( out value );
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
        || typeof( T ) == typeof( openplx.Math.Vec3 ) )
        return ValueType.Vec3;

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
        s_typeCache[ tempIO.Boolean() - 1 ]               = ValueType.Ignored;
        s_typeCache[ tempIO.Percentage() - 1 ]            = ValueType.Real;
        s_typeCache[ tempIO.Composite() - 1 ]             = ValueType.Ignored;
        s_typeCache[ tempIO.Integer() - 1 ]               = ValueType.Integer;
        s_typeCache[ tempIO.Duration() - 1 ]              = ValueType.Real;

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
    #endregion
  }
}
