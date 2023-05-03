using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Aerodynamics Parameters" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#aerodynamics-parameters" )]
  public class AerodynamicsParameters : WindAndWaterParameters<agxModel.AerodynamicsParameters>
  {
    [SerializeField]
    private bool m_aerodynamicsEnabled = true;

    public bool AerodynamicsEnabled
    {
      get { return m_aerodynamicsEnabled; }
      set
      {
        m_aerodynamicsEnabled = value;
        if ( m_objects != null && WindAndWaterManager.HasInstance ) {
          var manager = WindAndWaterManager.Instance.GetInitialized<WindAndWaterManager>().Native;
          foreach ( var shape in m_objects.Shapes ) {
            if ( shape.GetInitialized<Collide.Shape>() == null )
              continue;

            manager.setEnableAerodynamics( shape.NativeGeometry, m_aerodynamicsEnabled );
          }

          foreach ( var wire in m_objects.Wires ) {
            if ( wire.GetInitialized<Wire>() == null )
              continue;

            manager.setEnableAerodynamics( wire.Native, m_aerodynamicsEnabled );
          }

          foreach ( var cable in m_objects.Cables ) {
            if ( cable.GetInitialized<Cable>() == null )
              continue;

            manager.setEnableAerodynamics( cable.Native, m_aerodynamicsEnabled );
          }
        }
      }
    }
  }
}
