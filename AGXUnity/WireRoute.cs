using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Wire route object containing nodes that initializes a wire.
  /// This object is an IEnumerable, add "using System.Linq" to
  /// get a wide range of "features" such as ToArray().
  /// </summary>
  /// <example>
  /// using System.Linq;
  /// ...
  /// WireRoute route = wire.Route;
  /// var freeNodes = from node in route select node.Type == Wire.NodeType.FreeNode;
  /// Wire.RouteNode myNode = route.FirstOrDefault( node => node.Frame == thisFrame );
  /// </example>
  [HideInInspector]
  [AddComponentMenu( "" )]
  public class WireRoute : Route<WireRouteNode>
  {
    /// <summary>
    /// Checks validity of current route.
    /// </summary>
    /// <returns>Validated wire route.</returns>
    public override ValidatedRoute GetValidated()
    {
      ValidatedRoute validatedRoute = new ValidatedRoute();
      // Less than thee nodes is always valid from the nodes point of view.
      var nodes = this.ToArray();
      if ( NumNodes < 3 ) {
        for ( int i = 0; i < NumNodes; ++i )
          validatedRoute.Nodes.Add( new ValidatedNode() { Node = nodes[ i ], Valid = true } );
      }
      // More than two nodes. Intermediate nodes may not be body fixed, connecting or winch.
      else {
        validatedRoute.Nodes.Add( new ValidatedNode() { Node = nodes[ 0 ], Valid = true } );
        for ( int i = 1; i < NumNodes - 1; ++i ) {
          WireRouteNode node = nodes[ i ];
          string errorString = node.Type == Wire.NodeType.BodyFixedNode ||
                               node.Type == Wire.NodeType.ConnectingNode ||
                               node.Type == Wire.NodeType.WinchNode ?
                                 node.Type.ToString().SplitCamelCase() + " can only be at the begin or at the end of a wire." :
                               string.Empty;
          validatedRoute.Nodes.Add( new ValidatedNode() { Node = node, Valid = ( errorString == string.Empty ), ErrorString = errorString } );
        }
        validatedRoute.Nodes.Add( new ValidatedNode() { Node = nodes[ NumNodes - 1 ], Valid = true } );
      }

      if ( NumNodes < 2 ) {
        validatedRoute.Valid = false;
        validatedRoute.ErrorString = "Route has to contain at least two or more nodes.";
      }
      else {
        bool nodesValid = true;
        foreach ( var validatedNode in validatedRoute.Nodes )
          nodesValid &= validatedNode.Valid;
        validatedRoute.Valid = nodesValid;
        validatedRoute.ErrorString = "One or more nodes are wrong.";
      }

      return validatedRoute;
    }

    /// <summary>
    /// Wire this route belongs to.
    /// </summary>
    private Wire m_wire = null;

    /// <summary>
    /// Get or set the wire this route belongs to.
    /// </summary>
    public Wire Wire
    {
      get
      {
        if ( m_wire == null ) {
          m_wire = GetComponent<Wire>();
          foreach ( var node in this )
            node.Wire = m_wire;
        }
        return m_wire;
      }
    }

    /// <summary>
    /// Add node to this route given type, parent, local position and local rotation.
    /// </summary>
    /// <param name="type">Node type.</param>
    /// <param name="parent">Node parent object.</param>
    /// <param name="localPosition">Local position relative parent.</param>
    /// <param name="localRotation">Local rotation relative parent.</param>
    /// <returns>Added route node.</returns>
    public WireRouteNode Add( Wire.NodeType type,
                              GameObject parent = null,
                              Vector3 localPosition = default( Vector3 ),
                              Quaternion localRotation = default( Quaternion ) )
    {
      var node = WireRouteNode.Create( type, parent, localPosition, localRotation );
      if ( !Add( node ) )
        return null;

      return node;
    }

    /// <summary>
    /// Add route node given native winch instance. Data will be copied from
    /// native to the created route node.
    /// </summary>
    /// <param name="nativeWinch">Native winch.</param>
    /// <param name="parent">Parent game object as in nativeWinch.getRigidBody().</param>
    /// <returns>Winch route node if added, otherwise null.</returns>
    public WireRouteNode Add( agxWire.WireWinchController nativeWinch, GameObject parent )
    {
      if ( nativeWinch == null )
        return null;

      var node = WireRouteNode.Create( Wire.NodeType.WinchNode, parent );
      if ( !Add( node ) )
        return null;

      node.LocalPosition = nativeWinch.getStopNode().getPosition().ToHandedVector3();
      node.LocalRotation = Quaternion.LookRotation( nativeWinch.getNormal().ToHandedVector3(), Vector3.up );

      node.Winch.RestoreLocalDataFrom( nativeWinch );

      return node;
    }

    /// <summary>
    /// Add route node given native node instance. If the node type is unsupported
    /// this method returns null, ignoring the node.
    /// </summary>
    /// <remarks>
    /// If the given node is a lumped node it will be assumed to be NodeType.FreeNode.
    /// </remarks>
    /// <param name="nativeNode">Native node instance.</param>
    /// <param name="parent">Parent game object as in nativeNode.getRigidBody().</param>
    /// <returns>Wire route node if added, otherwise null.</returns>
    public WireRouteNode Add( agxWire.WireNode nativeNode, GameObject parent )
    {
      if ( nativeNode == null )
        return null;

      var nodeType = Wire.Convert( nativeNode.getType() );
      if ( nodeType == Wire.NodeType.Unknown )
        return null;

      // Assume free if lumped node.
      if ( nodeType == Wire.NodeType.BodyFixedNode && agxWire.Wire.isLumpedNode( nativeNode.getRigidBody() ) )
        nodeType = Wire.NodeType.FreeNode;

      var node = WireRouteNode.Create( nodeType, parent );
      if ( !Add( node ) )
        return null;

      if ( nodeType != Wire.NodeType.FreeNode )
        node.LocalPosition = nativeNode.getPosition().ToHandedVector3();
      else
        node.Position = nativeNode.getWorldPosition().ToHandedVector3();

      return node;
    }

    public override void Clear()
    {
      var nodes = this.ToArray();
      for ( int i = 0; i < nodes.Length; ++i )
        OnRemovedFromList( nodes[ i ], i );

      base.Clear();
    }

    private WireRoute()
    {
      OnNodeAdded   += this.OnAddedToList;
      OnNodeRemoved += this.OnRemovedFromList;
    }

    private void OnAddedToList( WireRouteNode node, int index )
    {
      node.Wire = Wire;
    }

    private void OnRemovedFromList( WireRouteNode node, int index )
    {
      node.Wire = null;
    }
  }
}
