using AGXUnity.Rendering;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Cable Damage" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( AGXUnity.Cable ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-damage" )]
  public class CableDamage : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable damage.
    /// </summary>
    public agxCable.CableDamage Native { get; private set; }

    [System.NonSerialized]
    private Cable m_cable = null;

    /// <summary>
    /// The Cable ScriptComponent that this CableDamage follows
    /// </summary>
    [HideInInspector]
    public Cable Cable { get { return m_cable ?? ( m_cable = GetComponent<Cable>() ); } }

    [System.NonSerialized]
    private CableRenderer m_cableRenderer = null;

    /// <summary>
    /// The CableRenderer ScriptComponent that might be used to render the damages
    /// </summary>
    [HideInInspector]
    public CableRenderer CableRenderer { get { return m_cableRenderer ?? ( m_cableRenderer = GetComponent<CableRenderer>() ); } }

    /// <summary>
    /// Will use the first CableRenderer component instance on this object to render the cable damage if active.
    /// </summary>
    [SerializeField]
    private bool m_renderCableDamage = false;
    public bool RenderCableDamage
    {
      get { return m_renderCableDamage; }
      set
      {
        m_renderCableDamage = value;
        if ( CableRenderer == null )
          Debug.LogWarning( "No CableRenderer to use for rendering cable damages" );
        else
          CableRenderer.SetRenderDamages( value && m_properties != null );
      }
    }

    /// <summary>
    /// ScriptableAsset with weights of each of the cable damage components, determining which one contributes how much to the cable damage score
    /// </summary>
    [SerializeField]
    private CableDamageProperties m_properties = null;

    [AllowRecursiveEditing]
    public CableDamageProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( Native != null && m_properties != null && m_properties != value )
          m_properties.Unregister( this );

        m_properties = value;

        if ( Native != null && m_properties != null )
          m_properties.Register( this );

        if ( CableRenderer == null )
          Debug.LogWarning( "No CableRenderer to use for rendering cable damages" );
        else
          CableRenderer.SetRenderDamages( m_properties != null && m_renderCableDamage );
      }
    }

    [System.NonSerialized]
    private agxCable.SegmentDamagePtrVector m_currentDamages;
    public agxCable.SegmentDamagePtrVector CurrentDamages => m_currentDamages;

    [System.NonSerialized]
    private agxCable.SegmentDamagePtrVector m_accumulatedDamages;
    public agxCable.SegmentDamagePtrVector AccumulatedDamages => m_accumulatedDamages;

    private float[] m_damageValues = new float[ 0 ];
    public float DamageValue( int index ) => index < m_damageValues.Length ? m_damageValues[ index ] : 0;
    [HideInInspector]
    public int DamageValueCount => m_damageValues.Length;

    private float m_maxDamage = 0;
    [HideInInspector]
    public float MaxDamage => m_properties != null
                                ? ( m_properties.DamageColorMode == CableDamageProperties.MaxDamageColorMode.HighestPerFrame ? m_maxDamage : m_properties.SetDamageForMaxColor )
                                : 0;

    protected override bool Initialize()
    {
      Native = new agxCable.CableDamage();

      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable == null ) {
        Debug.LogWarning( "Unable to find Cable component for CableDamage - cable damage instance ignored.", this );
        return false;
      }

      cable.addComponent( Native );

      if ( Properties == null ) {
        Properties = ScriptAsset.Create<CableDamageProperties>();
        Properties.name = "Cable Damage Properties";
      }

      return true;
    }

    void Update()
    {
      if ( !enabled )
        return;

      m_currentDamages = Native.getCurrentDamages();
      m_accumulatedDamages = Native.getAccumulatedDamages();

      if ( m_properties == null ) {
        Debug.LogWarning( "No CableDamageProperties set - no Cable Damage Calculated!" );
        return;
      }

      int count = m_properties.DamageType == CableDamageProperties.DamageTypeMode.CurrentDamage ? m_currentDamages.Count : m_accumulatedDamages.Count;

      if ( RenderCableDamage && CableRenderer ) {
        if ( m_damageValues.Length != count )
          m_damageValues = new float[ count ];

        m_maxDamage = float.MinValue;
        for ( int i = 0; i < count; i++ ) {
          float value = m_properties.DamageType == CableDamageProperties.DamageTypeMode.CurrentDamage ? (float)m_currentDamages[ i ].total() : (float)m_accumulatedDamages[ i ].total();
          m_maxDamage = Mathf.Max( m_maxDamage, value );
          m_damageValues[ i ] = value;
        }
      }
    }

    protected override void OnDestroy()
    {
      if ( Properties != null )
        Properties.Unregister( this );

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      RenderCableDamage = RenderCableDamage;
    }

    protected override void OnDisable()
    {
      CableRenderer.SetRenderDamages( false );
    }

    private void Reset()
    {
      if ( GetComponent<Cable>() == null )
        Debug.LogError( "Component: CableDamage requires Cable component.", this );
    }
  }
}
