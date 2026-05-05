using System;
using System.Reflection;
using AGXUnity.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/PID Controller 1D" )]
  [DisallowMultipleComponent]
  public class PidController1D : ScriptComponent
  {
    [Serializable]
    public class FloatEvent : UnityEvent<float> { }

    /// <summary>
    /// Stores a reference to a component and the name of a writable float or Vector3 property or
    /// field on it. For Vector3 members, Axis selects which component receives the output value.
    /// Call Bind() once at runtime to cache reflection data, then Set(value) to write.
    /// </summary>
    [Serializable]
    public class ComponentFloatProperty
    {
      public enum Vector3Axis { X, Y, Z }

      [SerializeField]
      public Component Target = null;

      [SerializeField]
      public string MemberName = string.Empty;

      [SerializeField]
      public Vector3Axis Axis = Vector3Axis.X;

      private PropertyInfo m_property;
      private FieldInfo    m_field;
      private bool         m_isVector3;

      public bool IsValid => Target != null && ( m_property != null || m_field != null );

      public void Bind()
      {
        m_property  = null;
        m_field     = null;
        m_isVector3 = false;

        if ( Target == null || string.IsNullOrEmpty( MemberName ) )
          return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        var type = Target.GetType();

        var prop = type.GetProperty( MemberName, flags );
        if ( prop != null && prop.CanWrite ) {
          if ( prop.PropertyType == typeof( float ) || prop.PropertyType == typeof( Vector3 ) ) {
            m_property  = prop;
            m_isVector3 = prop.PropertyType == typeof( Vector3 );
            return;
          }
        }

        var field = type.GetField( MemberName, flags );
        if ( field != null && ( field.FieldType == typeof( float ) || field.FieldType == typeof( Vector3 ) ) ) {
          m_field     = field;
          m_isVector3 = field.FieldType == typeof( Vector3 );
        }
      }

      public void Set( float value )
      {
        if ( m_isVector3 ) {
          var v = m_property != null
            ? (Vector3)m_property.GetValue( Target )
            : (Vector3)m_field.GetValue( Target );

          switch ( Axis ) {
            case Vector3Axis.X: v.x = value; break;
            case Vector3Axis.Y: v.y = value; break;
            case Vector3Axis.Z: v.z = value; break;
          }

          m_property?.SetValue( Target, v );
          m_field?.SetValue( Target, v );
        }
        else {
          m_property?.SetValue( Target, value );
          m_field?.SetValue( Target, value );
        }
      }
    }

    public enum OutputWriteCallback
    {
      Manual,
      PreSynchronizeTransforms,
      PostSynchronizeTransforms
    }

    public enum OutputTarget
    {
      Field,
      UnityEvent,
      OpenPLX,
      ComponentProperty
    }

    /// <summary>
    /// Native instance if initialized, otherwise null.
    /// </summary>
    public agxModel.PidController1D Native { get; private set; } = null;

    [SerializeField]
    private float m_setPoint = 0.0f;

    /// <summary>
    /// The target value the controller drives the process value toward.
    /// </summary>
    public float SetPoint
    {
      get => m_setPoint;
      set
      {
        m_setPoint = value;
        Native?.setSetPoint( m_setPoint );
      }
    }

    [SerializeField]
    private float m_proportionalGain = 1.0f;

    /// <summary>
    /// Proportional gain (Kp).
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ProportionalGain
    {
      get => m_proportionalGain;
      set
      {
        m_proportionalGain = value;
        Native?.setProportionalGain( m_proportionalGain );
      }
    }

    [SerializeField]
    private float m_integralGain = 0.0f;

    /// <summary>
    /// Integral gain (Ki).
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float IntegralGain
    {
      get => m_integralGain;
      set
      {
        m_integralGain = value;
        Native?.setIntegralGain( m_integralGain );
      }
    }

    [SerializeField]
    private float m_derivativeGain = 0.0f;

    /// <summary>
    /// Derivative gain (Kd).
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float DerivativeGain
    {
      get => m_derivativeGain;
      set
      {
        m_derivativeGain = value;
        Native?.setDerivativeGain( m_derivativeGain );
      }
    }

    [SerializeField]
    private float m_frequency = 60.0f;

    /// <summary>
    /// Update frequency of the controller in Hz.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Frequency
    {
      get => m_frequency;
      set
      {
        m_frequency = value;
        Native?.setFrequency( m_frequency );
      }
    }

    [SerializeField]
    private float m_integralTime = 0.0f;

    /// <summary>
    /// Integral time constant (Ti). Zero disables integral action.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float IntegralTime
    {
      get => m_integralTime;
      set
      {
        m_integralTime = value;
        Native?.setIntegralTime( m_integralTime );
      }
    }

    [SerializeField]
    private float m_derivativeTime = 0.0f;

    /// <summary>
    /// Derivative time constant (Td). Zero disables derivative action.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float DerivativeTime
    {
      get => m_derivativeTime;
      set
      {
        m_derivativeTime = value;
        Native?.setDerivativeTime( m_derivativeTime );
      }
    }

    [SerializeField]
    private agxModel.PidController1D.DerivationMethod m_derivationMethod =
      agxModel.PidController1D.DerivationMethod.FIRSTORDER;

    /// <summary>
    /// Method used to compute the derivative term.
    /// </summary>
    public agxModel.PidController1D.DerivationMethod DerivationMethod
    {
      get => m_derivationMethod;
      set
      {
        m_derivationMethod = value;
        Native?.setDerivationType( m_derivationMethod );
      }
    }

    [SerializeField]
    private bool m_enableIntegralWindupProtection = false;

    [InspectorGroupBegin( Name = "Limits", DefaultExpanded = false )]
    public bool EnableIntegralWindupProtection
    {
      get => m_enableIntegralWindupProtection;
      set
      {
        m_enableIntegralWindupProtection = value;
        Native?.setIntegralWindupProtection( m_enableIntegralWindupProtection );
      }
    }

    [SerializeField]
    private Vector2 m_integralWindupClamping = new Vector2( -1.0f, 1.0f );

    [DynamicallyShowInInspector( nameof( EnableIntegralWindupProtection ) )]
    public Vector2 IntegralWindupClamping
    {
      get => m_integralWindupClamping;
      set
      {
        m_integralWindupClamping = value;
        Native?.setIntegralWindupClamping( m_integralWindupClamping.x, m_integralWindupClamping.y );
      }
    }

    [SerializeField]
    private Vector2 m_manipulatedVariableLimit = new Vector2( -1.0e9f, 1.0e9f );

    public Vector2 ManipulatedVariableLimit
    {
      get => m_manipulatedVariableLimit;
      set
      {
        m_manipulatedVariableLimit = value;
        Native?.setManipulatedVariableLimit( m_manipulatedVariableLimit.x, m_manipulatedVariableLimit.y );
      }
    }

    [SerializeField]
    private Vector2 m_manipulatedVariableAccelerationLimit = new Vector2( -1.0e9f, 1.0e9f );

    [InspectorGroupEnd]
    public Vector2 ManipulatedVariableAccelerationLimit
    {
      get => m_manipulatedVariableAccelerationLimit;
      set
      {
        m_manipulatedVariableAccelerationLimit = value;
        Native?.setManipulatedVariableAccelerationLimit( m_manipulatedVariableAccelerationLimit.x,
                                                         m_manipulatedVariableAccelerationLimit.y );
      }
    }

    [SerializeField]
    private OutputWriteCallback m_outputWriteCallback = OutputWriteCallback.PostSynchronizeTransforms;

    /// <summary>
    /// When to automatically call WriteProcessValueToOutput. Set to Manual to drive it yourself.
    /// </summary>
    public OutputWriteCallback Callback
    {
      get => m_outputWriteCallback;
      set
      {
        m_outputWriteCallback = value;
        UpdateCallbackRegistration();
      }
    }

    [SerializeField]
    private float m_processValue = 0.0f;

    /// <summary>
    /// Measured process variable fed into the controller each update.
    /// </summary>
    public float ProcessValue
    {
      get => m_processValue;
      set => m_processValue = value;
    }

    [SerializeField]
    private float m_outputValue = 0.0f;

    /// <summary>
    /// Manipulated variable (output) written by WriteProcessValueToOutput.
    /// </summary>
    public float OutputValue
    {
      get => m_outputValue;
      set => m_outputValue = value;
    }

    [SerializeField]
    private OutputTarget m_outputTarget = OutputTarget.Field;

    [InspectorSeparator]
    [InspectorGroupBegin( Name = "Output", DefaultExpanded = true )]
    public OutputTarget Output
    {
      get => m_outputTarget;
      set => m_outputTarget = value;
    }

    private bool IsOutputUnityEvent      => m_outputTarget == OutputTarget.UnityEvent;
    private bool IsOutputOpenPLX         => m_outputTarget == OutputTarget.OpenPLX;
    private bool IsOutputComponentProperty => m_outputTarget == OutputTarget.ComponentProperty;

    [SerializeField]
    private FloatEvent m_onOutput = new FloatEvent();

    /// <summary>
    /// Invoked with the output value each time WriteProcessValueToOutput runs (OutputTarget.UnityEvent).
    /// </summary>
    [DynamicallyShowInInspector( nameof( IsOutputUnityEvent ) )]
    public FloatEvent OnOutput => m_onOutput;

    [SerializeField]
    private string m_openPLXSignalName = string.Empty;

    /// <summary>
    /// Full path of the OpenPLX input signal to send the output to (OutputTarget.OpenPLX).
    /// </summary>
    [DynamicallyShowInInspector( nameof( IsOutputOpenPLX ) )]
    public string OpenPLXSignalName
    {
      get => m_openPLXSignalName;
      set => m_openPLXSignalName = value;
    }

    [SerializeField]
    private ComponentFloatProperty m_componentProperty = new ComponentFloatProperty();

    /// <summary>
    /// Component property or field to write the output value to (OutputTarget.ComponentProperty).
    /// </summary>
    [DynamicallyShowInInspector( nameof( IsOutputComponentProperty ) )]
    [InspectorGroupEnd]
    public ComponentFloatProperty TargetProperty => m_componentProperty;

    [RuntimeValue]
    public bool RuntimeEnabled { get; private set; } = false;

    [RuntimeValue]
    public float RuntimeError { get; private set; } = 0.0f;

    [RuntimeValue]
    public float RuntimeMeasuredProcessValue { get; private set; } = 0.0f;

    [RuntimeValue]
    public float RuntimeManipulatedVariable { get; private set; } = 0.0f;

    [RuntimeValue]
    public float RuntimeProportionalTerm { get; private set; } = 0.0f;

    [RuntimeValue]
    public float RuntimeIntegralTerm { get; private set; } = 0.0f;

    [RuntimeValue]
    public float RuntimeDerivativeTerm { get; private set; } = 0.0f;

    /// <summary>
    /// Feeds the current ProcessValue into the controller and dispatches the result to the configured output.
    /// </summary>
    /// <returns>The new OutputValue.</returns>
    public float WriteProcessValueToOutput()
    {
      return WriteProcessValueToOutput( m_processValue );
    }

    /// <summary>
    /// Feeds <paramref name="processValue"/> into the controller and dispatches the result to the configured output.
    /// </summary>
    /// <returns>The new OutputValue.</returns>
    public float WriteProcessValueToOutput( float processValue )
    {
      m_processValue = processValue;

      if ( Native == null )
        return m_outputValue;

      Native.update( m_processValue, Simulation.HasInstance ? Simulation.Instance.TimeStep : 0.0 );
      m_outputValue = (float)Native.getManipulatedVariable();

      UpdateRuntimeValues();
      DispatchOutput( m_outputValue );

      return m_outputValue;
    }

    [InvokableInInspector( "Write Process Value To Output", true )]
    private void WriteProcessValueToOutputInInspector()
    {
      WriteProcessValueToOutput();
    }

    protected override bool Initialize()
    {
      try {
        Native = new agxModel.PidController1D();
      }
      catch ( Exception exception ) {
        Debug.LogException( exception, this );
        return false;
      }

      SynchronizePropertiesToNative();
      UpdateRuntimeValues();

      InitializeOpenPLXTarget();
      m_componentProperty.Bind();

      UpdateCallbackRegistration();

      return base.Initialize();
    }

    protected override void OnEnable()
    {
      if ( Native != null )
        Native.enablePidController();
      UpdateCallbackRegistration();
    }

    protected override void OnDisable()
    {
      UnregisterCallbacks();
      if ( Native != null && Native.isEnabled() )
        Native.disablePidController();
    }

    protected override void OnDestroy()
    {
      UnregisterCallbacks();
      Native?.Dispose();
      Native = null;
      base.OnDestroy();
    }

    private void DispatchOutput( float value )
    {
      switch ( m_outputTarget ) {
        case OutputTarget.UnityEvent:
          m_onOutput.Invoke( value );
          break;

        case OutputTarget.OpenPLX:
          m_openPLXInputTarget?.SendSignal( value );
          break;

        case OutputTarget.ComponentProperty:
          m_componentProperty.Set( value );
          break;
      }
    }

    private void InitializeOpenPLXTarget()
    {
      if ( m_outputTarget != OutputTarget.OpenPLX || string.IsNullOrEmpty( m_openPLXSignalName ) )
        return;

      var signals = gameObject.GetInitializedComponentInParent<IO.OpenPLX.OpenPLXSignals>();
      if ( signals == null )
        signals = FindObjectOfType<IO.OpenPLX.OpenPLXSignals>()?.GetInitialized<IO.OpenPLX.OpenPLXSignals>();

      m_openPLXInputTarget = signals?.FindInputTarget( m_openPLXSignalName );

      if ( m_openPLXInputTarget == null )
        Debug.LogWarning( $"PidController1D: OpenPLX input signal '{m_openPLXSignalName}' not found.", this );
    }

    private void SynchronizePropertiesToNative()
    {
      if ( Native == null )
        return;

      Native.setSetPoint( m_setPoint );
      Native.setProportionalGain( m_proportionalGain );
      Native.setIntegralGain( m_integralGain );
      Native.setDerivativeGain( m_derivativeGain );
      Native.setFrequency( m_frequency );
      Native.setIntegralTime( m_integralTime );
      Native.setDerivativeTime( m_derivativeTime );
      Native.setDerivationType( m_derivationMethod );
      Native.setIntegralWindupProtection( m_enableIntegralWindupProtection );
      Native.setIntegralWindupClamping( m_integralWindupClamping.x, m_integralWindupClamping.y );
      Native.setManipulatedVariableLimit( m_manipulatedVariableLimit.x, m_manipulatedVariableLimit.y );
      Native.setManipulatedVariableAccelerationLimit( m_manipulatedVariableAccelerationLimit.x,
                                                      m_manipulatedVariableAccelerationLimit.y );

      if ( isActiveAndEnabled )
        Native.enablePidController();
      else
        Native.disablePidController();
    }

    private void UpdateCallbackRegistration()
    {
      UnregisterCallbacks();

      if ( !isActiveAndEnabled || !Simulation.HasInstance || Native == null )
        return;

      if ( m_outputWriteCallback == OutputWriteCallback.PreSynchronizeTransforms ) {
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += OnWriteCallback;
        m_callbackRegistered = true;
      }
      else if ( m_outputWriteCallback == OutputWriteCallback.PostSynchronizeTransforms ) {
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnWriteCallback;
        m_callbackRegistered = true;
      }
    }

    private void UnregisterCallbacks()
    {
      if ( !m_callbackRegistered || !Simulation.HasInstance )
        return;

      Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= OnWriteCallback;
      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnWriteCallback;
      m_callbackRegistered = false;
    }

    private void OnWriteCallback()
    {
      WriteProcessValueToOutput();
    }

    private void UpdateRuntimeValues()
    {
      if ( Native == null ) {
        RuntimeEnabled                = false;
        RuntimeError                  = 0.0f;
        RuntimeMeasuredProcessValue   = 0.0f;
        RuntimeManipulatedVariable    = m_outputValue;
        RuntimeProportionalTerm       = 0.0f;
        RuntimeIntegralTerm           = 0.0f;
        RuntimeDerivativeTerm         = 0.0f;
        return;
      }

      RuntimeEnabled              = Native.isEnabled();
      RuntimeError                = (float)Native.getError();
      RuntimeMeasuredProcessValue = (float)Native.getMeasuredProcessVariable();
      RuntimeManipulatedVariable  = (float)Native.getManipulatedVariable();
      RuntimeProportionalTerm     = (float)Native.getProportionalTerm();
      RuntimeIntegralTerm         = (float)Native.getIntegraTerm();
      RuntimeDerivativeTerm       = (float)Native.getDerivativeTerm();
    }

    private IO.OpenPLX.InputTarget m_openPLXInputTarget = null;
    private bool m_callbackRegistered = false;
  }
}
