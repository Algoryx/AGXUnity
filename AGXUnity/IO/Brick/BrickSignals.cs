using AGXUnity.Utils;
using Brick.DriveTrain;
using Brick.Physics.Signals;
using System;
using System.Collections.Generic;
using UnityEngine;

using Input = Brick.Physics.Signals.Input;
using Object = Brick.Core.Object;
using Signals = Brick.Physics3D.Signals;

namespace AGXUnity.IO.BrickIO
{
  [RequireComponent( typeof( BrickRoot ) )]
  public class BrickSignals : ScriptComponent
  {
    [Serializable]
    public struct SignalMetadata : ISerializationCallbackReceiver
    {
      [SerializeField]
      public bool input;
      
      public Type type;

      [SerializeField]
      private string m_serializedType;

      public void OnAfterDeserialize()
      {
        if(m_serializedType != null)
          type = Type.GetType( m_serializedType );
      }

      public void OnBeforeSerialize()
      {
        if ( type == null )
          m_serializedType = null;
        else 
          m_serializedType = type.AssemblyQualifiedName;
      }
    }

    [SerializeField]
    private List<string> m_signals = new List<string>();

    [HideInInspector]
    public string[] Signals => m_signals.ToArray();

    [SerializeField]
    private SerializableDictionary<string, SignalMetadata> m_metadata = new SerializableDictionary<string, SignalMetadata>();

    public SignalMetadata? GetMetadata( string signal ) {
      if ( !m_metadata.TryGetValue( signal, out var data ) )
        return null;
      return data;
    }

    private List<Output> m_outputs = new List<Output>();
    private List<Input> m_inputs = new List<Input>();

    private Queue<InputSignal> m_inputSignalQueue = new Queue<InputSignal>();
    private List<OutputSignal> m_outputSignalList = new List<OutputSignal>();

    [HideInInspector]
    public List<OutputSignal> OutputSignals => m_outputSignalList;

    [HideInInspector]
    public BrickRoot Root => GetComponent<BrickRoot>();

    [HideInInspector]
    public Dictionary<string, OutputSignal> m_outputCache = new Dictionary<string, OutputSignal>();

    public void RegisterSignal<T>( string signal, T brickSignal )
      where T : Brick.Core.Object 
    {
      if( brickSignal is not Output && brickSignal is not Input ) {
        Debug.LogError( "Provided signal is neither an input nor an output" );
        return;
      }

      m_signals.Add( signal );
      m_metadata[ signal ] = new SignalMetadata()
      {
        input = brickSignal is Input,
        type = brickSignal.GetType(),
      };
    }

    public Input FindInputTarget( string name )
    {
      if ( State != States.INITIALIZED )
        return null;

      return InitializeNativeSignal( name ) as Input;
    }

    private Object InitializeNativeSignal( string signal )
    {
      var relativeSigName = signal.Replace(Root.Native.getName() + ".", "").Trim();
      var signalObj = Root.Native.getObject(relativeSigName);
      if ( signalObj != null )
        return signalObj;
      else {
        Debug.LogError( $"{signal} does not exist!" );
        return null;
      }
    }

    protected override bool Initialize()
    {
      Root.GetInitialized();

      var ok = true;
      foreach ( var signal in m_signals ) {
        var natSig = InitializeNativeSignal(signal);
        if ( natSig is Output o )
          m_outputs.Add( o );
        else if ( natSig is Input i )
          m_inputs.Add( i );
        else
          ok = false;
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
            if(source is CombustionEngine ce ) {
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
      }
    }

    void Post()
    {
      m_outputSignalList.Clear();
      foreach ( var output in m_outputs ) {
        ValueOutputSignal signal = null;
        if ( output is Signals.HingeAngleOutput hao ) {
          var hinge = Root.FindMappedObject( hao.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_angle( constraint.GetCurrentAngle(), hao );
        }
        else if ( output is Signals.HingeAngularVelocityOutput havo ) {
          var hinge = Root.FindMappedObject( havo.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_angular_velocity_1d( constraint.GetCurrentSpeed(), havo );
        }
        else if ( output is Signals.PrismaticPositionOutput ppo ) {
          var prismatic = Root.FindMappedObject( ppo.prismatic().getName() );
          var constraint = prismatic.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_distance( constraint.GetCurrentAngle(), ppo );
        }
        else if ( output is Signals.PrismaticVelocityOutput pvo ) {
          var prismatic = Root.FindMappedObject( pvo.prismatic().getName() );
          var constraint = prismatic.GetComponent<Constraint>();
          signal = ValueOutputSignal.from_velocity_1d( constraint.GetCurrentSpeed(), pvo );
        }
        else if ( output is Signals.RigidBodyPositionOutput rbpo ) {
          var go = Root.FindMappedObject(rbpo.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var pos = rb.Native.getPosition();
          signal = ValueOutputSignal.from_position_3d( pos.ToBrickVec3(), rbpo );
        }
        else if ( output is Signals.RigidBodyVelocityOutput rbvo ) {
          var go = Root.FindMappedObject(rbvo.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var vel = rb.LinearVelocity.ToLeftHanded();
          signal = ValueOutputSignal.from_velocity_3d( vel.ToBrickVec3(), rbvo );
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
          m_outputCache[ output.getName() ] = signal;
        }
      }
    }

    public void SendInputSignal( InputSignal input )
    {
      m_inputSignalQueue.Enqueue( input );
    }
    public Value GetOutputValue( Output output )
    {
      return GetOutputValue( output.getName() );
    }

    public Value GetOutputValue( string outputName )
    {
      if ( !m_outputCache.ContainsKey( outputName ) )
        return null;

      OutputSignal signal = m_outputCache[ outputName ] as ValueOutputSignal;

      if ( signal is not ValueOutputSignal vos )
        return null;

      return vos.value();
    }

    public T GetConvertedOutputValue<T>( Output output )
    {
      return GetConvertedOutputValue<T>( output.getName() );
    }

    public T GetConvertedOutputValue<T>( string outputName )
    {
      if ( !m_outputCache.ContainsKey( outputName ) )
        throw new ArgumentException( "Specified output does not have a cached value", "outputName" );

      OutputSignal signal = m_outputCache[ outputName ] as ValueOutputSignal;

      bool realTypeRequested = Type.GetTypeCode( typeof( T ) ) switch
      {
        TypeCode.Byte => true,
        TypeCode.SByte => true,
        TypeCode.UInt16 => true,
        TypeCode.UInt32 => true,
        TypeCode.UInt64 => true,
        TypeCode.Int16 => true,
        TypeCode.Int32 => true,
        TypeCode.Int64 => true,
        TypeCode.Decimal => true,
        TypeCode.Double => true,
        TypeCode.Single => true,
        _ => false
      };

      if ( signal is not ValueOutputSignal vos ) {
        throw new ArgumentException( $"Given output '{outputName}' did not send a ValueOutputSignal" );
      }

      bool vec3TypeRequested =
           typeof(T) == typeof(Vector3)
        || typeof(T) == typeof(agx.Vec3)
        || typeof(T) == typeof(agx.Vec3f)
        || typeof(T) == typeof(Brick.Math.Vec3);

      var value = vos.value();
      if ( realTypeRequested ) {
        if ( value is not RealValue realVal )
          throw new InvalidCastException( "Cannot convert non-real signal to provided type" );
        return (T)Convert.ChangeType( realVal.value(), typeof( T ) );
      }
      else if ( vec3TypeRequested ) {
        if ( value is not Vec3Value v3val )
          throw new InvalidCastException( "Cannot convert non-Vec3 signal to provided type" );
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
  }
}