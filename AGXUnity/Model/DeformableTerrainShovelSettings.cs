﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  public class DeformableTerrainShovelSettings : ScriptAsset
  {
    [SerializeField]
    private int m_numberOfTeeth = 6;
    [ClampAboveZeroInInspector]
    public int NumberOfTeeth
    {
      get { return m_numberOfTeeth; }
      set
      {
        m_numberOfTeeth = value;
        Propagate( shovel => shovel.setNumberOfTeeth( Convert.ToUInt64( m_numberOfTeeth ) ) );
      }
    }

    [SerializeField]
    private float m_toothLength = 0.15f;
    public float ToothLength
    {
      get { return m_toothLength; }
      set
      {
        m_toothLength = value;
        Propagate( shovel => shovel.setToothLength( m_toothLength ) );
      }
    }

    [SerializeField]
    private RangeReal m_toothRadius = new RangeReal( 0.015f, 0.075f );
    public RangeReal ToothRadius
    {
      get { return m_toothRadius; }
      set
      {
        m_toothRadius = value;
        Propagate( shovel =>
        {
          shovel.setToothMinimumRadius( m_toothRadius.Min );
          shovel.setToothMaximumRadius( m_toothRadius.Max );
        } );
      }
    }

    [SerializeField]
    private float m_noMergeExtensionDistance = 0.5f;
    [ClampAboveZeroInInspector( true )]
    public float NoMergeExtensionDistance
    {
      get { return m_noMergeExtensionDistance; }
      set
      {
        m_noMergeExtensionDistance = value;
        Propagate( shovel => shovel.setNoMergeExtensionDistance( m_noMergeExtensionDistance ) );
      }
    }

    [SerializeField]
    private float m_minimumSubmergedContactLengthFraction = 0.5f;
    [FloatSliderInInspector( 0.0f, 1.0f )]
    public float MinimumSubmergedContactLengthFraction
    {
      get { return m_minimumSubmergedContactLengthFraction; }
      set
      {
        m_minimumSubmergedContactLengthFraction = value;
        Propagate( shovel => shovel.setMinimumSubmergedContactLengthFraction( m_minimumSubmergedContactLengthFraction ) );
      }
    }

    [SerializeField]
    private float m_verticalBladeSoilMergeDistance = 0.0f;
    [ClampAboveZeroInInspector( true )]
    public float VerticalBladeSoilMergeDistance
    {
      get { return m_verticalBladeSoilMergeDistance; }
      set
      {
        m_verticalBladeSoilMergeDistance = value;
        Propagate( shovel => shovel.setVerticalBladeSoilMergeDistance( m_verticalBladeSoilMergeDistance ) );
      }
    }

    [SerializeField]
    private float m_secondarySeparationDeadloadLimit = 0.8f;
    [ClampAboveZeroInInspector( true )]
    public float SecondarySeparationDeadloadLimit
    {
      get { return m_secondarySeparationDeadloadLimit; }
      set
      {
        m_secondarySeparationDeadloadLimit = value;
        Propagate( shovel => shovel.setSecondarySeparationDeadloadLimit( m_secondarySeparationDeadloadLimit ) );
      }
    }

    [SerializeField]
    private float m_penetrationDepthThreshold = 0.2f;
    [ClampAboveZeroInInspector( true )]
    public float PenetrationDepthThreshold
    {
      get { return m_penetrationDepthThreshold; }
      set
      {
        m_penetrationDepthThreshold = value;
        Propagate( shovel => shovel.setPenetrationDepthThreshold( m_penetrationDepthThreshold ) );
      }
    }

    [SerializeField]
    private float m_penetrationForceScaling = 1.0f;
    [ClampAboveZeroInInspector( true )]
    public float PenetrationForceScaling
    {
      get { return m_penetrationForceScaling; }
      set
      {
        m_penetrationForceScaling = value;
        Propagate( shovel => shovel.setPenetrationForceScaling( m_penetrationForceScaling ) );
      }
    }

    [SerializeField]
    private float m_maxPenetrationForce = float.PositiveInfinity;
    [ClampAboveZeroInInspector( true )]
    public float MaxPenetrationForce
    {
      get { return m_maxPenetrationForce; }
      set
      {
        m_maxPenetrationForce = value;
        Propagate( shovel => shovel.setMaxPenetrationForce( m_maxPenetrationForce ) );
      }
    }

    [SerializeField]
    private bool m_removeContacts = false;
    public bool RemoveContacts
    {
      get { return m_removeContacts; }
      set
      {
        m_removeContacts = value;
        Propagate( shovel => shovel.setAlwaysRemoveShovelContacts( m_removeContacts ) );
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
    /// Register as listener of these settings. Current settings will
    /// be applied to the shovel instance directly when added.
    /// </summary>
    /// <param name="shovel">Deformable shovel instance to which these settings should apply.</param>
    public void Register( DeformableTerrainShovel shovel )
    {
      if ( !m_shovels.Contains( shovel ) ) {
        m_shovels.Add( shovel );

        // Synchronizing settings for all shovels. Could be
        // avoided by adding a state so that Propagate only
        // shows current added shovel.
        Utils.PropertySynchronizer.Synchronize( this );
      }
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
      return true;
    }

    private void Propagate( Action<agxTerrain.Shovel> action )
    {
      if ( action == null )
        return;

      foreach ( var shovel in m_shovels )
        if ( shovel.Native != null )
          action( shovel.Native );
    }

    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();
  }
}
