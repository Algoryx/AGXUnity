using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Cable route object containing nodes that initializes a cable.
  /// This object is an IEnumerable, add "using System.Linq" to
  /// get a wide range of "features" such as ToArray().
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#routing-the-cable" )]
  public class CableRoute : Route<CableRouteNode>
  {
    /// <summary>
    /// Add node to this route given type, parent, local position and local rotation.
    /// </summary>
    /// <param name="nodeType">Node type.</param>
    /// <param name="parent">Node parent object.</param>
    /// <param name="localPosition">Local position relative parent.</param>
    /// <param name="localRotation">Local rotation relative parent.</param>
    /// <returns></returns>
    public CableRouteNode Add( Cable.NodeType nodeType,
                               GameObject parent = null,
                               Vector3 localPosition = default( Vector3 ),
                               Quaternion localRotation = default( Quaternion ) )
    {
      var node = CableRouteNode.Create( nodeType, parent, localPosition, localRotation );
      if ( !Add( node ) )
        return null;

      return node;
    }

    /// <summary>
    /// Add route node given native cable segment instance.
    /// </summary>
    /// <param name="nativeSegment">Native cable segment.</param>
    /// <param name="attachmentParentCallback">
    /// Callback that maps native rigid body to game object, i.e., an rigid body game object
    /// created given the attachment. When attachment == null the parent object should be returned - null if world.
    /// </param>
    /// <returns>Cable route node with attachments (if any). Null if not added.</returns>
    public CableRouteNode Add( agxCable.CableSegment nativeSegment, Func<agxCable.SegmentAttachment, GameObject> attachmentParentCallback )
    {
      if ( nativeSegment == null )
        return null;

      // TODO: Use attachments instead of node type when we're reading from native?
      Cable.NodeType nodeType = nativeSegment.asBodyFixedNode() != null ?
                                  Cable.NodeType.BodyFixedNode :
                                  Cable.NodeType.FreeNode;

      CableRouteNode node = null;
      if ( nodeType == Cable.NodeType.BodyFixedNode ) {
        if ( nativeSegment.getAttachments().Count == 0 )
          throw new AGXUnity.Exception( "Native cable body fixed node without attachments is undefined." );

        var attachment = nativeSegment.getAttachments()[ 0 ].get();
        node = CableRouteNode.Create( Cable.NodeType.BodyFixedNode,
                                      attachmentParentCallback( attachment ),
                                      attachment.getFrame().getLocalTranslate().ToHandedVector3(),
                                      attachment.getFrame().getLocalRotate().ToHandedQuaternion() );
      }
      else {
        node = CableRouteNode.Create( Cable.NodeType.FreeNode,
                                      attachmentParentCallback( null ),
                                      nativeSegment.getBeginPosition().ToHandedVector3(),
                                      Quaternion.LookRotation( nativeSegment.getDirection().ToHandedVector3(), Vector3.up ) );
      }

      if ( !Add( node ) )
        return null;

      int attachmentStartIndex = nodeType == Cable.NodeType.BodyFixedNode ? 1 : 0;
      for ( int i = attachmentStartIndex; i < nativeSegment.getAttachments().Count; ++i ) {
        var nativeAttachment = nativeSegment.getAttachments()[ i ].get();
        var attachmentType   = nativeAttachment.asPointSegmentAttachment() != null ?
                                 CableAttachment.AttachmentType.Ball :
                               nativeAttachment.asRigidSegmentAttachment() != null ?
                                  CableAttachment.AttachmentType.Rigid :
                                  CableAttachment.AttachmentType.Unknown;
        var attachment       = node.Add( attachmentType,
                                         attachmentParentCallback( nativeAttachment ),
                                         nativeAttachment.getFrame().getLocalTranslate().ToHandedVector3(),
                                         nativeAttachment.getFrame().getLocalRotate().ToHandedQuaternion() );
        if ( attachment == null )
          Debug.LogWarning( "Cable node attachment rejected by cable node.", attachmentParentCallback( nativeAttachment ) );
      }

      return node;
    }

    /// <summary>
    /// Checks if this route is synchronized with the given point curve.
    /// </summary>
    /// <param name="pointCurve">Point curve.</param>
    /// <param name="tolerance">Max position change tolerance.</param>
    /// <returns>True if synchronized, otherwise false.</returns>
    public bool IsSynchronized( PointCurve pointCurve, float tolerance )
    {
      if ( pointCurve == null || pointCurve.NumPoints != NumNodes )
        return false;

      for ( int i = 0; i < NumNodes; ++i )
        if ( Vector3.Distance( this[ i ].Position, pointCurve[ i ] ) > tolerance )
          return false;
      return true;
    }

    public override void Clear()
    {
      base.Clear();
    }
  }
}
