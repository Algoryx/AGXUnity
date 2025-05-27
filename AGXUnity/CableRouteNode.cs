using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  [Serializable]
  public class BodyFixedData : IExtraNodeData
  {
    [field: SerializeField]
    public bool RigidAttachment { get; set; } = true;

    [field: SerializeField]
    public bool IgnoreNodeRotation { get; set; } = false;

    public bool Initialize( RouteNode parent )
    {
      throw new NotImplementedException();
    }
  }

  [Serializable]
  public class CableRouteNode : RouteNode, ISerializationCallbackReceiver
  {
    /// <summary>
    /// Construct a route node given parent game object, local position to parent and
    /// local rotation to parent.
    /// </summary>
    /// <param name="parent">Parent game object - world if null.</param>
    /// <param name="localPosition">Position in parent frame. If parent is null this is the position in world frame.</param>
    /// <param name="localRotation">Rotation in parent frame. If parent is null this is the rotation in world frame.</param>
    /// <returns>Route node instance.</returns>
    public static CableRouteNode Create( Cable.NodeType nodeType,
                                         GameObject parent = null,
                                         Vector3 localPosition = default( Vector3 ),
                                         Quaternion localRotation = default( Quaternion ) )
    {
      var node = Create<CableRouteNode>( parent, localPosition, localRotation );
      node.Type = nodeType;

      return node;
    }

    /// <summary>
    /// Native instance of this node - present after Initialize has been called.
    /// </summary>
    public agxCable.RoutingNode Native { get; private set; }

    /// <summary>
    /// Type of this node. Paired with property Type.
    /// </summary>
    [SerializeField]
    private Cable.NodeType m_type = Cable.NodeType.BodyFixedNode;

    /// <summary>
    /// Type of this node.
    /// </summary>
    public Cable.NodeType Type
    {
      get { return m_type; }
      set
      {
        if ( value != m_type || NodeData == null ) {
          m_type = value;
          if ( m_type == Cable.NodeType.BodyFixedNode )
            NodeData = new BodyFixedData();
          else
            NodeData = new NoExtraData();
        }
      }
    }

    [SerializeField]
    private List<CableAttachment> m_attachments = new List<CableAttachment>();

    /// <summary>
    /// Cable node attachments.
    /// </summary>
    public CableAttachment[] Attachments
    {
      get { return m_attachments.ToArray(); }
    }

    /// <summary>
    /// Creates and adds an attachment to this cable node.
    /// </summary>
    /// <param name="attachmentType">Attachment type.</param>
    /// <param name="parent">Parent game object - world if null.</param>
    /// <param name="localPosition">Position in parent frame. If parent is null this is the position in world frame.</param>
    /// <param name="localRotation">Rotation in parent frame. If parent is null this is the rotation in world frame.</param>
    /// <returns>Create attachment if added - otherwise null.</returns>
    public CableAttachment Add( CableAttachment.AttachmentType attachmentType,
                                GameObject parent = null,
                                Vector3 localPosition = default( Vector3 ),
                                Quaternion localRotation = default( Quaternion ) )
    {
      var attachment = CableAttachment.Create( attachmentType, parent, localPosition, localRotation );
      if ( !Add( attachment ) )
        return null;
      return attachment;
    }

    /// <summary>
    /// Add an attachment to this node.
    /// </summary>
    /// <param name="attachment">Attachment to add.</param>
    /// <returns>True if added, false if null or already present.</returns>
    public bool Add( CableAttachment attachment )
    {
      if ( attachment == null || m_attachments.Contains( attachment ) )
        return false;

      m_attachments.Add( attachment );

      return true;
    }

    public override void OnDestroy()
    {
      Native = null;

      base.OnDestroy();
    }

    protected override bool Initialize()
    {
      if ( Native != null )
        return true;

      RigidBody rb = Parent != null ? Parent.GetInitializedComponentInParent<RigidBody>() : null;

      agx.Vec3 position = rb != null && Type == Cable.NodeType.BodyFixedNode ?
                            CalculateLocalPosition( rb.gameObject ).ToHandedVec3() :
                            Position.ToHandedVec3();

      agx.Quat rotation = rb != null && Type == Cable.NodeType.BodyFixedNode ?
                            CalculateLocalRotation( rb.gameObject ).ToHandedQuat() :
                            Rotation.ToHandedQuat();

      if ( Type == Cable.NodeType.BodyFixedNode )
        Native = new agxCable.CableBodyFixedNode( rb != null ? rb.Native : null, new agx.AffineMatrix4x4( rotation, position ) );
      else if ( Type == Cable.NodeType.FreeNode ) {
        Native = new agxCable.CableFreeNode( position );
        Native.getRigidBody().setRotation( Rotation.ToHandedQuat() );
      }
      else
        return false;

      foreach ( var attachment in m_attachments ) {
        var attachmentRb       = attachment.Parent != null ? attachment.Parent.GetInitializedComponentInParent<RigidBody>() : null;
        var attachmentPosition = attachmentRb != null ? attachment.CalculateLocalPosition( attachmentRb.gameObject ).ToHandedVec3() : attachment.Position.ToHandedVec3();
        agx.Quat attachmentRotation;
        if ( attachment.IgnoreNodeRotation )
          attachmentRotation = attachmentRb != null ? CalculateLocalRotation( attachmentRb.gameObject ).ToHandedQuat() : attachment.Rotation.ToHandedQuat();
        else
          attachmentRotation = attachmentRb != null ? attachment.CalculateLocalRotation( attachmentRb.gameObject ).ToHandedQuat() : attachment.Rotation.ToHandedQuat();
        agxCable.SegmentAttachment nativeAttachment = null;
        if ( attachment.Type == CableAttachment.AttachmentType.Ball )
          nativeAttachment = new agxCable.PointSegmentAttachment( attachmentRb != null ? attachmentRb.Native : null, attachmentPosition );
        else if ( attachment.Type == CableAttachment.AttachmentType.Rigid )
          nativeAttachment = new agxCable.RigidSegmentAttachment( attachmentRb != null ? attachmentRb.Native : null, new agx.AffineMatrix4x4( attachmentRotation, attachmentPosition ) );

        if ( nativeAttachment == null )
          Debug.LogWarning( "Unknown cable node attachment type. Ignored attachment." );
        else
          Native.add( nativeAttachment );
      }

      return true;
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
      if ( NodeData == null ) {
        if ( Type == Cable.NodeType.BodyFixedNode ) {
          // If cable was created prior to 5.3.0, the default was to have IgnoreNodeRotation = true. 
          // We set it to the same when we deserialize a node without data to ensure consistent behaviour across versions.
          var bfd = new BodyFixedData();
          bfd.RigidAttachment = true;
          bfd.IgnoreNodeRotation = true;
          NodeData = bfd;
        }
        else {
          NodeData = new NoExtraData();
        }
      }
    }
  }
}
