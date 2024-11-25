using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Cable Tunneling Guard" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( AGXUnity.Cable ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-tunneling-guard" )]
  public class CableTunnelingGuard : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable tuneling guard.
    /// </summary>
    public agxCable.CableTunnelingGuard Native { get; private set; }

    [System.NonSerialized]
    private Cable m_cable = null;

    /// <summary>
    /// The Cable ScriptComponent that this CableTunnelingGuard follows
    /// </summary>
    [HideInInspector]
    public Cable Cable { get { return m_cable ??= GetComponent<Cable>(); } }

    /// <summary>
    /// The mesh which is used to visualise a hull
    /// </summary>
    private Mesh m_mesh = null;

    [SerializeField]
    private double m_hullScale = 4;

    public double HullScale
    {
      get { return m_hullScale; }
      set
      {
        value = System.Math.Max( value, 1.0f );

        if ( m_hullScale != value ) {
          m_hullScale = value;
          UpdateRenderingMesh();
        }

        if ( Native != null ) {
          Native.setHullScale( m_hullScale );
        }
      }
    }

    private void UpdateRenderingMesh()
    {
      if ( m_mesh == null )
        m_mesh = new Mesh();

      if ( m_pointCurveCache != null && m_pointCurveCache.Length >= 2 ) {
        float segmentLength = ( Cable.GetRoutePoints()[ 0 ]-Cable.GetRoutePoints()[ 1 ] ).magnitude;
        CapsuleShapeUtils.CreateCapsuleMesh( Cable.Radius * (float)m_hullScale, segmentLength, 0.7f, m_mesh );
      }
    }

    // See documentation / tutorials for a more detailed description of the native parameters

    /// <summary>
    /// The angle to cable ends at which and approaching contact is accepted
    /// </summary>
    [SerializeField]
    private double m_angleThreshold = 90.0 * 0.9;

    public double AngleThreshold
    {
      get { return m_angleThreshold; }
      set
      {
        m_angleThreshold = value;
        if ( Native != null ) {
          Native.setAngleThreshold( m_angleThreshold / 180.0 * Mathf.PI );
        }
      }
    }

    /// <summary>
    /// A parameter which controls how far the estimated penetration depth must be at the enxt step to attempt to
    /// prevent a tunneling occurence
    /// </summary>
    [SerializeField]
    private double m_leniency = 0;

    public double Leniency
    {
      get { return m_leniency; }
      set
      {
        m_leniency = value;
        if ( Native != null ) {
          Native.setLeniency( m_leniency );
        }
      }
    }

    /// <summary>
    /// The amount of steps for which the component will continue adding contacts to the solver after a contact has
    /// been predicted.
    /// </summary>
    [SerializeField]
    private uint m_debounceSteps = 0;

    public uint DebounceSteps
    {
      get { return m_debounceSteps; }
      set
      {
        m_debounceSteps = value;
        if ( Native != null ) {
          Native.setDebounceSteps( m_debounceSteps );
        }
      }
    }

    /// <summary>
    /// When set to true the component will not attempt any predictions and will always add the contacts it encounters 
    /// through the hulls to the solver
    /// </summary>
    [SerializeField]
    private bool m_alwaysAdd = false;

    public bool AlwaysAdd
    {
      get { return m_alwaysAdd; }
      set
      {
        m_alwaysAdd = value;
        if ( Native != null ) {
          Native.setAlwaysAdd( m_alwaysAdd );
        }
      }
    }

    /// <summary>
    /// When true the component will predict tunneling with its own segments as well
    /// </summary>
    [SerializeField]
    private bool m_enableSelfInteraction = true;

    public bool EnableSelfInteraction
    {
      get { return m_enableSelfInteraction; }
      set
      {
        m_enableSelfInteraction = value;
        if ( Native != null ) {
          Native.setEnableSelfInteraction( m_enableSelfInteraction );
        }
      }
    }

    protected override bool Initialize()
    {
      Native = new agxCable.CableTunnelingGuard(m_hullScale);

      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable == null ) {
        Debug.LogWarning( "Unable to find Cable component for CableTunnelingGuard - cable tunneling guard instance ignored.", this );
        return false;
      }

      cable.addComponent( Native );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( GetSimulation() == null )
        return;

      var cable = Cable.Native;
      if ( cable != null ) {
        cable.removeComponent( Native );
      }

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      Native?.setEnabled( true );
    }

    protected override void OnDisable()
    {
      Native?.setEnabled( false );
    }

    private void Reset()
    {
      if ( GetComponent<Cable>() == null )
        Debug.LogError( "Component: CableDamage requires Cable component.", this );
    }

    private Vector3[] m_pointCurveCache = null;

    private bool CheckCableRouteChanges()
    {
      var routePointCahce = Cable.GetRoutePoints();
      if ( m_pointCurveCache != routePointCahce ) {
        m_pointCurveCache = routePointCahce;
        return true;
      }
      return false;
    }

    private void OnDrawGizmosSelected()
    {
      if ( CheckCableRouteChanges() ) {
        UpdateRenderingMesh();
      }

      if ( enabled )
      {
        // Algoryx orange
        Gizmos.color = new Color32(0xF3, 0x8B, 0x00, 0xF);     
        if (Application.isPlaying && Cable?.Native != null)
        {
          foreach ( var segment in Cable.Native.getSegments()) {
            Vector3 direction = (segment.getEndPosition() - segment.getBeginPosition()).ToHandedVector3();
            Vector3 center = segment.getCenterPosition().ToHandedVector3();
            Gizmos.DrawWireMesh( m_mesh, center, Quaternion.FromToRotation( Vector3.up, direction ) );
          }
        } 
        else if ( m_pointCurveCache != null && m_pointCurveCache.Length != 0 ) {          
          Vector3 prevPoint = m_pointCurveCache[0];
          foreach ( var point in m_pointCurveCache.Skip( 1 ) ) {
            Vector3 direction = point-prevPoint;
            Vector3 center = prevPoint + direction * 0.5f;
            Gizmos.DrawWireMesh( m_mesh, center, Quaternion.FromToRotation( Vector3.up, direction ) );
            prevPoint = point;
          }
        }
      }
    }
  }

}
