using UnityEngine;
using System.Linq;

using AGXUnity.BrickUnity.Signals;

using B_Component = Brick.Physics.Component;
using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_Signal = Brick.Signal;
using B_BrickSimulation = Brick.AgxBrick.BrickSimulation;
using B_Interaction = Brick.Physics.Mechanics.AttachmentPairInteraction;
using B_Agent = Brick.MachineLearning.RLAgent;

namespace AGXUnity.BrickUnity
{
  public class BrickRuntimeComponent : AGXUnity.ScriptComponent
  {
    public string filePath;
    public string modelName;

    protected B_Component m_component;
    private B_BrickSimulation m_brickSimulation;

    public B_Agent GetBrickAgent(string agentName)
    {
      B_Agent agent = null;

      var b_agents = m_component._RecursiveValues.OfType<B_Agent>();
      foreach (var b_agent in b_agents)
      {
        var name = b_agent._ModelValuePath.Name.Str;
        if (name.Equals(agentName))
        {
          agent = b_agent;
        }
      }
      if (agent == null)
        Debug.LogWarning("Could not find brick agent with name: " + agentName);

      return agent;
    }



    protected override bool Initialize()
    {
      Debug.Log($"Synchronizing Brick component {filePath}:{modelName}");
      m_component = BrickUtils.LoadComponentFromFile(filePath, modelName);
      var au_sim = AGXUnity.Simulation.Instance.GetInitialized<AGXUnity.Simulation>();
      m_brickSimulation = new B_BrickSimulation(au_sim.Native);
      au_sim.StepCallbacks.PreStepForward += m_brickSimulation.SyncInputParameters;
      au_sim.StepCallbacks.PostStepForward += m_brickSimulation.SyncOutputParameters;

      HandleRigidBodies();
      HandleConstraints();
      AddSignals();
      AddROSConnection();
      AddDriveTrains();
      if (Application.isEditor)
        HandleSignals();

      return base.Initialize();
    }


    private void HandleRigidBodies()
    {
      foreach (var au_body in GetComponentsInChildren<AGXUnity.RigidBody>())
      {
        var c_brickObject = au_body.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_body = GetBrickValue<B_RigidBody>(c_brickObject);
        if (b_body == null)
          continue;
        var agx_body = au_body.GetInitialized<AGXUnity.RigidBody>().Native;
        this.m_brickSimulation.BodyMap.Add(b_body, agx_body);
      }
    }


    private void HandleConstraints()
    {
      foreach (var au_constraint in GetComponentsInChildren<AGXUnity.Constraint>())
      {
        var c_brickObject = au_constraint.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_connector = GetBrickValue<B_Connector>(c_brickObject);
        if (b_connector == null)
          continue;
        var nativeConstraint = au_constraint.GetInitialized<AGXUnity.Constraint>().Native;
        this.m_brickSimulation.InteractionMap.Add(b_connector.MainInteraction, nativeConstraint);
        foreach (var b_interaction in b_connector.Interactions)
        {
          if (b_interaction is B_Interaction.Interaction1D b_interaction1D)
          {
            this.m_brickSimulation.InteractionMap.Add(b_interaction1D, nativeConstraint);
          }
          else if (b_interaction != b_connector.MainInteraction)
          {
            var brickObject = GetComponent<BrickObject>();
            Debug.LogError($"Unhandled Brick Interaction: {brickObject.path}. The Interaction is neither a MainInteraction or a controller.");
          }
        }
      }
    }


    private void AddDriveTrains()
    {
      m_brickSimulation.CreatePowerLineUnits(m_component);
      m_brickSimulation.ConnectPowerLineUnits(m_component);
    }


    private void AddROSConnection()
    {
      this.m_brickSimulation.CreateROSConnection(m_component);
    }


    private void AddSignals()
    {
      this.m_brickSimulation.CreateSignals(m_component);
    }


    private T GetBrickValue<T>(BrickObject brickObject) where T : Brick.Object
    {

      var b_path = brickObject.GetBrickPathRelativeRoot();
      var b_object = m_component._Get(b_path);
      if (b_object is T b_T)
        return b_T;
      Debug.LogWarning($"Type of Brick object {b_path} ({b_object.GetType()}) does not match the expected type {typeof(T)}");
      return null;
    }


    private void HandleSignals()
    {
      var b_signals = m_component._RecursiveValues.OfType<B_Signal.SignalBase>();
      var go_signals = new GameObject("Signals");
      go_signals.transform.SetParent(this.transform);
      foreach (var b_signal in b_signals)
      {
        var go_signal = new GameObject(b_signal._ModelValuePath.Str);
        go_signal.transform.SetParent(go_signals.transform);
        switch (b_signal)
        {
          case B_Signal.Input<double> doubleInput:
            {
              var comp = go_signal.AddComponent<BrickDoubleInput>();
              comp.signal = doubleInput;
            }
            break;
          case B_Signal.Output<double> doubleOutput:
            {
              var comp = go_signal.AddComponent<BrickDoubleOutput>();
              comp.signal = doubleOutput;
            }
            break;
          case B_Signal.Output<Brick.Math.Vec3> vec3Output:
            {
              var comp = go_signal.AddComponent<BrickVec3Output>();
              comp.signal = vec3Output;
            }
            break;
          case B_Signal.Output<Brick.Math.Quat> quatOutput:
            {
              var comp = go_signal.AddComponent<BrickQuatOutput>();
              comp.signal = quatOutput;
            }
            break;
          case B_Signal.Output<Brick.Scene.Transform> transformOutput:
            {
              var comp = go_signal.AddComponent<BrickTransformOutput>();
              comp.signal = transformOutput;
            }
            break;
          default:
            Debug.LogWarning($"Unkown signal type: {b_signal.GetType()}");
            break;
        }
      }
    }
  }
}