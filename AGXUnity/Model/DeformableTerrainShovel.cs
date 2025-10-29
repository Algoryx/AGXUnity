using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Shovel" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#shovel" )]
  public class DeformableTerrainShovel : ScriptComponent, ISerializationCallbackReceiver
  {
    /// <summary>
    /// Native instance of this shovel.
    /// </summary>
    [HideInInspector]
    public agxTerrain.Shovel Native { get; private set; } = null;

    /// <summary>
    /// Rigid body component of this shovel.
    /// </summary>
    [HideInInspector]
    public RigidBody RigidBody { get { return m_rb ?? ( m_rb = GetComponent<RigidBody>() ); } }

    [SerializeField]
    private Line m_topEdge = new Line();

    [HideInInspector]
    public Line TopEdge
    {
      get { return m_topEdge; }
      set
      {
        m_topEdge = value ?? new Line();
        if ( m_topEdge.Valid && Native != null )
          Native.setTopEdge( m_topEdge.ToNativeEdge( RigidBody.gameObject ) );
      }
    }

    [SerializeField]
    private Line m_cuttingEdge = new Line();

    [HideInInspector]
    public Line CuttingEdge
    {
      get { return m_cuttingEdge; }
      set
      {
        m_cuttingEdge = value ?? new Line();
        if ( m_cuttingEdge.Valid && Native != null )
          Native.setCuttingEdge( m_cuttingEdge.ToNativeEdge( RigidBody.gameObject ) );
      }
    }

    [field: SerializeField]
    private bool m_hasTeeth = false;

    [HideInInspector]
    public bool HasTeeth
    {
      get => m_hasTeeth;
      set
      {
        m_hasTeeth = value;
        UpdateTeethSettings();
      }
    }

    [SerializeField]
    private Line m_cuttingDirection = null;

    [SerializeField]
    private Line m_toothDirection = new Line();

    [HideInInspector]
    public Line ToothDirection
    {
      get { return m_toothDirection; }
      set
      {
        m_toothDirection = value ?? new Line();
        UpdateTeethSettings();
      }
    }

    private void UpdateTeethSettings()
    {
      if ( Native == null )
        return;

      if ( !m_hasTeeth )
        Native.getSettings().setToothDirection( new agx.Vec3( 0 ) );
      else if ( m_toothDirection.Valid )
        Native.getSettings().setToothDirection( m_toothDirection.CalculateLocalDirection( RigidBody.gameObject ).ToHandedVec3().normal() );
    }

    [SerializeField]
    private DeformableTerrainShovelSettings m_settings = null;

    [AllowRecursiveEditing]
    public DeformableTerrainShovelSettings Settings
    {
      get { return m_settings; }
      set
      {
        if ( Native != null && m_settings != null && m_settings != value )
          m_settings.Unregister( this );

        m_settings = value;

        if ( Native != null && m_settings != null )
          m_settings.Register( this );
      }
    }

    /// <summary>
    /// Checks if top, cutting edges and cutting direction is valid.
    /// </summary>
    /// <returns>True if all edges are valid - otherwise false.</returns>
    public bool HasValidateEdges()
    {
      return TopEdge.Valid &&
             CuttingEdge.Valid;
    }

    protected override bool Initialize()
    {
      var rb = RigidBody?.GetInitialized<RigidBody>()?.Native;
      if ( rb == null ) {
        Debug.LogWarning( "Unable to find RigidBody component for DeformableTerrainShovel - shovel instance ignored.", this );
        return false;
      }

      if ( !TopEdge.Valid )
        Debug.LogWarning( "Unable to create shovel - invalid Top Edge.", this );
      if ( !CuttingEdge.Valid )
        Debug.LogWarning( "Unable to create shovel - invalid Cutting Edge.", this );
      if ( HasTeeth ) {
        if ( !ToothDirection.Valid )
          Debug.LogWarning( "Tooth direction was not configured, this might lead to incorrect simulation results", this );
        if ( Mathf.Abs( Vector3.Dot( ToothDirection.Direction, CuttingEdge.Direction ) ) > 0.05 )
          Debug.LogWarning( "Tooth direction is not orthogonal to Cutting Edge, this might lead to incorrect simulation results", this );
      }

      if ( !HasValidateEdges() )
        return false;

      Native = new agxTerrain.Shovel( rb,
                                      TopEdge.ToNativeEdge( gameObject ),
                                      CuttingEdge.ToNativeEdge( gameObject ) );

      if ( Settings == null ) {
        Settings = ScriptAsset.Create<DeformableTerrainShovelSettings>();
        Settings.name = "[Temporary]Shovel Settings";
      }

      Simulation.Instance.Native.add( Native );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( Settings != null )
        Settings.Unregister( this );

      if ( Simulation.HasInstance )
        Simulation.Instance.Native.remove( Native );

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      if ( Native != null )
        Native.setEnable( true );
    }

    protected override void OnDisable()
    {
      if ( Native != null )
        Native.setEnable( false );
    }

    private void Reset()
    {
      if ( GetComponent<RigidBody>() == null )
        Debug.LogError( "Component: DeformableTerrainShovel requires RigidBody component.", this );
    }

    public void OnBeforeSerialize() { }

    /// <summary>
    /// Unfortunately we cannot check directly against null since unity will construct a default initialized object when "null" is value-serialized.
    /// Use this method instead to check whether the frames of the cutting direction was serilaized as "null".
    /// </summary>
    private bool IsNullFrame( IFrame frame )
    {
      return
        frame.Parent == null &&
        frame.LocalPosition == Vector3.zero &&
        frame.LocalRotation == Quaternion.identity;
    }

    public void OnAfterDeserialize()
    {
      // See not above
      if ( !IsNullFrame( m_cuttingDirection.Start ) || !IsNullFrame( m_cuttingDirection.End ) ) {
        m_hasTeeth = true;
        m_toothDirection = m_cuttingDirection;
        m_cuttingDirection = null;
      }
    }

    private RigidBody m_rb = null;
  }
}
