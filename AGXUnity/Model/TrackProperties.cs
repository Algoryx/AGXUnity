using UnityEngine;
using UnityEngine.Serialization;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#track-properties" )]
  public class TrackProperties : ScriptAsset
  {
    public agxVehicle.TrackProperties Native { get; private set; }

    [SerializeField]
    private Vector2 m_hingeComplianceRotational = 1.0E-10f * Vector2.one;

    [SerializeField]
    private Vector2 m_hingeDampingRotational = 0.04f * Vector2.one;

    protected override bool PerformMigration()
    {
      if ( m_serializationVersion < 2 ) {
        System.Func<float, float> convertCompliance = (float old) => {
          // Calculate correction term based on an assumed node length of 0.1m
          const float numNodesPerMeter = 10;
          const float correction = 1.0f - 1.0f / (numNodesPerMeter + 1.0f);
          const float invPairLength = correction / 0.1f;

          return 1 / (old * invPairLength);
        };

        System.Func<float, float> convertDamping = (float old) => {
          float dt = 0.02f; // Unity serialization mechanism does not allow us to get the current fixed timestep here so we assume a dt of 0.02

          return old / dt;
        };

        HingeStiffnessTranslational = new Vector3( convertCompliance( HingeStiffnessTranslational.x ), convertCompliance( HingeStiffnessTranslational.y ), convertCompliance( HingeStiffnessTranslational.z ) );
        HingeAttenuationTranslational = new Vector3( convertDamping( HingeAttenuationTranslational.x ), convertDamping( HingeAttenuationTranslational.y ), convertDamping( HingeAttenuationTranslational.z ) );
        HingeStiffnessRotational = new Vector3( convertCompliance( m_hingeComplianceRotational.x ), convertCompliance( m_hingeComplianceRotational.y ), 100.0f );
        HingeAttenuationRotational = new Vector3( convertDamping( m_hingeDampingRotational.x ), convertDamping( m_hingeDampingRotational.y ), 2.0f );

        return true;
      }

      return false;
    }

    [field: SerializeField]
    public bool FullDoF { get; set; } = false;

    [DynamicallyShowInInspector( nameof( FullDoF ), invert: true )]
    [ClampAboveZeroInInspector]
    public float BendingStiffness
    {
      get => m_hingeStiffnessRotational.z;
      set
      {
        m_hingeStiffnessRotational.z = value;
        Native?.setBendingStiffness( m_hingeStiffnessRotational.z, agxVehicle.TrackProperties.Axis.LATERAL );
      }
    }

    [DynamicallyShowInInspector( nameof( FullDoF ), invert: true )]
    [ClampAboveZeroInInspector]
    public float BendingAttenuation
    {
      get => m_hingeAttenuationRotational.z;
      set
      {
        m_hingeAttenuationRotational.z = value;
        Native?.setBendingAttenuation( m_hingeAttenuationRotational.z, agxVehicle.TrackProperties.Axis.LATERAL );
      }
    }

    [DynamicallyShowInInspector( nameof( FullDoF ), invert: true )]
    [ClampAboveZeroInInspector]
    public float TensileStiffness
    {
      get => m_hingeStiffnessTranslational.y;
      set
      {
        m_hingeStiffnessTranslational.y = value;
        Native?.setTensileStiffness( m_hingeStiffnessTranslational.y );
      }
    }

    [DynamicallyShowInInspector( nameof( FullDoF ), invert: true )]
    [ClampAboveZeroInInspector]
    public float TensileAttenuation
    {
      get => m_hingeAttenuationTranslational.y;
      set
      {
        m_hingeAttenuationTranslational.y = value;
        Native?.setTensileAttenuation( m_hingeAttenuationTranslational.y );
      }
    }

    [SerializeField]
    [FormerlySerializedAs("m_hingeComplianceTranslational")]
    private Vector3 m_hingeStiffnessTranslational = 1.1E10f * Vector3.one;

    /// <summary>
    /// Compliance for the translational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [1.0E-10, 1.0E-10, 1.0E-10]
    /// </summary>
    [InspectorGroupBegin( Name = "Node Hinge Properties", DefaultExpanded = true )]
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Compliance for the translational degrees of freedom in the hinges between track nodes. \nX: The axis along the thickness of the track.\nY: The axis along the track.\nZ: The axis along the width of the track." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public Vector3 HingeStiffnessTranslational
    {
      get { return m_hingeStiffnessTranslational; }
      set
      {
        m_hingeStiffnessTranslational = value;
        if ( Native != null ) {
          Native.setShearStiffness( m_hingeStiffnessTranslational.x, agxVehicle.TrackProperties.Axis.LATERAL );
          Native.setTensileStiffness( m_hingeStiffnessTranslational.y );
          Native.setShearStiffness( m_hingeStiffnessTranslational.z, agxVehicle.TrackProperties.Axis.VERTICAL );
        }
      }
    }

    [SerializeField]
    [FormerlySerializedAs("m_hingeDampingTranslational")]
    private Vector3 m_hingeAttenuationTranslational = 2 * Vector3.one;

    /// <summary>
    /// Damping for the translational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [0.04, 0.04, 0.04]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Damping for the translational degrees of freedom in the hinges between track nodes. \nX: The axis along the thickness of the track.\nY: The axis along the track.\nZ: The axis along the width of the track." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public Vector3 HingeAttenuationTranslational
    {
      get { return m_hingeAttenuationTranslational; }
      set
      {
        m_hingeAttenuationTranslational = value;
        if ( Native != null ) {
          Native.setShearAttenuation( m_hingeAttenuationTranslational.x, agxVehicle.TrackProperties.Axis.LATERAL );
          Native.setTensileAttenuation( m_hingeAttenuationTranslational.y );
          Native.setShearAttenuation( m_hingeAttenuationTranslational.z, agxVehicle.TrackProperties.Axis.VERTICAL );
        }
      }
    }

    [SerializeField]
    private Vector3 m_hingeStiffnessRotational = new Vector3(1.1E10f, 1.1E10f, 50);

    /// <summary>
    /// Compliance for the rotational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [1.0E-10, 1.0E-10]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Compliance for the rotational degrees of freedom in the hinges between track nodes. \nX: Rotation along the axis orthogonal to the track.\nY: Rotation along the axis of the track." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public Vector3 HingeStiffnessRotational
    {
      get { return m_hingeStiffnessRotational; }
      set
      {
        m_hingeStiffnessRotational = value;
        if ( Native != null ) {
          Native.setBendingStiffness( m_hingeStiffnessRotational.x, agxVehicle.TrackProperties.Axis.VERTICAL );
          Native.setTorsionalStiffness( m_hingeStiffnessRotational.y );
          Native.setBendingStiffness( m_hingeStiffnessRotational.z, agxVehicle.TrackProperties.Axis.LATERAL );
        }
      }
    }

    [SerializeField]
    private Vector3 m_hingeAttenuationRotational = 2.0f * Vector2.one;

    /// <summary>
    /// Damping for the rotational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [0.04, 0.04]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Damping for the rotational degrees of freedom in the hinges between track nodes. \nX: Rotation along the axis orthogonal to the track.\nY: Rotation along the axis of the track." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public Vector3 HingeAttenuationRotational
    {
      get { return m_hingeAttenuationRotational; }
      set
      {
        m_hingeAttenuationRotational = value;
        if ( Native != null ) {
          Native.setBendingAttenuation( m_hingeAttenuationRotational.x, agxVehicle.TrackProperties.Axis.VERTICAL );
          Native.setTorsionalAttenuation( m_hingeAttenuationRotational.y );
          Native.setBendingAttenuation( m_hingeAttenuationRotational.z, agxVehicle.TrackProperties.Axis.LATERAL );
        }
      }
    }

    [SerializeField]
    private bool m_hingeRangeEnabled = true;

    /// <summary>
    /// True to enable the range in the hinges between the
    /// track nodes to define how the track may bend.
    /// Default: Enabled
    /// </summary>
    [Tooltip( "True to enable the 1range in the hinges between the track nodes to define how the track may bend." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public bool HingeRangeEnabled
    {
      get { return m_hingeRangeEnabled; }
      set
      {
        m_hingeRangeEnabled = value;
        if ( Native != null )
          Native.setEnableHingeRange( m_hingeRangeEnabled );
      }
    }

    [SerializeField]
    private RangeReal m_hingeRangeRange = new RangeReal( -120.0f, 20.0f );

    /// <summary>
    /// Range used if the hinge range between the nodes are
    /// enabled - given in degrees.
    /// Default: [-120, 20]
    /// </summary>
    [Tooltip( "Range used if the hinge range between the nodes are enabled - given in degrees." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public RangeReal HingeRangeRange
    {
      get { return m_hingeRangeRange; }
      set
      {
        m_hingeRangeRange = value;
        if ( Native != null )
          Native.setHingeRangeRange( Mathf.Deg2Rad * m_hingeRangeRange.Min,
                                     Mathf.Deg2Rad * m_hingeRangeRange.Max );
      }
    }

    [SerializeField]
    private bool m_onInitializeMergeNodesToWheelsEnabled = false;

    /// <summary>
    /// When the track has been initialized some nodes are in contact with the wheels.
    /// If this flag is true the interacting nodes will be merged to the wheel directly
    /// after initialize, if false the nodes will be merged during the first (or later)
    /// time step.
    /// Default: Disabled
    /// </summary>
    [InspectorGroupBegin( Name = "Merge/Split Properties", DefaultExpanded = true )]
    [Tooltip( "When the track has been initialized some nodes are in contact with the wheels. If this flag is true the interacting nodes will be merged to the wheel directly after initialize, if false the nodes will be merged during the first (or later) time step." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public bool OnInitializeMergeNodesToWheelsEnabled
    {
      get { return m_onInitializeMergeNodesToWheelsEnabled; }
      set
      {
        m_onInitializeMergeNodesToWheelsEnabled = value;
        if ( Native != null )
          Native.setEnableOnInitializeMergeNodesToWheels( m_onInitializeMergeNodesToWheelsEnabled );
      }
    }

    [SerializeField]
    private bool m_onInitializeTransformNodesToWheelsEnabled = true;

    /// <summary>
    /// True to position/transform the track nodes to the surface of the wheels after
    /// the track has been initialized.When false, the routing algorithm positions
    /// are used.
    /// Default: Enabled
    /// </summary>
    [Tooltip( "True to position/transform the track nodes to the surface of the wheels after the track has been initialized.When false, the routing algorithm positions are used." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public bool OnInitializeTransformNodesToWheelsEnabled
    {
      get { return m_onInitializeTransformNodesToWheelsEnabled; }
      set
      {
        m_onInitializeTransformNodesToWheelsEnabled = value;
        if ( Native != null )
          Native.setEnableOnInitializeTransformNodesToWheels( m_onInitializeTransformNodesToWheelsEnabled );
      }
    }

    [SerializeField]
    private float m_transformNodesToWheelsOverlap = 1.0E-3f;

    /// <summary>
    /// When the nodes are transformed to the wheels, this is the final target overlap.
    /// Default: 1.0E-3
    /// </summary>
    [Tooltip( "When the nodes are transformed to the wheels, this is the final target overlap" )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public float TransformNodesToWheelsOverlap
    {
      get { return m_transformNodesToWheelsOverlap; }
      set
      {
        m_transformNodesToWheelsOverlap = value;
        if ( Native != null )
          Native.setTransformNodesToWheelsOverlap( m_transformNodesToWheelsOverlap );
      }
    }

    [SerializeField]
    private float m_nodesToWheelsMergeThreshold = -0.1f;

    /// <summary>
    /// Threshold when to merge a node to a wheel. Given a reference direction in the
    /// track, this value is the projection of the deviation (from the reference direction)
    /// of the node direction onto the wheel radial direction vector. I.e., when the
    /// projection is negative the node can be considered "wrapped" on the wheel.
    /// Default: -0.1
    /// </summary>
    [Tooltip( "Threshold when to merge a node to a wheel. Given a reference direction in the track, this value is the projection of the deviation (from the reference direction) of the node direction onto the wheel radial direction vector. I.e., when the projection is negative the node can be considered \"wrapped\" on the wheel." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public float NodesToWheelsMergeThreshold
    {
      get { return m_nodesToWheelsMergeThreshold; }
      set
      {
        m_nodesToWheelsMergeThreshold = value;
        if ( Native != null )
          Native.setNodesToWheelsMergeThreshold( m_nodesToWheelsMergeThreshold );
      }
    }

    [SerializeField]
    private float m_nodesToWheelsSplitThreshold = -0.05f;

    /// <summary>
    /// Threshold when to split a node from a wheel. Given a reference direction in the
    /// track, this value is the projection of the deviation (from the reference direction)
    /// of the node direction onto the wheel radial direction vector. I.e., when the
    /// projection is negative the node can be considered "wrapped" on the wheel.
    /// Default: -0.05
    /// </summary>
    [Tooltip( "Threshold when to split a node from a wheel. Given a reference direction in the track, this value is the projection of the deviation (from the reference direction) of the node direction onto the wheel radial direction vector. I.e., when the projection is negative the node can be considered \"wrapped\" on the wheel." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public float NodesToWheelsSplitThreshold
    {
      get { return m_nodesToWheelsSplitThreshold; }
      set
      {
        m_nodesToWheelsSplitThreshold = value;
        if ( Native != null )
          Native.setNodesToWheelsSplitThreshold( m_nodesToWheelsSplitThreshold );
      }
    }

    [SerializeField]
    private int m_numNodesIncludedInAverageDirection = 3;

    /// <summary>
    /// Average direction of non-merged nodes entering or exiting a wheel is used as
    /// reference direction to split of a merged node.This is the number of nodes to
    /// include into this average direction.
    /// Default: 3
    /// </summary>
    [Tooltip( "Average direction of non-merged nodes entering or exiting a wheel is used as reference direction to split of a merged node. This is the number of nodes to include into this average direction." )]
    [ClampAboveZeroInInspector( true )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public int NumNodesIncludedInAverageDirection
    {
      get { return m_numNodesIncludedInAverageDirection; }
      set
      {
        m_numNodesIncludedInAverageDirection = value;
        if ( Native != null )
          Native.setNumNodesIncludedInAverageDirection( (ulong)m_numNodesIncludedInAverageDirection );
      }
    }

    [SerializeField]
    private float m_minStabilizingHingeNormalForce = 100.0f;

    /// <summary>
    /// Minimum value of the normal force (the hinge force along the track) used in "internal"
    /// friction calculations.I.e., when the track is compressed, this value is used with
    /// the friction coefficient as a minimum stabilizing compliance. If this value is negative
    /// there will be stabilization when the track is compressed.
    /// Default: 100.0
    /// </summary>
    [InspectorGroupBegin( Name = "Stabilizing Properties" )]
    [Tooltip( "Minimum value of the normal force (the hinge force along the track) used in \"internal\" friction calculations.I.e., when the track is compressed, this value is used with the friction coefficient as a minimum stabilizing compliance. If this value is negative there will be stabilization when the track is compressed." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public float MinStabilizingHingeNormalForce
    {
      get { return m_minStabilizingHingeNormalForce; }
      set
      {
        m_minStabilizingHingeNormalForce = value;
        if ( Native != null )
          Native.setMinStabilizingHingeNormalForce( m_minStabilizingHingeNormalForce );
      }
    }

    [SerializeField]
    private float m_stabilizingHingeFrictionParameter = 1.0f;

    /// <summary>
    /// Friction parameter of the internal friction in the node hinges. This parameter scales
    /// the normal force in the hinge.
    /// Default: 1.0
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Friction parameter of the internal friction in the node hinges. This parameter scales the normal force in the hinge." )]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public float StabilizingHingeFrictionParameter
    {
      get { return m_stabilizingHingeFrictionParameter; }
      set
      {
        m_stabilizingHingeFrictionParameter = value;
        if ( Native != null )
          Native.setStabilizingHingeFrictionParameter( m_stabilizingHingeFrictionParameter );
      }
    }

    public override void Destroy()
    {
      Native = null;
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      Native = new agxVehicle.TrackProperties();

      return true;
    }
  }
}
