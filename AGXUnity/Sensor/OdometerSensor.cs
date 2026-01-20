using agxSensor;
using AGXUnity.Utils;
using UnityEngine;
using System;

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
    /// <summary>
    /// Native instance
    /// </summary>
    public Odometer Native { get; private set; } = null;
    private OdometerModel m_nativeModel = null;

    /// <summary>
    /// Optional: Explicitly assign the constraint component to attach the odometer to.
    /// If left empty the component will use the first compatible parent.
    /// Compatible with: Hinge, CylindricalJoint, SplineJoint, WheelJoint, SlackHingeJoint, SlackCylindricalJoint
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Constraint component to attach odometer to: Hinge, CylindricalJoint, SplineJoint, WheelJoint, slackjoints. If unset, the first compatible parent is used." )]
    [DisableInRuntimeInspector]
    public Constraint ConstraintComponent { get; set; } = null;

    /// <summary>
    /// Wheel radius in meters
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Wheel radius in meters" )]
    [DisableInRuntimeInspector]
    [ClampAboveZeroInInspector]
    public float WheelRadius { get; set; } = 0.5f;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableTotalGaussianNoise { get; set; } = false;
    /// <summary>
    /// Noise RMS in meters.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Gaussian noise RMS" )]
    [DynamicallyShowInInspector( "EnableTotalGaussianNoise" )]
    [DisableInRuntimeInspector]
    public float TotalGaussianNoiseRms { get; set; } = 0.0f;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableSignalResolution { get; set; } = false;
    /// <summary>
    /// Pulses per wheel revolution.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Pulses per wheel revolution" )]
    [DynamicallyShowInInspector( "EnableSignalResolution" )]
    [DisableInRuntimeInspector]
    [ClampAboveZeroInInspector]
    public int PulsesPerRevolution { get; set; } = 1024;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public bool EnableSignalScaling { get; set; } = false;
    /// <summary>
    /// Constant scaling factor.
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Scaling factor applied to the distance signal" )]
    [DynamicallyShowInInspector( "EnableSignalScaling" )]
    [DisableInRuntimeInspector]
    public float SignalScaling { get; set; } = 1.0f;

    [RuntimeValue] public float SensorValue { get; private set; }

    private uint m_outputID = 0;
    [HideInInspector]
    public double OutputBuffer { get; private set; }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      if ( WheelRadius <= 0.0f ) {
        Debug.LogWarning( "Invalid WheelRadius, odometer will be inactive" );
        return false;
      }

      // Find a constraint component if not explicitly assigned
      if ( ConstraintComponent == null ) {
        ConstraintComponent = GetComponentInParent<Constraint>();
      }

      if ( ConstraintComponent == null ) {
        Debug.LogWarning( "No constraint component found/assigned, odometer will be inactive" );
        return false;
      }

      var modifiers = new IMonoaxialSignalSystemNodeRefVector();

      if ( EnableTotalGaussianNoise )
        modifiers.Add( new MonoaxialGaussianNoise( (double)TotalGaussianNoiseRms ) );

      if ( EnableSignalResolution )
        modifiers.Add( new MonoaxialSignalResolution( GetSignalResolution() ) );

      if ( EnableSignalScaling )
        modifiers.Add( new MonoaxialSignalScaling( (double)SignalScaling ) );

      m_nativeModel = new OdometerModel( (double)WheelRadius, modifiers );

      if ( m_nativeModel == null ) {
        Debug.LogWarning( "Could not create native odometer model, odometer will be inactive" );
        return false;
      }

      PropertySynchronizer.Synchronize( this );

      var initializedConstraint = ConstraintComponent.GetInitialized<Constraint>();
      if ( initializedConstraint == null ) {
        Debug.LogWarning( "Constraint component not initializable, odometer will be inactive" );
        return false;
      }

      Native = CreateNativeOdometerFromConstraint( initializedConstraint.Native, m_nativeModel );
      if ( Native == null ) {
        Debug.LogWarning( "Unsupported constraint type for odometer, odometer will be inactive" );
        return false;
      }

      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      var output = new OdometerOutputDistance();
      Native.getOutputHandler().add( m_outputID, output );

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    private double GetSignalResolution()
    {
      return 2.0 * System.Math.PI * WheelRadius / PulsesPerRevolution;
    }

    private static Odometer CreateNativeOdometerFromConstraint( agx.Constraint nativeConstraint, OdometerModel model )
    {
      if ( nativeConstraint is agx.Hinge hinge )
        return new Odometer( hinge, model );

      if ( nativeConstraint is agxVehicle.WheelJoint wheel )
        return new Odometer( wheel, model );

      if ( nativeConstraint is agx.CylindricalJoint cylindrical )
        return new Odometer( cylindrical, model );

      if ( nativeConstraint is agx.SplineJoint spline )
        return new Odometer( spline, model );

      if ( nativeConstraint is agx.SlackHingeJoint slackHinge )
        return new Odometer( slackHinge, model );

      if ( nativeConstraint is agx.SlackCylindricalJoint slackCylindrical )
        return new Odometer( slackCylindrical, model );

      return null;
    }

    public double GetOutput( OdometerOutput output )
    {
      if ( Native == null || output == null )
        return 0;

      var view = output.view();
      return view[ 0 ];
    }

    private void OnPostStepForward()
    {
      if ( !gameObject.activeInHierarchy )
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
