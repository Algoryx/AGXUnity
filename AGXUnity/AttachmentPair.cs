using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Constraint attachments for two objects - a reference object and a connected.
  /// The frame of the reference object, the reference frame, is by default the
  /// frame the constraint will be created from. It's possible to detach the relation
  /// between the frames, setting Synchronized to false.
  /// </summary>
  [HideInInspector]
  public class AttachmentPair : ScriptComponent
  {
    public static AttachmentPair Create( GameObject gameObject )
    {
      AttachmentPair instance = gameObject.AddComponent<AttachmentPair>();
      return instance;
    }

    /// <summary>
    /// The reference object that must contain a RigidBody
    /// component for the constraint to be valid.
    /// </summary>
    public GameObject ReferenceObject
    {
      get { return m_referenceFrame.Parent; }
      set
      {
        if ( value != null && value.GetComponentInParent<RigidBody>() == null ) {
          Debug.LogWarning( "Reference object must have a AGXUnity.RigidBody component (or in parents). Ignoring reference object.", value );
          return;
        }

        m_referenceFrame.SetParent( value );
      }
    }

    /// <summary>
    /// Connected object, the object constrained with the reference object.
    /// Null means "World".
    /// </summary>
    public GameObject ConnectedObject
    {
      get { return m_connectedFrame.Parent; }
      set
      {
        m_connectedFrame.SetParent( value );
      }
    }

    /// <summary>
    /// Reference frame holding world and relative to reference object
    /// transform. Paired with property ReferenceFrame.
    /// </summary>
    [SerializeField]
    private ConstraintFrame m_referenceFrame = new ConstraintFrame();

    /// <summary>
    /// Reference frame holding world and relative to reference object
    /// transform.
    /// </summary>
    public ConstraintFrame ReferenceFrame
    {
      get { return m_referenceFrame; }
    }

    /// <summary>
    /// Connected frame holding world and relative to connected object
    /// transform. Paired with property ConnectedFrame.
    /// </summary>
    [SerializeField]
    private ConstraintFrame m_connectedFrame = new ConstraintFrame();

    /// <summary>
    /// Connected frame holding world and relative to connected object
    /// transform.
    /// </summary>
    public ConstraintFrame ConnectedFrame
    {
      get { return m_connectedFrame; }
    }

    /// <summary>
    /// Synchronized flag. If synchronized the connected frame will, in world,
    /// have the same transform as the reference frame. Set this to false to
    /// have full control over the transform of the connected frame. Paired
    /// with property Synchronized.
    /// </summary>
    [SerializeField]
    private bool m_synchronized = true;

    /// <summary>
    /// Synchronized flag. If synchronized the connected frame will, in world,
    /// have the same transform as the reference frame. Set this to false to
    /// have full control over the transform of the connected frame.
    /// </summary>
    public bool Synchronized
    {
      get { return m_synchronized; }
      set { m_synchronized = value; }
    }

    /// <summary>
    /// Copies all values and objects from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Source</param>
    public void CopyFrom( AttachmentPair source )
    {
      if ( source == null )
        return;

      m_referenceFrame.CopyFrom( source.m_referenceFrame );
      m_connectedFrame.CopyFrom( source.m_connectedFrame );

      m_synchronized = source.m_synchronized;
    }

    /// <summary>
    /// Copies all values and objects from legacy constraint attachment pair.
    /// </summary>
    /// <param name="legacySource">Legacy constraint attachment pair.</param>
    public void CopyFrom( ConstraintAttachmentPair legacySource )
    {
      if ( legacySource == null )
        return;

      legacySource.CopyTo( this );
    }

    /// <summary>
    /// True if this attachment contains <paramref name="rb"/>.
    /// </summary>
    /// <param name="rb">Rigid body instance.</param>
    /// <returns>True if <paramref name="rb"/> is included in this attachment pair.</returns>
    public bool Contains( RigidBody rb )
    {
      return rb != null &&
             ( ( ReferenceFrame.Parent != null && ReferenceFrame.Parent.GetComponentInParent<RigidBody>() == rb ) ||
               ( ConnectedFrame.Parent != null && ConnectedFrame.Parent.GetComponentInParent<RigidBody>() == rb ) );
    }

    /// <summary>
    /// Update callback from some manager, synchronizing the frames if Synchronized == true.
    /// </summary>
    public void Update()
    {
      if ( Synchronized ) {
        m_connectedFrame.Position = m_referenceFrame.Position;
        m_connectedFrame.Rotation = m_referenceFrame.Rotation;
      }
    }

    protected override bool Initialize()
    {
      return true;
    }

    private AttachmentPair()
    {
    }
  }
}
