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

    [HideInInspector]
    public OpenPLXRoot Root => GetComponent<OpenPLXRoot>();

    private Dictionary<string, SignalEndpoint> m_declaredNameEndpointMap = new Dictionary<string, SignalEndpoint>();

    private std.StringReferenceLookup m_nativeMap;
    private agxopenplx.AgxObjectMap NativeMapper;

    public InputTarget FindInputTarget( string name ) => m_inputs.Find( it => it.Name == name );
    public InputWrapper<T> FindInputTarget<T>( string name ) where T : new()
      => new InputWrapper<T>( m_inputs.Find( it => it.Name == name ) );

    public OutputSource FindOutputSource( string name ) => m_outputs.Find( os => os.Name == name );
    public OutputWrapper<T> FindOutputSource<T>( string name ) where T : new()
      => new OutputWrapper<T>( m_outputs.Find( os => os.Name == name ) );

    //public void SendInputSignal( InputSignal input ) => NativeInputQueue.send( input );

    private agxopenplx.AgxMetadata m_meta;

    public openplx.ControlDispatch ControlDispatch { get; private set; }
    public openplx.ControlInterface ControlInterface { get; private set; }
    public openplx.HeapControlInterface HeapControlInterface { get; private set; }

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

      NativeMapper = agxopenplx.AgxObjectMap.createPreMapped( m_nativeMap, agxopenplx.AgxObjectMapMode.Name );

      m_meta = new agxopenplx.AgxMetadata();

      ControlDispatch = new openplx.ControlDispatch();
      agxOpenPLXSWIG.register_control_handlers( ControlDispatch, NativeMapper, m_meta );
      ControlInterface = new openplx.ControlInterface( ControlDispatch );
      ControlInterface.add_controls_from_object( Root.Native );
      ControlInterface.prepare_controls();
      HeapControlInterface = new openplx.HeapControlInterface( ControlInterface );

      return ok;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks._Internal_OpenPLXSignalPostSync -= Post;

      foreach ( var output in m_outputs )
        output.Invalidate();

      foreach ( var input in m_inputs )
        input.Invalidate();
    }

    void Post()
    {
      //m_outputSignalList.Clear();
      //if ( !isActiveAndEnabled )
      //  return;
      //foreach ( var signal in NativeOutputQueue.getSignals() ) {
      //  if ( signal != null ) {
      //    m_outputSignalList.Add( signal );
      //    m_outputSignalCache[ signal.source() ] = signal;
      //  }
      //}
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
  }
}
