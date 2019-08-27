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

    /// <summary>
    /// Simulation step event - pre-collide.
    /// </summary>
    public StepCallbackDef SimulationPreCollide;

    /// <summary>
    /// Simulation step event - pre.
    /// </summary>
    public StepCallbackDef SimulationPre;

    /// <summary>
    /// Simulation step event - post.
    /// </summary>
    public StepCallbackDef SimulationPost;

    /// <summary>
    /// Simulation step event - last.
    /// </summary>
    public StepCallbackDef SimulationLast;

    public void OnInitialize( agxSDK.Simulation simulation )
    {
      m_simulationStepEvents = new SimulationStepEvents( this );
      simulation.add( m_simulationStepEvents );
    }

    public void OnDestroy( agxSDK.Simulation simulation )
    {
      if ( m_simulationStepEvents == null )
        return;

      simulation.remove( m_simulationStepEvents );

      m_simulationStepEvents.Dispose();
      m_simulationStepEvents = null;
    }

    private SimulationStepEvents m_simulationStepEvents = null;
    private class SimulationStepEvents : agxSDK.StepEventListener
    {
      private StepCallbackFunctions m_functions = null;

      public SimulationStepEvents( StepCallbackFunctions functions )
        : base( (int)ActivationMask.ALL )
      {
        m_functions = functions;
      }

      public sealed override void preCollide( double time )
      {
        if ( m_functions.SimulationPreCollide != null )
          m_functions.SimulationPreCollide.Invoke();
      }

      public sealed override void pre( double time )
      {
        if ( m_functions.SimulationPre != null )
          m_functions.SimulationPre.Invoke();
      }

      public sealed override void post( double time )
      {
        if ( m_functions.SimulationPost != null )
          m_functions.SimulationPost.Invoke();
      }

      public sealed override void last( double time )
      {
        if ( m_functions.SimulationLast != null )
          m_functions.SimulationLast.Invoke();
      }
    }
  }
}
