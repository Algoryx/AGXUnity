using System;
using System.ComponentModel;

using UnityEngine;

namespace AGXUnity
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#solver-settings" )]
  public class SolverSettings : ScriptAsset
  {
    /// <summary>
    /// Default number of AGX threads. Assuming processor count includes
    /// hyper threading, default value is number of physical cores minus one
    /// but not more than four.
    /// </summary>
    [HideInInspector]
    public static int DefaultNumberOfThreads { get { return Math.Min( SystemInfo.processorCount / 2 - 1, 4 ); } }

    /// <summary>
    /// Simulation instance is set when this asset is active.
    /// </summary>
    [HideInInspector]
    public agxSDK.Simulation SimulationInstance { get; private set; }

    [SerializeField]
    private int m_numberOfThreads = 1;

    /// <summary>
    /// Maximum number of threads used by AGX.
    /// </summary>
    /// <remarks>
    /// It's not recommended to assign values larger than the number of physical cores on
    /// the target computer.
    /// </remarks>
    [Description( "Maximum number of threads used by AGX." )]
    public int NumberOfThreads
    {
      get { return m_numberOfThreads; }
      set
      {
        m_numberOfThreads = Math.Max( value, 1 );
        if ( SimulationInstance != null )
          agx.agxSWIG.setNumThreads( System.Convert.ToUInt32( m_numberOfThreads ) );
      }
    }

    [SerializeField]
    private bool m_warmStartDirectContacts = false;

    /// <summary>
    /// True to warm start frictional contacts in the direct solver. Setting this
    /// to true could improve solver performance in relatively stationary scenes.
    /// </summary>
    [Description( "True to warm start frictional contacts in the direct solver. Default: false" )]
    public bool WarmStartDirectContacts
    {
      get { return m_warmStartDirectContacts; }
      set
      {
        m_warmStartDirectContacts = value;
        if ( SimulationInstance != null )
          SimulationInstance.getDynamicsSystem().setEnableContactWarmstarting( m_warmStartDirectContacts );
      }
    }

    [SerializeField]
    private int m_restingIterations = 16;

    /// <summary>
    /// Number iterations in the iterative solver. Note that the iterative solver
    /// is used in synergy with the direct solver, so changing this value will
    /// affect default contact/friction model.
    /// </summary>
    [Description( "Number of iteration in the iterative solver. Default: 16" )]
    public int RestingIterations
    {
      get { return m_restingIterations; }
      set
      {
        m_restingIterations = Math.Max( value, 0 );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().setNumRestingIterations( System.Convert.ToUInt64( m_restingIterations ) );
      }
    }

    [SerializeField]
    private int m_dryFrictionIterations = 7;

    /// <summary>
    /// Number of dry friction iterations used on solve islands where all constraints
    /// are DIRECT_AND_ITERATIVE and all contact friction models are SPLIT.
    /// </summary>
    [Description( "Number of dry friction iterations used on solve islands where " +
                  "all constraints are DIRECT_AND_ITERATIVE and all contacts SPLIT. Default: 7" )]
    public int DryFrictionIterations
    {
      get { return m_dryFrictionIterations; }
      set
      {
        m_dryFrictionIterations = Math.Max( value, 0 );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().setNumDryFrictionIterations( System.Convert.ToUInt64( m_dryFrictionIterations ) );
      }
    }

    public enum McpAlgorithmType
    {
      BlockPivot,
      Keller,
      HybridPivot
    }

    /// <summary>
    /// Convert MCP algorithm type from native to this.
    /// </summary>
    /// <param name="type">Native MCP algorithm type.</param>
    /// <returns>Managed MCP algorithm type.</returns>
    public static McpAlgorithmType Convert( agx.Solver.McpAlgoritmType type )
    {
      return type == agx.Solver.McpAlgoritmType.BLOCK_PIVOT ? McpAlgorithmType.BlockPivot :
             type == agx.Solver.McpAlgoritmType.KELLER ?      McpAlgorithmType.Keller :
                                                              McpAlgorithmType.HybridPivot;
    }

    /// <summary>
    /// Convert MCP algorithm type from this to native.
    /// </summary>
    /// <param name="type">MCP algorithm type.</param>
    /// <returns>Native MCP algorithm type.</returns>
    public static agx.Solver.McpAlgoritmType Convert( McpAlgorithmType type )
    {
      return type == McpAlgorithmType.BlockPivot ? agx.Solver.McpAlgoritmType.BLOCK_PIVOT :
             type == McpAlgorithmType.Keller     ? agx.Solver.McpAlgoritmType.KELLER :
                                                   agx.Solver.McpAlgoritmType.HYBRID_PIVOT;
    }

    [SerializeField]
    private McpAlgorithmType m_mcpAlgorithm = McpAlgorithmType.HybridPivot;

    /// <summary>
    /// MCP solver algorithm type. Block Pivot is fast but can fail. Keller is still
    /// fast but might never fail. Hybrid Pivot (default) tries to solver the system
    /// using Block Pivot but use Keller when Block Pivot fails.
    /// </summary>
    [Description( "MCP solver algorithm type. Block Pivot is fast but can fail. " +
                  "Keller is still fast but might never fail. Hybrid Pivot (default) " +
                  "solves failing Block Pivot using Keller. Default: Hybrid Pivot" )]
    public McpAlgorithmType McpAlgorithm
    {
      get { return m_mcpAlgorithm; }
      set
      {
        m_mcpAlgorithm = value;
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().setMcpAlgorithmType( Convert( m_mcpAlgorithm ) );
      }
    }

    [SerializeField]
    private int m_mcpInnerIterations = 7;

    /// <summary>
    /// Maximum number of MCP iterations used during direct linear solves. Default: 7
    /// </summary>
    [Description( "Maximum number of MCP iterations used during direct linear solves. Default: 7" )]
    public int McpInnerIterations
    {
      get { return m_mcpInnerIterations; }
      set
      {
        m_mcpInnerIterations = Math.Max( value, 0 );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().getNlMcpConfig().numMcpIterations = System.Convert.ToUInt64( m_mcpInnerIterations );
      }
    }

    [SerializeField]
    private float m_mcpInnerTolerance = 1.0E-6f;

    /// <summary>
    /// MCP solver tolerance during linear solves. Default: 1.0E-6
    /// </summary>
    [Description( "MCP solver tolerance during linear solves. Default: 1.0E-6" )]
    public float McpInnerTolerance
    {
      get { return m_mcpInnerTolerance; }
      set
      {
        m_mcpInnerTolerance = Mathf.Max( value, 0.0f );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().getNlMcpConfig().mcpTolerance = m_mcpInnerTolerance;
      }
    }

    [SerializeField]
    private int m_mcpOuterIterations = 5;

    /// <summary>
    /// Maximum number of non-linear (outer) iterations if the direct solver. Default: 5
    /// </summary>
    [Description( "Maximum number of non-linear (outer) iterations if the direct solver. Default: 5" )]
    public int McpOuterIterations
    {
      get { return m_mcpOuterIterations; }
      set
      {
        m_mcpOuterIterations = value;
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().getNlMcpConfig().numOuterIterations = System.Convert.ToUInt64( m_mcpOuterIterations );
      }
    }

    [SerializeField]
    private float m_mcpOuterTolerance = 1.0E-2f;

    /// <summary>
    /// Tolerance of non-linear (outer) direct solves. Default: 1.0E-2
    /// </summary>
    [Description( "Tolerance of non-linear (outer) direct solves. Default: 1.0E-2" )]
    public float McpOuterTolerance
    {
      get { return m_mcpOuterTolerance; }
      set
      {
        m_mcpOuterTolerance = Mathf.Max( value, 0.0f );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().getNlMcpConfig().outerTolerance = m_mcpOuterTolerance;
      }
    }

    [SerializeField]
    private int m_ppgsRestingIterations = 25;

    /// <summary>
    /// Parallel Projected Gauss Seidel (PPGS) resting iterations. Default: 25
    /// </summary>
    [Description( "Number of Parallel Projected Gauss Seidel (PPGS) resting iterations, if it is used in the solver. Default: 25" )]
    public int PpgsRestingIterations
    {
      get { return m_ppgsRestingIterations; }
      set
      {
        m_ppgsRestingIterations = Math.Max( value, 0 );
        if ( SimulationInstance != null )
          SimulationInstance.getSolver().setNumPPGSRestingIterations( (ulong)m_ppgsRestingIterations );
      }
    }

    /// <summary>
    /// Assigns default values to a native simulation instance.
    /// </summary>
    /// <param name="simulation">Native simulation instance.</param>
    public static void AssignDefault( agxSDK.Simulation simulation )
    {
      if ( simulation == null )
        return;

      var tmp = Create<SolverSettings>();
      tmp.SetSimulation( simulation );
      Utils.PropertySynchronizer.Synchronize( tmp );
      tmp.SetSimulation( null );
      ScriptAsset.Destroy( tmp );
    }

    /// <summary>
    /// Internal.
    /// </summary>
    public void SetSimulation( agxSDK.Simulation simulation )
    {
      SimulationInstance = simulation;
    }

    public SolverSettings RestoreLocalDataFrom( agxSDK.Simulation simulation )
    {
      var solver = simulation.getSolver();
      var config = solver.getNlMcpConfig();

      NumberOfThreads         = System.Convert.ToInt32( agx.agxSWIG.getNumThreads() );
      WarmStartDirectContacts = simulation.getDynamicsSystem().getUpdateTask().getSubtask( "MatchContactStates" ).isEnabled();
      RestingIterations       = System.Convert.ToInt32( solver.getNumRestingIterations() );
      DryFrictionIterations   = System.Convert.ToInt32( solver.getNumDryFrictionIterations() );
      McpAlgorithm            = Convert( config.mcpAlgorithmType );
      McpInnerIterations      = System.Convert.ToInt32( config.numMcpIterations );
      McpInnerTolerance       = System.Convert.ToSingle( config.mcpTolerance );
      McpOuterIterations      = System.Convert.ToInt32( config.numOuterIterations );
      McpOuterTolerance       = System.Convert.ToSingle( config.outerTolerance );

      return this;
    }

    public override void Destroy()
    {
      SimulationInstance = null;
    }

    protected override void Construct()
    {
      NumberOfThreads = DefaultNumberOfThreads;
    }

    protected override bool Initialize()
    {
      return true;
    }
  }
}
