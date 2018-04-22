using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Wind and water manager handling agxModel.WindAndWaterController,
  /// and water shapes.
  /// </summary>
  [AddComponentMenu( "" )]
  public class WindAndWaterManager : UniqueGameObject<WindAndWaterManager>
  {
    /// <summary>
    /// Native instance.
    /// </summary>
    private agxModel.WindAndWaterController m_windAndWaterController = null;

    /// <summary>
    /// Get native instance, if initialized.
    /// </summary>
    public agxModel.WindAndWaterController Native { get { return m_windAndWaterController; } }

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
          Collide.Shape[] shapes = m_water.GetComponentsInChildren<Collide.Shape>();
          if ( Native != null ) {
            foreach ( Collide.Shape shape in shapes ) {
              Native.addWater( shape.GetInitialized<Collide.Shape>().NativeGeometry );
              Native.addWaterFlowGenerator( shape.NativeGeometry, m_waterCurrentGenerator );
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
      m_windAndWaterController = new agxModel.WindAndWaterController();
      GetSimulation().add( m_windAndWaterController );

      m_waterCurrentGenerator = new agxModel.ConstantWaterFlowGenerator( WaterVelocity.ToHandedVec3() );
      m_windGenerator = new agxModel.ConstantWindGenerator( WindVelocity.ToHandedVec3() );
      m_windAndWaterController.setWindGenerator( m_windGenerator );
    
      return base.Initialize();
    }

    protected override void OnDestroy()
    {
      if ( GetSimulation() != null )
        GetSimulation().remove( m_windAndWaterController );

      m_windAndWaterController = null;

      base.OnDestroy();
    }
  }
}
