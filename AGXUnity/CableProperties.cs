using System;
using UnityEngine;

namespace AGXUnity
{
  [Serializable]
  public class CableProperty
  {
    public static CableProperty Create( CableProperties.Direction dir )
    {
      CableProperty property = new CableProperty();
      property.Direction = dir;

      return property;
    }

    public Action<CableProperties.Direction> OnValueCanged = delegate { };

    [SerializeField]
    private CableProperties.Direction m_direction = CableProperties.Direction.Bend;
    public CableProperties.Direction Direction
    {
      get { return m_direction; }
      private set { m_direction = value; }
    }

    [SerializeField]
    private float m_youngsModulus = 1.0E9f;
    [ClampAboveZeroInInspector]
    public float YoungsModulus
    {
      get { return m_youngsModulus; }
      set
      {
        m_youngsModulus = value;

        OnValueCanged( Direction );
      }
    }

    [SerializeField]
    private float m_poissonsRatio = 0.333f;

    [HideInInspector]
    public float PoissonsRatio
    {
      get
      {
        // This is rendered in the Inspector by CablePropertiesEditor
        // in CableTool and will only show up for the Twist direction,
        // where it's used.
        return m_poissonsRatio;
      }
      set
      {
        // Poisson's ratio at -1.0 will result in a division by 0.
        m_poissonsRatio = Utils.Math.Clamp( value, -0.999f, 0.5f );
        OnValueCanged( Direction );
      }
    }

    [SerializeField]
    private float m_yieldPoint = float.PositiveInfinity;

    [ClampAboveZeroInInspector( true )]
    public float YieldPoint
    {
      get { return m_yieldPoint; }
      set
      {
        m_yieldPoint = value;

        OnValueCanged( Direction );
      }
    }

    [SerializeField]
    private float m_damping = 2.0f / 50;

    [ClampAboveZeroInInspector( true )]
    public float Damping
    {
      get { return m_damping; }
      set
      {
        m_damping = value;

        OnValueCanged( Direction );
      }
    }
  }

  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-properties" )]
  public class CableProperties : ScriptAsset
  {
    public enum Direction
    {
      Bend,
      Twist,
      Stretch
    }

    public static Array Directions { get { return Enum.GetValues( typeof( Direction ) ); } }

    public static agxCable.Direction ToNative( Direction dir )
    {
      return (agxCable.Direction)dir;
    }

    [SerializeField]
    private CableProperty[] m_properties = new CableProperty[ Enum.GetValues( typeof( Direction ) ).Length ];
    public CableProperty this[ Direction dir ]
    {
      get { return m_properties[ (int)dir ]; }
      private set { m_properties[ (int)dir ] = value; }
    }

    public Action<Direction> OnPropertyUpdated = delegate { };

    public CableProperties RestoreLocalDataFrom( agxCable.CableProperties native, agxCable.CablePlasticity plasticity )
    {
      if ( native == null )
        return this;

      foreach ( Direction dir in Directions ) {
        this[ dir ].YoungsModulus = Convert.ToSingle( native.getYoungsModulus( ToNative( dir ) ) );
        this[ dir ].Damping       = Convert.ToSingle( native.getDamping( ToNative( dir ) ) );
        this[ dir ].PoissonsRatio = Convert.ToSingle( native.getPoissonsRatio( ToNative( dir ) ) );
        this[ dir ].YieldPoint    = plasticity != null ?
                                      Convert.ToSingle( plasticity.getYieldPoint( ToNative( dir ) ) ) :
                                      float.PositiveInfinity;
      }

      return this;
    }

    public bool IsListening( Cable cable )
    {
      var invocationList = OnPropertyUpdated.GetInvocationList();
      foreach ( var listener in invocationList )
        if ( cable.Equals( listener.Target ) )
          return true;

      return false;
    }

    public override void Destroy()
    {
      foreach ( Direction dir in Directions )
        this[ dir ].OnValueCanged -= OnPropertyChanged;
    }

    protected override void Construct()
    {
      foreach ( Direction dir in Directions )
        this[ dir ] = CableProperty.Create( dir );
    }

    protected override bool Initialize()
    {
      foreach ( Direction dir in Directions )
        this[ dir ].OnValueCanged += OnPropertyChanged;

      return true;
    }

    private void OnPropertyChanged( Direction dir )
    {
      OnPropertyUpdated( dir );
    }
  }
}
