using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Constraint attachments for two objects - a reference object and a connected.
  /// The frame of the reference object, the reference frame, is by default the
  /// frame the constraint will be created from. It's possible to detach the relation
  /// between the frames, setting Synchronized to false.
  /// </summary>
  [AddComponentMenu( "" )]
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
      set
      {
        m_referenceFrame = value;
        Synchronize();
      }
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
      set
      {
        m_connectedFrame = value;
        Synchronize();
      }
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
    /// True if <paramref name="rb1"/> and <paramref name="rb2"/> are part
    /// of this attachment pair - otherwise false.
    /// </summary>
    /// <param name="rb1">First rigid body.</param>
    /// <param name="rb2">Second rigid body.</param>
    /// <returns>True if both rigid bodies are part of this attachment pair.</returns>
    public bool Match( RigidBody rb1, RigidBody rb2 )
    {
      return Contains( rb1 ) && Contains( rb2 );
    }

    /// <summary>
    /// Finds other rigid body or null if attached in world. Throws
    /// exception if <paramref name="rb"/> isn't part of this attachment.
    /// </summary>
    /// <param name="rb">Rigid body instance part of this attachment.</param>
    /// <returns>Other rigid body or null if attached in world.</returns>
    public RigidBody Other( RigidBody rb )
    {
      var rbRef = ReferenceFrame.Parent != null ?
                    ReferenceFrame.Parent.GetComponentInParent<RigidBody>() :
                    null;
      var rbCon = ConnectedFrame.Parent != null ?
                    ConnectedFrame.Parent.GetComponentInParent<RigidBody>() :
                    null;
      if ( rb == null || ( rb != rbRef && rb != rbCon ) )
        throw new Exception( "AttachmentPair.Other: Subject rigid body isn't part of this attachment pair." );
      return rbRef == rb ? rbCon : rbRef;
    }

    /// <summary>
    /// Update callback from some manager, synchronizing the frames if Synchronized == true.
    /// </summary>
    public void Synchronize()
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

    protected virtual void Reset()
    {
      hideFlags |= HideFlags.HideInInspector;
    }

    private AttachmentPair()
    {
    }
  }
}
