using UnityEngine;

namespace AGXUnity.Model
{
  public class SteeringParameters : ScriptAsset
  {
    public agxVehicle.SteeringParameters Native { get; private set; }

    [field: SerializeField]
    [HideInInspector]
    public Steering.SteeringMechanism Mechanism { get; set; } = Steering.SteeringMechanism.Ackermann;

    private bool Show_alpha0 => Mechanism == Steering.SteeringMechanism.BellCrank || Mechanism == Steering.SteeringMechanism.RackPinion;
    private bool Show_lc => Mechanism != Steering.SteeringMechanism.Ackermann;
    private bool Show_lr => Mechanism == Steering.SteeringMechanism.RackPinion || Mechanism == Steering.SteeringMechanism.Davis;
    private bool Show_side => Mechanism == Steering.SteeringMechanism.Ackermann;

    [ SerializeField ]
    private float m_phi0 = -105.0f * Mathf.Deg2Rad;

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

    public enum SteeringArmSide
    {
      Left = 0,
      Right = 1
    };

    [SerializeField]
    private SteeringArmSide m_side = SteeringArmSide.Left;

    [DynamicallyShowInInspector( "Show_side" )]
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

    [AGXUnity.InvokableInInspector]
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

      Phi0    = (float)tmp.phi0;
      L       = (float)tmp.l;
      Alpha0  = (float)tmp.alpha0;
      Lc      = (float)tmp.lc;
      Lr      = (float)tmp.lr;
      Gear    = (float)tmp.gear;
      Side    = (SteeringArmSide)tmp.side;
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
