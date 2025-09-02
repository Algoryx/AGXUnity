using openplx.Physics.Signals;
using System;
using Output = openplx.Physics.Signals.Output;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public class OutputSource : SignalEndpoint
  {
    public Output Native { get; private set; }

    public OutputSignal CachedSignal { get; internal set; }

    public bool HasSendSignal => CachedSignal != null;

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

    private bool TryConvertOutputSignal<T>( ValueOutputSignal vos, out T converted )
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

    public T GetValue<T>()
    {
      if ( !Enabled )
        throw new ArgumentException( $"Output '{Name}' is not enabled" );

      if ( CachedSignal == null )
        throw new ArgumentException( $"Output '{Name}' does not have a cached value", "output" );

      if ( CachedSignal is not ValueOutputSignal vos )
        throw new ArgumentException( $"Output '{Name}' did not send a ValueOutputSignal" );

      if ( !OpenPLXSignals.IsValueTypeCompatible<T>( CachedSignal.source().type(), true ) )
        throw new InvalidCastException( $"Cannot convert signal value of type '{CachedSignal.source().GetType().Name}' to provided type '{typeof( T ).Name}'" );

      if ( !TryConvertOutputSignal<T>( vos, out T converted ) )
        throw new InvalidCastException( "Could not map signal type to requested type" );

      return converted;
    }

    public bool TryGetValue<T>( out T output )
    {
      output = default;
      if ( !Enabled ||
           CachedSignal == null ||
           CachedSignal is not ValueOutputSignal vos ||
           !OpenPLXSignals.IsValueTypeCompatible<T>( CachedSignal.source().type(), true ) ||
           !TryConvertOutputSignal( vos, out output ) )
        return false;

      return true;
    }

    public override bool IsValueTypeCompatible<T>() => OpenPLXSignals.IsValueTypeCompatible<T>( ValueTypeCode, true );


    public Value GetValue()
    {
      if ( CachedSignal == null )
        return null;

      if ( CachedSignal is not ValueOutputSignal vos )
        return null;

      return vos.value();

    }
  }
}
