using System.Collections.Generic;
using UnityEngine;

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


  public static int GetNrObservations(this B_Agent b_agent)
  {
    int nrObservations = 0;
    foreach (var b_signal in b_agent.Observations)
    {
      switch (b_signal)
      {
        case B_Signal.Output<double> doubleOutput:
          nrObservations += 1;
          break;
        case B_Signal.Output<Brick.Math.Vec3> vec3Output:
          nrObservations += 3;
          break;
        case B_Signal.Output<Brick.Math.Quat> quatOutput:
          nrObservations += 4;
          break;
        case B_Signal.Output<Brick.Scene.Transform> transformOutput:
          nrObservations += 7;
          break;
        default:
          Debug.LogWarning($"Unkown signal type: {b_signal.GetType()}");
          break;
      }
    }

    return nrObservations;
  }

  public static List<float> GetSignalObservations(this B_Agent b_agent)
  {
    var list = new List<float>();
    foreach (var b_signal in b_agent.Observations)
    {
      switch (b_signal)
      {
        case B_Signal.Output<double> doubleOutput:
          list.Add((float)doubleOutput.GetData());
          break;
        case B_Signal.Output<Brick.Math.Vec3> vec3Output:
          var vec3 = vec3Output.GetData().ToHandedVector3();
          list.AddVector3(vec3);
          break;
        case B_Signal.Output<Brick.Math.Quat> quatOutput:
          var quat = quatOutput.GetData().ToHandedQuaternion();
          list.AddQuaternion(quat);
          break;
        case B_Signal.Output<Brick.Scene.Transform> transformOutput:
          var b_transform = transformOutput.GetData();
          var pos = b_transform.Position.ToHandedVector3();
          var rot = b_transform.Rotation.ToHandedQuaternion();
          list.AddVector3(pos);
          list.AddQuaternion(rot);
          break;
        default:
          Debug.LogWarning($"Unkown signal type: {b_signal.GetType()}");
          break;
      }
    }
    return list;
  }

  public static void SetActionSignals(this B_Agent b_agent, float[] vectorAction)
  {
    int i = 0;
    foreach (var b_action in b_agent.Actions)
    {
      switch (b_action.Signal)
      {
        case B_Signal.Input<double> doubleInput:
          var value = b_action.Scaling*Mathf.Clamp(vectorAction[i], -1, 1);
          doubleInput.SetData(value);
          i++;
          break;
        default:
          Debug.LogWarning($"Unkown signal type: {b_action.Signal.GetType()}");
          break;
      }
    }
  }

  static void AddVector3(this List<float> list, Vector3 vec3)
  {
    list.Add(vec3.x);
    list.Add(vec3.y);
    list.Add(vec3.z);
  }

  static void AddQuaternion(this List<float> list, Quaternion quat)
  {
    list.Add(quat.x);
    list.Add(quat.y);
    list.Add(quat.z);
    list.Add(quat.w);
  }
}
