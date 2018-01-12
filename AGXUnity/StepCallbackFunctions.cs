using System;

namespace AGXUnity
{
  /// <summary>
  /// Simulation step callback functions.
  /// </summary>
  public class StepCallbackFunctions
  {
    /// <summary>
    /// Step callback signature: void callback()
    /// </summary>
    public delegate void StepCallbackDef();

    /// <summary>
    /// Before native simulation.stepForward is called. This callback is
    /// fired before the native transforms are written so feel free to
    /// change and use Unity object transforms.
    /// </summary>
    public StepCallbackDef PreStepForward;

    /// <summary>
    /// After PreStepForward, before native simulation.stepForward is called.
    /// Synchronize all native transforms during this callback.
    /// </summary>
    public StepCallbackDef PreSynchronizeTransforms;

    /// <summary>
    /// Callback after native simulation.stepForward is done and before any
    /// other post callbacks. Write back transforms from the simulation to
    /// the Unity objects during this call.
    /// </summary>
    public StepCallbackDef PostSynchronizeTransforms;

    /// <summary>
    /// After PostSynchronizeTransforms where the Unity objects have the
    /// transforms of the simulation step. During this callback it's possible
    /// to use "all" data from the simulation.
    /// </summary>
    public StepCallbackDef PostStepForward;
  }
}
