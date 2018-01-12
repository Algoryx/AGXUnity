using UnityEngine;

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
            foreach ( Collide.Shape shape in shapes )
              Native.addWater( shape.GetInitialized<Collide.Shape>().NativeGeometry );
          }
        }
      }
    }

    protected override bool Initialize()
    {
      m_windAndWaterController = new agxModel.WindAndWaterController();
      GetSimulation().add( m_windAndWaterController );
    
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
