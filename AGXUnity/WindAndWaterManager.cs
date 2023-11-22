using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Wind and water manager handling agxModel.WindAndWaterController,
  /// and water shapes.
  /// </summary>
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#wind-and-water-manager" )]
  public class WindAndWaterManager : UniqueGameObject<WindAndWaterManager>
  {
    /// <summary>
    /// Get native instance, if initialized.
    /// </summary>
    public agxModel.WindAndWaterController Native { get; private set; } = null;

    /// <summary>
    /// Water game object, paired with property Water.
    /// </summary>
    [SerializeField]
    private GameObject m_water = null;

    /// <summary>
    /// Get or set game object (with shapes), defining the water.
    /// </summary>
    public GameObject Water
    {
      get { return m_water; }
      set
      {
        m_water = value;
        if ( m_water != null ) {
          var shapes = m_water.GetComponentsInChildren<Collide.Shape>();
          if ( Native != null ) {
            foreach ( var shape in shapes ) {
              Native.addWater( shape.GetInitialized<Collide.Shape>().NativeGeometry );
              Native.setWaterFlowGenerator( shape.NativeGeometry, m_waterCurrentGenerator );
            }
          }
        }
      }
    }

    /// <summary>
    /// Global water velocity, paired with property WaterVelocity.
    /// </summary>
    [SerializeField]
    private Vector3 m_waterVelocity = Vector3.zero;

    /// <summary>
    /// Global water current possible to change in realtime.
    /// </summary>
    public Vector3 WaterVelocity
    {
      get { return m_waterVelocity; }
      set
      {
        m_waterVelocity = value;

        if ( m_waterCurrentGenerator != null )
          m_waterCurrentGenerator.setVelocity( m_waterVelocity.ToHandedVec3() );
      }
    }

    /// <summary>
    /// Global wind velocity, paired with property WindVelocity.
    /// </summary>
    [SerializeField]
    private Vector3 m_windVelocity = Vector3.zero;

    /// <summary>
    /// Global wind velocity possible to change in realtime.
    /// </summary>
    public Vector3 WindVelocity
    {
      get { return m_windVelocity; }
      set
      {
        m_windVelocity = value;
        if ( m_windGenerator != null )
          m_windGenerator.setVelocity( m_windVelocity.ToHandedVec3() );
      }
    }

    private agxModel.ConstantWaterFlowGenerator m_waterCurrentGenerator = null;
    private agxModel.ConstantWindGenerator m_windGenerator = null;

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXHydrodynamics, this ) )
        return false;

      Native = new agxModel.WindAndWaterController();
      GetSimulation().add( Native );

      m_waterCurrentGenerator = new agxModel.ConstantWaterFlowGenerator( WaterVelocity.ToHandedVec3() );
      m_windGenerator = new agxModel.ConstantWindGenerator( WindVelocity.ToHandedVec3() );
      Native.setWindGenerator( m_windGenerator );
    
      return true;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().remove( Native );

      Native = null;

      base.OnDestroy();
    }
  }
}
