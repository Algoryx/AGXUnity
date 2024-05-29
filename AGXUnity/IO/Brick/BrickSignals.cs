using AGXUnity.Utils;
using Brick.Physics.Signals;
using System.Collections.Generic;
using UnityEngine;

using Input = Brick.Physics.Signals.Input;
using Object = Brick.Core.Object;
using Signals = Brick.Physics3D.Signals;

namespace AGXUnity.IO.BrickIO
{
  [Icon( "Assets/Brick/brick-icon.png" )]
  [RequireComponent( typeof( BrickRoot ) )]
  public class BrickSignals : ScriptComponent
  {
    [SerializeField]
    private List<string> m_signals = new List<string>();

    private List<Output> m_outputs = new List<Output>();
    private List<Input> m_inputs = new List<Input>();

    private Queue<InputSignal> m_inputSignalQueue = new Queue<InputSignal>();
    private List<OutputSignal> m_outputSignalList = new List<OutputSignal>();

    public List<OutputSignal> OutputSignals => m_outputSignalList;

    public BrickRoot Root => GetComponent<BrickRoot>();

    public void RegisterSignal( string signal )
    {
      m_signals.Add( signal );
    }

    public Input FindInputTarget( string name )
    {
      if ( State != States.INITIALIZED )
        return null;

      return InitializeNativeSignal( name ) as Input;
    }

    private Object InitializeNativeSignal( string signal )
    {
      var cur = Root.Native;
      var splitName = signal.Split('.');
      for ( int i = 1; i < splitName.Length; i++ ) {
        var child = cur.getDynamic( splitName[ i ] );
        if ( child.isObject() )
          cur = child.asObject();
        else {
          Debug.LogError( $"{splitName[ i ]} is not an Object!" );
          return null;
        }
      }

      if ( cur.getName() != signal ) {
        Debug.LogError( $"Could not find signal '{signal}'!" );
        return null;
      }

      return cur;
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
            var spring = hinge.GetComponent<LockController>();
            spring.Position = (float)realSig.value();
          }
          else if ( target is Signals.LinearVelocityMotorVelocityInput lvmvi ) {
            var prismatic = Root.FindMappedObject( lvmvi.motor().getName() );
            var motor = prismatic.GetComponent<TargetSpeedController>();
            motor.Speed = (float)realSig.value();
          }
          else if ( target is Signals.RotationalVelocityMotorVelocityInput rvmvi ) {
            var hinge = Root.FindMappedObject( rvmvi.motor().getName() );
            var motor = hinge.GetComponent<TargetSpeedController>();
            motor.Speed = (float)realSig.value();
          }
          else if ( target is Brick.Physics1D.Signals.RotationalVelocityMotor1DVelocityInput rvm1dvi ) {
            var motor = Root.FindRuntimeMappedObject( rvm1dvi.motor().getName() );
            if ( motor is agxDriveTrain.VelocityConstraint vc )
              vc.setTargetVelocity( (float)realSig.value() );
            else
              Debug.LogError( $"Could not find runtime mapped VelocityConstraint for signal target '{rvm1dvi.motor().getName()}'" );
          }
          else if ( target is Brick.DriveTrain.Signals.CombustionEngineThrottleInput ceti ) {
            var engine = Root.FindRuntimeMappedObject( ceti.combustion_engine().getName() );
            if ( engine is agxDriveTrain.CombustionEngine ce ) {
              Debug.Log( realSig.value() );
              ce.setThrottle( realSig.value() );
            }
            else
              Debug.LogError( $"Could not find runtime mapped CombustionEngine for signal target '{ceti.combustion_engine().getName()}'" );
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
      foreach ( var signal in m_outputs ) {
        if ( signal is Signals.HingeAngleOutput hao ) {
          var hinge = Root.FindMappedObject( hao.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          m_outputSignalList.Add( ValueOutputSignal.fromAngle( constraint.GetCurrentAngle(), hao ) );
        }
        else if ( signal is Signals.HingeAngularVelocityOutput havo ) {
          var hinge = Root.FindMappedObject( havo.hinge().getName() );
          var constraint = hinge.GetComponent<Constraint>();
          m_outputSignalList.Add( ValueOutputSignal.fromAngularVelocity( constraint.GetCurrentSpeed(), havo ) );
        }
        else if ( signal is Signals.PrismaticPositionOutput ppo ) {
          var prismatic = Root.FindMappedObject( ppo.prismatic().getName() );
          var constraint = prismatic.GetComponent<Constraint>();
          m_outputSignalList.Add( ValueOutputSignal.fromDistance( constraint.GetCurrentAngle(), ppo ) );
        }
        else if ( signal is Signals.PrismaticVelocityOutput pvo ) {
          var prismatic = Root.FindMappedObject( pvo.prismatic().getName() );
          var constraint = prismatic.GetComponent<Constraint>();
          m_outputSignalList.Add( ValueOutputSignal.fromVelocity1D( constraint.GetCurrentSpeed(), pvo ) );
        }
        else if ( signal is Signals.RigidBodyPositionOutput rbpo ) {
          var go = Root.FindMappedObject(rbpo.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var pos = rb.Native.getPosition();
          m_outputSignalList.Add( ValueOutputSignal.fromPosition3D( pos.ToBrickVec3(), rbpo ) );
        }
        else if ( signal is Signals.RigidBodyVelocityOutput rbvo ) {
          var go = Root.FindMappedObject(rbvo.rigid_body().getName());
          var rb = go.GetComponent<RigidBody>();
          var vel = rb.LinearVelocity.ToLeftHanded();
          var sig = ValueOutputSignal.fromVelocity3D( vel.ToBrickVec3(), rbvo );
          m_outputSignalList.Add( sig );
        }
        else if ( signal is Brick.Physics1D.Signals.RotationalBodyAngularVelocityOutput rbavo ) {
          if ( Root.FindRuntimeMappedObject( rbavo.body().getName() ) is not agxPowerLine.Unit rotBod || rotBod.asRotationalUnit() == null )
            Debug.LogError( $"{rbavo.body().getName()} was not mapped to a powerline unit" );
          else
            m_outputSignalList.Add( ValueOutputSignal.fromAngularVelocity( rotBod.asRotationalUnit().getAngularVelocity(), rbavo ) );
        }
        else {
          Debug.LogWarning( $"Unhandled output type {signal.getType().getName()}" );
        }
      }
    }

    public void SendInputSignal( InputSignal input )
    {
      m_inputSignalQueue.Enqueue( input );
    }
  }
}