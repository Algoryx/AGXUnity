using Brick.Physics.Signals;
using System;
using Output = Brick.Physics.Signals.Output;

namespace AGXUnity.IO.BrickIO
{
  [Serializable]
  public class OutputSource : SignalEndpoint
  {
    public Output Native { get; private set; }

    public OutputSource( string name, Output output )
    {
      Name = name;
      m_serializedType = new SerializedType( output.GetType() );
      ValueTypeCode = output.type();
    }

    internal override bool Initialize( BrickSignals signalRoot )
    {
      base.Initialize( signalRoot );
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Output;
      return Native != null;
    }

    public T GetCachedValue<T>()
    {
      return SignalRoot.GetConvertedOutputValue<T>( Native );
    }

    public override bool IsValueTypeCompatible<T>() => BrickSignals.IsValueTypeCompatible<T>( ValueTypeCode, true );
  }
}
