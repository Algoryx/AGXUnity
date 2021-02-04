using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrickTransformOutput : BrickOutput<Brick.Scene.Transform, Vector3>
{
  public Quaternion displayData2;

  protected override Vector3 GetSignalData(Brick.Scene.Transform internalData)
  {
    return internalData.Position.ToVector3();
  }

  protected override void Update()
  {
    base.Update();
    displayData2 = signal.GetData().Rotation.ToQuaternion();
  }
}
