using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  public class TrackInternalMergeProperties : ScriptAsset
  {
    /// <summary>
    /// Contact reduction of merged nodes against other objects.
    /// </summary>
    public enum ContactReductionMode
    {
      None,
      Minimal,
      Moderate,
      Aggressive
    }

    /// <summary>
    /// Convert ContactReductionMode to agxVehicle.TrackInternalMergeProperties.ContactReduction.
    /// </summary>
    public static agxVehicle.TrackInternalMergeProperties.ContactReduction ToNative( ContactReductionMode mode )
    {
      return (agxVehicle.TrackInternalMergeProperties.ContactReduction)(int)mode;
    }

    [SerializeField]
    private bool m_mergeEnabled = false;

    /// <summary>
    /// Enable/disable merge of nodes to segments in the track.
    /// Default: Disabled
    /// </summary>
    public bool MergeEnabled
    {
      get { return m_mergeEnabled; }
      set
      {
        m_mergeEnabled = value;
        Propagate( properties => properties.setEnableMerge( m_mergeEnabled ) );
      }
    }

    [SerializeField]
    private int m_numNodesPerMergeSegment = 3;

    /// <summary>
    /// Number of nodes in a row that may merge together.
    /// Default: 3
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public int NumNodesPerMergeSegment
    {
      get { return m_numNodesPerMergeSegment; }
      set
      {
        m_numNodesPerMergeSegment = value;
        Propagate( properties => properties.setNumNodesPerMergeSegment( (ulong)m_numNodesPerMergeSegment ) );
      }
    }

    [SerializeField]
    private ContactReductionMode m_contactReduction = ContactReductionMode.Minimal;

    /// <summary>
    /// Contact reduction level of merged nodes against other objects.
    /// </summary>
    public ContactReductionMode ContactReduction
    {
      get { return m_contactReduction; }
      set
      {
        m_contactReduction = value;
        Propagate( properties => properties.setContactReduction( ToNative( m_contactReduction ) ) );
      }
    }

    [SerializeField]
    private bool m_lockToReachMergeConditionEnabled = true;

    /// <summary>
    /// Enable/disable the usage of hinge lock to reach merge
    /// condition (angle close to zero).
    /// Default: Enabled
    /// </summary>
    public bool LockToReachMergeConditionEnabled
    {
      get { return m_lockToReachMergeConditionEnabled; }
      set
      {
        m_lockToReachMergeConditionEnabled = value;
        Propagate( properties => properties.setEnableLockToReachMergeCondition( m_lockToReachMergeConditionEnabled ) );
      }
    }

    [SerializeField]
    private float m_lockToReachMergeConditionCompliance = 1.0E-11f;

    /// <summary>
    /// Compliance of the hinge lock used to reach merge condition.
    /// Default: 1.0E-11
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float LockToReachMergeConditionCompliance
    {
      get { return m_lockToReachMergeConditionCompliance; }
      set
      {
        m_lockToReachMergeConditionCompliance = value;
        Propagate( properties => properties.setLockToReachMergeConditionCompliance( m_lockToReachMergeConditionCompliance ) );
      }
    }

    [SerializeField]
    private float m_lockToReachMergeConditionDamping = 0.06f;

    /// <summary>
    /// Damping of the hinge lock used to reach merge condition.
    /// Default: 0.06
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float LockToReachMergeConditionDamping
    {
      get { return m_lockToReachMergeConditionDamping; }
      set
      {
        m_lockToReachMergeConditionDamping = value;
        Propagate( properties => properties.setLockToReachMergeConditionDamping( m_lockToReachMergeConditionDamping ) );
      }
    }

    [SerializeField]
    private float m_maxAngleMergeCondition = 6.0E-4f;

    /// <summary>
    /// Maximum angle > 0, in degrees, to trigger merge between nodes. I.e., when
    /// the angle between two nodes less than maxAngleToMerge the nodes will merge.
    /// </summary>
    [ ClampAboveZeroInInspector( true )]
    public float MaxAngleMergeCondition
    {
      get { return m_maxAngleMergeCondition; }
      set
      {
        m_maxAngleMergeCondition = value;
        Propagate( properties => properties.setMaxAngleMergeCondition( Mathf.Deg2Rad * m_maxAngleMergeCondition ) );
      }
    }

    public void Register( Track track )
    {
      if ( !m_tracks.Contains( track ) ) {
        m_tracks.Add( track );

        // Synchronizing properties for all tracks. Could be
        // avoided by adding a state so that Propagate only
        // shows current added terrain.
        Utils.PropertySynchronizer.Synchronize( this );
      }
    }

    public void Unregister( Track track )
    {
      m_tracks.Remove( track );
    }

    public override void Destroy()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    private void Propagate( Action<agxVehicle.TrackInternalMergeProperties> action )
    {
      foreach ( var track in m_tracks )
        if ( track.Native != null )
          action( track.Native.getInternalMergeProperties() );
    }

    private List<Track> m_tracks = new List<Track>();
  }
}
