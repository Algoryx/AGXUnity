using UnityEngine;

namespace AGXUnity.Model
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#steering-parameters" )]
  public class SteeringParameters : ScriptAsset
  {
    public agxVehicle.SteeringParameters Native { get; private set; }

    /// <summary>
    /// The steering mechanism that this set of steering parameters is inteded for
    /// </summary>
    [field: SerializeField]
    [HideInInspector]
    public Steering.SteeringMechanism Mechanism { get; set; } = Steering.SteeringMechanism.Ackermann;

    private bool Show_alpha0 => Mechanism == Steering.SteeringMechanism.BellCrank || Mechanism == Steering.SteeringMechanism.RackPinion;
    private bool Show_lc => Mechanism != Steering.SteeringMechanism.Ackermann;
    private bool Show_lr => Mechanism == Steering.SteeringMechanism.RackPinion || Mechanism == Steering.SteeringMechanism.Davis;
    private bool Show_side => Mechanism == Steering.SteeringMechanism.Ackermann;

    [SerializeField]
    private float m_phi0 = -105.0f * Mathf.Deg2Rad;

    [Tooltip( "Specifies the initial angle of between the wheel axis and the kingpin rod." )]
    public float Phi0
    {
      get => m_phi0;
      set
      {
        m_phi0 = value;
        if ( Native != null )
          Native.phi0 = m_phi0;
      }
    }

    [SerializeField]
    private float m_l = 0.16f;

    [ClampAboveZeroInInspector]
    [Tooltip( "Specifies the length of the kingpin rod." )]
    public float L
    {
      get => m_l;
      set
      {
        m_l = value;
        if ( Native != null )
          Native.l = m_l;
      }
    }

    [SerializeField]
    private float m_alpha0 = 76 * Mathf.Deg2Rad;

    [DynamicallyShowInInspector( "Show_alpha0" )]
    [Tooltip( "Specifies the initial angle between the wheel axis and the tie rod." )]
    public float Alpha0
    {
      get => Mechanism == Steering.SteeringMechanism.Davis ? ( Mathf.PI - Phi0 ) : m_alpha0;
      set
      {
        m_alpha0 = value;
        if ( Mechanism == Steering.SteeringMechanism.Davis ) {
          if ( !IsSynchronizingProperties )
            Debug.LogWarning( "Davis steering model has tie rod angle (PI - Phi0). Set value will be ignored" );
        }
        else if ( Native != null )
          Native.alpha0 = m_alpha0;
      }
    }

    [SerializeField]
    private float m_lc = 1.0f;

    [ClampAboveZeroInInspector]
    [DynamicallyShowInInspector( "Show_lc" )]
    [Tooltip( "Specifies the length of the steering arm." )]
    public float Lc
    {
      get => m_lc;
      set
      {
        m_lc = value;
        if ( Native != null )
          Native.lc = m_lc;
      }
    }

    [SerializeField]
    private float m_lr = 0.25f;

    [ClampAboveZeroInInspector]
    [FloatSliderInInspector( 0, 1 )]
    [DynamicallyShowInInspector( "Show_lr" )]
    [Tooltip( "Specifies the length of the rack." )]
    public float Lr
    {
      get => Mechanism == Steering.SteeringMechanism.BellCrank ? 0.0f : m_lr;
      set
      {
        m_lr = value;
        if ( Mechanism == Steering.SteeringMechanism.BellCrank ) {
          if ( !IsSynchronizingProperties )
            Debug.LogWarning( "Bell-Crank steering model has implicit rack length (0.0). Set value will be ignored" );
        }
        else if ( Native != null )
          Native.lr = m_lr;
      }
    }

    [SerializeField]
    private float m_gear = 1.0f;

    [Tooltip( "The gear ratio between the steering wheel rotation and the steering arm rotation." )]
    public float Gear
    {
      get => m_gear;
      set
      {
        m_gear = value;
        if ( Native != null )
          Native.gear = m_gear;
      }
    }

    /// <summary>
    /// Specifies a side on which the steering arm is located.
    /// </summary>
    public enum SteeringArmSide
    {
      Left = 0,
      Right = 1
    };

    [SerializeField]
    private SteeringArmSide m_side = SteeringArmSide.Left;

    [DynamicallyShowInInspector( "Show_side" )]
    [Tooltip( "When using the Ackermann steering mechanism, this parameter decides which side the steering arm is located on." )]
    public SteeringArmSide Side
    {
      get => m_side;
      set
      {
        m_side = value;
        if ( Native != null )
          Native.side = (ulong)m_side;
      }
    }

    /// <summary>
    /// Assigns the default values to each parameter given the current steering mechanism.
    /// </summary>
    public void AssignDefaults()
    {
      agxVehicle.SteeringParameters tmp = Mechanism switch
      {
        Steering.SteeringMechanism.Ackermann => agxVehicle.SteeringParameters.Ackermann(),
        Steering.SteeringMechanism.BellCrank => agxVehicle.SteeringParameters.BellCrank(),
        Steering.SteeringMechanism.RackPinion => agxVehicle.SteeringParameters.RackPinion(),
        Steering.SteeringMechanism.Davis => agxVehicle.SteeringParameters.Davis(),
        _ => null
      };

      if ( tmp == null ) {
        Debug.LogError( "Failed to find default steering parameters" );
        return;
      }

      Phi0      = (float)tmp.phi0;
      L         = (float)tmp.l;
      Lc        = (float)tmp.lc;
      Gear      = (float)tmp.gear;
      Side      = (SteeringArmSide)tmp.side;
      // Assign these directly to avoid warnings when setting implicit parameters
      m_lr      = (float)tmp.lr;
      m_alpha0  = (float)tmp.alpha0;
    }

    public override void Destroy()
    {
      Native = null;
    }

    protected override void Construct()
    {
    }

    protected void Reset()
    {
      AssignDefaults();
    }

    protected override bool Initialize()
    {
      Native = new agxVehicle.SteeringParameters();
      return true;
    }
  }
}
