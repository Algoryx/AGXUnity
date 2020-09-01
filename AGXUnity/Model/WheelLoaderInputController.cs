using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Wheel Loader Input Controller" )]
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

    public enum ActionMode
    {
      Devices,
      Manual
    }

    [SerializeField]
    private ActionMode m_inputMode = ActionMode.Devices;

    public ActionMode InputMode
    {
      get { return m_inputMode; }
      set { m_inputMode = value; }
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

#if ENABLE_INPUT_SYSTEM
    [SerializeField]
    private InputActionAsset m_inputAsset = null;

    public InputActionAsset InputAsset
    {
      get
      {
        return m_inputAsset;
      }
      set
      {
        m_inputAsset = value;
        InputMap = m_inputAsset?.FindActionMap( "WheelLoader" );

        if ( InputMap != null && IsSynchronizingProperties ) {
          m_hasValidInputActionMap = true;
          foreach ( var actionName in System.Enum.GetNames( typeof( ActionType ) ) ) {
            if ( InputMap.FindAction( actionName ) == null ) {
              Debug.LogWarning( $"Unable to find Input Action: WheelLoader.{actionName}" );
              m_hasValidInputActionMap = false;
            }
          }

          if ( m_hasValidInputActionMap )
            InputMap.Enable();
          else
            Debug.LogWarning( "WheelLoader input disabled due to missing action(s) in the action map." );
        }

        if ( m_inputAsset != null && InputMap == null )
          Debug.LogWarning( "InputActionAsset doesn't contain an ActionMap named \"WheelLoader\"." );
      }
    }

    public InputActionMap InputMap = null;
#endif

    [HideInInspector]
    public float Steer
    {
      get { return GetValue( ActionType.Steer ); }
      set { SetValue( ActionType.Steer, value ); }
    }

    [HideInInspector]
    public float Throttle
    {
      get { return GetValue( ActionType.Throttle ); }
      set { SetValue( ActionType.Throttle, value ); }
    }

    [HideInInspector]
    public float Brake
    {
      get { return GetValue( ActionType.Brake ); }
      set { SetValue( ActionType.Brake, value ); }
    }

    [HideInInspector]
    public float Elevate
    {
      get { return GetValue( ActionType.Elevate ); }
      set { SetValue( ActionType.Elevate, value ); }
    }

    [HideInInspector]
    public float Tilt
    {
      get { return GetValue( ActionType.Tilt ); }
      set { SetValue( ActionType.Tilt, value ); }
    }

    public float GetValue( ActionType action )
    {
      if ( InputMode == ActionMode.Manual )
        return m_manualInputs[ (int)action ];

#if ENABLE_INPUT_SYSTEM
      return m_hasValidInputActionMap ? InputMap[ action.ToString() ].ReadValue<float>() : 0.0f;
#else
      var name = action.ToString();
      var jAction = Input.GetAxis( 'j' + name );
      return jAction != 0.0f ? jAction : Input.GetAxis( 'k' + name );
#endif
    }

    public void SetValue( ActionType action, float value )
    {
      m_manualInputs[ (int)action ] = Utils.Math.Clamp( value, -1.0f, 1.0f );
    }

    protected override bool Initialize()
    {
      if ( WheelLoader == null ) {
        Debug.LogError( "Unable to initialize: AGXUnity.Model.WheelLoader component not found.", this );
        return false;
      }

      WheelLoader.SteeringHinge.GetController<TargetSpeedController>().Enable = false;
      WheelLoader.SteeringHinge.GetController<LockController>().Enable = true;

      return true;
    }

    private void Reset()
    {
#if ENABLE_INPUT_SYSTEM
      InputAsset = Resources.Load<InputActionAsset>( "Input/AGXUnityInputControls" );
#endif
    }

    private void Update()
    {
      SetSpeed( WheelLoader.SteeringHinge, Steer );

      var speed    = WheelLoader.Speed;
      var throttle = Throttle;
      var brake    = Brake;

      var idleSpeed = 0.05f;
      if ( Utils.Math.EqualsZero( throttle ) && Utils.Math.EqualsZero( brake ) ) {
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
      var motorEnable = !Utils.Math.EqualsZero( speed );
      var mc = constraint.GetController<TargetSpeedController>();
      var lc = constraint.GetController<LockController>();
      mc.Enable = motorEnable;
      mc.Speed = speed;
      if ( !motorEnable && !lc.Enable )
        lc.Position = constraint.GetCurrentAngle();
      lc.Enable = !motorEnable;
    }

    private WheelLoader m_wheelLoader = null;
#if ENABLE_INPUT_SYSTEM
    private bool m_hasValidInputActionMap = false;
#endif
    private float[] m_manualInputs = new float[ System.Enum.GetValues( typeof( ActionType ) ).Length ];
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
  