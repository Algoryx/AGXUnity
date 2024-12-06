using AGXUnity;
using AGXUnity.IO.OpenPLX;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnityTesting.Runtime.Aux
{

  class PDController
  {
    public double KP { get; set; }
    public double KD { get; set; }
    public double Goal { get; set; }

    public PDController( double kp, double kd, double goal )
    {
      KP = kp;
      KD = kd;
      Goal = goal;
    }

    public double Observe( double x, double xdot )
    {
      double error = Goal - x;
      return KP * error - KD * xdot;
    }
  }

  public class InvertedPendulumController : ScriptComponent
  {
    OpenPLXSignals signal = null;

    InputTarget motor_input = null;
    PDController cart;
    PDController pole;


    // Start is called before the first frame update
    protected override bool Initialize()
    {
      signal = gameObject.GetInitializedComponent<OpenPLXSignals>();
      motor_input = signal.FindInputTarget( "PendulumScene.motor_input" );

      if ( motor_input == null )
        Debug.LogWarning( "Could not find motor input" );

      cart = new PDController( 11, 5, 0 );
      pole = new PDController( 19, 5, 0 );

      Simulation.Instance.StepCallbacks.PreStepForward += Pre;

      hao = signal.FindOutputSource( "PendulumScene.hinge_angle_output" );
      havo = signal.FindOutputSource( "PendulumScene.hinge_angular_velocity_output" );
      poso = signal.FindOutputSource( "PendulumScene.cart_position_output" );
      velo = signal.FindOutputSource( "PendulumScene.cart_velocity_output" );

      return true;
    }

    OutputSource hao;
    OutputSource havo;
    OutputSource poso;
    OutputSource velo;

    double ha = 0.0f;
    double hav = 0.0f;
    double xpos = 0.0f;
    double xvel = 0.0f;
    double u_cart = 0.0f;
    double u_pole = 0.0f;
    double raw = 0.0f;

    private void Pre()
    {
      try {
        ha = hao.GetCachedValue<double>();
        hav = havo.GetCachedValue<double>();
        xpos = poso.GetCachedValue<Vector3>().x;
        xvel = velo.GetCachedValue<Vector3>().x;
      }
      catch {
        return;
      }

      u_cart = cart.Observe( xpos, xvel );
      u_pole = pole.Observe( ha, hav );

      raw = -u_cart - u_pole;

      motor_input.SendSignal( Math.Clamp( raw, -1000, 1000 ) );
    }

    private void OnGUI()
    {
      GUILayout.Label( $"Angle: {ha}" );
      GUILayout.Label( $"A. Vel: {hav}" );
      GUILayout.Label( $"Pos: {xpos}" );
      GUILayout.Label( $"Vel: {xvel}" );
      GUILayout.Label( $"u_cart: {u_cart}" );
      GUILayout.Label( $"u_pole: {u_pole}" );
      GUILayout.Label( $"Raw: {raw}" );
    }
  }
}
