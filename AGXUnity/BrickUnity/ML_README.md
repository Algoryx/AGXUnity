# Setting up an ML Agents scene with Brick

To set up a machine learning (ML) scene with ML Agents and Brick you first need to install ML-Agents and its package. Make sure you use the correct version of the package (“Show preview packages” probably needs to be checked).

In Brick, the observations and actions of the Agent are set up as signals, and by using the functions in `BrickRLAgentUtils` it is easy to transfer these directly to ML-Agents and Unity. Note that right now, only continuous actions are supported in Brick.

An example of a reinforcement learning agent from Brick can be seen in Tutorial12_ML.yml in Brick.

In Unity the Brick file can be read using `Assets->Import Brick file as prefab`. This will add the prefab created from the Brick-file to the scene and the assets.

To set up the reinforcement learning environment in Unity, first a new script has to be created, which inherits from the Agent-class in ML-Agents. To access the values of the `RLAgent`-brick class, the brick agent has to be read from the BrickRuntimeComponent.

```cs
brickRuntimeComponent = brickGameObject.GetComponent<BrickRuntimeComponent>().GetInitialized<BrickRuntimeComponent>();
brickAgent = brickRuntimeComponent.GetBrickAgent(agentName);
```

Adding this scipt to a game object will automatically add the `Behavior Parameters` component as well. Some of these parameters might have been set in the Brick-file and you can read these values into the behavior parameters and the agent. To set the number of observations in the behavior parameters, there is a help function that counts the number of observations in the Brick agent:

```cs
parameters = GetComponent<BehaviorParameters>();
parameters.BrainParameters.VectorObservationSize = brickAgent.GetNrObservations();
```

Reading observations can be done in the following way:

```cs
public override void CollectObservations(VectorSensor sensor)
{
  sensor.AddObservation(brickAgent.GetSignalObservations());
}
```

And setting Actions:

```cs
public override void OnActionReceived(ActionBuffers actionBuffers)
{
  brickAgent.SetActionSignals(actionBuffers.ContinuousActions.Array);

  ...
```

The easies way to reset the agent is if there is a prefab connected to it. Then you can simply remove the game object from the prefab and then reload the prefab.

```cs
if (agentGameObject != null)
  DestroyImmediate(agentGameObject);

Simulation.Instance.Native.garbageCollect();
agentGameObject = Instantiate(Resources.Load<GameObject>("name_of_brick_prefab"));
```
There are some methods to help reset the scene if you cannot simply remove and reload the prefab. To use these methods you need to first collect the bodies and its position and rotation into dictionaries.

```cs
AGXUnity.RigidBody[] bodies = brickGameObject.GetComponentsInChildren<AGXUnity.RigidBody>();
Dictionary<string, Vector3> inititalPosition = BrickRLAgentUtils.GetLocalPositions(bodies);
Dictionary<string, Quaternion> inititalRotation = BrickRLAgentUtils.GetLocalRotations(bodies);
```

Then it is possibe to reset the collected bodies as:

```cs
BrickRLAgentUtils.SetLocalPositions(bodies, inititalPosition);
BrickRLAgentUtils.SetLocalRotations(bodies, inititalRotation);
foreach(AGXUnity.RigidBody body in bodies)
{
  body.AngularVelocity = new Vector3(0, 0, 0);
  body.LinearVelocity = new Vector3(0, 0, 0);
}
```

The last part is needed to make sure the bodies don't have any velocities left from before moving them.

To step the agent a Decision Requester is needed. This is added as a component to the game object containing the agent and the behavior parameters. To make sure the Agent steps after the simulation has steppet, you can do the following:

```cs
Academy.Instance.AutomaticSteppingEnabled = false;
Simulation.Instance.StepCallbacks.PostStepForward += Academy.Instance.EnvironmentStep;
```
