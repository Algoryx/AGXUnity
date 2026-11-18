using openplx.Physics.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Input = openplx.Physics.Signals.Input;
using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public struct SignalInterface
  {
    [SerializeField] public string Name;
    [SerializeField] public string Path;
    [SerializeField] public bool Enabled;
    [SerializeField] public List<OutputSource> Outputs;
    [SerializeField] public List<InputTarget> Inputs;

    public OutputSource FindOutput( string name ) => Outputs.FirstOrDefault( s => s.Name == name || s.Name.EndsWith( "." + name, StringComparison.Ordinal ) );
    public InputTarget FindInput( string name ) => Inputs.FirstOrDefault( s => s.Name == name || s.Name.EndsWith( "." + name, StringComparison.Ordinal ) );
  }

  [RequireComponent( typeof( OpenPLXRoot ) )]
  [AddComponentMenu( "" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#openplx-signals" )]
  public class OpenPLXSignals : ScriptComponent
  {
    [SerializeField] private List<OutputSource> m_outputs = new List<OutputSource>();
    [SerializeField] private List<InputTarget> m_inputs = new List<InputTarget>();
    [SerializeField] private List<SignalInterface> m_interfaces = new List<SignalInterface>();

    public OutputSource[] Outputs => m_outputs.ToArray();
    public InputTarget[] Inputs => m_inputs.ToArray();
    public SignalInterface[] Interfaces => m_interfaces.ToArray();
    [HideInInspector] public OpenPLXRoot Root => GetComponent<OpenPLXRoot>();

    private Dictionary<string, SignalEndpoint> m_declaredNameEndpointMap = new Dictionary<string, SignalEndpoint>();
    private std.StringReferenceLookup m_nativeMap;
    private agxopenplx.AgxObjectMap NativeMapper;
    private agxopenplx.AgxMetadata m_meta;
    private LidarSignalBindings m_lidarBindings;

    private IEnumerable<SignalEndpoint> Endpoints => m_inputs.Cast<SignalEndpoint>().Concat( m_outputs )
      .Concat( m_interfaces.SelectMany( value => value.Inputs.Cast<SignalEndpoint>().Concat( value.Outputs ) ) );

    public openplx.ControlDispatch ControlDispatch { get; private set; }
    public openplx.ControlInterface ControlInterface { get; private set; }
    public openplx.HeapControlInterface HeapControlInterface { get; private set; }

    public InputTarget FindInputTarget( string name ) => m_inputs.Find( it => it.Name == name );
    public InputWrapper<T> FindInputTarget<T>( string name ) => new InputWrapper<T>( FindInputTarget( name ) );
    public OutputSource FindOutputSource( string name ) => m_outputs.Find( os => os.Name == name );
    public OutputWrapper<T> FindOutputSource<T>( string name ) => new OutputWrapper<T>( FindOutputSource( name ) );

    protected override bool Initialize()
    {
      try {
        if ( Root.GetInitialized() == null )
          return false;

        for ( int i = 0; i < m_interfaces.Count; ++i ) {
          var signalInterface = m_interfaces[ i ];
          signalInterface.Name = RootRelativeInterfaceName( signalInterface.Name, signalInterface.Path, Root.Native.getName() );
          m_interfaces[ i ] = signalInterface;
        }

        var ok = true;
        foreach ( var endpoint in Endpoints ) {
          ok &= endpoint.Initialize( this );
          m_declaredNameEndpointMap[ endpoint.Name ] = endpoint;
        }
        if ( !ok ) {
          ReleaseNativeResources();
          return false;
        }

        m_nativeMap = new std.StringReferenceLookup();
        foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() ) {
          foreach ( var decl in openPLXObj.SourceDeclarations ) {
            var obj = Root.FindNestedNative( decl );
            if ( obj == null )
              continue;
            var native = openPLXObj.FindCorrespondingNative( Root, obj );
            if ( native != null )
              // Native handlers use the runtime name, including its bundle.
              m_nativeMap.Add( new( obj.getName(), native ) );
          }
        }
        foreach ( var (key, value) in Root.RuntimeMapped )
          m_nativeMap.Add( new( key, value ) );
        NativeMapper = agxopenplx.AgxObjectMap.createPreMapped( m_nativeMap, agxopenplx.AgxObjectMapMode.Name );

        m_meta = new agxopenplx.AgxMetadata();
        m_lidarBindings = new LidarSignalBindings( m_meta );
        foreach ( var endpoint in Endpoints.OfType<OutputSource>() ) {
          if ( !( endpoint.Native is openplx.Sensors.Signals.LidarOutput definition ) )
            continue;
          var source = definition.source();
          var sourceObject = source != null ? Root.FindMappedObject( source.getName() ) : null;
          var sensor = sourceObject != null ? sourceObject.GetComponent<AGXUnity.Sensor.LidarSensor>() : null;
          if ( sensor == null || sensor.GetInitialized<AGXUnity.Sensor.LidarSensor>() == null )
            throw new InvalidOperationException( $"Lidar output '{endpoint.Name}' has no initialized, imported source sensor." );
          // Interface aliases share a definition UUID and the same owned output.
          m_lidarBindings.Register( definition, sensor.Native, AGXUnity.Sensor.SensorEnvironment.Instance.GenerateOutputID() );
        }

        ControlDispatch = new openplx.ControlDispatch();
        agxOpenPLXSWIG.register_control_handlers( ControlDispatch, NativeMapper, m_meta );
        ControlInterface = new openplx.ControlInterface( ControlDispatch );
        ControlInterface.add_controls_from_object( Root.Native );
        ControlInterface.prepare_controls();
        HeapControlInterface = new openplx.HeapControlInterface( ControlInterface );
        Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync += Post;
        return true;
      }
      catch ( Exception exception ) {
        Debug.LogException( exception, this );
        ReleaseNativeResources();
        return false;
      }
    }

    protected override void OnDestroy()
    {
      try {
        if ( Simulation.HasInstance )
          Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync -= Post;
        ReleaseNativeResources();
      }
      finally {
        base.OnDestroy();
      }
    }

    private void ReleaseNativeResources()
    {
      foreach ( var endpoint in Endpoints )
        endpoint.Invalidate();
      m_declaredNameEndpointMap.Clear();
      try {
        // Unregister metadata and detach outputs while native definitions live.
        m_lidarBindings?.Dispose();
      }
      finally {
        m_lidarBindings = null;
        HeapControlInterface?.Dispose();
        HeapControlInterface = null;
        ControlInterface?.Dispose();
        ControlInterface = null;
        ControlDispatch?.Dispose();
        ControlDispatch = null;
        m_meta?.Dispose();
        m_meta = null;
        NativeMapper?.Dispose();
        NativeMapper = null;
        m_nativeMap?.Dispose();
        m_nativeMap = null;
      }
    }

    void Post() { }

    internal void RegisterSignal( string signal, Input openPLXSignal ) => m_inputs.Add( new InputTarget( signal, openPLXSignal ) );
    internal void RegisterSignal( string signal, Output openPLXSignal ) => m_outputs.Add( new OutputSource( signal, openPLXSignal ) );
    internal void RegisterInterface( SignalInterface sigInterface )
    {
      sigInterface.Name = RootRelativeInterfaceName( sigInterface.Name, sigInterface.Path, Root.Native.getName() );
      m_interfaces.Add( sigInterface );
    }

    internal static string RootRelativeInterfaceName( string name, string path, string rootName )
    {
      if ( string.IsNullOrEmpty( path ) || path == rootName )
        return name;
      var modelName = rootName.Substring( rootName.LastIndexOf( '.' ) + 1 );
      if ( path == modelName )
        return name;
      foreach ( var prefix in new[] { rootName + ".", modelName + "." } )
        if ( path.StartsWith( prefix, StringComparison.Ordinal ) ) {
          path = path.Substring( prefix.Length );
          break;
        }
      return name.StartsWith( path + ".", StringComparison.Ordinal ) ? name : path + "." + name;
    }

    internal Object InitializeNativeEndpoint( string endpoint )
    {
      var signalObj = Root.FindNestedNative( endpoint );
      if ( signalObj != null )
        return signalObj;
      Debug.LogError( $"{endpoint} does not exist!" );
      return null;
    }
  }
}
