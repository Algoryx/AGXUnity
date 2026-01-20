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
    [field: SerializeField]
    [Tooltip( "Constraint / WheelJoint component to attach encoder to. If unset, the first compatible parent is used" )]
    [DisableInRuntimeInspector]
    public ScriptComponent ConstraintComponent { get; set; } = null;

    /// <summary>
    /// Accumulation mode of the encoder
    /// INCREMENTAL wraps within the cycle range, ABSOLUTE is single-turn absolute
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Encoder accumulation mode. INCREMENTAL wraps within the cycle range; ABSOLUTE is single-turn absolute" )]
    [DisableInRuntimeInspector]
    public EncoderModel.Mode Mode { get; set; } = EncoderModel.Mode.INCREMENTAL;

    /// <summary>
    /// Cycle range [min, max] in sensor units.
    /// Rotary encoders typically use [0, 2*pi] radians.
    /// Linear encoders typically use a range in meters.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Cycle range [min, max] in sensor units. Rotary: radians (commonly [0, 2π]). Linear: meters." )]
    [DisableInRuntimeInspector]
    public RangeReal MeasurementRange { get; set; } = new RangeReal( float.MinValue, float.MaxValue );

    public enum TwoDofSample
    {
      First,
      Second
    }

    [field: SerializeField]
    [Tooltip( "Which DoF to sample for 2-DoF constraints (Cylindrical)" )]
    [DisableInRuntimeInspector]
    [DynamicallyShowInInspector( "HasTwoDof", true )]
    public TwoDofSample SampleTwoDof { get; set; } = TwoDofSample.First;

    public enum WheelJointSample
    {
      Steering,
      WheelAxle,
      Suspension
    }

    [field: SerializeField]
    [Tooltip( "Which secondary constraint to sample for WheelJoint" )]
    [DisableInRuntimeInspector]
    [DynamicallyShowInInspector( "IsWheelJoint", true )]
    public WheelJointSample SampleWheelJoint { get; set; } = WheelJointSample.WheelAxle;

    /// <summary>
    /// Output selection
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Include position in the output" )]
    [DisableInRuntimeInspector]
    public bool OutputPosition { get; set; } = true;

    [field: SerializeField]
    [Tooltip( "Include speed in the output" )]
    [DisableInRuntimeInspector]
    public bool OutputSpeed { get; set; } = false;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableTotalGaussianNoise { get; set; } = false;

    /// <summary>
    /// Noise RMS in sensor units (position units: radians/meters).
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Gaussian noise RMS (position units: radians/meters)." )]
    [DynamicallyShowInInspector( "EnableTotalGaussianNoise" )]
    [DisableInRuntimeInspector]
    public float TotalGaussianNoiseRms { get; set; } = 0.0f;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableSignalResolution { get; set; } = false;

    /// <summary>
    /// Resolution/bin size in sensor units (position units: radians/meters).
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Resolution/bin size (position units: radians/meters)." )]
    [DynamicallyShowInInspector( "EnableSignalResolution" )]
    [DisableInRuntimeInspector]
    [ClampAboveZeroInInspector]
    public float SignalResolution { get; set; } = 0.01f;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableSignalScaling { get; set; } = false;

    /// <summary>
    /// Constant scaling factor.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Scaling factor applied to encoder outputs." )]
    [DynamicallyShowInInspector( "EnableSignalScaling" )]
    [DisableInRuntimeInspector]
    public float SignalScaling { get; set; } = 1.0f;

    [RuntimeValue] public float PositionValue { get; private set; }
    [RuntimeValue] public float SpeedValue { get; private set; }

    private uint m_outputID = 0;

    [HideInInspector]
    public double PositionBuffer { get; private set; }

    [HideInInspector]
    public double SpeedBuffer { get; private set; }

    public bool IsWheelJoint => ConstraintComponent == null || ConstraintComponent is WheelJoint;

    public bool HasTwoDof => ConstraintComponent == null || (ConstraintComponent is Constraint constraint && constraint.Type == ConstraintType.CylindricalJoint);

    private ScriptComponent FindParentJoint()
    {
      ScriptComponent component = GetComponentInParent<WheelJoint>();
      if ( component == null )
        component = GetComponentInParent<Constraint>();
      return component;
    }

    // Set constraint on component creation
    private void Reset()
    {
      if (ConstraintComponent == null)
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

      var modifiers = new IMonoaxialSignalSystemNodeRefVector();

      if ( EnableTotalGaussianNoise )
        modifiers.Add( new MonoaxialGaussianNoise( (double)TotalGaussianNoiseRms ) );

      if ( EnableSignalResolution )
        modifiers.Add( new MonoaxialSignalResolution( (double)SignalResolution ) );

      if ( EnableSignalScaling )
        modifiers.Add( new MonoaxialSignalScaling( (double)SignalScaling ) );

      m_nativeModel = new EncoderModel( Mode, MeasurementRange.Native, modifiers );

      if ( m_nativeModel == null ) {
        Debug.LogWarning( "Could not create native encoder model, encoder will be inactive" );
        return false;
      }

      PropertySynchronizer.Synchronize( this );

      var initializedConstraint = ConstraintComponent.GetInitialized<Constraint>();
      if ( initializedConstraint == null ) {
        Debug.LogWarning( "Constraint component not initializable, encoder will be inactive" );
        return false;
      }

      Native = CreateNativeEncoderFromConstraint( initializedConstraint.Native, m_nativeModel, SampleTwoDof, SampleWheelJoint );
      if ( Native == null ) {
        Debug.LogWarning( "Unsupported constraint type for encoder, encoder will be inactive" );
        return false;
      }

      if ( OutputPosition || OutputSpeed ) {
        m_outputID = SensorEnvironment.Instance.GenerateOutputID();

        var output = new EncoderOutput();
        if ( OutputPosition )
          output.add( EncoderOutput.Field.POSITION_F64 );
        if ( OutputSpeed )
          output.add( EncoderOutput.Field.SPEED_F64 );

        Native.getOutputHandler().add( m_outputID, output );

        Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;
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

      // Dedicated constructors (preferred).
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
      return value == TwoDofSample.Second ? agx.Constraint2DOF.DOF.SECOND : agx.Constraint2DOF.DOF.FIRST;
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
    private void OnPostStepForward()
    {
      if ( !gameObject.activeInHierarchy || Native == null )
        return;

      var output = Native.getOutputHandler().get( m_outputID );
      if ( output == null ) {
        PositionBuffer = 0;
        SpeedBuffer = 0;
      }
      else {
        var viewPosition = output.viewPosition();
        var viewSpeed = output.viewPosition();

        if ( viewPosition != null )
          PositionBuffer = viewPosition[ 0 ];

        if ( viewSpeed != null )
          SpeedBuffer = viewSpeed[ 0 ];
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
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;

      Native?.Dispose();
      Native = null;

      m_nativeModel?.Dispose();
      m_nativeModel = null;

      base.OnDestroy();
    }

    [NonSerialized]
    private Mesh m_nodeGizmoMesh = null;

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
      if ( m_nodeGizmoMesh == null )
        m_nodeGizmoMesh = Resources.Load<Mesh>( @"Debug/Models/Icosahedron" );

      if ( m_nodeGizmoMesh == null )
        return;

      Gizmos.color = Color.yellow;
      Gizmos.DrawWireMesh( m_nodeGizmoMesh,
                           transform.position,
                           transform.rotation,
                           Vector3.one * 0.2f );
#endif
    }
  }
}
