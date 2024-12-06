using AGXUnity;
using AGXUnity.IO.OpenPLX;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnityTesting.Runtime.Aux
{
  public class SimplerInvertedPendulum : ScriptComponent
  {
    OpenPLXSignals signal = null;

    InputTarget motor_input = null;
    PDController pole;

    // Start is called before the first frame update
    protected override bool Initialize()
    {
      signal = gameObject.GetInitializedComponent<OpenPLXSignals>();
      motor_input = signal.FindInputTarget( "PendulumScene.motor_input" );

      if ( motor_input == null )
        Debug.LogWarning( "Could not find motor input" );

      pole = new PDController( 5, 0.2f, 0 );

      Simulation.Instance.StepCallbacks.SimulationPre += Pre;

      hao = signal.FindOutputSource( "PendulumScene.hinge_angle_output" );
      havo = signal.FindOutputSource( "PendulumScene.hinge_angular_velocity_output" );

      return true;
    }

    OutputSource hao;
    OutputSource havo;

    double ha = 0.0f;
    double hav = 0.0f;
    double u_pole = 0.0f;

    private void Pre()
    {
      try {
        ha = hao.GetCachedValue<double>();
        hav = havo.GetCachedValue<double>();
      }
      catch {
        return;
      }

      u_pole = pole.Observe( ha, hav );

      motor_input.SendSignal( u_pole );
    }
  }
}
