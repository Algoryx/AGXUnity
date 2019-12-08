using UnityEngine;

namespace AGXUnity.Models
{
  public class Track : ScriptComponent
  {
    public agxVehicle.Track Native { get; private set; } = null;

    protected override bool Initialize()
    {
      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }
  }
}
