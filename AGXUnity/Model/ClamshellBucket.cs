using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Clamshell Bucket" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#clamshell-buckets" )]
  public class ClamshellBucket : ScriptComponent
  {
    public agxTerrain.ClamShellBucket Native { get; private set; } = null;

    [field: SerializeField]
    public DeformableTerrainShovel Shovel1 { get; set; } = null;

    [field: SerializeField]
    public DeformableTerrainShovel Shovel2 { get; set; } = null;

    [SerializeField]
    private float m_closedThreshold = 0.1f;
    public float ClosedThreshold
    {
      get => m_closedThreshold;
      set
      {
        m_closedThreshold = value;
        if ( Native != null )
          Native.setClosedThreshold( value );
      }
    }

    protected override bool Initialize()
    {
      var s1 = Shovel1?.GetInitialized<DeformableTerrainShovel>()?.Native;
      var s2 = Shovel2?.GetInitialized<DeformableTerrainShovel>()?.Native;
      if ( s1 == null || s2 == null ) {
        Debug.LogError( "Failed to initialize one of the provided clamshell halves!" );
        return false;
      }

      Native = new agxTerrain.ClamShellBucket( s1, s2 );
      Simulation.Instance.Native.add( Native );
      return true;
    }
  }
}
