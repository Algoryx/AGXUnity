using System;
using Output = openplx.Physics.Signals.Output;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public class OutputSource : SignalEndpoint
  {
    public Output Native { get; private set; }

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
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Output;
      return Native != null;
    }

    public T GetCachedValue<T>()
    {
      return SignalRoot.GetConvertedOutputValue<T>( Native );
    }

    public override bool IsValueTypeCompatible<T>() => OpenPLXSignals.IsValueTypeCompatible<T>( ValueTypeCode, true );
  }
}
