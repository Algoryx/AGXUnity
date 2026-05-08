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
    /// Stores a reference to a component and a path to a float or Vector3 member, with optional
    /// drilling through an ElementaryConstraint[] member to reach a property on a specific constraint.
    /// Call Bind() once at runtime to cache reflection data, then Get()/Set() to read/write.
    /// </summary>
    [Serializable]
    public class ComponentFloatProperty
    {
      public enum Vector3Axis { X, Y, Z }

      [SerializeField]
      public Component TargetComponent = null;

      // Level-1 member on Target (float, Vector3, or ElementaryConstraint[])
      [SerializeField]
      public string MemberName = string.Empty;

      [SerializeField]
      public Vector3Axis Axis = Vector3Axis.X;

      // Enum argument values for top-level method (one int per parameter, cast to/from enum)
      [SerializeField]
      public int[] MethodArgs = new int[ 0 ];

      // Level-2: used when MemberName resolves to ElementaryConstraint[]
      [SerializeField]
      public int ECIndex = 0;

      [SerializeField]
      public string ECMemberName = string.Empty;

      [SerializeField]
      public Vector3Axis ECAxis = Vector3Axis.X;

      // Enum argument values for EC-level method
      [SerializeField]
      public int[] ECMethodArgs = new int[ 0 ];

      private PropertyInfo m_property;
      private FieldInfo    m_field;
      private bool         m_isVector3;
      private bool         m_isECArray;
      private MethodInfo   m_method;

      private PropertyInfo m_ecProperty;
      private FieldInfo    m_ecField;
      private bool         m_ecIsVector3;
      private MethodInfo   m_ecMethod;

      public bool IsValid => TargetComponent != null && (
        m_method != null ||
        ( m_isECArray
          ? ( ( m_property != null || m_field != null ) &&
              ( m_ecProperty != null || m_ecField != null || m_ecMethod != null ) )
          : ( m_property != null || m_field != null ) )
      );

      public void Bind()
      {
        m_property    = null;
        m_field       = null;
        m_isVector3   = false;
        m_isECArray   = false;
        m_method      = null;
        m_ecProperty  = null;
        m_ecField     = null;
        m_ecIsVector3 = false;
        m_ecMethod    = null;

        if ( TargetComponent == null || string.IsNullOrEmpty( MemberName ) )
          return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        var type = TargetComponent.GetType();

        var prop = type.GetProperty( MemberName, flags );
        if ( prop != null && prop.CanRead ) {
          if ( prop.PropertyType == typeof( float ) || prop.PropertyType == typeof( Vector3 ) ) {
            m_property  = prop;
            m_isVector3 = prop.PropertyType == typeof( Vector3 );
            return;
          }
          if ( prop.PropertyType == typeof( ElementaryConstraint[] ) ) {
            m_property  = prop;
            m_isECArray = true;
            BindECMember();
            return;
          }
        }

        var field = type.GetField( MemberName, flags );
        if ( field != null ) {
          if ( field.FieldType == typeof( float ) || field.FieldType == typeof( Vector3 ) ) {
            m_field     = field;
            m_isVector3 = field.FieldType == typeof( Vector3 );
            return;
          }
          if ( field.FieldType == typeof( ElementaryConstraint[] ) ) {
            m_field     = field;
            m_isECArray = true;
            BindECMember();
            return;
          }
        }

        m_method = FindFloatMethod( type, MemberName );
      }

      public float Get()
      {
        if ( m_method != null )
          return (float)m_method.Invoke( TargetComponent, BuildInvokeArgs( m_method, MethodArgs ) );

        if ( m_isECArray ) {
          var arr = GetECArray();
          if ( arr == null || ECIndex < 0 || ECIndex >= arr.Length ) return 0.0f;
          var ec = arr[ ECIndex ];
          if ( m_ecMethod != null )
            return (float)m_ecMethod.Invoke( ec, BuildInvokeArgs( m_ecMethod, ECMethodArgs ) );
          if ( m_ecIsVector3 ) {
            var v = m_ecProperty != null
              ? (Vector3)m_ecProperty.GetValue( ec )
              : (Vector3)m_ecField.GetValue( ec );
            switch ( ECAxis ) {
              case Vector3Axis.X: return v.x;
              case Vector3Axis.Y: return v.y;
              case Vector3Axis.Z: return v.z;
              default:            return v.x;
            }
          }
          return m_ecProperty != null
            ? (float)m_ecProperty.GetValue( ec )
            : (float)m_ecField.GetValue( ec );
        }

        if ( m_property == null && m_field == null ) return 0.0f;

        if ( m_isVector3 ) {
          var v = m_property != null
            ? (Vector3)m_property.GetValue( TargetComponent )
            : (Vector3)m_field.GetValue( TargetComponent );
          switch ( Axis ) {
            case Vector3Axis.X: return v.x;
            case Vector3Axis.Y: return v.y;
            case Vector3Axis.Z: return v.z;
            default:            return v.x;
          }
        }

        return m_property != null
          ? (float)m_property.GetValue( TargetComponent )
          : (float)m_field.GetValue( TargetComponent );
      }

      public void Set( float value )
      {
        // Methods are read-only; skip silently
        if ( m_method != null || ( m_isECArray && m_ecMethod != null ) )
          return;

        if ( m_isECArray ) {
          var arr = GetECArray();
          if ( arr == null || ECIndex < 0 || ECIndex >= arr.Length ) return;
          var ec = arr[ ECIndex ];
          if ( m_ecIsVector3 ) {
            var v = m_ecProperty != null
              ? (Vector3)m_ecProperty.GetValue( ec )
              : (Vector3)m_ecField.GetValue( ec );
            switch ( ECAxis ) {
              case Vector3Axis.X: v.x = value; break;
              case Vector3Axis.Y: v.y = value; break;
              case Vector3Axis.Z: v.z = value; break;
            }
            if ( m_ecProperty != null && m_ecProperty.CanWrite )
              m_ecProperty.SetValue( ec, v );
            else
              m_ecField?.SetValue( ec, v );
          }
          else {
            if ( m_ecProperty != null && m_ecProperty.CanWrite )
              m_ecProperty.SetValue( ec, value );
            else
              m_ecField?.SetValue( ec, value );
          }
          return;
        }

        if ( m_isVector3 ) {
          var v = m_property != null
            ? (Vector3)m_property.GetValue( TargetComponent )
            : (Vector3)m_field.GetValue( TargetComponent );
          switch ( Axis ) {
            case Vector3Axis.X: v.x = value; break;
            case Vector3Axis.Y: v.y = value; break;
            case Vector3Axis.Z: v.z = value; break;
          }
          if ( m_property != null && m_property.CanWrite )
            m_property.SetValue( TargetComponent, v );
          else
            m_field?.SetValue( TargetComponent, v );
        }
        else {
          if ( m_property != null && m_property.CanWrite )
            m_property.SetValue( TargetComponent, value );
          else
            m_field?.SetValue( TargetComponent, value );
        }
      }

      private void BindECMember()
      {
        m_ecProperty  = null;
        m_ecField     = null;
        m_ecIsVector3 = false;
        m_ecMethod    = null;

        if ( string.IsNullOrEmpty( ECMemberName ) )
          return;

        var arr = GetECArray();
        if ( arr == null || ECIndex < 0 || ECIndex >= arr.Length )
          return;

        var ecType = arr[ ECIndex ].GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        var prop = ecType.GetProperty( ECMemberName, flags );
        if ( prop != null && prop.CanRead &&
             ( prop.PropertyType == typeof( float ) || prop.PropertyType == typeof( Vector3 ) ) ) {
          m_ecProperty  = prop;
          m_ecIsVector3 = prop.PropertyType == typeof( Vector3 );
          return;
        }

        var field = ecType.GetField( ECMemberName, flags );
        if ( field != null && ( field.FieldType == typeof( float ) || field.FieldType == typeof( Vector3 ) ) ) {
          m_ecField     = field;
          m_ecIsVector3 = field.FieldType == typeof( Vector3 );
          return;
        }

        m_ecMethod = FindFloatMethod( ecType, ECMemberName );
      }

      private static MethodInfo FindFloatMethod( Type type, string name )
      {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach ( var m in type.GetMethods( flags ) ) {
          if ( m.Name != name || m.ReturnType != typeof( float ) ) continue;
          bool allOptional = true;
          foreach ( var p in m.GetParameters() )
            if ( !p.IsOptional ) { allOptional = false; break; }
          if ( allOptional ) return m;
        }
        return null;
      }

      private static object[] BuildInvokeArgs( MethodInfo method, int[] storedArgs )
      {
        var parms = method.GetParameters();
        if ( parms.Length == 0 ) return null;
        var args = new object[ parms.Length ];
        for ( int i = 0; i < parms.Length; i++ ) {
          if ( storedArgs != null && i < storedArgs.Length && parms[ i ].ParameterType.IsEnum )
            args[ i ] = Enum.ToObject( parms[ i ].ParameterType, storedArgs[ i ] );
          else
            args[ i ] = parms[ i ].DefaultValue;
        }
        return args;
      }

      private ElementaryConstraint[] GetECArray()
      {
        if ( TargetComponent == null ) return null;
        if ( m_property != null ) return m_property.GetValue( TargetComponent ) as ElementaryConstraint[];
        if ( m_field    != null ) return m_field.GetValue( TargetComponent ) as ElementaryConstraint[];
        return null;
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
      OpenPLXSignal,
      ComponentProperty
    }

    public enum InputSource
    {
      Field,
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

    [InspectorGroupBegin( Name = "Gains", DefaultExpanded = true )]
    /// <summary>
    /// Proportional gain (Kp).
    /// </summary>
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

    [InspectorGroupEnd]
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
    private InputSource m_inputSource = InputSource.Field;

    [SerializeField]
    private ComponentFloatProperty m_inputProperty = new ComponentFloatProperty();

    [InspectorSeparator]
    [InspectorGroupBegin( Name = "Input", DefaultExpanded = true )]
    public InputSource InputSourceType
    {
      get => m_inputSource;
      set => m_inputSource = value;
    }

    private bool IsInputField             => m_inputSource == InputSource.Field;
    private bool IsInputComponentProperty => m_inputSource == InputSource.ComponentProperty;

    [DynamicallyShowInInspector( nameof( IsInputComponentProperty ) )]
    public ComponentFloatProperty SourceProperty => m_inputProperty;

    [SerializeField]
    private float m_processValue = 0.0f;

    /// <summary>
    /// Measured process variable fed into the controller each update.
    /// </summary>
    [DynamicallyShowInInspector( nameof( IsInputField ) )]
    public float ProcessValue
    {
      get => m_processValue;
      set => m_processValue = value;
    }

    [SerializeField]
    private float m_inputScale = 1.0f;

    [InspectorGroupEnd]
    public float InputScale
    {
      get => m_inputScale;
      set => m_inputScale = value;
    }

    [SerializeField]
    private OutputTarget m_outputTarget = OutputTarget.Field;

    [InspectorSeparator]
    [InspectorGroupBegin( Name = "Output", DefaultExpanded = true )]
    public OutputTarget OutputTargetType
    {
      get => m_outputTarget;
      set => m_outputTarget = value;
    }

    private bool IsOutputUnityEvent => m_outputTarget == OutputTarget.UnityEvent;
    private bool IsOutputOpenPLX => m_outputTarget == OutputTarget.OpenPLXSignal;
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
    public ComponentFloatProperty TargetProperty => m_componentProperty;

    [SerializeField]
    private float m_outputScale = 1.0f;

    [InspectorGroupEnd]
    public float OutputScale
    {
      get => m_outputScale;
      set => m_outputScale = value;
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
    private OutputWriteCallback m_outputWriteCallback = OutputWriteCallback.PostSynchronizeTransforms;

    /// <summary>
    /// When to automatically call WriteProcessValueToOutput. Set to Manual if wanting to update it from a custom script.
    /// </summary>
    public OutputWriteCallback OutputCallback
    {
      get => m_outputWriteCallback;
      set
      {
        m_outputWriteCallback = value;
        UpdateCallbackRegistration();
      }
    }

    [RuntimeValue]
    public bool NativeEnabled { get; private set; } = false;

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

      var time = Simulation.HasInstance ? Simulation.Instance.Native.getTimeStamp() : 0;
      Native.update( time, m_processValue * m_inputScale );
      m_outputValue = (float)Native.getManipulatedVariable() * m_outputScale;

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
      m_inputProperty.Bind();

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

        case OutputTarget.OpenPLXSignal:
          m_openPLXInputTarget?.SendSignal( value );
          break;

        case OutputTarget.ComponentProperty:
          m_componentProperty.Set( value );
          break;
      }
    }

    private void InitializeOpenPLXTarget()
    {
      if ( m_outputTarget != OutputTarget.OpenPLXSignal || string.IsNullOrEmpty( m_openPLXSignalName ) )
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
      if ( m_inputSource == InputSource.ComponentProperty && m_inputProperty.IsValid )
        WriteProcessValueToOutput( m_inputProperty.Get() );
      else
        WriteProcessValueToOutput();
    }

    private void UpdateRuntimeValues()
    {
      if ( Native == null ) {
        NativeEnabled                = false;
        RuntimeError                  = 0.0f;
        RuntimeMeasuredProcessValue   = 0.0f;
        RuntimeManipulatedVariable    = m_outputValue;
        RuntimeProportionalTerm       = 0.0f;
        RuntimeIntegralTerm           = 0.0f;
        RuntimeDerivativeTerm         = 0.0f;
        return;
      }

      NativeEnabled               = Native.isEnabled();
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
