using openplx.Physics.Signals;
using System;
using Output = openplx.Physics.Signals.Output;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public class OutputSource : SignalEndpoint
  {
    public Output Native { get; private set; }

    private OpenPLXSignals m_signalRoot;

    public bool HasSentSignal => Native != null && m_signalRoot.HasCachedSignal( Native );

    public OutputSource( string name, Output output )
    {
      Name = name;
      Enabled = output.enabled();
      m_serializedType = new SerializedType( output.GetType() );
      ValueTypeCode = output.type();
    }

    internal override bool Initialize( OpenPLXSignals signalRoot )
    {
      base.Initialize( signalRoot );
      m_signalRoot = signalRoot;
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Output;
      return Native != null;
    }

    public Value GetValue() => m_signalRoot?.GetValue( Native );

    public T GetValue<T>() => m_signalRoot != null ? m_signalRoot.GetValue<T>( Native ) : throw new ArgumentException( "Cannot get value of uninitialized output" );
    public bool TryGetValue<T>( out T output )
    {
      if ( m_signalRoot == null ) {
        output = default( T );
        return false;
      }
      return m_signalRoot.TryGetValue( Native, out output );
    }

    public override bool IsValueTypeCompatible<T>() => OpenPLXSignals.IsValueTypeCompatible<T>( ValueTypeCode, true );
  }
}
