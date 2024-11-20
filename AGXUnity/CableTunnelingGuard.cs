using AGXUnity.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Cable Tunneling Guard" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( AGXUnity.Cable ) )]
  //[HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-tunneling-guard" )]
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
        if(m_hullScale != value)
        {          
          UpdateRenderingMesh();
        }

        m_hullScale = value;            
        if ( Native != null ) {
          Native.setHullScale( m_hullScale );
        }
      }
    }

    private void UpdateRenderingMesh()
    {
      if ( Cable.GetRoutePoints().Length >= 2 )
      {
        double segmentLength = (Cable.GetRoutePoints()[0]-Cable.GetRoutePoints()[1]).magnitude;
        var meshData = agxUtil.PrimitiveMeshGenerator.createCapsule(Cable.Radius * m_hullScale, segmentLength).getMeshData();
        m_mesh = new Mesh();

        m_mesh.vertices = meshData.getVertices().Select(x => x.ToHandedVector3()).ToArray();
        m_mesh.triangles = meshData.getIndices().Select(x => (int)x).ToArray();

        m_mesh.name = "CableTunnelingGuard - Hull mesh";
        m_mesh.RecalculateBounds();
        m_mesh.RecalculateNormals();
        m_mesh.RecalculateTangents();
      }
    }

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
      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable == null ) {
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

    protected void LateUpdate()
    {
      // Late update from Editor. Exit if the application is running.
      if ( Application.isPlaying )
        return;

      
    }

    private void OnDrawGizmos()
    {
      foreach(var point in Cable.GetRoutePoints())
      {
        Gizmos.DrawWireMesh(m_mesh, point);
      }
    }
  }

}
