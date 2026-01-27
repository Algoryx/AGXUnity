using AGXUnity.Utils;
using agxVehicle;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  /// <summary>
  /// Assembly object representing a continuous track with a given number of shoes (nodes).
  /// </summary>
  [AddComponentMenu( "AGXUnity/Model/Track" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#track" )]
  public class Track : ScriptComponent
  {
    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public agxVehicle.Track Native { get; private set; } = null;

    [SerializeField]
    private bool m_fullDoF = false;

    /// <summary>
    /// When enabled, the track will simulate individual links in the track based on the track properties.
    /// While this results in higher fidelity in the simulation results, it can also cause the simualation to become more unstable
    /// under harsh conditions (heavy impacts, high speeds) or when the track properties is not properly configured for stability.
    /// 
    /// In general, the Low DoF model should be preferred unless the high fidelity simulation is a necessity.
    /// </summary>
    [Tooltip( "When enabled, the track will simulate individual links in the track based on the track properties. " +
              "While this results in higher fidelity in the simulation results, it can also cause the simualation to become more unstable" +
              " under harsh conditions (heavy impacts, high speeds) or when the track properties is not properly configured for stability. " +
              "In general, the Low DoF model should be preferred unless the high fidelity simulation is a necessity." )]
    public bool FullDoF
    {
      get => m_fullDoF;
      set
      {
        m_fullDoF = value;
        if ( Native != null )
          Native.setEnableFullDegreeModel( m_fullDoF );
      }
    }

    [DisableInRuntimeInspector]
    [Tooltip("An object")]
    public GameObject ReferenceObject;

    [SerializeField]
    private int m_numberOfNodes = 64;

    /// <summary>
    /// Approximate number of nodes in the track. The final value
    /// may differ depending on the configuration of the wheels.
    /// Default: 64
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    [DisableInRuntimeInspector]
    [Tooltip( "Approximate number of nodes in the track. The final value may differ depending on the configuration of the wheels." )]
    public int NumberOfNodes
    {
      get { return m_numberOfNodes; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change number of nodes on an initialized track.", this );
          return;
        }
        m_numberOfNodes = value;
      }
    }

    [SerializeField]
    private float m_thickness = 0.05f;

    /// <summary>
    /// Thickness of this track.
    /// Default: 0.05
    /// </summary>
    [IgnoreSynchronization]
    [DisableInRuntimeInspector]
    [ClampAboveZeroInInspector]
    public float Thickness
    {
      get { return m_thickness; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change thickness of nodes on an initialized track.", this );
          return;
        }
        m_thickness = value;
      }
    }

    [SerializeReference]
    private TrackNodeVariation m_thicknessVariation = null;

    [IgnoreSynchronization]
    [HideInInspector]
    public TrackNodeVariation ThicknessVariation
    {
      get => m_thicknessVariation;
      set
      {
        if ( value == m_thicknessVariation )
          return;

        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change thickness variation of nodes on an initialized track,", this );
          return;
        }
        m_thicknessVariation = value;
      }
    }

    [SerializeField]
    private float m_width = 0.35f;

    /// <summary>
    /// Width of this track.
    /// Default: 0.35
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Width
    {
      get { return m_width; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change width of nodes on an initialized track,", this );
          return;
        }
        m_width = value;
      }
    }

    [SerializeReference]
    private TrackNodeVariation m_widthVariation = null;

    [IgnoreSynchronization]
    [HideInInspector]
    public TrackNodeVariation WidthVariation
    {
      get => m_widthVariation;
      set
      {
        if ( value == m_widthVariation )
          return;

        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change width variation of nodes on an initialized track,", this );
          return;
        }
        m_widthVariation = value;
      }
    }

    [SerializeField]
    private float m_initialTensionDistance = 1.0E-3f;

    /// <summary>
    /// Value (distance) of how much shorter each node should be which causes tension in the
    /// system of tracks and wheels. Ideal case
    ///     track_tension = initialDistanceTension * track_constraint_compliance.
    /// Since contacts and other factors are included it's not possible to know
    /// the exact tension after the system has been created.
    /// Default: 1.0E-3
    /// </summary>
    [IgnoreSynchronization]
    [DisableInRuntimeInspector]
    public float InitialTensionDistance
    {
      get { return m_initialTensionDistance; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change initial tension distance on an initialized track.", this );
          return;
        }
        m_initialTensionDistance = value;
      }
    }

    [SerializeField]
    private TrackProperties m_properties = null;

    /// <summary>
    /// Properties collection of this track.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public TrackProperties Properties
    {
      get { return m_properties; }
      set
      {
        m_properties = value;
        if ( Native != null )
          Native.setProperties( m_properties != null ?
                                  m_properties.GetInitialized<TrackProperties>().Native :
                                  null );
      }
    }

    [SerializeField]
    private TrackInternalMergeProperties m_internalMergeProperties = null;

    /// <summary>
    /// Node to node merge properties of this track.
    /// </summary>
    [AllowRecursiveEditing]
    [DynamicallyShowInInspector( nameof( FullDoF ) )]
    public TrackInternalMergeProperties InternalMergeProperties
    {
      get { return m_internalMergeProperties; }
      set
      {
        if ( Native != null && m_internalMergeProperties != null )
          m_internalMergeProperties.Unregister( this );

        m_internalMergeProperties = value;

        if ( Native != null && m_internalMergeProperties != null )
          m_internalMergeProperties.Register( this );
      }
    }

    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Shape material of this track.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( Native != null )
          Native.setMaterial( m_material != null ?
                                m_material.GetInitialized<ShapeMaterial>().Native :
                                null );
      }
    }

    [SerializeField]
    private List<TrackWheel> m_wheels = new List<TrackWheel>();

    /// <summary>
    /// Registered track wheel instances.
    /// </summary>
    [HideInInspector]
    public TrackWheel[] Wheels
    {
      get { return m_wheels.ToArray(); }
    }

    /// <summary>
    /// Associate track wheel instance to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to add.</param>
    /// <returns>True if added, false if null or already added.</returns>
    public bool Add( TrackWheel wheel )
    {
      if ( wheel == null || m_wheels.Contains( wheel ) )
        return false;

      m_wheels.Add( wheel );

      return true;
    }

    /// <summary>
    /// Disassociate track wheel instance from this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this track.</returns>
    public bool Remove( TrackWheel wheel )
    {
      if ( wheel == null )
        return false;

      return m_wheels.Remove( wheel );
    }

    /// <summary>
    /// True if <paramref name="wheel"/> is associated to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance.</param>
    /// <returns>True if <paramref name="wheel"/> is associated to this track.</returns>
    public bool Contains( TrackWheel wheel )
    {
      return m_wheels.Contains( wheel );
    }

    /// <summary>
    /// Verifies so that all added track wheels still exists. Wheels that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidWheels()
    {
      m_wheels.RemoveAll( wheel => wheel == null );
    }

    private class OnInitializeAdapter : TrackNodeOnInitializeCallback
    {
      private int m_nodeIdx = 0;

      private TrackNodeVariation m_widthVariation;
      private TrackNodeVariation m_heightVariation;

      public OnInitializeAdapter( float width, float height, TrackNodeVariation widthVariation, TrackNodeVariation heightVariation )
      {
        m_widthVariation = widthVariation;
        m_heightVariation = heightVariation;
      }

      public override void onInitialize( TrackNode node )
      {
        var applied = VariationUtils.ApplyVariations(m_widthVariation, m_heightVariation, node.getHalfExtents(), m_nodeIdx);
        node.getRigidBody().add( new agxCollide.Geometry( new agxCollide.Box( applied.Item1 ) ), agx.AffineMatrix4x4.translate( new agx.Vec3( applied.Item2.x, 0.0f, node.getHalfExtents().z ) ) );
        m_nodeIdx++;
      }
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTracks, this ) )
        return false;

      RemoveInvalidWheels();

      if ( m_wheels.Count == 0 ) {
        Debug.LogError( "Component: Track requires at least one wheel to initialize.", this );
        return false;
      }

      if ( m_wheels.Find( wheel => wheel.GetInitialized<TrackWheel>() == false ) != null ) {
        Debug.LogError( "Component: Track failed to initialize - one or several wheels failed to initialize.", this );
        return false;
      }

      agx.RigidBody refBody = null;

      if ( ReferenceObject != null )
        refBody = ReferenceObject.gameObject.GetInitializedComponentInParent<RigidBody>().Native;

      if ( !FullDoF && refBody == null )
        Debug.LogWarning( $"Track '{this.name}' is using the reduced DoF model but does not specify a reference body. This is likely to cause errors in the simulation." );

      if ( FullDoF && Properties != null && !Properties.FullDoF )
        Debug.LogWarning( $"Track '{this.name}' is using the full DoF model but it's properties are configured for the reduced DoF model. " +
                          $"While this is supported, it can lead to errors due to the properties not being fully specified for the model" );

      Native = new agxVehicle.Track( refBody,
                                     (ulong)NumberOfNodes,
                                     Width,
                                     Thickness,
                                     new agxVehicle.InitialTrackTension( InitialTensionDistance ) );

      if ( Properties != null )
        Native.setProperties( Properties.GetInitialized<TrackProperties>().Native );

      foreach ( var wheel in Wheels )
        Native.add( wheel.Native );

      if ( WidthVariation != null || ThicknessVariation != null )
        Native.initialize( new OnInitializeAdapter( Width, Thickness, WidthVariation, ThicknessVariation ) );

      if ( isActiveAndEnabled )
        GetSimulation().add( Native );

      return true;
    }

    protected override void OnEnable()
    {
      if ( Simulation.HasInstance && Native != null )
        GetSimulation().add( Native );
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance && Native != null )
        GetSimulation().remove( Native );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance && Native != null )
        GetSimulation().remove( Native );

      if ( InternalMergeProperties != null )
        InternalMergeProperties.Unregister( this );

      Native = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      if ( GetComponent<Rendering.TrackRenderer>() != null )
        GetComponent<Rendering.TrackRenderer>().OnTrackReset();

      // Is this too risky or desired? Sharing references and
      // copying values from an already created track.
      var otherTracks = GetComponents<Track>().Where( track => track != this ).ToArray();
      if ( otherTracks.Length > 0 ) {
        // This could be made "automatically" with PropertySynchronizer
        // but there are many properties with [IgnoreSynchronization]
        // in this class so it has to be overridden.
        NumberOfNodes           = otherTracks[ 0 ].NumberOfNodes;
        Thickness               = otherTracks[ 0 ].Thickness;
        Width                   = otherTracks[ 0 ].Width;
        InitialTensionDistance  = otherTracks[ 0 ].InitialTensionDistance;
        Properties              = otherTracks[ 0 ].Properties;
        InternalMergeProperties = otherTracks[ 0 ].InternalMergeProperties;
        Material                = otherTracks[ 0 ].Material;
      }
    }
  }
}
