using agxSensor;
using AGXUnity.Utils;
using UnityEngine;
using System;
using AGXUnity.Model;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// Encoder Sensor Component - measures constraint position/speed (on one dof)
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/Encoder Sensor" )]
  [HelpURL( "https://www.algoryx.se/documentation/complete/agx/tags/latest/doc/UserManual/source/agxsensor.html#encoder" )]
  public class EncoderSensor : ScriptComponent
  {
    private const double DisabledTotalGaussianNoiseRms = 0.0;
    private const double DisabledSignalScaling = 1.0;

    /// <summary>
    /// Native instance
    /// </summary>
    public Encoder Native { get; private set; } = null;
    private EncoderModel m_nativeModel = null;

    /// <summary>
    /// Optional: Explicitly assign the constraint component to attach the encoder to.
    /// If left empty the component will use the first compatible parent.
    /// Compatible with: Hinge, Prismatic, CylindricalJoint, WheelJoint
    /// </summary>
    [SerializeField]
    [Tooltip( "Constraint / WheelJoint component to attach encoder to. If unset, the first compatible parent is used" )]
    [DisableInRuntimeInspector]
    public ScriptComponent ConstraintComponent { get; set; } = null;

    /// <summary>
    /// Accumulation mode of the encoder
    /// INCREMENTAL wraps within the cycle range, ABSOLUTE is single-turn absolute
    /// </summary>
    [SerializeField]
    private EncoderModel.Mode m_mode = EncoderModel.Mode.ABSOLUTE;
    [Tooltip( "Encoder accumulation mode. INCREMENTAL wraps within the cycle range; ABSOLUTE is single-turn absolute" )]
    public EncoderModel.Mode Mode
    {
      get => m_mode;
      set
      {
        m_mode = value;
        if ( Native != null )
          Native.getModel().setMode( m_mode );
      }
    }

    /// <summary>
    /// Cycle range [min, max] in sensor units.
    /// Rotary encoders typically use [0, 2*pi] radians.
    /// Linear encoders typically use a range in meters.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Cycle range [min, max] in sensor units. Rotary: radians (commonly [0, 2π]). Linear: meters." )]
    private RangeReal m_measurementRange = new RangeReal( float.MinValue, float.MaxValue );
    public RangeReal MeasurementRange
    {
      get => m_measurementRange;
      set
      {
        m_measurementRange = value;
        if ( Native != null )
          Native.getModel().setRange( m_measurementRange.Native );
      }
    }

    public enum TwoDofSample
    {
      Rotational,
      Translational
    }

    [SerializeField]
    private TwoDofSample m_sampleTwoDof = TwoDofSample.Rotational;
    [Tooltip( "Which DoF to sample for 2-DoF constraints (Cylindrical)" )]
    [DynamicallyShowInInspector( nameof( HasTwoDof ), true )]
    public TwoDofSample SampleTwoDof
    {
      get => m_sampleTwoDof;
      set
      {
        m_sampleTwoDof = value;
        if ( Native != null && Native.getConstraint() is agx.CylindricalJoint )
          Native.setConstraintSampleDof( (ulong)ToConstraint2Dof( m_sampleTwoDof ) );
      }
    }

    public enum WheelJointSample
    {
      Steering,
      WheelAxle,
      Suspension
    }

    [SerializeField]
    private WheelJointSample m_sampleWheelJoint = WheelJointSample.WheelAxle;
    [Tooltip( "Which secondary constraint to sample for WheelJoint" )]
    [DisableInRuntimeInspector]
    [DynamicallyShowInInspector( nameof( IsWheelJoint ), true )]
    public WheelJointSample SampleWheelJoint
    {
      get => m_sampleWheelJoint;
      set
      {
        m_sampleWheelJoint = value;
        if ( Native != null && Native.getConstraint() is agxVehicle.WheelJoint )
          Native.setConstraintSampleDof( (ulong)ToWheelSecondaryConstraint( m_sampleWheelJoint ) );
      }
    }

    /// <summary>
    /// Output selection
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Include position in the output" )]
    public bool OutputPosition { get; set; } = true;

    [field: SerializeField]
    [Tooltip( "Include speed in the output" )]
    public bool OutputSpeed { get; set; } = false;

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
    /// Noise RMS in sensor units (position units: radians/meters).
    /// </summary>
    [SerializeField]
    private float m_totalGaussianNoiseRms = 0.0f;
    [Tooltip( "Gaussian noise RMS (position units: radians/meters)." )]
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
    /// Resolution/bin size in sensor units (position units: radians/meters).
    /// </summary>
    [SerializeField]
    private float m_signalResolution = 0.01f;
    [Tooltip( "Resolution/bin size (position units: radians/meters)." )]
    [DynamicallyShowInInspector( nameof( EnableSignalResolution ) )]
    [ClampAboveZeroInInspector]
    public float SignalResolution
    {
      get => m_signalResolution;
      set
      {
        m_signalResolution = value > 0 ? value : m_signalResolution;
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
    [Tooltip( "Scaling factor applied to encoder outputs." )]
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

    [RuntimeValue] public float PositionValue { get; private set; }
    [RuntimeValue] public float SpeedValue { get; private set; }

    private IMonoaxialSignalSystemNodeRefVector m_modifiers = new IMonoaxialSignalSystemNodeRefVector();
    private MonoaxialGaussianNoise m_totalGaussianNoiseModifier = null;
    private MonoaxialSignalResolution m_signalResolutionModifier = null;
    private MonoaxialSignalScaling m_signalScalingModifier = null;

    private uint m_outputID = 0;

    [HideInInspector]
    public double PositionBuffer { get; private set; }

    [HideInInspector]
    public double SpeedBuffer { get; private set; }

    [HideInInspector]
    private bool IsWheelJoint => ConstraintComponent == null || ConstraintComponent is WheelJoint;

    [HideInInspector]
    private bool HasTwoDof => ConstraintComponent == null || ( ConstraintComponent is Constraint constraint && constraint.Type == ConstraintType.CylindricalJoint );

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

    private double GetSignalResolutionValue() => SignalResolution;

    private double GetSignalScalingValue() => EnableSignalScaling ? SignalScaling : DisabledSignalScaling;


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

      // Find a constraint component if not explicitly assigned.
      if ( ConstraintComponent == null ) {
        ConstraintComponent = FindParentJoint();
      }

      if ( ConstraintComponent == null ) {
        Debug.LogWarning( "No constraint component found/assigned, encoder will be inactive" );
        return false;
      }

      m_modifiers = new IMonoaxialSignalSystemNodeRefVector();
      m_totalGaussianNoiseModifier = new MonoaxialGaussianNoise( GetTotalGaussianNoiseRms() );
      m_signalResolutionModifier = new MonoaxialSignalResolution( GetSignalResolutionValue() );
      m_signalScalingModifier = new MonoaxialSignalScaling( GetSignalScalingValue() );

      m_modifiers.Add( m_totalGaussianNoiseModifier );
      m_modifiers.Add( m_signalResolutionModifier );
      m_modifiers.Add( m_signalScalingModifier );

      m_nativeModel = new EncoderModel( Mode, MeasurementRange.Native, m_modifiers );

      if ( m_nativeModel == null ) {
        Debug.LogWarning( "Could not create native encoder model, encoder will be inactive" );
        return false;
      }

      if ( IsWheelJoint ) {
        var initializedWheelJoint = ConstraintComponent.GetInitialized<WheelJoint>();
        if ( initializedWheelJoint == null ) {
          Debug.LogWarning( "Wheel Joint component not initializable, encoder will be inactive" );
          return false;
        }

        Native = CreateNativeEncoderFromConstraint( initializedWheelJoint.Native, m_nativeModel, SampleTwoDof, SampleWheelJoint );
      }
      else {
        var initializedConstraint = ConstraintComponent.GetInitialized<Constraint>();
        if ( initializedConstraint == null ) {
          Debug.LogWarning( "Constraint component not initializable, encoder will be inactive" );
          return false;
        }

        Native = CreateNativeEncoderFromConstraint( initializedConstraint.Native, m_nativeModel, SampleTwoDof, SampleWheelJoint );
      }

      if ( Native == null ) {
        Debug.LogWarning( "Unsupported constraint type for encoder, encoder will be inactive" );
        return false;
      }

      if ( OutputPosition || OutputSpeed ) {
        m_outputID = SensorEnvironment.Instance.GenerateOutputID();

        var output = new EncoderOutputPositionSpeed();

        Native.getOutputHandler().add( m_outputID, output );

        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;
      }
      else {
        Debug.LogWarning( "No output configured for encoder" );
      }

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    private static Encoder CreateNativeEncoderFromConstraint( agx.Constraint nativeConstraint,
                                                             EncoderModel model,
                                                             TwoDofSample sampleTwoDof,
                                                             WheelJointSample sampleWheelJoint )
    {
      if ( nativeConstraint == null || model == null )
        return null;

      if ( nativeConstraint is agx.Hinge hinge )
        return new Encoder( hinge, model );

      if ( nativeConstraint is agx.Prismatic prismatic )
        return new Encoder( prismatic, model );

      if ( nativeConstraint is agx.CylindricalJoint cylindrical )
        return new Encoder( cylindrical, model, ToConstraint2Dof( sampleTwoDof ) );

      if ( nativeConstraint is agxVehicle.WheelJoint wheel )
        return new Encoder( wheel, model, ToWheelSecondaryConstraint( sampleWheelJoint ) );

      return null;
    }

    private static agx.Constraint2DOF.DOF ToConstraint2Dof( TwoDofSample value )
    {
      return value == TwoDofSample.Translational ? agx.Constraint2DOF.DOF.SECOND : agx.Constraint2DOF.DOF.FIRST;
    }

    private static agxVehicle.WheelJoint.SecondaryConstraint ToWheelSecondaryConstraint( WheelJointSample value )
    {
      switch ( value ) {
        case WheelJointSample.Steering:
          return agxVehicle.WheelJoint.SecondaryConstraint.STEERING;
        case WheelJointSample.Suspension:
          return agxVehicle.WheelJoint.SecondaryConstraint.SUSPENSION;
        default:
          return agxVehicle.WheelJoint.SecondaryConstraint.WHEEL;
      }
    }

    // Will only run if there is an output
    private void OnPostSynchronizeTransforms()
    {
      if ( !gameObject.activeInHierarchy || Native == null )
        return;

      var output = Native.getOutputHandler().get( m_outputID );
      if ( output == null ) {
        PositionBuffer = 0;
        SpeedBuffer = 0;
      }
      else {
        var viewPosSpeed = output.viewPositionSpeed();
        if ( viewPosSpeed != null && viewPosSpeed.size() > 0 ) {
          var first = viewPosSpeed.begin();
          if ( OutputSpeed )
            SpeedBuffer = first.speed;
          if ( OutputPosition )
            PositionBuffer = first.position;
        }
      }

      // Convenience runtime display of output
      PositionValue = (float)PositionBuffer;
      SpeedValue = (float)SpeedBuffer;
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
