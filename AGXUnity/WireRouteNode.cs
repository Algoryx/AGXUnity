using AGXUnity.Utils;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace AGXUnity
{

  [Serializable]
  public class EyeNodeData : IExtraNodeData
  {

    [field: SerializeField]
    public Vector2 FrictionCoefficients { get; set; } = new Vector2( 0, 0 );


    public virtual bool Initialize( WireRouteNode parent )
    {
      parent.Native.getMaterial().setFrictionCoefficient( FrictionCoefficients.x, agxWire.NodeMaterial.Direction.NEGATIVE );
      parent.Native.getMaterial().setFrictionCoefficient( FrictionCoefficients.y, agxWire.NodeMaterial.Direction.POSITIVE );
      return true;
    }
  }

  /// <summary>
  /// Representation of nodes, used while routing.
  /// </summary>
  [Serializable]
  public class WireRouteNode : RouteNode
  {
    /// <summary>
    /// Construct a route node given type, parent game object, local position to parent and
    /// local rotation to parent.
    /// </summary>
    /// <param name="type">Node type.</param>
    /// <param name="parent">Parent game object - world if null.</param>
    /// <param name="localPosition">Position in parent frame. If parent is null this is the position in world frame.</param>
    /// <param name="localRotation">Rotation in parent frame. If parent is null this is the rotation in world frame.</param>
    /// <returns>Wire route node instance.</returns>
    public static WireRouteNode Create( Wire.NodeType type = Wire.NodeType.BodyFixedNode,
                                        GameObject parent = null,
                                        Vector3 localPosition = default( Vector3 ),
                                        Quaternion localRotation = default( Quaternion ) )
    {
      WireRouteNode node = Create<WireRouteNode>( parent, localPosition, localRotation );
      node.Type = type;

      return node;
    }

    public agxWire.WireNode Native { get; private set; }

    /// <summary>
    /// Type of this node. Paired with property Type.
    /// </summary>
    [SerializeField]
    private Wire.NodeType m_type = Wire.NodeType.BodyFixedNode;

    /// <summary>
    /// Type of this node.
    /// </summary>
    public Wire.NodeType Type
    {
      get { return m_type; }
      set
      {
        m_type = value;
        OnNodeType();
      }
    }

    /// <summary>
    /// Reference back to the wire.
    /// </summary>
    [SerializeField]
    private Wire m_wire = null;

    /// <summary>
    /// Get or set wire of this route node.
    /// </summary>
    public Wire Wire
    {
      get { return m_wire; }
      set
      {
        m_wire = value;
        OnNodeType();
      }
    }

    /// <summary>
    /// If this route node is a winch, this field is set.
    /// </summary>
    [SerializeField]
    [Obsolete]
    [FormerlySerializedAs("m_winch")]
    private Deprecated.WireWinch m_deprecatedWinchComponent = null;

    [Obsolete]
    public Deprecated.WireWinch DeprecatedWinchComponent => m_deprecatedWinchComponent;

    /// <summary>
    /// Get winch if assigned from the route.
    /// </summary>
    [field: SerializeReference]
    public WireWinch Winch { get; private set; }

    /// <summary>
    /// Creates native instance given current properties.
    /// </summary>
    /// <returns>Native instance of this node.</returns>
    protected override bool Initialize()
    {
      RigidBody rb        = null;
      Collide.Shape shape = null;
      if ( Parent != null ) {
        rb    = Parent.GetInitializedComponentInParent<RigidBody>();
        shape = Parent.GetInitializedComponentInParent<Collide.Shape>();
      }

      // We don't know if the parent is the rigid body.
      // It could be a mesh, or some other object.
      // Also - use world position if Type == FreeNode.
      agx.Vec3 point = rb != null && Type != Wire.NodeType.FreeNode ?
                          CalculateLocalPosition( rb.gameObject ).ToHandedVec3() :
                          Position.ToHandedVec3();

      agx.RigidBody nativeRb = rb != null ? rb.Native : null;
      if ( Type == Wire.NodeType.BodyFixedNode )
        Native = new agxWire.WireBodyFixedNode( nativeRb, point );
      // Create a free node if type is contact and shape == null.
      else if ( Type == Wire.NodeType.FreeNode || ( Type == Wire.NodeType.ContactNode && shape == null ) )
        Native = new agxWire.WireFreeNode( point );
      else if ( Type == Wire.NodeType.ConnectingNode )
        Native = new agxWire.WireConnectingNode( nativeRb, point, double.PositiveInfinity );
      else if ( Type == Wire.NodeType.EyeNode )
        Native = new agxWire.WireEyeNode( nativeRb, point );
      else if ( Type == Wire.NodeType.ContactNode )
        Native = new agxWire.WireContactNode( shape.NativeGeometry, CalculateLocalPosition( shape.gameObject ).ToHandedVec3() );
      else if ( Type == Wire.NodeType.WinchNode ) {
        if ( Winch == null )
          throw new AGXUnity.Exception( "No reference to a wire winch component in the winch node." );

        Winch.Initialize( this );

        Native = Winch.Native != null ? Winch.Native.getStopNode() : null;
      }

      if ( NodeData != null && Native != null )
        NodeData.Initialize( this );

      return Native != null;
    }

    public override void OnDestroy()
    {
      Native = null;

      base.OnDestroy();
    }

    /// <summary>
    /// When the user is changing node type, e.g., between fixed and winch,
    /// we receive a callback handling winch dependencies.
    /// </summary>
    private void OnNodeType()
    {
      if ( Winch != null ) {
        if ( Wire == null || Type != Wire.NodeType.WinchNode )
          Winch = null;
      }
      else if ( Wire != null && Type == Wire.NodeType.WinchNode )
        Winch = new WireWinch();

      if ( Type == Wire.NodeType.EyeNode ) {
        if ( NodeData is not EyeNodeData )
          NodeData = new EyeNodeData();
      }
      else
        NodeData = null;
    }
  }
}
