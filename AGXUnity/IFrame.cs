using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [Serializable]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#frames" )]
  public class IFrame
  {
    /// <summary>
    /// Construct an IFrame given parent game object, local position to parent and
    /// local rotation to parent.
    /// </summary>
    /// <param name="parent">Parent game object - world if null.</param>
    /// <param name="localPosition">Position in parent frame. If parent is null this is the position in world frame.</param>
    /// <param name="localRotation">Rotation in parent frame. If parent is null this is the rotation in world frame.</param>
    /// <returns>IFrame instance.</returns>
    public static T Create<T>( GameObject parent = null,
                               Vector3 localPosition = default( Vector3 ),
                               Quaternion localRotation = default( Quaternion ) )
      where T : IFrame, new()
    {
      T frame = new T();

      if ( object.Equals( localRotation, default( Quaternion ) ) )
        localRotation = Quaternion.identity;

      frame.SetParent( parent );
      frame.LocalPosition = localPosition;
      frame.LocalRotation = localRotation;

      return frame;
    }

    [SerializeField]
    private GameObject m_parent = null;
    /// <summary>
    /// Current parent.
    /// </summary>
    public GameObject Parent { get { return m_parent; } }

    [SerializeField]
    private Vector3 m_localPosition = Vector3.zero;
    /// <summary>
    /// Local position to parent. Same as world if parent == null.
    /// </summary>
    public Vector3 LocalPosition { get { return m_localPosition; } set { m_localPosition = value; } }

    [SerializeField]
    private Quaternion m_localRotation = Quaternion.identity;
    /// <summary>
    /// Local rotation to parent. Same as world if parent == null.
    /// </summary>
    public Quaternion LocalRotation { get { return m_localRotation; } set { m_localRotation = value; } }

    /// <summary>
    /// Current position of this frame.
    /// </summary>
    public Vector3 Position
    {
      get { return CalculateWorldPosition( Parent, LocalPosition ); }
      set
      {
        LocalPosition = CalculateLocalPosition( Parent, value );
      }
    }

    /// <summary>
    /// Current rotation of this frame.
    /// </summary>
    public Quaternion Rotation
    {
      get { return CalculateWorldRotation( Parent, LocalRotation ); }
      set
      {
        LocalRotation = CalculateLocalRotation( Parent, value );
      }
    }

    /// <summary>
    /// Native matrix representation of this frame.
    /// </summary>
    public agx.AffineMatrix4x4 NativeMatrix
    {
      get
      {
        return new agx.AffineMatrix4x4( Rotation.ToHandedQuat(),
                                        Position.ToHandedVec3() );
      }
    }

    /// <summary>
    /// Local native matrix representation of this frame.
    /// </summary>
    public agx.AffineMatrix4x4 NativeLocalMatrix
    {
      get
      {
        return new agx.AffineMatrix4x4( LocalRotation.ToHandedQuat(),
                                        LocalPosition.ToHandedVec3() );
      }
    }

    /// <summary>
    /// Default constructor with world (null) as parent.
    /// </summary>
    public IFrame()
      : this( null )
    {
    }

    /// <summary>
    /// Construct given a parent.
    /// </summary>
    /// <param name="parent">Parent object.</param>
    public IFrame( GameObject parent )
      : base()
    {
      m_parent = parent;
    }

    /// <summary>
    /// Copy values/objects from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Source.</param>
    public void CopyFrom( IFrame source )
    {
      if ( source == null )
        return;

      m_localPosition = source.m_localPosition;
      m_localRotation = source.m_localRotation;
      m_parent = source.m_parent;
    }

    /// <summary>
    /// Assign new parent and choose whether the frame will "jump" to
    /// the new object, i.e., keep local transform (inheritWorldTransform = false),
    /// or to calculate a new local transform given the new parent with
    /// inheritWorldTransform = true.
    /// </summary>
    /// <param name="parent">New parent.</param>
    /// <param name="inheritWorldTransform">If true, new local transform will be calculated.
    ///                                     If false, local transform is preserved and new world transform.</param>
    public void SetParent( GameObject parent, bool inheritWorldTransform = true )
    {
      if ( parent == Parent )
        return;

      // New local position/rotation given current world transform.
      if ( inheritWorldTransform ) {
        Vector3 worldPosition = Position;
        Quaternion worldRotation = Rotation;

        m_parent = parent;

        LocalPosition = CalculateLocalPosition( Parent, worldPosition );
        LocalRotation = CalculateLocalRotation( Parent, worldRotation );
      }
      // New world position/rotation given current local transform.
      else {
        m_parent = parent;
      }
    }

    /// <summary>
    /// Calculates current world position in <paramref name="gameObject"/> local frame.
    /// </summary>
    /// <returns></returns>
    public Vector3 CalculateLocalPosition( GameObject gameObject )
    {
      return CalculateLocalPosition( gameObject, Position );
    }

    /// <summary>
    /// Calculate current world rotation in <paramref name="gameObject"/> local frame.
    /// </summary>
    /// <returns></returns>
    public Quaternion CalculateLocalRotation( GameObject gameObject )
    {
      return CalculateLocalRotation( gameObject, Rotation );
    }

    public T GetInitialized<T>()
      where T : IFrame
    {
      if ( m_state == State.INITIALIZING )
        throw new Exception( "Initialize call when object is being initialized. Implement wait until initialized?" );

      if ( m_state == State.CONSTRUCTED ) {
        m_state = State.INITIALIZING;
        m_state = Initialize() ? State.INITIALIZED : State.CONSTRUCTED;
      }

      return m_state == State.INITIALIZED ? this as T : null;
    }

    public virtual void OnDestroy()
    {
      m_state = State.DESTROYED;
    }

    protected virtual bool Initialize()
    {
      return true;
    }

    protected enum State
    {
      CONSTRUCTED,
      INITIALIZING,
      INITIALIZED,
      DESTROYED
    }

    private State m_state = State.CONSTRUCTED;

    public static Vector3 CalculateLocalPosition( GameObject gameObject, Vector3 worldPosition )
    {
      if ( gameObject == null )
        return worldPosition;

      return gameObject.transform.InverseTransformDirection( worldPosition - gameObject.transform.position );
    }

    public static Vector3 CalculateWorldPosition( GameObject gameObject, Vector3 localPosition )
    {
      if ( gameObject == null )
        return localPosition;

      return gameObject.transform.position + gameObject.transform.TransformDirection( localPosition );
    }

    public static Quaternion CalculateLocalRotation( GameObject gameObject, Quaternion worldRotation )
    {
      if ( gameObject == null )
        return worldRotation;

#if UNITY_2018_1_OR_NEWER
      return ( Quaternion.Inverse( gameObject.transform.rotation ) * worldRotation ).normalized;
#else
      return ( Quaternion.Inverse( gameObject.transform.rotation ) * worldRotation ).Normalize();
#endif
    }

    public static Quaternion CalculateWorldRotation( GameObject gameObject, Quaternion localRotation )
    {
      if ( gameObject == null )
        return localRotation;

#if UNITY_2018_1_OR_NEWER
      return ( gameObject.transform.rotation * localRotation ).normalized;
#else
      return ( gameObject.transform.rotation * localRotation ).Normalize();
#endif
    }
  }
}
