using System;
using System.Collections.Generic;
using System.Linq;


using AGXUnity;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace AGXUnity.Model
{
  [AddComponentMenu("AGXUnity/Model/GraspingRobot Input Controller")]
  public class GraspingRobotInputController : ScriptComponent
  {
    public enum ActionType
    {
      UpDown,
      LeftRight,
      ForwardBackward,
      Hinge2,
      Hinge3
    }

    [HideInInspector]
    public float UpDown { get { return GetValue(ActionType.UpDown); } }

    [HideInInspector]
    public float LeftRight { get { return GetValue(ActionType.LeftRight); } }

    [HideInInspector]
    public float ForwardBackward { get { return GetValue(ActionType.ForwardBackward); } }

    [HideInInspector]
    public float Hinge2 { get { return GetValue(ActionType.Hinge2); } }
    public float Hinge3 { get { return GetValue(ActionType.Hinge3); } }


    public GraspingRobot Robot
    {
      get
      {
        if (m_robot == null)
          m_robot = GetComponent<GraspingRobot>();
        return m_robot;
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
        InputMap = m_inputAsset?.FindActionMap("GraspingRobot");

        if (InputMap != null && IsSynchronizingProperties)
        {
          m_hasValidInputActionMap = true;
          foreach (var actionName in System.Enum.GetNames(typeof(ActionType)))
          {
            if (InputMap.FindAction(actionName) == null)
            {
              Debug.LogWarning($"Unable to find Input Action: GraspingRobot.{actionName}");
              m_hasValidInputActionMap = false;
            }
          }

          if (m_hasValidInputActionMap)
            InputMap.Enable();
          else
            Debug.LogWarning("GraspingRobot input disabled due to missing action(s) in the action map.");
        }

        if (m_inputAsset != null && InputMap == null)
          Debug.LogWarning("InputActionAsset doesn't contain an ActionMap named \"GraspingRobot\".");
      }
    }

    private InputAction GetInputAction(string name)
    {
      InputAction action = null;

      InputMap = m_inputAsset?.FindActionMap("GraspingRobot");
      if (InputMap != null)
      {
        action = InputMap.FindAction(name);
      }
      return action;
    }

    Dictionary<string, InputAction> m_buttonActions = null;

    public void RegisterAction(string name, Func<bool, int> callback)
    {
      var action = GetInputAction(name);
      if (action != null)
      {

        if (m_buttonActions == null)
          m_buttonActions = new Dictionary<string, InputAction>();

        action.started += _ => callback(true);
        action.canceled += _ => callback(false);
        // Start listening for control changes.
        action.Enable();

        m_buttonActions.Add(name, action);
      }
      else
        Debug.LogWarning(string.Format("Unable to find InputAction named {0}", name));
    }

    private InputAction SaveAction;
    public InputActionMap InputMap = null;
#endif

    public float GetValue(ActionType action)
    {
#if ENABLE_INPUT_SYSTEM
      return m_hasValidInputActionMap ? InputMap[action.ToString()].ReadValue<float>() : 0.0f;
#else
      var name = action.ToString();
      var jAction = Input.GetAxis( 'j' + name );
      return jAction != 0.0f ? jAction : Input.GetAxis( 'k' + name );
#endif
    }

    public int Close(bool down)
    {
      var controller = Robot.GraspingPrismatic.GetController<TargetSpeedController>();
      controller.Speed = 0.03f;

      return 0;
    }

    public int Open(bool down)
    {
      var controller = Robot.GraspingPrismatic.GetController<TargetSpeedController>();
      controller.Speed = down ? -0.03f : 0;
      return 0;
    }

    public int RotateWristRight(bool down)
    {
      var controller = Robot.WristHinge.GetController<TargetSpeedController>();
      controller.Speed = down ? -2.0f : 0;

      return 0;
    }

    public int RotateWristLeft(bool down)
    {
      var controller = Robot.WristHinge.GetController<TargetSpeedController>();
      controller.Speed = down ? 2.0f : 0;

      return 0;
    }

    private agx.RigidBody m_kinematicBody;
    private agx.LockJoint m_lockJoint;
    private agx.RigidBody m_dynamicBody;

    /// <summary>
    /// Create an invisible kinematic rigid body by which we can move the upper part of the 
    /// robot arm.
    /// The kinematic body is locked to the robot arm with a LockJoint with limited force.
    /// If the force applied by the lock joint becomes larger than a certain threshold
    /// we will reposition the kinematic body
    /// </summary>
    private void InitKinematicControl()
    {
      if (m_kinematicBody == null)
      {
        m_kinematicBody = new agx.RigidBody();
        m_kinematicBody.setMotionControl(agx.RigidBody.MotionControl.KINEMATICS);
        GetSimulation().add(m_kinematicBody);
      }

      var observer = Robot.KinematicAttachment.Native;

      if (m_dynamicBody == null)
        m_dynamicBody = observer.getRigidBody();
            
      // Position the kinematic body at the position of the observer
      m_kinematicBody.setPosition(observer.getPosition());

      // Are we here for the first time?
      if (m_lockJoint == null)
      {
        var f1 = new agx.Frame();
        var f2 = new agx.Frame();

        var pos = observer.getLocalPosition();
        agx.Constraint.calculateFramesFromBody(pos, new agx.Vec3(0, 0, 1), m_dynamicBody, f1, m_kinematicBody, f2);

        // We will disable all rotational DOF, we are only interested in controlling translation
        m_lockJoint = new agx.LockJoint(m_dynamicBody, f1, m_kinematicBody, f2);
        m_lockJoint.setCompliance(1.0, (long)agx.LockJoint.DOF.ROTATIONAL_1);
        m_lockJoint.setCompliance(1.0, (long)agx.LockJoint.DOF.ROTATIONAL_2);
        m_lockJoint.setCompliance(1.0, (long)agx.LockJoint.DOF.ROTATIONAL_3);
        m_lockJoint.setEnableComputeForces(true);
        GetSimulation().add(m_lockJoint);
      }
    }

    protected override bool Initialize()
    {
      if (Robot == null)
      {
        Debug.LogError("Unable to initialize: AGXUnity.Model.GraspingRobot component not found.", this);
        return false;
      }

      InitKinematicControl();

      RegisterAction("Close", Close);
      RegisterAction("Open", Open);
      RegisterAction("RotateWristRight", RotateWristRight);
      RegisterAction("RotateWristLeft", RotateWristLeft);
           
      SaveAction = new InputAction("Save", binding: "<Keyboard>/o");
      SaveAction.Enable();

      return true;
    }

    private void Reset()
    {
#if ENABLE_INPUT_SYSTEM
      InputAsset = Resources.Load<InputActionAsset>("Input/AGXUnityInputControls");
#endif
    }

    public float ForceLimit
    {
      get
      {
        return m_forceLimit;
      }

      set
      {
        m_forceLimit = value;
      }
    }

    [SerializeField]
    private float m_forceLimit = 4000; // At this force limit, we wil reposition the kinematic body
    private void LimitForce()
    {
      

      double maxForce = 0;
      for(int i=0; i < 5; i++)
      {
        var force = m_lockJoint.getCurrentForce((ulong)i);
        maxForce = Math.Max(maxForce, Math.Abs(force));
      }

      // Lets make the lock joint a bit stronger than the force limit
      m_lockJoint.setForceRange(new agx.RangeReal(ForceLimit * 1.05));

      // If the force applied by the lockjoint, then reposition the kinematic body
      if (maxForce > ForceLimit)
      {
        var attachment = m_lockJoint.getAttachment(0);
        var frame = attachment.getFrame();
        var m = frame.getLocalMatrix() * m_dynamicBody.getTransform();
        m_kinematicBody.setTransform(m);
      }
    }

    private void Update()
    {
      LimitForce();

      if (SaveAction.triggered)
      {
        GetSimulation().write("agxunity_simulation.agx");
      }

      var forwardBackward = ForwardBackward;
      var upDown = UpDown;
      var leftRight = LeftRight;
  
      Robot.Hinge2.GetController<TargetSpeedController>().Speed = Hinge2;
      Robot.Hinge3.GetController<TargetSpeedController>().Speed = Hinge3;
      
      MoveKinematicBody(forwardBackward, upDown, leftRight);

    }

    /// <summary>
    /// Set the speed of the kinematic body based on the input from keyboard/gamepad
    /// </summary>
    /// <param name="forwardBackward"></param>
    /// <param name="upDown"></param>
    /// <param name="leftRight"></param>
    private void MoveKinematicBody(float forwardBackward, float upDown, float leftRight)
    {
      float threshold = 0.05f;

      forwardBackward = Mathf.Abs(forwardBackward) > threshold ? forwardBackward : 0.0f;
      upDown          = Mathf.Abs(upDown) > threshold ? upDown : 0.0f;
      leftRight       = Mathf.Abs(leftRight) > threshold ? leftRight : 0.0f;

      float scale = 0.45f;
      var vel = new agx.Vec3(leftRight, upDown, forwardBackward);

      m_kinematicBody.setVelocity(vel * scale);
    }

    private GraspingRobot m_robot = null;

#if ENABLE_INPUT_SYSTEM
    private bool m_hasValidInputActionMap = false;
#endif
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
}
