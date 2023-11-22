using UnityEngine;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Shovel" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#shovel" )]
  public class DeformableTerrainShovel : ScriptComponent
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

    [SerializeField]
    private Line m_cuttingDirection = new Line();

    [HideInInspector]
    public Line CuttingDirection
    {
      get { return m_cuttingDirection; }
      set
      {
        m_cuttingDirection = value ?? new Line();
        if ( m_cuttingDirection.Valid && Native != null )
          Native.setCuttingDirection( m_cuttingDirection.CalculateLocalDirection( RigidBody.gameObject ).ToHandedVec3().normal() );
      }
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
             CuttingEdge.Valid &&
             CuttingDirection.Valid;
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
      if ( !CuttingDirection.Valid )
        Debug.LogWarning( "Unable to create shovel - invalid Cutting Direction.", this );

      if ( !HasValidateEdges() )
        return false;

      Native = new agxTerrain.Shovel( rb,
                                      TopEdge.ToNativeEdge( gameObject ),
                                      CuttingEdge.ToNativeEdge( gameObject ),
                                      CuttingDirection.CalculateLocalDirection( gameObject ).ToHandedVec3().normal() );

      if ( Settings == null ) {
        Settings = ScriptAsset.Create<DeformableTerrainShovelSettings>();
        Settings.name = "[Temporary]Shovel Settings";
      }

      return true;
    }

    protected override void OnDestroy()
    {
      if ( Settings != null )
        Settings.Unregister( this );

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

    private RigidBody m_rb = null;
  }
}
