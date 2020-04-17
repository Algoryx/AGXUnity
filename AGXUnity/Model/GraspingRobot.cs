using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  using AGXUnity;

  public static class TransformDeepChildExtension
  {
    //Breadth-first search
    public static Transform FindDeepChild(this Transform aParent, string aName)
    {
      Queue<Transform> queue = new Queue<Transform>();
      queue.Enqueue(aParent);
      while (queue.Count > 0)
      {
        var c = queue.Dequeue();
        if (c.name == aName)
          return c;
        foreach (Transform t in c)
          queue.Enqueue(t);
      }
      return null;
    }
  }



  [AddComponentMenu("AGXUnity/Model/Grasping Robot")]
  [DisallowMultipleComponent]

  public class GraspingRobot : AGXUnity.ScriptComponent
  {

    [InspectorGroupBegin(Name = "Controlled Constraints")]

    [AllowRecursiveEditing]
    public Constraint BaseHinge
    {
      get
      {
        if (m_baseHinge == null)
          m_baseHinge = FindChild<Constraint>("GraspingRobotBaseHinge");
        return m_baseHinge;
      }
    }
    private Constraint m_baseHinge = null;

    [AllowRecursiveEditing]
    public Constraint WristHinge
    {
      get
      {
        if (m_wristHinge == null)
          m_wristHinge = FindChild<Constraint>("GraspingRobotWristHinge");
        return m_wristHinge;
      }
    }
    private Constraint m_wristHinge = null;

    public Constraint Hinge2
    {
      get
      {
        if (m_hinge2 == null)
          m_hinge2 = FindChild<Constraint>("GraspingRobotHinge2");
        return m_hinge2;
      }
    }
    private Constraint m_hinge2 = null;

    public Constraint Hinge3
    {
      get
      {
        if (m_hinge3 == null)
          m_hinge3 = FindChild<Constraint>("GraspingRobotHinge3");
        return m_hinge3;
      }
    }
    private Constraint m_hinge3 = null;

    [AllowRecursiveEditing]
    public Constraint GraspingPrismatic
    {
      get
      {
        if (m_graspingPrismatic == null)
          m_graspingPrismatic = FindChild<Constraint>("GraspingRobotGripPrismatic");
        return m_graspingPrismatic;
      }
    }
    private Constraint m_graspingPrismatic = null;

    public AGXUnity.ObserverFrame KinematicAttachment
    {
      get
      {
        if (m_kinematicAttachment == null)
          m_kinematicAttachment = FindChild<ObserverFrame>("KinematicObserver");

        return m_kinematicAttachment;
      }
    }
    private AGXUnity.ObserverFrame m_kinematicAttachment = null;



    private T FindChild<T>(string name)
      where T : ScriptComponent
    {

      var t = TransformDeepChildExtension.FindDeepChild(transform, name);
      return t.GetComponentInChildren<T>();
    }


    protected override bool Initialize()
    {
      return true;
    }
    // Update is called once per frame
    void Update()
    {

    }
  }
}