using AGXUnity.Rendering;
using AGXUnity.Utils;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Wire object.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Wire" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#wire" )]
  public class Wire : ScriptComponent
  {
    /// <summary>
    /// Route node, node types.
    /// </summary>
    public enum NodeType
    {
      BodyFixedNode,
      FreeNode,
      ConnectingNode,
      EyeNode,
      ContactNode,
      WinchNode,
      Unknown
    }

    /// <summary>
    /// Converts from native type to NodeType.
    /// </summary>
    /// <param name="nativeType">Native node type.</param>
    /// <returns>NodeType if supported - otherwise NodeType.Unknwon.</returns>
    public static NodeType Convert( agxWire.WireNode.Type nativeType )
    {
      return nativeType == agxWire.WireNode.Type.BODY_FIXED ?
               NodeType.BodyFixedNode :
             nativeType == agxWire.WireNode.Type.FREE ?
               NodeType.FreeNode :
             nativeType == agxWire.WireNode.Type.CONNECTING ?
               NodeType.ConnectingNode :
             nativeType == agxWire.WireNode.Type.EYE ?
               NodeType.EyeNode :
             nativeType == agxWire.WireNode.Type.CONTACT || nativeType == agxWire.WireNode.Type.SHAPE_CONTACT ?
               NodeType.ContactNode :
               NodeType.Unknown;
    }

    /// <summary>
    /// Converts from NodeType to native node type.
    /// </summary>
    /// <param name="nodeType">Node type.</param>
    /// <returns>Native node type given NodeType.</returns>
    public static agxWire.WireNode.Type Convert( NodeType nodeType )
    {
      return nodeType == NodeType.BodyFixedNode ?
               agxWire.WireNode.Type.BODY_FIXED :
             nodeType == NodeType.FreeNode ?
               agxWire.WireNode.Type.FREE :
             nodeType == NodeType.ConnectingNode ?
               agxWire.WireNode.Type.CONNECTING :
             nodeType == NodeType.EyeNode ?
               agxWire.WireNode.Type.EYE :
             nodeType == NodeType.ContactNode ?
               agxWire.WireNode.Type.SHAPE_CONTACT :
               agxWire.WireNode.Type.NOT_DEFINED;
    }

    /// <summary>
    /// Get native instance, if initialized.
    /// </summary>
    public agxWire.Wire Native { get; private set; }

    private agxWire.Wire AssignedNative = null;

    /// <summary>
    /// Radius of this wire - default 0.015. Paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.015f;

    /// <summary>
    /// Get or set radius of this wire - default 0.015.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        m_radius = value;
        if ( Native != null )
          Native.setRadius( m_radius );
      }
    }

    /// <summary>
    /// Convenience property for diameter of this wire.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Diameter
    {
      get { return 2.0f * Radius; }
      set { Radius = 0.5f * value; }
    }

    /// <summary>
    /// Resolution of this wire - default 1.5. Paired with property ResolutionPerUnitLength.
    /// </summary>
    [SerializeField]
    private float m_resolutionPerUnitLength = 1.5f;

    /// <summary>
    /// Get or set resolution of this wire. Default 1.5.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ResolutionPerUnitLength
    {
      get { return m_resolutionPerUnitLength; }
      set
      {
        m_resolutionPerUnitLength = value;
        if ( Native != null )
          Native.setResolutionPerUnitLength( m_resolutionPerUnitLength );
      }
    }

    /// <summary>
    /// Linear velocity damping of this wire.
    /// </summary>
    [SerializeField]
    private float m_linearVelocityDamping = 0.0f;

    /// <summary>
    /// Get or set linear velocity damping of this wire. Default 0.0.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float LinearVelocityDamping
    {
      get { return m_linearVelocityDamping; }
      set
      {
        m_linearVelocityDamping = value;
        if ( Native != null )
          Native.setLinearVelocityDamping( m_linearVelocityDamping );
      }
    }

    /// <summary>
    /// Internal. Scale constant of this wire - default 0.35. Paired with
    /// property ScaleConstant.
    /// </summary>
    [SerializeField]
    private float m_scaleConstant = 0.35f;

    /// <summary>
    /// Internal. Get or set scale constant of this wire. Default 0.35.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float ScaleConstant
    {
      get { return m_scaleConstant; }
      set
      {
        m_scaleConstant = value;
        if ( Native != null )
          Native.getParameterController().setScaleConstant( m_scaleConstant );
      }
    }

    /// <summary>
    /// Shape material of this wire. Default null.
    /// </summary>
    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Get or set shape material of this wire. Default null.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( Native != null && m_material != null && m_material.Native != null )
          Native.setMaterial( m_material.Native );
      }
    }

    /// <summary>
    /// Get route to initialize this wire.
    /// </summary>
    [field: SerializeReference]
    public WireRoute Route { get; private set; }

    /// <summary>
    /// Winch at begin of this wire if exists.
    /// </summary>
    [HideInInspector]
    public WireWinch BeginWinch
    {
      get { return Route.NumNodes > 0 ? Route.First().Winch : null; }
    }

    /// <summary>
    /// Winch at end of this wire if exists.
    /// </summary>
    [HideInInspector]
    public WireWinch EndWinch
    {
      get { return Route.NumNodes > 1 ? Route.Last().Winch : null; }
    }

    public Wire()
    {
      Route = new WireRoute( this );
    }

    /// <summary>
    /// Copies data from native instance to this wire.
    /// </summary>
    /// <param name="native">Native instance.</param>
    public void RestoreLocalDataFrom( agxWire.Wire native )
    {
      if ( native == null )
        return;

      Radius                  = System.Convert.ToSingle( native.getRadius() );
      ResolutionPerUnitLength = System.Convert.ToSingle( native.getResolutionPerUnitLength() );
      ScaleConstant           = System.Convert.ToSingle( native.getParameterController().getScaleConstant() );
    }

    protected override void OnEnable()
    {
      if ( Native != null && Simulation.HasInstance )
        GetSimulation().add( Native );
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXWires, this ) )
        return false;

      if ( AssignedNative != null ) {
        Native = AssignedNative;

        Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;
        return Native.initialized();
      }

      WireRoute.ValidatedRoute validatedRoute = Route.GetValidated();
      if ( !validatedRoute.Valid ) {
        Debug.LogError( validatedRoute.ErrorString, this );
        for ( int i = 0; i < validatedRoute.Nodes.Count; ++i )
          if ( !validatedRoute.Nodes[ i ].Valid )
            Debug.LogError( "[" + i + "]: " + validatedRoute.Nodes[ i ].ErrorString, this );

        return false;
      }

      try {
        Native = new agxWire.Wire( Radius, ResolutionPerUnitLength );
        Material = m_material != null ? m_material.GetInitialized<ShapeMaterial>() : null;
        int nodeCounter = 0;
        foreach ( WireRouteNode routeNode in Route ) {
          agxWire.WireNode node = routeNode.GetInitialized<WireRouteNode>().Native;

          bool success = true;
          if ( node.getType() == agxWire.WireNode.Type.CONNECTING ) {
            // This is the first node, CM-node goes first.
            if ( nodeCounter == 0 ) {
              success = success && Native.add( node.getAsConnecting().getCmNode() );
              success = success && Native.add( node );
            }
            // This has to be the last node, CM-node goes last.
            else {
              success = success && Native.add( node );
              success = success && Native.add( node.getAsConnecting().getCmNode() );
            }
          }
          else if ( routeNode.Type == NodeType.WinchNode ) {
            if ( node == null )
              throw new AGXUnity.Exception( "Unable to initialize wire winch." );

            success = success && Native.add( routeNode.Winch.Native );
          }
          else
            success = success && Native.add( node );

          if ( !success )
            throw new AGXUnity.Exception( "Unable to add node " + nodeCounter + ": " + routeNode.Type );

          ++nodeCounter;
        }

        Native.setName( name );

        // Wires doesn't have setEnable( true/false ) but supports
        // re-adding to a simulation. I.e., the state of the previously
        // removed wire will be recovered when the wire is added again.
        // Initialize the wire (by adding it) and remove the wire if this
        // component isn't active.
        GetSimulation().add( Native );
        if ( !isActiveAndEnabled )
          GetSimulation().remove( Native );

        Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;
      }
      catch ( System.Exception e ) {
        Debug.LogException( e, this );
        return false;
      }

      return Native.initialized();
    }

    protected override void OnDisable()
    {
      if ( Native != null && Simulation.HasInstance )
        GetSimulation().remove( Native );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }

      Native = null;

      base.OnDestroy();
    }

    private Rendering.WireRenderer m_renderer = null;
    private void OnPostStepForward()
    {
      if ( m_renderer == null )
        m_renderer = GetComponent<Rendering.WireRenderer>();

      if ( m_renderer != null )
        m_renderer.OnPostStepForward( this );
    }

    private static Wire FromNative( agxWire.Wire native )
    {
      var GO = new GameObject("New Wire");
      var wire = GO.AddComponent<AGXUnity.Wire>();
      wire.AssignedNative = native;
      native.reference();
      return wire.GetInitialized();
    }

    public Wire Cut( Vector3 worldPoint )
    {
      var cutWire = Native.cut( worldPoint.ToHandedVec3(), 2 );
      if ( cutWire == null )
        return null;

      var newWire = FromNative( cutWire );
      newWire.m_material = m_material;
      newWire.m_linearVelocityDamping = m_linearVelocityDamping;
      newWire.m_radius = m_radius;
      newWire.m_scaleConstant = m_scaleConstant;
      newWire.m_resolutionPerUnitLength = m_resolutionPerUnitLength;
      if ( EndWinch != null ) {
        newWire.Route.Add( NodeType.FreeNode ); // Dummy node
        newWire.Route.Add( NodeType.WinchNode ).Wire = this;
        newWire.EndWinch.InitializeFrom( EndWinch.Native );
        Route.Remove( Route.Last() );
      }

      if ( TryGetComponent<WireRenderer>( out var renderer ) ) {
        var newRenderer = newWire.gameObject.AddComponent<WireRenderer>();
        newRenderer.enabled = renderer.enabled;
        newRenderer.Material = renderer.Material;
        newRenderer.ReceiveShadows = renderer.ReceiveShadows;
      }

      return newWire;
    }

    public bool Merge( Wire other, bool removeGameObject = true )
    {
      var res = Native.merge( other.Native );
      if ( !res ) {
        Debug.LogWarning( $"Failed to merge wires '{name}' and '{other.name}'" );
        return false;
      }

      if ( other.BeginWinch != null ) {
        var node = WireRouteNode.Create( NodeType.WinchNode );
        node.Wire = this;
        Route.InsertBefore( node, Route.First() );
        BeginWinch.InitializeFrom( other.BeginWinch.Native );
      }

      if ( other.EndWinch != null ) {
        if ( Route.NumNodes == 0 )
          Route.Add( NodeType.FreeNode ); // Dummy node
        Route.Add( NodeType.WinchNode ).Wire = this;

        EndWinch.InitializeFrom( other.EndWinch.Native );
      }

      if ( other.TryGetComponent<WireRenderer>( out var renderer ) )
        Destroy( renderer );
      Destroy( other );

      if ( removeGameObject )
        Destroy( other.gameObject );
      return true;
    }

    private void Reset()
    {
      Route.Clear();
    }
  }
}
