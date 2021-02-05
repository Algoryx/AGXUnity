using UnityEngine;

namespace AGXUnity.BrickUnity.Signals
{
  public class BrickQuatOutput : BrickOutput<Brick.Math.Quat, Quaternion>
  {
    protected override Quaternion GetSignalData(Brick.Math.Quat internalData)
    {
      return internalData.ToQuaternion();
    }
  }
}