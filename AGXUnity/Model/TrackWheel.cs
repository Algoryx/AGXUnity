using System;
using UnityEngine;

namespace AGXUnity.Model
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

  [Flags]
  public enum TrackWheelProperty
  {
    None = 0,
    MergeNodes = agxVehicle.TrackWheel.Property.MERGE_NODES,
    SplitSegments = agxVehicle.TrackWheel.Property.SPLIT_SEGMENTS,
    MoveNodesToRotationPlane = agxVehicle.TrackWheel.Property.MOVE_NODES_TO_ROTATION_PLANE,
    MoveNodesToWheel = agxVehicle.TrackWheel.Property.MOVE_NODES_TO_WHEEL
  }

  [AddComponentMenu( "AGXUnity/Model/Track Wheel" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#track-wheel" )]
  public class TrackWheel : ScriptComponent
  {
    /// <summary>
    /// Adds TrackWheel component to <paramref name="parent"/> and configures
    /// the wheel properties given shapes and meshes.
    /// </summary>
    /// <param name="parent">Parent game object, will become TrackWheel.Frame.Parent.</param>
    /// <param name="positionAlongRotationAxis">Position offset along the rotation axis. Default: 0.</param>
    /// <returns>TrackWheel component if added, otherwise null (throws if <paramref name="parent"/> already has a TrackWheel component)</returns>
    public static TrackWheel Create( GameObject parent, float positionAlongRotationAxis = 0.0f )
    {
      if ( parent == null )
        return null;

      return parent.AddComponent<TrackWheel>().Configure( parent, positionAlongRotationAxis );
    }

    /// <summary>
    /// Finds TrackWheelModel given name (case insensitive). If the name contains
    /// "sprocket" results in TrackWheelModel.Sprocket, "idler" in TrackWheelModel.Idler
    /// and "roller" in TrackWheelModel.Roller.
    /// </summary>
    /// <param name="name">Name of the object.</param>
    /// <param name="model">Output track wheel model if found.</param>
    /// <param name="nameModelPredicate">Match name vs. model - true if name match the model, otherwise false.</param>
    /// <returns>True if <paramref name="model"/> is set given <paramref name="name"/>, otherwise false.</returns>
    public static bool TryFindModel( string name,
                                     ref TrackWheelModel model,
                                     Func<string, TrackWheelModel, bool> nameModelPredicate = null )
    {
      if ( string.IsNullOrEmpty( name ) )
        return false;

      var predicate = nameModelPredicate ?? new Func<string, TrackWheelModel, bool>( (n, m) => false );
      name = name.ToLower();
      if ( name.Contains( "sprocket" ) || predicate( name, TrackWheelModel.Sprocket ) ) {
        model = TrackWheelModel.Sprocket;
        return true;
      }
      else if ( name.Contains( "idler" ) || predicate( name, TrackWheelModel.Idler ) ) {
        model = TrackWheelModel.Idler;
        return true;
      }
      else if ( name.Contains( "roller" ) || predicate( name, TrackWheelModel.Roller ) ) {
        model = TrackWheelModel.Roller;
        return true;
      }

      return false;
    }

    /// <summary>
    /// Native instance, created in Initialize.
    /// </summary>
    public agxVehicle.TrackWheel Native { get; private set; } = null;

    /// <summary>
    /// Converts TrackWheelModel to agxVehicle.TrackWheel.Model.
    /// </summary>
    /// <param name="model">TrackWheelModel type.</param>
    /// <returns>agxVehicle.TrackWheel.Model of TrackWheelModel.</returns>
    public static agxVehicle.TrackWheel.Model ToNative( TrackWheelModel model )
    {
      return (agxVehicle.TrackWheel.Model)(int)model;
    }

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

        // Unsure about changing properties here if the user has specified
        // something different but MergeNodes is important.
        if ( m_model != value ) {
          if ( value == TrackWheelModel.Sprocket || value == TrackWheelModel.Idler )
            Properties = TrackWheelProperty.MergeNodes;
          else
            Properties = TrackWheelProperty.None;
        }

        m_model = value;
      }
    }

    [SerializeField]
    private TrackWheelProperty m_properties = TrackWheelProperty.None;

    /// <summary>
    /// Track wheel properties. Sprockets and idlers has MergeNodes and
    /// rollers None. These properties will be overridden when changing
    /// model.
    /// </summary>
    public TrackWheelProperty Properties
    {
      get { return m_properties; }
      set
      {
        m_properties = value;
        if ( Native != null ) {
          foreach ( agxVehicle.TrackWheel.Property eValue in typeof( agxVehicle.TrackWheel.Property ).GetEnumValues() )
            Native.setEnableProperty( eValue, ((int)m_properties & (int)eValue) != 0 );
        }
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
          m_rb = Frame.Parent?.GetComponentInParent<RigidBody>();
        return m_rb;
      }
    }

    /// <summary>
    /// Configure this component to <paramref name="parent"/> and configures
    /// the wheel properties given shapes and meshes.
    /// </summary>
    /// <param name="parent">Parent game object, will become TrackWheel.Frame.Parent.</param>
    /// <param name="positionAlongRotationAxis">Position offset along the rotation axis. Default: 0.</param>
    /// <param name="nameModelPredicate">Match name vs. model - true if name match the model, otherwise false.</param>
    /// <returns>TrackWheel component if added, otherwise null (throws if <paramref name="parent"/> already has a TrackWheel component)</returns>
    public TrackWheel Configure( GameObject parent,
                                 float positionAlongRotationAxis = 0.0f,
                                 Func<string, TrackWheelModel, bool> nameModelPredicate = null )
    {
      // What if we don't have a rigid body in hierarchy? Initialize will fail.
      m_rb = parent.GetComponentInParent<RigidBody>();
      m_frame.SetParent( parent );

      Radius = Tire.FindRadius( parent, true );

      // Up is z.
      var upVector          = parent.transform.parent != null ?
                                parent.transform.parent.TransformDirection( Vector3.up ) :
                                Vector3.up;
      var rotationAxis      = Tire.FindRotationAxisWorld( parent );
      m_frame.Rotation      = Quaternion.FromToRotation( Vector3.up, rotationAxis );
      m_frame.Rotation     *= Quaternion.Euler( 0, Vector3.Angle( m_frame.Rotation * Vector3.forward, upVector ), 0 );
      m_frame.LocalPosition = positionAlongRotationAxis * Vector3.up;

      var model = TrackWheelModel.Roller;
      if ( TryFindModel( parent.name, ref model, nameModelPredicate ) ||
           TryFindModel( RigidBody?.name, ref model, nameModelPredicate ) )
        Model = model;
      else
        Model = TrackWheelModel.Roller;

      return this;
    }

    protected override bool Initialize()
    {
      if ( RigidBody == null ) {
        Debug.LogError( "Component: TrackWheel requires RigidBody component in hierarchy.", this );
        return false;
      }
      else if ( RigidBody.GetInitialized<RigidBody>() == null ) {
        // Assuming RigidBody is printing relevant error message.
        return false;
      }

      Native = new agxVehicle.TrackWheel( ToNative( Model ),
                                          Radius,
                                          RigidBody.Native,
                                          Frame.NativeMatrix * RigidBody.Native.getTransform().inverse() );

      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }

    private void Reset()
    {
      // We cannot use this.RigidBody when it used frame parent and
      // parent of the frame is set in Configure.
      if ( GetComponentInParent<RigidBody>() == null )
        Debug.LogError( "Component: TrackWheel requires RigidBody component in hierarchy.", this );
    }

    private RigidBody m_rb = null;
  }
}
