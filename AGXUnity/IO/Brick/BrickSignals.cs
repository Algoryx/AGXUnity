using AGXUnity.Utils;
using Brick.DriveTrain;
using Brick.Physics.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Input = Brick.Physics.Signals.Input;
using Object = Brick.Core.Object;
using Signals = Brick.Physics3D.Signals;

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

    public InputTarget FindInputTarget( string name ) =>  m_inputs.Find( it => it.Name == name );
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
        var target = inpSig.target();
        if ( inpSig is RealInputSignal realSig && target != null ) {
          if ( target is Signals.TorsionSpringAngleInput tsai ) {
            var hinge = Root.FindMappedObject( tsai.spring().getName() );
            var spring = hinge.GetComponent<Constraint>().GetController<LockController>();
            spring.Position = (float)realSig.value();
          }
          else if ( target is Signals.LinearVelocityMotorVelocityInput lvmvi ) {
            var prismatic = Root.FindMappedObject( lvmvi.motor().getName() );
            var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
            motor.Speed = (float)realSig.value();
          }
          else if ( target is Signals.RotationalVelocityMotorVelocityInput rvmvi ) {
            var hinge = Root.FindMappedObject( rvmvi.motor().getName() );
            var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
            motor.Speed = (float)realSig.value();
          }
          else if ( target is Brick.Physics1D.Signals.RotationalVelocityMotor1DVelocityInput rvm1dvi ) {
            var motor = Root.FindRuntimeMappedObject( rvm1dvi.motor().getName() );
            if ( motor is agxDriveTrain.VelocityConstraint vc )
              vc.setTargetVelocity( (float)realSig.value() );
            else
              Debug.LogError( $"Could not find runtime mapped VelocityConstraint for signal target '{rvm1dvi.motor().getName()}'" );
          }
          else if ( target is Torque1DInput t1di ) {
            var source = t1di.source();
            if ( source is Brick.Physics3D.Interactions.TorqueMotor tm ) {
              var hinge = Root.FindMappedObject(tm.getName());
              var motor = hinge.GetComponent<Constraint>().GetController<TargetSpeedController>();
              var torque = Mathf.Clamp((float)realSig.value(),(float)tm.min_effort(),(float)tm.max_effort());
              motor.ForceRange = new RangeReal( torque, torque );
            }
            else if ( source is TorqueMotor tm_dt ) {
              foreach ( var charge in tm_dt.charges() ) {
                var unit = (agxPowerLine.Unit)Root.FindRuntimeMappedObject(charge.getOwner().getName());
                var rot_unit = unit.asRotationalUnit();
                if ( rot_unit != null ) {
                  var torque = Mathf.Clamp((float)realSig.value(),(float)tm_dt.min_effort(),(float)tm_dt.max_effort());
                  rot_unit.getRotationalDimension().addLoad( torque );
                }
              }
            }
          }
          else if ( target is Signals.ForceMotorForceInput fmfi ) {
            var prismatic = Root.FindMappedObject(fmfi.motor().getName());
            var motor = prismatic.GetComponent<Constraint>().GetController<TargetSpeedController>();
            var torque = Mathf.Clamp((float)realSig.value(),(float)fmfi.motor().min_effort(),(float)fmfi.motor().max_effort());
            motor.ForceRange = new RangeReal( torque, torque );
          }
          else if ( target is FractionInput fi ) {
            var source = fi.source();
            if ( source is CombustionEngine ce ) {
              var engine = Root.FindRuntimeMappedObject( ce.getName() );
              if ( engine is agxDriveTrain.CombustionEngine mappedCe ) {
                mappedCe.setThrottle( realSig.value() );
              }
              else
                Debug.LogError( $"Could not find runtime mapped CombustionEngine for signal target '{ce.getName()}'" );
            }
          }
          else {
            Debug.LogWarning( $"Unhandled input type {target.getType().getName()}" );
          }
        }
        else if ( inpSig is IntInputSignal iis ) {
          if ( target is IntInput intTarget ) {
            var source = intTarget.source();
            if ( source is GearBox gearbox ) {
              if ( Root.FindRuntimeMappedObject( gearbox.getName() ) is not agxDriveTrain.GearBox agxGearbox )
                Debug.LogError( $"{gearbox.getName()} was not mapped to a powerline unit" );
              else {
                var numReverse = gearbox.reverse_gears().Count;
                var numForward = gearbox.forward_gears().Count;
                int gear = (int)iis.value();
                if ( gear < 0 && Mathf.Abs( gear ) > numReverse )
                  gear = -numReverse;
                if ( gear > 0 && Mathf.Abs( gear ) > numForward )
                  gear = numForward;

                var adjustedGear = gear + numReverse;
                if ( adjustedGear >= agxGearbox.getNumGears() || adjustedGear < 0 )
                  Debug.LogError( $"Signal had gear {adjustedGear} which is out of range 0 - {agxGearbox.getNumGears()} for agxDriveTrain.GearBox" );
                agxGearbox.setGear( adjustedGear );
              }
            }
          }
        }
      }
    }

    void Post()
    {
      m_outputSignalList.Clear();
      foreach ( var outputSource in m_outputs ) {
        var output = outputSource.Native;
        ValueOutputSignal signal = null;

        if ( output is IntOutput io ) {
          var source = io.source();
          if ( source is GearBox gearbox ) {
            if ( Root.FindRuntimeMappedObject( gearbox.getName() ) is not agxDriveTrain.GearBox agxGearbox ) {
              Debug.LogError( $"{gearbox.getName()} was not mapped to a powerline unit" );
            }
            else {
              var num_reverse = gearbox.reverse_gears().Count;
              signal = ValueOutputSignal.from_int( agxGearbox.getGear() - num_reverse, io );
            }
          }
        }
        else if ( output is Signals.HingeAngleOutput hao ) {
          var hinge = Root.FindMappedObject( hao.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_angle( constraint.GetCurrentAngle(), hao );
        }
        else if ( output is Signals.HingeAngularVelocityOutput havo ) {
          var hinge = Root.FindMappedObject( havo.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_angular_velocity_1d( constraint.GetCurrentSpeed(), havo );
        }
        else if ( output is Position1DOutput p1do ) {
          if ( p1do.source() is Brick.Physics3D.Interactions.Prismatic sourcePrismatic ) {
            var prismatic = Root.FindMappedObject( sourcePrismatic.getName() );
            var constraint = prismatic.GetComponent<Constraint>();
            signal = ValueOutputSignal.from_distance( constraint.GetCurrentAngle(), p1do );
          }
        }
        else if ( output is LinearVelocity1DOutput lv1do ) {
          if ( lv1do.source() is Brick.Physics3D.Interactions.Prismatic sourcePrismatic ) {
            var prismatic = Root.FindMappedObject( sourcePrismatic.getName() );
            var constraint = prismatic.GetComponent<Constraint>();
            signal = ValueOutputSignal.from_velocity_1d( constraint.GetCurrentSpeed(), lv1do );
          }
        }
        else if ( output is Signals.RigidBodyPositionOutput rbpo ) {
          var go = Root.FindMappedObject(rbpo.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var pos = rb.Native.getPosition();
          signal = ValueOutputSignal.from_position_3d( pos.ToBrickVec3(), rbpo );
        }
        else if ( output is Signals.LinearVelocity3DOutput lv3do ) {
          if ( lv3do.source() is Brick.Physics3D.Bodies.RigidBody sourceRB ) {
            var go = Root.FindMappedObject(sourceRB.getName());
            var rb = go.GetComponent<RigidBody>();
            var vel = rb.LinearVelocity.ToLeftHanded();
            signal = ValueOutputSignal.from_velocity_3d( vel.ToBrickVec3(), lv3do );
          }
        }
        else if ( output is Signals.RigidBodyRPYOutput rbrpy ) {
          var go = Root.FindMappedObject(rbrpy.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var vel = rb.Native.getRotation().getAsEulerAngles();
          signal = ValueOutputSignal.from_rpy( vel.ToBrickVec3(), rbrpy );
        }
        else if ( output is Brick.Physics1D.Signals.RotationalBodyAngularVelocityOutput rbavo ) {
          if ( Root.FindRuntimeMappedObject( rbavo.body().getName() ) is not agxPowerLine.Unit rotBod || rotBod.asRotationalUnit() == null )
            Debug.LogError( $"{rbavo.body().getName()} was not mapped to a powerline unit" );
          else
            signal = ValueOutputSignal.from_angular_velocity_1d( rotBod.asRotationalUnit().getAngularVelocity(), rbavo );
        }
        else {
          Debug.LogWarning( $"Unhandled output type {output.getType().getName()}" );
        }

        if ( signal != null ) {
          m_outputSignalList.Add( signal );
          m_outputCache[ output ] = signal;
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