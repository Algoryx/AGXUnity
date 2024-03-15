using System;
using UnityEngine;

namespace AGXUnity
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#constraint-merge-split-thresholds" )]
  public class ConstraintMergeSplitThresholds : MergeSplitThresholds<ConstraintMergeSplitThresholds>
  {
    [SerializeField]
    private float m_maxRelativeSpeed = 5.0E-3f;

    [ClampAboveZeroInInspector( true )]
    [InspectorGroupBegin(Name = "Merge conditions", DefaultExpanded = true)]
    [Tooltip( "When the relative motion between the objects is less than this threshold, the objects may merge." )]
    public float MaxRelativeSpeed
    {
      get { return m_maxRelativeSpeed; }
      set
      {
        m_maxRelativeSpeed = value;
        if ( Native != null )
          Native.setMaxRelativeSpeed( value );
      }
    }

    [SerializeField]
    private float m_maxDesiredSpeedDiff = 1.0E-5f;

    [ClampAboveZeroInInspector( true )]
    [InspectorGroupBegin(Name = "Split conditions", DefaultExpanded = true)]
    [Tooltip( "When the relative motion between the objects is less than this threshold, the objects may merge." )]
    public float MaxDesiredSpeedDiff
    {
      get { return m_maxDesiredSpeedDiff; }
      set
      {
        m_maxDesiredSpeedDiff = value;
        if ( Native != null )
          Native.setMaxDesiredSpeedDiff( value );
      }
    }

    [SerializeField]
    private float m_maxDesiredLockAngleDiff = 1.0E-5f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The maximum difference the 'force range'/desired force range parameter in any controller may change without splitting the constrained objects. " )]
    public float MaxDesiredLockAngleDiff
    {
      get { return m_maxDesiredLockAngleDiff; }
      set
      {
        m_maxDesiredLockAngleDiff = value;
        if ( Native != null )
          Native.setMaxDesiredLockAngleDiff( value );
      }
    }

    [SerializeField]
    private float m_maxDesiredRangeAngleDiff = 1.0E-5f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The maximum difference the 'position'/desired angle parameter in a lock controller may change without splitting the constrained objects. " )]
    public float MaxDesiredRangeAngleDiff
    {
      get { return m_maxDesiredRangeAngleDiff; }
      set
      {
        m_maxDesiredRangeAngleDiff = value;
        if ( Native != null )
          Native.setMaxDesiredRangeAngleDiff( value );
      }
    }

    [SerializeField]
    private float m_maxDesiredForceRangeDiff = 1.0E-1f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The maximum difference the 'force range'/desired force range parameter in any controller may change without splitting the constrained objects. " )]
    public float MaxDesiredForceRangeDiff
    {
      get { return m_maxDesiredForceRangeDiff; }
      set
      {
        m_maxDesiredForceRangeDiff = value;
        if ( Native != null )
          Native.setMaxDesiredForceRangeDiff( value );
      }
    }

    public agxSDK.ConstraintMergeSplitThresholds Native { get; private set; }

    public override void ResetToDefault()
    {
      using ( var native = new agxSDK.ConstraintMergeSplitThresholds() ) {
        MaxRelativeSpeed         = Convert.ToSingle( native.getMaxRelativeSpeed() );
        MaxDesiredSpeedDiff      = Convert.ToSingle( native.getMaxDesiredSpeedDiff() );
        MaxDesiredLockAngleDiff  = Convert.ToSingle( native.getMaxDesiredLockAngleDiff() );
        MaxDesiredRangeAngleDiff = Convert.ToSingle( native.getMaxDesiredRangeAngleDiff() );
        MaxDesiredForceRangeDiff = Convert.ToSingle( native.getMaxDesiredForceRangeDiff() );
      }
    }

    public override void Destroy()
    {
      Native = null;

      base.Destroy();
    }

    protected override void Construct()
    {
      ResetToDefault();
    }

    protected override bool Initialize()
    {
      Native = new agxSDK.ConstraintMergeSplitThresholds();

      return base.Initialize();
    }
  }
}
