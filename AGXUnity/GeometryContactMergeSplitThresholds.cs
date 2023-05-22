using System;
using UnityEngine;

namespace AGXUnity
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#geometry-contact-merge-split-thresholds" )]
  public class GeometryContactMergeSplitThresholds : MergeSplitThresholds
  {
    [HideInInspector]
    public static string ResourcePath { get { return ResourceDirectory + @"/DefaultGeometryContactThresholds"; } }

    [HideInInspector]
    public static GeometryContactMergeSplitThresholds DefaultResource { get { return Resources.Load<GeometryContactMergeSplitThresholds>( ResourcePath ); } }

    [SerializeField]
    private float m_maxRelativeNormalSpeed = 0.01f;

    [ClampAboveZeroInInspector( true )]
    public float MaxRelativeNormalSpeed
    {
      get { return m_maxRelativeNormalSpeed; }
      set
      {
        m_maxRelativeNormalSpeed = value;
        if ( Native != null )
          Native.setMaxRelativeNormalSpeed( value );
      }
    }

    [SerializeField]
    private float m_maxRelativeTangentSpeed = 0.01f;

    [ClampAboveZeroInInspector( true )]
    public float MaxRelativeTangentSpeed
    {
      get { return m_maxRelativeTangentSpeed; }
      set
      {
        m_maxRelativeTangentSpeed = value;
        if ( Native != null )
          Native.setMaxRelativeTangentSpeed( value );
      }
    }

    [SerializeField]
    private float m_maxRollingSpeed = 0.01f;

    [ClampAboveZeroInInspector( true )]
    public float MaxRollingSpeed
    {
      get { return m_maxRollingSpeed; }
      set
      {
        m_maxRollingSpeed = value;
        if ( Native != null )
          Native.setMaxRollingSpeed( value );
      }
    }

    [SerializeField]
    private float m_maxImpactSpeed = 0.01f;

    [ClampAboveZeroInInspector( true )]
    public float MaxImpactSpeed
    {
      get { return m_maxImpactSpeed; }
      set
      {
        m_maxImpactSpeed = value;
        if ( Native != null )
          Native.setMaxImpactSpeed( value );
      }
    }

    [SerializeField]
    private bool m_splitOnLogicalImpact = false;

    public bool SplitOnLogicalImpact
    {
      get { return m_splitOnLogicalImpact; }
      set
      {
        m_splitOnLogicalImpact = value;
        if ( Native != null )
          Native.setSplitOnLogicalImpact( value );
      }
    }

    public agxSDK.GeometryContactMergeSplitThresholds Native { get; private set; }

    public override void ResetToDefault()
    {
      using ( var native = new agxSDK.GeometryContactMergeSplitThresholds() ) {
        MaxRelativeNormalSpeed  = Convert.ToSingle( native.getMaxRelativeNormalSpeed() );
        MaxRelativeTangentSpeed = Convert.ToSingle( native.getMaxRelativeTangentSpeed() );
        MaxRollingSpeed         = Convert.ToSingle( native.getMaxRollingSpeed() );
        MaxImpactSpeed          = Convert.ToSingle( native.getMaxImpactSpeed() );
        SplitOnLogicalImpact    = native.getSplitOnLogicalImpact();
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
      Native = new agxSDK.GeometryContactMergeSplitThresholds();

      return base.Initialize();
    }
  }
}
