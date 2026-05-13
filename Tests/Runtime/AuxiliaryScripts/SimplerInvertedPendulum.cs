using AGXUnity;
using AGXUnity.IO.OpenPLX;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnityTesting.Runtime.Aux
{
  public class SimplerInvertedPendulum : ScriptComponent
  {
    OpenPLXSignals signal = null;

    InputWrapper<double> motor_input = null;
    PDController pole;

    // Start is called before the first frame update
    protected override bool Initialize()
    {
      signal = gameObject.GetInitializedComponent<OpenPLXSignals>();
      motor_input = signal.FindInputTarget<double>( "PendulumScene.motor_input" );

      if ( motor_input == null )
        Debug.LogWarning( "Could not find motor input" );

      pole = new PDController( 5, 0.2f, 0 );

      Simulation.Instance.StepCallbacks.SimulationPre += Pre;

      hao = signal.FindOutputSource<float>( "PendulumScene.hinge_angle_output" );
      havo = signal.FindOutputSource<float>( "PendulumScene.hinge_angular_velocity_output" );

      return true;
    }

    OutputWrapper<float> hao;
    OutputWrapper<float> havo;

    double ha = 0.0f;
    double hav = 0.0f;
    double u_pole = 0.0f;

    private void Pre()
    {
      try {
        ha = hao.Read();
        hav = havo.Read();
      }
      catch {
        return;
      }

      u_pole = pole.Observe( ha, hav );

      motor_input.Write( u_pole );
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      Simulation.Instance.StepCallbacks.SimulationPre -= Pre;
    }
  }
}
