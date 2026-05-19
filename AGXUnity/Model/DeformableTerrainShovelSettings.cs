using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain-shovel-settings" )]
  public class DeformableTerrainShovelSettings : ScriptAsset
  {
    [SerializeField]
    private int m_numberOfTeeth = 6;
    [ClampAboveZeroInInspector( acceptZero: true )]
    [Tooltip( "The number of teeth that the shovel has" )]
    public int NumberOfTeeth
    {
      get { return m_numberOfTeeth; }
      set
      {
        m_numberOfTeeth = value;
        Propagate( shovel => shovel.getSettings().setNumberOfTeeth( Convert.ToUInt64( m_numberOfTeeth ) ) );
      }
    }

    [SerializeField]
    private bool m_enableExcavationAtTeethEdge = false;

    [Tooltip( "Whether or not to perform excavation at the teeth edge rather than the cutting edge" )]
    public bool EnableExcavationAtTeethEdge
    {
      get => m_enableExcavationAtTeethEdge;
      set
      {
        m_enableExcavationAtTeethEdge = value;
        Propagate( shovel => shovel.getSettings().setEnableExcavationAtTeethEdge( m_enableExcavationAtTeethEdge ) );
      }
    }

    [SerializeField]
    private float m_toothLength = 0.15f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The length of the shovel's teeth" )]
    public float ToothLength
    {
      get { return m_toothLength; }
      set
      {
        m_toothLength = value;
        Propagate( shovel => shovel.getSettings().setToothLength( m_toothLength ) );
      }
    }

    [SerializeField]
    private RangeReal m_toothRadius = new RangeReal( 0.015f, 0.075f );

    [ClampAboveZeroInInspector]
    [Tooltip( "The radius of the shovel's teeth" )]
    public RangeReal ToothRadius
    {
      get { return m_toothRadius; }
      set
      {
        m_toothRadius = value;
        Propagate( shovel => {
          shovel.getSettings().setToothMinimumRadius( m_toothRadius.Min );
          shovel.getSettings().setToothMaximumRadius( m_toothRadius.Max );
        } );
      }
    }

    [SerializeField]
    private float m_verticalBladeSoilMergeDistance = 0.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The vertical distance under the blade cutting edge that the soil is allowed to instantly merge up to. " )]
    public float VerticalBladeSoilMergeDistance
    {
      get { return m_verticalBladeSoilMergeDistance; }
      set
      {
        m_verticalBladeSoilMergeDistance = value;
        Propagate( shovel => shovel.getSettings().setVerticalBladeSoilMergeDistance( m_verticalBladeSoilMergeDistance ) );
      }
    }

    [SerializeField]
    private float m_penetrationDepthThreshold = 0.2f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The vertical penetration depth threshold for when the shovel tooth for penetration resistance should reach full effectiveness. The penetration depth is defined as the vertical distance between the tip of a shovel tooth and the surface position of the height field. The penetration resistance will increase from a baseline of 10% until maximum effectiveness is reached when the vertical penetration depth of the shovel reaches the specified value." )]
    public float PenetrationDepthThreshold
    {
      get { return m_penetrationDepthThreshold; }
      set
      {
        m_penetrationDepthThreshold = value;
        Propagate( shovel => shovel.getSettings().setPenetrationDepthThreshold( m_penetrationDepthThreshold ) );
      }
    }

    [SerializeField]
    private float m_penetrationForceScaling = 1.0f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The coefficient for scaling the penetration force that the terrain will give on this shovel." )]
    public float PenetrationForceScaling
    {
      get { return m_penetrationForceScaling; }
      set
      {
        m_penetrationForceScaling = value;
        Propagate( shovel => shovel.getSettings().setPenetrationForceScaling( m_penetrationForceScaling ) );
      }
    }

    [SerializeField]
    private float m_maxPenetrationForce = float.PositiveInfinity;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The maximum limit on penetration force (N) that the terrain will generate on this shovel. " )]
    public float MaxPenetrationForce
    {
      get { return m_maxPenetrationForce; }
      set
      {
        m_maxPenetrationForce = value;
        Propagate( shovel => shovel.getSettings().setMaxPenetrationForce( m_maxPenetrationForce ) );
      }
    }

    [SerializeField]
    private float m_noMergeExtensionDistance = 0.5f;

    [ClampAboveZeroInInspector( true )]
    [InspectorGroupBegin( Name = "Advanced" )]
    [Tooltip( "The margin outside the shovel bonding box where soil particle merging is forbidden. " )]
    public float NoMergeExtensionDistance
    {
      get { return m_noMergeExtensionDistance; }
      set
      {
        m_noMergeExtensionDistance = value;
        Propagate( shovel => shovel.getAdvancedSettings().setNoMergeExtensionDistance( m_noMergeExtensionDistance ) );
      }
    }

    [SerializeField]
    private float m_minimumSubmergedContactLengthFraction = 0.5f;

    [FloatSliderInInspector( 0.0f, 1.0f )]
    [Tooltip( "The minimum submerged cutting edge length fraction that generates submerged cutting. " )]
    public float MinimumSubmergedContactLengthFraction
    {
      get { return m_minimumSubmergedContactLengthFraction; }
      set
      {
        m_minimumSubmergedContactLengthFraction = value;
        Propagate( shovel => shovel.getAdvancedSettings().setMinimumSubmergedContactLengthFraction( m_minimumSubmergedContactLengthFraction ) );
      }
    }

    [SerializeField]
    private float m_secondarySeparationDeadloadLimit = 0.8f;

    [ClampAboveZeroInInspector( true )]
    [Tooltip( "The dead-load limit where secondary separation will start to active where the forward direction starts to change according to the virtual separation plate created by the material inside the shovel " )]
    public float SecondarySeparationDeadloadLimit
    {
      get { return m_secondarySeparationDeadloadLimit; }
      set
      {
        m_secondarySeparationDeadloadLimit = value;
        Propagate( shovel => shovel.getAdvancedSettings().setSecondarySeparationDeadloadLimit( m_secondarySeparationDeadloadLimit ) );
      }
    }

    [SerializeField]
    private OptionalOverrideValue<float> m_contactRegionThreshold = new OptionalOverrideValue<float>(0.02f);

    [Tooltip( "Set the starting distance threshold from the shovel planes where regular geometry contacts between the shovel underside and the terrain can be created. Contacts that are not past the distance threshold will be filtered away" )]
    public OptionalOverrideValue<float> ContactRegionThreshold => m_contactRegionThreshold;

    [SerializeField]
    private bool m_removeContacts = false;

    [Tooltip( "Whether shovel <-> terrain contacts should always be removed." )]
    public bool RemoveContacts
    {
      get { return m_removeContacts; }
      set
      {
        m_removeContacts = value;
        Propagate( shovel => shovel.getAdvancedSettings().setAlwaysRemoveShovelContacts( m_removeContacts ) );
      }
    }

    [Serializable]
    public struct ExcavationSettings
    {
      public bool Enabled;
      public bool CreateDynamicMassEnabled;
      public bool ForceFeedbackEnabled;

      public void SetEnabled( bool enabled )
      {
        Enabled = enabled;
      }

      public void SetCreateDynamicMassEnabled( bool enabled )
      {
        CreateDynamicMassEnabled = enabled;
      }

      public void SetForceFeedbackEnabled( bool enabled )
      {
        ForceFeedbackEnabled = enabled;
      }

      public agxTerrain.Shovel.ExcavationSettings ToNative()
      {
        var native = new agxTerrain.Shovel.ExcavationSettings();
        native.setEnable( Enabled );
        native.setEnableCreateDynamicMass( CreateDynamicMassEnabled );
        native.setEnableForceFeedback( ForceFeedbackEnabled );
        return native;
      }
    }

    [SerializeField]
    private ExcavationSettings m_primaryExcavationSettings = new ExcavationSettings()
    {
      Enabled = true,
      CreateDynamicMassEnabled = true,
      ForceFeedbackEnabled = true
    };

    [InspectorSeparator]
    [InspectorGroupEnd]
    public ExcavationSettings PrimaryExcavationSettings
    {
      get { return m_primaryExcavationSettings; }
      set
      {
        m_primaryExcavationSettings = value;
        Propagate( shovel => shovel.setExcavationSettings( agxTerrain.Shovel.ExcavationMode.PRIMARY, m_primaryExcavationSettings.ToNative() ) );
      }
    }

    [SerializeField]
    private ExcavationSettings m_deformBackExcavationSettings = new ExcavationSettings()
    {
      Enabled = true,
      CreateDynamicMassEnabled = true,
      ForceFeedbackEnabled = true
    };

    public ExcavationSettings DeformBackExcavationSettings
    {
      get { return m_deformBackExcavationSettings; }
      set
      {
        m_deformBackExcavationSettings = value;
        Propagate( shovel => shovel.setExcavationSettings( agxTerrain.Shovel.ExcavationMode.DEFORM_BACK, m_deformBackExcavationSettings.ToNative() ) );
      }
    }

    [SerializeField]
    private ExcavationSettings m_deformRightExcavationSettings = new ExcavationSettings()
    {
      Enabled = true,
      CreateDynamicMassEnabled = true,
      ForceFeedbackEnabled = true
    };

    public ExcavationSettings DeformRightExcavationSettings
    {
      get { return m_deformRightExcavationSettings; }
      set
      {
        m_deformRightExcavationSettings = value;
        Propagate( shovel => shovel.setExcavationSettings( agxTerrain.Shovel.ExcavationMode.DEFORM_RIGHT, m_deformRightExcavationSettings.ToNative() ) );
      }
    }

    [SerializeField]
    private ExcavationSettings m_deformLeftExcavationSettings = new ExcavationSettings()
    {
      Enabled = true,
      CreateDynamicMassEnabled = true,
      ForceFeedbackEnabled = true
    };

    public ExcavationSettings DeformLeftExcavationSettings
    {
      get { return m_deformLeftExcavationSettings; }
      set
      {
        m_deformLeftExcavationSettings = value;
        Propagate( shovel => shovel.setExcavationSettings( agxTerrain.Shovel.ExcavationMode.DEFORM_LEFT, m_deformLeftExcavationSettings.ToNative() ) );
      }
    }

    /// <summary>
    /// Explicit synchronization of all properties to the given
    /// terrain shovel instance.
    /// </summary>
    /// <remarks>
    /// This call wont have any effect unless the native instance
    /// of the shovel has been created.
    /// </remarks>
    /// <param name="shovel">Terrain shovel instance to synchronize.</param>
    public void Synchronize( DeformableTerrainShovel shovel )
    {
      try {
        m_singleSynchronizeInstance = shovel;
        Utils.PropertySynchronizer.Synchronize( this );
      }
      finally {
        m_singleSynchronizeInstance = null;
      }
    }

    /// <summary>
    /// Register as listener of these settings. Current settings will
    /// be applied to the shovel instance directly when added.
    /// </summary>
    /// <param name="shovel">Deformable shovel instance to which these settings should apply.</param>
    public void Register( DeformableTerrainShovel shovel )
    {
      if ( !m_shovels.Contains( shovel ) )
        m_shovels.Add( shovel );

      Synchronize( shovel );
    }

    /// <summary>
    /// Unregister as listener of these settings.
    /// </summary>
    /// <param name="shovel"></param>
    public void Unregister( DeformableTerrainShovel shovel )
    {
      m_shovels.Remove( shovel );
    }

    public override void Destroy()
    {
      m_shovels.Clear();
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      m_contactRegionThreshold.OnOverrideValue += OnBottomContactThresholdOverrideValue;
      m_contactRegionThreshold.OnUseOverrideToggle += OnBottomContactThresholdUseOverrideToggle;
      return true;
    }

    private void OnBottomContactThresholdOverrideValue( float newValue )
    {
      if ( m_contactRegionThreshold.UseOverride )
        Propagate( shovel => shovel.getAdvancedSettings().setContactRegionThreshold( newValue ) );
    }

    private void OnBottomContactThresholdUseOverrideToggle( bool newValue )
    {
      if ( newValue )
        Propagate( shovel => shovel.getAdvancedSettings().setContactRegionThreshold( m_contactRegionThreshold.OverrideValue ) );
      //else
      //  Propagate( shovel => shovel.getAdvancedSettings().setContactRegionThreshold( shovel.getAdvancedSettings().computeDefaultContactRegionThreshold() ) );
    }

    private void Propagate( Action<agxTerrain.Shovel> action )
    {
      if ( action == null )
        return;

      if ( m_singleSynchronizeInstance != null ) {
        if ( m_singleSynchronizeInstance.Native != null )
          action( m_singleSynchronizeInstance.Native );
        return;
      }

      foreach ( var shovel in m_shovels )
        if ( shovel.Native != null )
          action( shovel.Native );
    }

    [NonSerialized]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    [NonSerialized]
    private DeformableTerrainShovel m_singleSynchronizeInstance = null;
  }
}
