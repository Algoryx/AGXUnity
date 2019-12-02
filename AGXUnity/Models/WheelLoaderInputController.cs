using UnityEngine;

namespace AGXUnity.Models
{
  public class WheelLoaderInputController : ScriptComponent
  {
    public enum ActionType
    {
      Steer,
      Throttle,
      Brake,
      Elevate,
      Tilt
    }

    [HideInInspector]
    public WheelLoader WheelLoader
    {
      get
      {
        if ( m_wheelLoader == null )
          m_wheelLoader = GetComponent<WheelLoader>();
        return m_wheelLoader;
      }
    }

    public float Steer { get { return GetValue( ActionType.Steer ); } }

    public float Throttle { get { return GetValue( ActionType.Throttle ); } }

    public float Brake { get { return GetValue( ActionType.Brake ); } }

    public float Elevate { get { return GetValue( ActionType.Elevate ); } }

    public float Tilt { get { return GetValue( ActionType.Tilt ); } }

    public float GetValue( ActionType action )
    {
      var name = action.ToString();
      var jAction = Input.GetAxis( 'j' + name );
      return jAction != 0.0f ? jAction : Input.GetAxis( 'k' + name );
    }

    protected override bool Initialize()
    {
      if ( WheelLoader == null ) {
        Debug.LogError( "Unable to initialize: AGXUnity.Models.WheelLoader component not found.", this );
        return false;
      }

      WheelLoader.SteeringHinge.GetController<TargetSpeedController>().Enable = false;
      WheelLoader.SteeringHinge.GetController<LockController>().Enable = true;

      return true;
    }

    private void Update()
    {
      SetSpeed( WheelLoader.SteeringHinge, Steer );

      var speed    = WheelLoader.Speed;
      var throttle = Throttle;
      var brake    = Brake;

      var idleSpeed = 0.05f;
      if ( Mathf.Approximately( throttle, 0.0f ) && Mathf.Approximately( brake, 0.0f ) ) {
        SetThrottle( 0.0f );
        if ( Mathf.Abs( speed ) > idleSpeed )
          SetBrake( 0.1f );
        else
          SetBrake( 1.0f );
      }
      else {
        if ( throttle > 0.0f ) {
          // Throttle down but going backwards. Brake.
          if ( speed < -idleSpeed ) {
            SetThrottle( 0.0f );
            SetBrake( throttle );
          }
          else {
            WheelLoader.GearBox.setGear( 1 );
            SetThrottle( throttle );
            SetBrake( 0.0f );
          }
        }
        else if ( brake > 0.0f ) {
          // Brake down and going forward. Brake.
          if ( speed > idleSpeed ) {
            SetThrottle( 0.0f );
            SetBrake( brake );
          }
          else {
            WheelLoader.GearBox.setGear( 0 );
            SetThrottle( brake );
            SetBrake( 0.0f );
          }
        }
      }

      SetElevate( Elevate );
      SetTilt( Tilt );
    }

    private void SetBrake( float value )
    {
      var brakeTorque = value * 1.5E5f;
      WheelLoader.BrakeHinge.getMotor1D().setEnable( value > 0.0f );
      WheelLoader.BrakeHinge.getMotor1D().setSpeed( 0.0f );
      WheelLoader.BrakeHinge.getMotor1D().setForceRange( -brakeTorque, brakeTorque );
    }

    private void SetThrottle( float value )
    {
      WheelLoader.Engine.setThrottle( value );
    }

    private void SetElevate( float value )
    {
      var speed = 0.3f * value;
      foreach ( var prismatic in WheelLoader.ElevatePrismatics )
        SetSpeed( prismatic, speed );
    }

    private void SetTilt( float value )
    {
      SetSpeed( WheelLoader.TiltPrismatic, 0.25f * value );
    }

    private void SetSpeed( Constraint constraint, float speed )
    {
      var motorEnable = !Mathf.Approximately( speed, 0.0f );
      var mc = constraint.GetController<TargetSpeedController>();
      var lc = constraint.GetController<LockController>();
      mc.Enable = motorEnable;
      mc.Speed = speed;
      if ( !motorEnable && !lc.Enable )
        lc.Position = constraint.GetCurrentAngle();
      lc.Enable = !motorEnable;
    }

    private WheelLoader m_wheelLoader = null;
  }
}

  #region InputManager.asset
  /*
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!13 &1
InputManager:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Axes:
  - serializedVersion: 3
    m_Name: jSteer
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: 
    altNegativeButton: left
    altPositiveButton: right
    gravity: 3
    dead: 0.3
    sensitivity: 1
    snap: 1
    invert: 0
    type: 2
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: kSteer
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: left
    positiveButton: right
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.001
    sensitivity: 2
    snap: 1
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: jThrottle
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: 
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.05
    sensitivity: 1
    snap: 0
    invert: 0
    type: 2
    axis: 9
    joyNum: 0
  - serializedVersion: 3
    m_Name: kThrottle
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: up
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.001
    sensitivity: 2
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: jBrake
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: 
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.05
    sensitivity: 1
    snap: 0
    invert: 0
    type: 2
    axis: 8
    joyNum: 0
  - serializedVersion: 3
    m_Name: kBrake
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: down
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.001
    sensitivity: 2
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: jElevate
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: 
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.3
    sensitivity: 1
    snap: 0
    invert: 1
    type: 2
    axis: 1
    joyNum: 0
  - serializedVersion: 3
    m_Name: kElevate
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: s
    positiveButton: w
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.001
    sensitivity: 1
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: jTilt
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: 
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.3
    sensitivity: 1
    snap: 0
    invert: 0
    type: 2
    axis: 3
    joyNum: 0
  - serializedVersion: 3
    m_Name: kTilt
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: a
    positiveButton: d
    altNegativeButton: 
    altPositiveButton: 
    gravity: 3
    dead: 0.001
    sensitivity: 1
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: Enable Debug Button 1
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: left ctrl
    altNegativeButton: 
    altPositiveButton: joystick button 8
    gravity: 0
    dead: 0
    sensitivity: 0
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: Enable Debug Button 2
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: backspace
    altNegativeButton: 
    altPositiveButton: joystick button 9
    gravity: 0
    dead: 0
    sensitivity: 0
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
  - serializedVersion: 3
    m_Name: Debug Reset
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: left alt
    altNegativeButton: 
    altPositiveButton: joystick button 1
    gravity: 0
    dead: 0
    sensitivity: 0
    snap: 0
    invert: 0
    type: 0
    axis: 0
    joyNum: 0
    */
  #endregion
  