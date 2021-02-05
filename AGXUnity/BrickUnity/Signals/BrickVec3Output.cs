using UnityEngine;

namespace AGXUnity.BrickUnity.Signals
{
  public class BrickVec3Output : BrickOutput<Brick.Math.Vec3, Vector3>
  {
    protected override Vector3 GetSignalData(Brick.Math.Vec3 internalData)
    {
      return internalData.ToVector3();
    }
  }
}