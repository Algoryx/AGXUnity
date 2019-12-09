using UnityEngine;

namespace AGXUnity.Model
{
  public class Foo
  {

  }
}

namespace AGXUnity.Models
{
  /// <summary>
  /// Track wheel model types.
  /// </summary>
  public enum TrackWheelModel
  {
    /// <summary>
    /// Geared driving wheel.
    /// </summary>
    Sprocket,
    /// <summary>
    /// Geared non-powered wheel.
    /// </summary>
    Idler,
    /// <summary>
    /// Track return or road wheel.
    /// </summary>
    Roller
  }

  public class TrackWheel : ScriptComponent
  {
    public agxVehicle.TrackWheel Native { get; private set; } = null;

    [SerializeField]
    private TrackWheelModel m_model = TrackWheelModel.Roller;

    /// <summary>
    /// Model type:
    ///   Sprocket: Geared driving wheel.
    ///   Idler: Geared non-powered wheel.
    ///   Roller: Track return or road wheel.
    /// </summary>
    [IgnoreSynchronization]
    public TrackWheelModel Model
    {
      get { return m_model; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change track wheel model type of an initialized TrackWheel instance.", this );
          return;
        }

        m_model = value;
      }
    }

    [SerializeField]
    private float m_radius = 1.0f;

    /// <summary>
    /// Track wheel radius.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change track wheel radius of an initialized TrackWheel instance.", this );
          return;
        }

        m_radius = value;
      }
    }

    [SerializeField]
    private IFrame m_frame = new IFrame();

    /// <summary>
    /// Wheel frame in RigidBody instance coordinate frame. Rotation axis
    /// is by definition y and z up.
    /// </summary>
    [IgnoreSynchronization]
    public IFrame Frame
    {
      get { return m_frame; }
    }

    [HideInInspector]
    public RigidBody RigidBody
    {
      get
      {
        if ( m_rb == null )
          m_rb = GetComponent<RigidBody>();
        return m_rb;
      }
    }

    protected override bool Initialize()
    {
      var rb = GetComponent<RigidBody>();
      if ( rb == null ) {
        Debug.LogError( "Component: TrackWheel requires RigidBody component.", this );
        return false;
      }

      if ( rb.GetInitialized<RigidBody>() == null )
        return false;

      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }

    private void Reset()
    {
      if ( RigidBody == null ) {
        Debug.LogError( "Component: TrackWheel requires RigidBody component.", this );
      }
      else {
        Radius  = Tire.FindRadius( RigidBody );
        m_frame = IFrame.Create<IFrame>( RigidBody.gameObject );

        // Up is z (labeled forward).
        var upVector = RigidBody.transform.parent != null ?
                         RigidBody.transform.parent.TransformDirection( Vector3.forward ) :
                         Vector3.forward;
        var rotationAxis = Tire.FindRotationAxisWorld( RigidBody );
        m_frame.Rotation = Quaternion.LookRotation( rotationAxis,
                                                    upVector ) * Quaternion.FromToRotation( Vector3.forward, Vector3.up );
        // This should be rotation axis anchor point.
        m_frame.LocalPosition = Vector3.zero;
      }
    }

    private RigidBody m_rb = null;
  }
}
