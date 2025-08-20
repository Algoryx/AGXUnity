using openplx.Physics.Signals;
using System;
using Input = openplx.Physics.Signals.Input;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public class InputTarget : SignalEndpoint
  {
    public Input Native { get; private set; }

    public InputTarget( string name, Input input )
    {
      Name = name;
      Enabled = true;
      m_serializedType = new SerializedType( input.GetType() );
      ValueTypeCode = input.type();
    }

    internal override bool Initialize( OpenPLXSignals signalRoot )
    {
      base.Initialize( signalRoot );
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Input;
      return Native != null;
    }

    public void SendSignal<T>( T value )
    {
      if ( !IsValueTypeCompatible<T>() )
        throw new InvalidCastException( $"Cannot send value of type '{typeof( T ).Name}' to Input of type '{Type.Name}'" );

      InputSignal signal = OpenPLXSignals.GetOpenPLXTypeEnum(ValueTypeCode) switch
      {
        OpenPLXSignals.ValueType.Integer |
        OpenPLXSignals.ValueType.Real => RealInputSignal.create(Convert.ToDouble(value), Native),
        OpenPLXSignals.ValueType.Boolean => BoolInputSignal.create(Convert.ToBoolean(value), Native),
        OpenPLXSignals.ValueType.Vec3 =>
          Vec3InputSignal.create(
            (
              typeof( T ) == typeof( UnityEngine.Vector3 ) ? ((UnityEngine.Vector3)(object)value).ToOpenPLXVec3() :
              typeof( T ) == typeof( agx.Vec3 ) ? ((agx.Vec3)(object)value).ToOpenPLXVec3() :
              typeof( T ) == typeof( agx.Vec3f ) ? ((agx.Vec3f)(object)value).ToOpenPLXVec3() :
              typeof( T ) == typeof( openplx.Math.Vec3 ) ? (openplx.Math.Vec3)(object)value : null
            ),
            Native
          ),
        _ => null
      };
      SignalRoot.SendInputSignal( signal );
    }

    public override bool IsValueTypeCompatible<T>() => OpenPLXSignals.IsValueTypeCompatible<T>( ValueTypeCode, false );
  }
}
