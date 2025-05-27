using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Hydrodynamics Parameters" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#hydrodynamics-parameters" )]
  public class HydrodynamicsParameters : WindAndWaterParameters<agxModel.HydrodynamicsParameters>
  {
    protected override void OnDisable()
    {
      if ( State == States.INITIALIZED && !enabled )
        Debug.LogWarning( "Disabling an initialized HydrodynamicsParameters at runtime is not supported" );
      base.OnDisable();
    }
  }
}
