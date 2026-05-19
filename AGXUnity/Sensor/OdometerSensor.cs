using agxSensor;
using AGXUnity.Model;
using UnityEngine;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// Odometer Sensor Component - measures distance based on constraint value
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/Odometer Sensor" )]
  [HelpURL( "https://www.algoryx.se/documentation/complete/agx/tags/latest/doc/UserManual/source/agxsensor.html#odometer" )]
  public class OdometerSensor : ScriptComponent
  {
    private const double DisabledTotalGaussianNoiseRms = 0.0;
    private const double DisabledSignalScaling = 1.0;

    /// <summary>
    /// Native instance
    /// </summary>
    public Odometer Native { get; private set; } = null;
    private OdometerModel m_nativeModel = null;

    /// <summary>
    /// Optional: Explicitly assign the constraint component to attach the odometer to.
    /// If left empty the component will use the first compatible parent.
    /// Compatible with: Hinge, CylindricalJoint, WheelJoint
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Constraint / WheelJoint component to attach odometer to: Hinge, CylindricalJoint, WheelJoint. If unset, the first compatible parent is used." )]
    public ScriptComponent ConstraintComponent { get; set; } = null;

    /// <summary>
    /// Wheel radius in meters
    /// </summary>
    [SerializeField]
    private float m_wheelRadius = 0.5f;
    [Tooltip( "Wheel radius in meters" )]
    [ClampAboveZeroInInspector]
    public float WheelRadius
    {
      get => m_wheelRadius;
      set
      {
        m_wheelRadius = value > 0 ? value : m_wheelRadius;
        if ( Native != null )
          Native.getModel().setWheelRadius( m_wheelRadius );
        SynchronizeSignalResolutionModifier();
      }
    }

    [SerializeField]
    private bool m_enableTotalGaussianNoise = false;
    [InspectorGroupBegin( Name = "Modifiers", DefaultExpanded = true )]
    public bool EnableTotalGaussianNoise
    {
      get => m_enableTotalGaussianNoise;
      set
      {
        m_enableTotalGaussianNoise = value;
        SynchronizeTotalGaussianNoiseModifier();
      }
    }
    /// <summary>
    /// Noise RMS in meters.
    /// </summary>
    [SerializeField]
    private float m_totalGaussianNoiseRms = 0.0f;
    [Tooltip( "Gaussian noise RMS" )]
    [DynamicallyShowInInspector( nameof( EnableTotalGaussianNoise ) )]
    public float TotalGaussianNoiseRms
    {
      get => m_totalGaussianNoiseRms;
      set
      {
        m_totalGaussianNoiseRms = value;
        SynchronizeTotalGaussianNoiseModifier();
      }
    }

    [SerializeField]
    private bool m_enableSignalResolution = false;
    [DisableInRuntimeInspector]
    public bool EnableSignalResolution
    {
      get => m_enableSignalResolution;
      set
      {
        m_enableSignalResolution = value;
        SynchronizeSignalResolutionModifier();
      }
    }
    /// <summary>
    /// Pulses per wheel revolution.
    /// </summary>
    [SerializeField]
    private int m_pulsesPerRevolution = 1024;
    [Tooltip( "Pulses per wheel revolution" )]
    [DynamicallyShowInInspector( nameof( EnableSignalResolution ) )]
    [ClampAboveZeroInInspector]
    public int PulsesPerRevolution
    {
      get => m_pulsesPerRevolution;
      set
      {
        m_pulsesPerRevolution = value > 0 ? value : m_pulsesPerRevolution;
        SynchronizeSignalResolutionModifier();
      }
    }

    [SerializeField]
    private bool m_enableSignalScaling = false;
    public bool EnableSignalScaling
    {
      get => m_enableSignalScaling;
      set
      {
        m_enableSignalScaling = value;
        SynchronizeSignalScalingModifier();
      }
    }
    /// <summary>
    /// Constant scaling factor.
    /// </summary>
    [SerializeField]
    private float m_signalScaling = 1.0f;
    [Tooltip( "Scaling factor applied to the distance signal" )]
    [DynamicallyShowInInspector( nameof( EnableSignalScaling ) )]
    public float SignalScaling
    {
      get => m_signalScaling;
      set
      {
        m_signalScaling = value;
        SynchronizeSignalScalingModifier();
      }
    }

    [InspectorGroupEnd]

    [HideInInspector]
    private bool IsWheelJoint => ConstraintComponent == null || ConstraintComponent is WheelJoint;

    [RuntimeValue] public float SensorValue { get; private set; }

    private IMonoaxialSignalSystemNodeRefVector m_modifiers = new IMonoaxialSignalSystemNodeRefVector();
    private MonoaxialGaussianNoise m_totalGaussianNoiseModifier = null;
    private MonoaxialSignalResolution m_signalResolutionModifier = null;
    private MonoaxialSignalScaling m_signalScalingModifier = null;

    private uint m_outputID = 0;
    [HideInInspector]
    public double OutputBuffer { get; private set; }

    private ScriptComponent FindParentJoint()
    {
      ScriptComponent component = GetComponentInParent<WheelJoint>();
      if ( component == null )
        component = GetComponentInParent<Constraint>();
      return component;
    }

    private void Reset()
    {
      if ( ConstraintComponent == null )
        ConstraintComponent = FindParentJoint();
    }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      if ( WheelRadius <= 0.0f ) {
        Debug.LogWarning( "Invalid WheelRadius, odometer will be inactive" );
        return false;
      }

      // Find a constraint component if not explicitly assigned
      if ( ConstraintComponent == null ) {
        ConstraintComponent = FindParentJoint();
      }

      if ( ConstraintComponent == null ) {
        Debug.LogWarning( "No constraint component found/assigned, odometer will be inactive" );
        return false;
      }

      m_modifiers = new IMonoaxialSignalSystemNodeRefVector();
      if ( EnableTotalGaussianNoise ) {
        m_totalGaussianNoiseModifier = new MonoaxialGaussianNoise( GetTotalGaussianNoiseRms() );
        m_modifiers.Add( m_totalGaussianNoiseModifier );
      }
      if ( EnableSignalResolution ) {
        m_signalResolutionModifier = new MonoaxialSignalResolution( GetSignalResolutionValue() );
        m_modifiers.Add( m_signalResolutionModifier );
      }
      if ( EnableSignalScaling ) {
        m_signalScalingModifier = new MonoaxialSignalScaling( GetSignalScalingValue() );
        m_modifiers.Add( m_signalScalingModifier );
      }

      m_nativeModel = new OdometerModel( (double)WheelRadius, m_modifiers );

      if ( m_nativeModel == null ) {
        Debug.LogWarning( "Could not create native odometer model, odometer will be inactive" );
        return false;
      }

      if ( IsWheelJoint ) {
        var initializedWheelJoint = ConstraintComponent.GetInitialized<WheelJoint>();
        if ( initializedWheelJoint == null ) {
          Debug.LogWarning( "Wheel Joint component not initializable, encoder will be inactive" );
          return false;
        }

        Native = CreateNativeOdometerFromConstraint( initializedWheelJoint.Native, m_nativeModel );
      }
      else {
        var initializedConstraint = ConstraintComponent.GetInitialized<Constraint>();
        if ( initializedConstraint == null ) {
          Debug.LogWarning( "Constraint component not initializable, encoder will be inactive" );
          return false;
        }

        Native = CreateNativeOdometerFromConstraint( initializedConstraint.Native, m_nativeModel );
      }

      if ( Native == null ) {
        Debug.LogWarning( "Unsupported constraint type for odometer, odometer will be inactive" );
        return false;
      }

      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      var output = new OdometerOutputDistance();
      Native.getOutputHandler().add( m_outputID, output );

      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    private double SignalResolutionValue => 2.0 * System.Math.PI * WheelRadius / PulsesPerRevolution;

    private void SynchronizeTotalGaussianNoiseModifier()
    {
      m_totalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoiseRms() );
    }

    private void SynchronizeSignalResolutionModifier()
    {
      m_signalResolutionModifier?.setResolution( GetSignalResolutionValue() );
    }

    private void SynchronizeSignalScalingModifier()
    {
      m_signalScalingModifier?.setScaling( GetSignalScalingValue() );
    }

    private double GetTotalGaussianNoiseRms() => EnableTotalGaussianNoise ? TotalGaussianNoiseRms : DisabledTotalGaussianNoiseRms;

    private double GetSignalResolutionValue() => SignalResolutionValue;

    private double GetSignalScalingValue() => EnableSignalScaling ? SignalScaling : DisabledSignalScaling;

    private static Odometer CreateNativeOdometerFromConstraint( agx.Constraint nativeConstraint, OdometerModel model )
    {
      if ( nativeConstraint is agx.Hinge hinge )
        return new Odometer( hinge, model );

      if ( nativeConstraint is agxVehicle.WheelJoint wheel )
        return new Odometer( wheel, model );

      if ( nativeConstraint is agx.CylindricalJoint cylindrical )
        return new Odometer( cylindrical, model );

      return null;
    }

    private double GetOutput( OdometerOutput output )
    {
      if ( Native == null || output == null )
        return 0;

      var view = output.view();

      if ( view.size() > 0 )
        return view[ 0 ];
      else
        return 0;
    }

    private void OnPostSynchronizeTransforms()
    {
      if ( !isActiveAndEnabled )
        return;

      OutputBuffer = GetOutput( Native.getOutputHandler().get( m_outputID ) );

      // Convenience runtime display of output
      SensorValue = (float)OutputBuffer;
    }

    protected override void OnEnable()
    {
      Native?.setEnable( true );
    }

    protected override void OnDisable()
    {
      Native?.setEnable( false );
    }

    protected override void OnDestroy()
    {
      if ( SensorEnvironment.HasInstance )
        SensorEnvironment.Instance.Native?.remove( Native );

      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransforms;

      base.OnDestroy();
    }
  }
}
