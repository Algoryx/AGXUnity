﻿using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using AGXUnity.Utils;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AGXUnity
{
  /// <summary>
  /// Simulation object, either explicitly created and added or
  /// implicitly created when first used.
  /// </summary>
  [AddComponentMenu( "" )]
  public class Simulation : UniqueGameObject<Simulation>
  {
    /// <summary>
    /// Native instance.
    /// </summary>
    private agxSDK.Simulation m_simulation = null;
    private agx.DynamicsSystem m_system = null;
    private agxCollide.Space m_space = null;

    public enum AutoSteppingModes
    {
      /// <summary>
      /// Simulation step from FixedUpdate. By default Time.fixedDeltaTime is
      /// 0.02 and Time.fixedDeltaTime will be used as time step size.
      /// </summary>
      FixedUpdate,
      /// <summary>
      /// Simulation step from Update. Update callback is executed each frame,
      /// e.g, 60 Hz with VSync enabled on a 60 Hz monitor. Step forward is called when
      /// the elapsed time exceeds the time step size.
      /// </summary>
      Update,
      /// <summary>
      /// Simulation step invoked manually by the user.
      /// Previously EnableAutoStepping = false.
      /// </summary>
      Disabled
    }

    [SerializeField]
    private AutoSteppingModes m_autoSteppingMode = AutoSteppingModes.FixedUpdate;

    /// <summary>
    /// Simulation step mode.
    /// </summary>
    [HideInInspector]
    public AutoSteppingModes AutoSteppingMode
    {
      get { return m_autoSteppingMode; }
      set { m_autoSteppingMode = value; }
    }

    [SerializeField]
    private float m_fixedUpdateRealTimeFactor = 0.333f;

    /// <summary>
    /// Value defining the maximum time we may spend in FixedUpdate. Setting
    /// this value to 1.0 means we may not spend more time than Time.fixedDeltaTime,
    /// resulting in slow-motion looking simulations when the simulation time is
    /// high - but the rendering FPS is still (relatively) high. Default: 0.333,
    /// i.e., 3 * Time.fixedDeltaTime as maximum time spent in FixedUpdate to
    /// resolve simulation performance spikes.
    /// 
    /// 0.0: Disabled - every FixedUpdate callback will call simulation.stepForward().
    /// 0.333: Three times fixedDeltaTime may be spent stepping the simulation.
    /// 1.0: Additional FixedUpdate callbacks before Update will be ignored if the
    ///      simulation stepping time is high.
    /// </summary>
    [HideInInspector]
    public float FixedUpdateRealTimeFactor
    {
      get { return m_fixedUpdateRealTimeFactor; }
      set { m_fixedUpdateRealTimeFactor = Mathf.Max( value, 0.0f ); }
    }

    [SerializeField]
    private float m_updateRealTimeCorrectionFactor = 0.9f;

    /// <summary>
    /// Given 60 Hz, 1 frame VSync, the Update callbacks will be executed in 58 - 64 Hz.
    /// This value scales the time since last frame so that we don't lose a stepForward
    /// call when Update is called > 60 Hz. Default: 0.9.
    /// </summary>
    [HideInInspector]
    public float UpdateRealTimeCorrectionFactor
    {
      get { return m_updateRealTimeCorrectionFactor; }
      set { m_updateRealTimeCorrectionFactor = Mathf.Max( value, 0.0f ); }
    }

    /// <summary>
    /// Gravity, default -9.82 in y-direction. Paired with property Gravity.
    /// </summary>
    [SerializeField]
    Vector3 m_gravity = new Vector3( 0, -9.82f, 0 );

    /// <summary>
    /// Get or set gravity in this simulation. Default -9.82 in y-direction.
    /// </summary>
    public Vector3 Gravity
    {
      get { return m_gravity; }
      set
      {
        m_gravity = value;
        if ( m_simulation != null )
          m_simulation.setUniformGravity( m_gravity.ToVec3() );
      }
    }

    /// <summary>
    /// Time step size is the default callback frequency in Unity.
    /// </summary>
    [SerializeField]
    private float m_timeStep = 0.02f;

    /// <summary>
    /// Get or set time step size. Note that the time step has to
    /// match Unity update frequency.
    /// </summary>
    [HideInInspector]
    public float TimeStep
    {
      get { return m_timeStep; }
      set
      {
        m_timeStep = Mathf.Max( value, 1.0E-8f );
        if ( m_simulation != null )
          m_simulation.setTimeStep( m_timeStep );
      }
    }

    [SerializeField]
    private SolverSettings m_solverSettings = null;

    /// <summary>
    /// Get or set solver settings.
    /// </summary>
    [AllowRecursiveEditing]
    [IgnoreSynchronization]
    public SolverSettings SolverSettings
    {
      get { return m_solverSettings; }
      set
      {
        if ( m_solverSettings != null ) {
          m_solverSettings.SetSimulation( null );
          if ( value == null )
            SolverSettings.AssignDefault( m_simulation );
        }

        m_solverSettings = value;

        if ( m_solverSettings != null && m_simulation != null ) {
          m_solverSettings.SetSimulation( m_simulation );
          m_solverSettings.GetInitialized<SolverSettings>();
        }
      }
    }

    /// <summary>
    /// Display statistics window toggle.
    /// </summary>
    [SerializeField]
    private bool m_displayStatistics = false;

    /// <summary>
    /// Enable/disable statistics window showing timing and simulation data.
    /// </summary>
    [HideInInspector]
    public bool DisplayStatistics
    {
      get { return m_displayStatistics; }
      set
      {
        m_displayStatistics = value;

        if ( m_displayStatistics && m_statisticsWindowData == null )
          m_statisticsWindowData = new StatisticsWindowData( new Rect( new Vector2( 10, 10 ),
                                                                       new Vector2( 275, 236 ) ),
                                                             new Rect( new Vector2( 10, 10 ),
                                                                       new Vector2( 275, 320 ) ) );
        else if ( !m_displayStatistics && m_statisticsWindowData != null ) {
          m_statisticsWindowData.Dispose();
          m_statisticsWindowData = null;
        }
      }
    }

    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs( "m_memorySnapEnabled" )]
    bool m_displayMemoryAllocations = false;

    /// <summary>
    /// Enable/disable track of memory allocations during DoStep. If enabled,
    /// the collected data will be shown in the statistics window.
    /// </summary>
    [HideInInspector]
    public bool DisplayMemoryAllocations
    {
      get { return m_displayMemoryAllocations; }
      set { m_displayMemoryAllocations = value; }
    }

    private bool TrackMemoryAllocations
    {
      get { return DisplayMemoryAllocations && DisplayStatistics; }
    }

    [SerializeField]
    private bool m_enableMergeSplitHandler = false;
    public bool EnableMergeSplitHandler
    {
      get { return m_enableMergeSplitHandler; }
      set
      {
        m_enableMergeSplitHandler = value;
        if ( m_simulation != null )
          m_simulation.getMergeSplitHandler().setEnable( m_enableMergeSplitHandler );
      }
    }

    [SerializeField]
    private bool m_savePreFirstStep = false;
    [HideInInspector]
    public bool SavePreFirstStep
    {
      get { return m_savePreFirstStep; }
      set { m_savePreFirstStep = value; }
    }

    [SerializeField]
    private string m_savePreFirstStepPath = string.Empty;
    [HideInInspector]
    public string SavePreFirstStepPath
    {
      get { return m_savePreFirstStepPath; }
      set { m_savePreFirstStepPath = value; }
    }

    /// <summary>
    /// Get the native instance, if not deleted.
    /// </summary>
    public agxSDK.Simulation Native { get { return GetOrCreateSimulation(); } }

    /// <summary>
    /// Step callback interface. Valid use from "initialize" to "Destroy".
    /// </summary>
    public StepCallbackFunctions StepCallbacks { get; } = new StepCallbackFunctions();

    /// <summary>
    /// Save current simulation/scene to an AGX native file (.agx or .aagx).
    /// </summary>
    /// <param name="filename">Filename including path.</param>
    /// <returns>True if objects were written to file - otherwise false.</returns>
    public bool SaveToNativeFile( string filename )
    {
      if ( m_simulation == null ) { 
        Debug.LogWarning( Utils.GUI.AddColorTag( $"Unable to write {filename}: Simulation isn't active.",
                                                 Color.yellow ),
                          this );
        return false;
      }

      FileInfo file = new FileInfo( filename );
      if ( !file.Directory.Exists ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( $"Unable to write {filename}: Directory doesn't exist.",
                                                 Color.yellow ) );
        return false;
      }

      if ( file.Extension.ToUpper() != ".AGX" && file.Extension.ToUpper() != ".AAGX" ) {
        Debug.LogWarning( Utils.GUI.AddColorTag( $"Unable to write {filename}: File extension {file.Extension} is unknown. " ,
                                                 Color.yellow ) +
                          "Valid extensions are .agx and .aagx." );
        return false;
      }

      uint numObjects = m_simulation.write( file.FullName );
      return numObjects > 0;
    }

    /// <summary>
    /// Perform explicit simulation step.
    /// </summary>
    /// <remarks>
    /// Calling this method when AutoSteppingMode != AutoSteppingModes.Disabled will
    /// result in several steps being made each update loop.
    /// </remarks>
    public void DoStep()
    {
      if ( AutoSteppingMode != AutoSteppingModes.Disabled )
        Debug.LogWarning( "Explicit call to Simulation.DoStep() when auto stepping mode is enabled.", this );

      DoStepInternal();
    }

    private agx.Timer m_stepForwardTimer = null;

    protected override bool Initialize()
    {
      GetOrCreateSimulation();

      m_stepForwardTimer = new agx.Timer();

      return base.Initialize();
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      if ( m_simulation != null ) {
        StepCallbacks.OnDestroy( m_simulation );
        if ( m_solverSettings != null )
          m_solverSettings.SetSimulation( null );
        m_simulation.cleanup();
      }
      m_simulation = null;
    }

    protected override void OnApplicationQuit()
    {
      base.OnApplicationQuit();
      if ( m_simulation != null )
        m_simulation.cleanup();
    }

    private agxSDK.Simulation GetOrCreateSimulation()
    {
      if ( m_simulation == null ) {
        NativeHandler.Instance.MakeMainThread();

        m_simulation = new agxSDK.Simulation();
        m_space = m_simulation.getSpace();
        m_system = m_simulation.getDynamicsSystem();

        // Solver settings will assign number of threads.
        if ( m_solverSettings != null ) {
          m_solverSettings.SetSimulation( m_simulation );
          m_solverSettings.GetInitialized<SolverSettings>();
        }
        // No solver settings - set the default.
        else
          agx.agxSWIG.setNumThreads( Convert.ToUInt32( SolverSettings.DefaultNumberOfThreads ) );

        StepCallbacks.OnInitialize( m_simulation );
      }

      return m_simulation;
    }

    private void FixedUpdate()
    {
      var doFixedStep = AutoSteppingMode == AutoSteppingModes.FixedUpdate &&
                        (
                          // First time step.
                          !m_stepForwardTimer.isRunning() ||
                          // Do step as long as this FixedUpdate hasn't been called
                          // several times exceeding wall clock time > factor * time step size.
                          0.001f * (float)m_stepForwardTimer.getTime() / TimeStep <= 1.0f / FixedUpdateRealTimeFactor
                        );
      if ( doFixedStep ) {
        if ( !m_stepForwardTimer.isRunning() )
          m_stepForwardTimer.start();

        DoStepInternal();
      }
    }

    private void Update()
    {
      // Resetting timer during AutoSteppingMode == FixedUpdate, flagging
      // that next call to FixedUpdate may step the simulation.
      if ( AutoSteppingMode != AutoSteppingModes.Update ) {
        m_stepForwardTimer.reset();
        return;
      }

      var currTime = 0.001f * (float)m_stepForwardTimer.getTime();
      var performStep = AutoSteppingMode == AutoSteppingModes.Update &&
                        (
                          // First step.
                          !m_stepForwardTimer.isRunning() ||
                          // Time since last call exceeds the time step size.
                          currTime >= UpdateRealTimeCorrectionFactor * TimeStep
                        );
      if ( performStep ) {
        m_stepForwardTimer.reset( true );
        DoStepInternal();
      }
    }

    private void DoStepInternal()
    {
      if ( !NativeHandler.Instance.HasValidLicense || m_simulation == null )
        return;

      PreStepForward();
      InvokeStepForward();
      PostStepForward();
    }

    private void PreStepForward()
    {
      bool savePreFirstTimeStep = Application.isEditor &&
                                  SavePreFirstStep &&
                                  SavePreFirstStepPath != string.Empty &&
                                  m_simulation.getTimeStamp() == 0.0;
      if ( savePreFirstTimeStep ) {
        var saveSuccess = SaveToNativeFile( SavePreFirstStepPath );
        if ( saveSuccess )
          Debug.Log( Utils.GUI.AddColorTag( "Successfully wrote initial state to: ", Color.green ) +
                     new FileInfo( SavePreFirstStepPath ).FullName );
      }

      agx.Timer timer = null;
      if ( DisplayStatistics )
        timer = new agx.Timer( true );

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.Begin );

      if ( StepCallbacks.PreStepForward != null )
        StepCallbacks.PreStepForward.Invoke();

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.PreStepForward );

      if ( StepCallbacks.PreSynchronizeTransforms != null )
        StepCallbacks.PreSynchronizeTransforms.Invoke();

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.PreSynchronizeTransforms );

      if ( timer != null )
        timer.stop();
    }

    private void InvokeStepForward()
    {
      agx.agxSWIG.setEntityCreationThreadSafe( false );

      m_simulation.stepForward();

      agx.agxSWIG.setEntityCreationThreadSafe( true );
    }

    private void PostStepForward()
    {
      agx.Timer timer = null;
      if ( DisplayStatistics )
        timer = new agx.Timer( true );

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.StepForward );

      if ( StepCallbacks.PostSynchronizeTransforms != null )
        StepCallbacks.PostSynchronizeTransforms.Invoke();

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.PostSynchronizeTransforms );

      if ( StepCallbacks.PostStepForward != null )
        StepCallbacks.PostStepForward.Invoke();

      if ( TrackMemoryAllocations )
        MemoryAllocations.Snap( MemoryAllocations.Section.PostStepForward );

      Rendering.DebugRenderManager.OnActiveSimulationPostStep( m_simulation );

      if ( timer != null ) {
        timer.stop();
        m_statisticsWindowData.ManagedStepForward = Convert.ToSingle( timer.getTime() );
      }
    }

    private class MemoryAllocations
    {
      public enum Section
      {
        Begin,
        PreStepForward,
        PreSynchronizeTransforms,
        StepForward,
        PostSynchronizeTransforms,
        PostStepForward
      }

      public static MemoryAllocations Instance { get; set; }

      public static void Snap( Section section )
      {
        if ( Instance == null )
          return;

        Instance[ section ] = GC.GetTotalMemory( false );
      }

      public static string GetDeltaString( Section section )
      {
        if ( Instance == null || section == Section.Begin )
          return string.Empty;

        var delta    = Instance[ section ] - Instance[ section - 1 ];
        var absDelta = System.Math.Abs( delta );
        var suffix   = " B";
        var value    = Convert.ToSingle( delta );
        if ( absDelta > 512 * 1024 ) {
          suffix = "MB";
          value  = Convert.ToSingle( delta ) / ( 1024.0f * 1024.0f );
        }
        else if ( absDelta > 512 ) {
          suffix = "KB";
          value  = Convert.ToSingle( delta ) / 1024.0f;
        }

        return string.Format( "{0:0.#} {1}", value, suffix );
      }

      private long[] m_allocations = new long[ Enum.GetValues( typeof( Section ) ).Length ];

      public long this[ Section section ]
      {
        get { return m_allocations[ (int)section ]; }
        set { m_allocations[ (int)section ] = value; }
      }
    }

    private class StatisticsWindowData : IDisposable
    {
      public int Id { get; private set; }
      public Rect Rect { get; set; }
      public Rect RectMemoryEnabled { get; set; }
      public Font Font { get; private set; }
      public GUIStyle LabelStyle { get; set; }
      public float ManagedStepForward { get; set; }

      public StatisticsWindowData( Rect rect, Rect rectMemoryEnabled )
      {
        agx.Statistics.instance().setEnable( true );
        Id = GUIUtility.GetControlID( FocusType.Passive );
        Rect = rect;
        RectMemoryEnabled = rectMemoryEnabled;

        MemoryAllocations.Instance = new MemoryAllocations();
        ManagedStepForward = 0.0f;

        var fonts = Font.GetOSInstalledFontNames();
        foreach ( var font in fonts )
          if ( font == "Consolas" )
            Font = Font.CreateDynamicFontFromOSFont( font, 12 );

        LabelStyle = Utils.GUI.Align( Utils.GUI.Skin.label, TextAnchor.MiddleLeft );
        if ( Font != null )
          LabelStyle.font = Font;
      }

      public void Dispose()
      {
        MemoryAllocations.Instance = null;
        agx.Statistics.instance().setEnable( false );
      }
    }

    private StatisticsWindowData m_statisticsWindowData = null;

    protected void OnGUI()
    {
      if ( m_simulation == null )
        return;

      if ( !NativeHandler.Instance.HasValidLicense ) {
        GUILayout.Window( GUIUtility.GetControlID( FocusType.Passive ),
                          new Rect( new Vector2( 16,
                                                 0.5f * Screen.height ),
                                    new Vector2( Screen.width - 32, 32 ) ),
                          id =>
                          {
                            // Invalid license if initialized.
                            if ( NativeHandler.Instance.Initialized && agx.Runtime.instance().getStatus().Length > 0 )
                              GUILayout.Label( Utils.GUI.MakeLabel( "AGX Dynamics: " + agx.Runtime.instance().getStatus(),
                                                                    Color.red,
                                                                    18,
                                                                    true ),
                                               Utils.GUI.Skin.label );
                            else
                              GUILayout.Label( Utils.GUI.MakeLabel( "AGX Dynamics: Errors occurred during initialization of AGX Dynamics.",
                                                                    Color.red,
                                                                    18,
                                                                    true ),
                                               Utils.GUI.Skin.label );
                          },
                          "AGX Dynamics not properly initialized",
                          Utils.GUI.Skin.window );

        return;
      }

      if ( m_statisticsWindowData == null )
        return;

      var simColor      = Color.Lerp( Color.white, Color.blue, 0.2f );
      var spaceColor    = Color.Lerp( Color.white, Color.green, 0.2f );
      var dynamicsColor = Color.Lerp( Color.white, Color.yellow, 0.2f );
      var eventColor    = Color.Lerp( Color.white, Color.cyan, 0.2f );
      var dataColor     = Color.Lerp( Color.white, Color.magenta, 0.2f );
      var memoryColor   = Color.Lerp( Color.white, Color.red, 0.2f );

      var labelStyle = m_statisticsWindowData.LabelStyle;
      var stats = agx.Statistics.instance();
      var simTime            = stats.getTimingInfo( "Simulation", "Step forward time" );
      var spaceTime          = stats.getTimingInfo( "Simulation", "Collision-detection time" );
      var dynamicsSystemTime = stats.getTimingInfo( "Simulation", "Dynamics-system time" );
      var preCollideTime     = stats.getTimingInfo( "Simulation", "Pre-collide event time" );
      var preTime            = stats.getTimingInfo( "Simulation", "Pre-step event time" );
      var postTime           = stats.getTimingInfo( "Simulation", "Post-step event time" );
      var lastTime           = stats.getTimingInfo( "Simulation", "Last-step event time" );

      var numBodies      = m_system.getRigidBodies().Count;
      var numShapes      = m_space.getGeometries().Count;
      var numConstraints = m_system.getConstraints().Count +
                           m_space.getGeometryContacts().Count;

      GUILayout.Window( m_statisticsWindowData.Id,
                        DisplayMemoryAllocations ? m_statisticsWindowData.RectMemoryEnabled : m_statisticsWindowData.Rect,
                        id =>
                        {
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "Total time:            ", simColor ) + simTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms", 14, true ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Pre-collide step:      ", eventColor ) + preCollideTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Collision detection:   ", spaceColor ) + spaceTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Pre step:              ", eventColor ) + preTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Dynamics solvers:      ", dynamicsColor ) + dynamicsSystemTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Post step:             ", eventColor ) + postTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Last step:             ", eventColor ) + lastTime.current.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "Data:                  ", dataColor ), 14, true ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Update frequency:      ", dataColor ) + (int)( 1.0f / TimeStep + 0.5f ) + " Hz" ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Number of bodies:      ", dataColor ) + numBodies ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Number of shapes:      ", dataColor ) + numShapes ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Number of constraints: ", dataColor ) + numConstraints ), labelStyle );
                          GUILayout.Label( "" );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "StepForward (managed):", memoryColor ), 14, true ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Step forward:          ", memoryColor ) + m_statisticsWindowData.ManagedStepForward.ToString( "0.00" ).PadLeft( 5, ' ' ) + " ms" ), labelStyle );
                          if ( !DisplayMemoryAllocations )
                            return;
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "Allocations (managed):", memoryColor ), 14, true ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Pre step callbacks:    ", memoryColor ) + MemoryAllocations.GetDeltaString( MemoryAllocations.Section.PreStepForward ).PadLeft( 6, ' ' ) ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Pre synchronize:       ", memoryColor ) + MemoryAllocations.GetDeltaString( MemoryAllocations.Section.PreSynchronizeTransforms ).PadLeft( 6, ' ' ) ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Step forward:          ", memoryColor ) + MemoryAllocations.GetDeltaString( MemoryAllocations.Section.StepForward ).PadLeft( 6, ' ' ) ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Post synchronize:      ", memoryColor ) + MemoryAllocations.GetDeltaString( MemoryAllocations.Section.PostSynchronizeTransforms ).PadLeft( 6, ' ' ) ), labelStyle );
                          GUILayout.Label( Utils.GUI.MakeLabel( Utils.GUI.AddColorTag( "  - Post step callbacks:   ", memoryColor ) + MemoryAllocations.GetDeltaString( MemoryAllocations.Section.PostStepForward ).PadLeft( 6, ' ' ) ), labelStyle );
                        },
                        "AGX Dynamics statistics",
                        Utils.GUI.Skin.window );
    }

    public void OpenInNativeViewer()
    {
      if ( m_simulation == null ) {
        Debug.Log( "Unable to open simulation in native viewer.\nEditor has to be in play mode (or paused)." );
        return;
      }

      string path = Application.dataPath + @"/AGXUnityTemp/";
      if ( !System.IO.Directory.Exists( path ) )
        System.IO.Directory.CreateDirectory( path );

      var tmpFilename    = "openedInViewer.agx";
      var tmpLuaFilename = "openedInViewer.agxLua";
      var camera         = Camera.main ?? Camera.allCameras.FirstOrDefault();

      if ( camera == null ) {
        Debug.Log( "Unable to find a camera - failed to open simulation in native viewer." );
        return;
      }

      var cameraData = new
      {
        Eye               = camera.transform.position.ToHandedVec3().ToVector3(),
        Center            = ( camera.transform.position + 25.0f * camera.transform.forward ).ToHandedVec3().ToVector3(),
        Up                = camera.transform.up.ToHandedVec3().ToVector3(),
        NearClippingPlane = camera.nearClipPlane,
        FarClippingPlane  = camera.farClipPlane,
        FOV               = camera.fieldOfView
      };

      var luaFileContent = @"
assert( requestPlugin( ""agxOSG"" ) )
function buildScene( sim, app, root )
  assert( agxOSG.readFile( """ + path + tmpFilename + @""", sim, root ) )

  local cameraData             = app:getCameraData()
  cameraData.eye               = agx.Vec3( " + cameraData.Eye.x + ", " + cameraData.Eye.y + ", " + cameraData.Eye.z + @" )
  cameraData.center            = agx.Vec3( " + cameraData.Center.x + ", " + cameraData.Center.y + ", " + cameraData.Center.z + @" )
  cameraData.up                = agx.Vec3( " + cameraData.Up.x + ", " + cameraData.Up.y + ", " + cameraData.Up.z + @" )
  cameraData.nearClippingPlane = " + cameraData.NearClippingPlane + @"
  cameraData.farClippingPlane  = " + cameraData.FarClippingPlane + @"
  cameraData.fieldOfView       = " + cameraData.FOV + @"
  app:applyCameraData( cameraData )

  return root
end
if arg and not alreadyInitialized then
  alreadyInitialized = true
  local app = agxOSG.ExampleApplication()
  _G[ ""buildScene"" ] = buildScene
  app:addScene( arg[ 0 ], ""buildScene"", string.byte( ""1"" ) )
  local argParser = agxIO.ArgumentParser()
  argParser:readArguments( arg )
  if app:init( argParser ) then
    app:run()
  end
end";

      if ( !SaveToNativeFile( path + tmpFilename ) ) {
        Debug.Log( "Unable to start viewer.", this );
        return;
      }

      System.IO.File.WriteAllText( path + tmpLuaFilename, luaFileContent );

      try {
        Process.Start( new ProcessStartInfo()
        {
          FileName = @"luaagx.exe",
          Arguments = path + tmpLuaFilename + @" -p --renderDebug 1",
          UseShellExecute = false
        } );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }
    }
  }
}
