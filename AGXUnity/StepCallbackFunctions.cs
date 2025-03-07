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
    /// Callback after native simulation.stepForward has integrated the positions 
    /// in the simulation. Write back transforms from the simulation to
    /// the Unity objects during this call.
    /// Note that when this callback is invoked during simulation stepping depends
    /// on whether PreIntegratePositions is true in the Simulation class as this affects 
    /// when transforms are computed.
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

    /// <summary>
    /// Internal preparation callbacks.
    /// </summary>
    public StepCallbackDef _Internal_PrePre;

    /// <summary>
    /// Internal preparation callbacks.
    /// </summary>
    public StepCallbackDef _Internal_PrePost;

    /// <summary>
    /// Internal callbacks for handing incoming OpenPLX signals.
    /// </summary>
    public StepCallbackDef _Internal_OpenPLXSignalPreSync;

    /// <summary>
    /// Internal callbacks for handing outgoing OpenPLX signals.
    /// </summary>
    public StepCallbackDef _Internal_OpenPLXSignalPostSync;
    
    /// Internal preparation callbacks.
    /// </summary>
    public StepCallbackDef _Internal_PostSynchronizeTransform;

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
        if ( Simulation.Instance.PreIntegratePositions )
          Invoke( m_functions.PostSynchronizeTransforms, m_functions._Internal_PostSynchronizeTransform );

        Invoke( m_functions.SimulationPreCollide );
      }

      public sealed override void pre( double time )
      {
        Invoke( m_functions.SimulationPre, m_functions._Internal_PrePre );
      }

      public sealed override void post( double time )
      {
        Invoke( m_functions.SimulationPost, m_functions._Internal_PrePost );
      }

      public sealed override void last( double time )
      {
        Invoke( m_functions.SimulationLast );
      }

      private void Invoke( StepCallbackDef callbacks, StepCallbackDef internalPre = null )
      {
        if ( callbacks != null || internalPre != null ) {
          BeginManagedCallbacks();
          internalPre?.Invoke();
          callbacks?.Invoke();
          EndManagedCallbacks();
        }
      }

      private void BeginManagedCallbacks()
      {
        agx.agxSWIG.setEntityCreationThreadSafe( true );
      }

      private void EndManagedCallbacks()
      {
        agx.agxSWIG.setEntityCreationThreadSafe( false );
      }
    }
  }
}
