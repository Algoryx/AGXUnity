using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#track-properties" )]
  public class TrackProperties : ScriptAsset
  {
    public agxVehicle.TrackProperties Native { get; private set; }

    [SerializeField]
    private Vector3 m_hingeComplianceTranslational = 1.0E-10f * Vector3.one;

    /// <summary>
    /// Compliance for the translational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [1.0E-10, 1.0E-10, 1.0E-10]
    /// </summary>
    [InspectorGroupBegin( Name = "Node Hinge Properties" )]
    [ClampAboveZeroInInspector( true )]
    public Vector3 HingeComplianceTranslational
    {
      get { return m_hingeComplianceTranslational; }
      set
      {
        m_hingeComplianceTranslational = value;
        if ( Native != null ) {
          Native.setHingeCompliance( m_hingeComplianceTranslational.x, agx.Hinge.DOF.TRANSLATIONAL_1 );
          Native.setHingeCompliance( m_hingeComplianceTranslational.y, agx.Hinge.DOF.TRANSLATIONAL_2 );
          Native.setHingeCompliance( m_hingeComplianceTranslational.z, agx.Hinge.DOF.TRANSLATIONAL_3 );
        }
      }
    }

    [SerializeField]
    private Vector3 m_hingeDampingTranslational = 0.04f * Vector3.one;

    /// <summary>
    /// Damping for the translational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [0.04, 0.04, 0.04]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector3 HingeDampingTranslational
    {
      get { return m_hingeDampingTranslational; }
      set
      {
        m_hingeDampingTranslational = value;
        if ( Native != null ) {
          Native.setHingeDamping( m_hingeDampingTranslational.x, agx.Hinge.DOF.TRANSLATIONAL_1 );
          Native.setHingeDamping( m_hingeDampingTranslational.y, agx.Hinge.DOF.TRANSLATIONAL_2 );
          Native.setHingeDamping( m_hingeDampingTranslational.z, agx.Hinge.DOF.TRANSLATIONAL_3 );
        }
      }
    }

    [SerializeField]
    private Vector2 m_hingeComplianceRotational = 1.0E-10f * Vector2.one;

    /// <summary>
    /// Compliance for the rotational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [1.0E-10, 1.0E-10]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector2 HingeComplianceRotational
    {
      get { return m_hingeComplianceRotational; }
      set
      {
        m_hingeComplianceRotational = value;
        if ( Native != null ) {
          Native.setHingeCompliance( m_hingeComplianceRotational.x, agx.Hinge.DOF.ROTATIONAL_1 );
          Native.setHingeCompliance( m_hingeComplianceRotational.y, agx.Hinge.DOF.ROTATIONAL_2 );
        }
      }
    }

    [SerializeField]
    private Vector2 m_hingeDampingRotational = 0.04f * Vector2.one;

    /// <summary>
    /// Damping for the rotational degrees of freedom in the
    /// hinges between track nodes.
    /// Default: [0.04, 0.04]
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector2 HingeDampingRotational
    {
      get { return m_hingeDampingRotational; }
      set
      {
        m_hingeDampingRotational = value;
        if ( Native != null ) {
          Native.setHingeDamping( m_hingeDampingRotational.x, agx.Hinge.DOF.ROTATIONAL_1 );
          Native.setHingeDamping( m_hingeDampingRotational.y, agx.Hinge.DOF.ROTATIONAL_2 );
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
    [InspectorGroupBegin( Name = "Merge/Split Properties" )]
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
    [ClampAboveZeroInInspector( true )]
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
