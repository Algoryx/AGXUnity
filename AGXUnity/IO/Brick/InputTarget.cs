using Brick.Physics.Signals;
using System;
using Input = Brick.Physics.Signals.Input;

namespace AGXUnity.IO.BrickIO
{
  [Serializable]
  public class InputTarget : SignalEndpoint
  {
    public Input Native { get; private set; }

    public InputTarget( string name, Input input )
    {
      Name = name;
      m_serializedType = new SerializedType( input.GetType() );
      ValueTypeCode = input.type();
    }

    internal override bool Initialize( BrickSignals signalRoot )
    {
      base.Initialize( signalRoot );
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Input;
      return Native != null;
    }

    public void SendSignal<T>( T value )
    {
      if(!IsValueTypeCompatible<T>())
        throw new InvalidCastException( $"Cannot send value of type '{typeof(T).Name}' to Input of type '{Type.Name}'" );

      InputSignal signal = BrickSignals.GetBrickTypeEnum(ValueTypeCode) switch
      {
        BrickSignals.ValueType.Integer |
        BrickSignals.ValueType.Real => RealInputSignal.create(Convert.ToDouble(value), Native),
        BrickSignals.ValueType.Vec3 =>
          Vec3InputSignal.create(
            (
              typeof( T ) == typeof( UnityEngine.Vector3 ) ? ((UnityEngine.Vector3)(object)value).ToBrickVec3() :
              typeof( T ) == typeof( agx.Vec3 ) ? ((agx.Vec3)(object)value).ToBrickVec3() :
              typeof( T ) == typeof( agx.Vec3f ) ? ((agx.Vec3f)(object)value).ToBrickVec3() :
              typeof( T ) == typeof( Brick.Math.Vec3 ) ? (Brick.Math.Vec3)(object)value : null
            ),
            Native
          ),
        _ => null
      };
      SignalRoot.SendInputSignal( signal );
    }

    public override bool IsValueTypeCompatible<T>() => BrickSignals.IsValueTypeCompatible<T>( ValueTypeCode, false );
  }
}
