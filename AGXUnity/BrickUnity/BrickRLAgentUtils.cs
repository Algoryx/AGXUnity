using System.Collections.Generic;
using UnityEngine;

using AGXUnity.BrickUnity;

using B_Signal = Brick.Signal;
using B_Agent = Brick.MachineLearning.RLAgent;

public static class BrickRLAgentUtils
{
  public static Dictionary<string, Vector3> GetLocalPositions(AGXUnity.RigidBody[] bodies)
  {
    Dictionary<string, Vector3> dict = new Dictionary<string, Vector3>();

    foreach(var rb in bodies)
    {
      dict.Add(rb.name, rb.transform.localPosition);
    }

    return dict;
  }

  public static void SetLocalPositions(AGXUnity.RigidBody[] bodies, Dictionary<string, Vector3> positionDict)
  {
    foreach (var rb in bodies)
    {
      Vector3 position = positionDict[rb.name];
      rb.transform.localPosition = position;
      rb.SyncNativeTransform();
    }
  }


  public static Dictionary<string, Quaternion> GetLocalRotations(AGXUnity.RigidBody[] bodies)
  {
    Dictionary<string, Quaternion> dict = new Dictionary<string, Quaternion>();

    foreach (var rb in bodies)
    {
      dict.Add(rb.name, rb.transform.localRotation);
    }

    return dict;
  }

  public static void SetLocalRotations(AGXUnity.RigidBody[] bodies, Dictionary<string, Quaternion> rotationDict)
  {
    foreach (var rb in bodies)
    {
      Quaternion rotation = rotationDict[rb.name];
      rb.transform.localRotation = rotation;
      rb.SyncNativeTransform();
    }
  }

  public static int GetNrActions(this B_Agent b_agent)
  {
    int nrActions = 0;
    foreach (var b_action in b_agent.Actions)
    {
      switch (b_action.Signal)
      {
        case B_Signal.Input<double> doubleInput:
          nrActions++;
          break;
        default:
          Debug.LogWarning($"Unkown signal type: {b_action.Signal.GetType()}");
          break;
      }
    }
    return nrActions;
  }
}
