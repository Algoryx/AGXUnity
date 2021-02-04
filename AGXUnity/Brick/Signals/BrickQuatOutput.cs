using UnityEngine;

public class BrickQuatOutput : BrickOutput<Brick.Math.Quat, Quaternion>
{
  protected override Quaternion GetSignalData(Brick.Math.Quat internalData)
  {
    return internalData.ToQuaternion();
  }
}
