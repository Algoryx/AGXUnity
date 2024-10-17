using System;
using UnityEngine;

namespace AGXUnity
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#geometry-contact-merge-split-thresholds" )]
  public class GeometryContactMergeSplitThresholds : MergeSplitThresholds<GeometryContactMergeSplitThresholds>
  {
    [SerializeField]
    private float m_maxRelativeNormalSpeed = 0.01f;

    [InspectorGroupBegin( Name = "Merge conditions", DefaultExpanded = true )]
    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Maximum speed along a contact normal for a contact to be considered resting." )]
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
    [Tooltip( "Maximum (sliding) speed along a contact tangent for a contact to be considered resting." )]
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
    [Tooltip( "Maximum rolling speed for a contact to be considered resting. " )]
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
    [InspectorGroupBegin( Name = "Split conditions", DefaultExpanded = true )]
    [Tooltip( "Maximum impact speed (along a contact normal) a merged object can resist without being split. " )]
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
    private float m_normalAdhesion = 0.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Adhesive force in the normal directions preventing the object to split (if > 0) when the object is subject to external interactions (e.g., constraints)." )]
    public float NormalAdhesion
    {
      get => m_normalAdhesion;
      set
      {
        m_normalAdhesion = value;
        if ( Native != null )
          Native.setNormalAdhesion( value );
      }
    }

    [SerializeField]
    private float m_tangentialAdhesion = 0.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "Adhesive force in the tangential directions preventing the object to split (if > 0) when the object is subject to external interactions (e.g., constraints)." )]
    public float TangentialAdhesion
    {
      get => m_tangentialAdhesion;
      set
      {
        m_tangentialAdhesion = value;
        if ( Native != null )
          Native.setTangentialAdhesion( value );
      }
    }

    [SerializeField]
    private bool m_splitOnLogicalImpact = false;

    [Tooltip( "If true, merged bodies will split when objects first collide, else the \"Max Impact speed\" will be used." )]
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

    [SerializeField]
    private bool m_maySplitInGravityField = false;

    [Tooltip( "If true, check split given external forces for all objects merged (i.e., rb->getForce() the sum of rb->addForce(), including the gravity force). " )]
    public bool MaySplitInGravityField
    {
      get => m_maySplitInGravityField;
      set
      {
        m_maySplitInGravityField = value;
        if ( Native != null )
          Native.setMaySplitInGravityField( value );
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
