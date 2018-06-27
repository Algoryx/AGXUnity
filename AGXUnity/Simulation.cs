using System;
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
	
    /// <summary>
    /// Control automatic stepping of simulation.
    /// Also see the EnableAutomaticStepping property.
    /// </summary>
    [SerializeField]
    private bool m_enableAutomaticStepping = true;

    /// <summary>
    /// Set to true to enable automatic stepping during FixedUpdate.
    /// If set to false, the DoStep() method will have to be manually called.
    /// </summary>
    public bool EnableAutomaticStepping
    {
      get { return m_enableAutomaticStepping; }
      set { m_enableAutomaticStepping = value; }
    }
    
    public static float DefaultTimeStep { get { return Time.fixedDeltaTime; } }

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
    private float m_timeStep = 1.0f / 50.0f;

    /// <summary>
    /// Get or set time step size. Note that the time step has to
    /// match Unity update frequency.
    /// </summary>
    public float TimeStep
    {
      get { return m_timeStep; }
      set
      {
        m_timeStep = value;
        if ( m_simulation != null )
          m_simulation.setTimeStep( m_timeStep );
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
    public bool DisplayStatistics
    {
      get { return m_displayStatistics; }
      set
      {
        m_displayStatistics = value;

        if ( m_displayStatistics && m_statisticsWindowData == null )
          m_statisticsWindowData = new StatisticsWindowData( new Rect( new Vector2( 10, 10 ), new Vector2( 275, 320 ) ) );
        else if ( !m_displayStatistics && m_statisticsWindowData != null ) {
          m_statisticsWindowData.Dispose();
          m_statisticsWindowData = null;
        }
      }
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

    private StepCallbackFunctions m_stepCallbackFunctions = new StepCallbackFunctions();

    /// <summary>
    /// Step callback interface. Valid use from "initialize" to "Destroy".
    /// </summary>
    public StepCallbackFunctions StepCallbacks { get { return m_stepCallbackFunctions; } }

    /// <summary>
    /// Save current simulation/scene to an AGX native file (.agx or .aagx).
    /// </summary>
    /// <param name="filename">Filename including path.</param>
    /// <returns>True if objects were written to file - otherwise false.</returns>
    public bool SaveToNativeFile( string filename )
    {
      if ( m_simulation == null ) { 
        Debug.LogWarning( "Simulation isn't active - ignoring save scene to file: " + filename );
        return false;
      }

      uint numObjects = m_simulation.write( filename );
      return numObjects > 0;
    }

    protected override bool Initialize()
    {
      GetOrCreateSimulation();

      return base.Initialize();
    }

    private void FixedUpdate()
    {
      if (EnableAutomaticStepping)
    	DoStep();
    }		

    public void DoStep()
    {
      if ( !NativeHandler.Instance.HasValidLicense )
        return;

      if ( m_simulation != null ) {
        bool savePreFirstTimeStep = Application.isEditor &&
                                    SavePreFirstStep &&
                                    SavePreFirstStepPath != string.Empty &&
                                    m_simulation.getTimeStamp() == 0.0;
        if ( savePreFirstTimeStep ) {
          var saveSuccess = SaveToNativeFile( SavePreFirstStepPath );
          if ( saveSuccess )
            Debug.Log( "Successfully wrote initial state to: " + SavePreFirstStepPath );
        }

        agx.Timer timer = null;
        if ( DisplayStatistics )
          timer = new agx.Timer( true );

        MemoryAllocations.Snap( MemoryAllocations.Section.Begin );

        if ( StepCallbacks.PreStepForward != null )
          StepCallbacks.PreStepForward.Invoke();

        MemoryAllocations.Snap( MemoryAllocations.Section.PreStepForward );

        if ( StepCallbacks.PreSynchronizeTransforms != null )
          StepCallbacks.PreSynchronizeTransforms.Invoke();

        MemoryAllocations.Snap( MemoryAllocations.Section.PreSynchronizeTransforms );

        if ( timer != null )
          timer.stop();

        m_simulation.stepForward();

        if ( timer != null )
          timer.start();

        MemoryAllocations.Snap( MemoryAllocations.Section.StepForward );

        if ( StepCallbacks.PostSynchronizeTransforms != null )
          StepCallbacks.PostSynchronizeTransforms.Invoke();

        MemoryAllocations.Snap( MemoryAllocations.Section.PostSynchronizeTransforms );

        if ( StepCallbacks.PostStepForward != null )
          StepCallbacks.PostStepForward.Invoke();

        MemoryAllocations.Snap( MemoryAllocations.Section.PostStepForward );

        Rendering.DebugRenderManager.OnActiveSimulationPostStep( m_simulation );

        if ( timer != null ) {
          timer.stop();
          m_statisticsWindowData.ManagedStepForward = Convert.ToSingle( timer.getTime() );
        }
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
      public Font Font { get; private set; }
      public GUIStyle LabelStyle { get; set; }
      public float ManagedStepForward { get; set; }

      public StatisticsWindowData( Rect rect )
      {
        agx.Statistics.instance().setEnable( true );
        Id = GUIUtility.GetControlID( FocusType.Passive );
        Rect = rect;

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

      var simTime            = agx.Statistics.instance().getTimingInfo( "Simulation", "Step forward time" );
      var spaceTime          = agx.Statistics.instance().getTimingInfo( "Simulation", "Collision-detection time" );
      var dynamicsSystemTime = agx.Statistics.instance().getTimingInfo( "Simulation", "Dynamics-system time" );
      var preCollideTime     = agx.Statistics.instance().getTimingInfo( "Simulation", "Pre-collide event time" );
      var preTime            = agx.Statistics.instance().getTimingInfo( "Simulation", "Pre-step event time" );
      var postTime           = agx.Statistics.instance().getTimingInfo( "Simulation", "Post-step event time" );
      var lastTime           = agx.Statistics.instance().getTimingInfo( "Simulation", "Last-step event time" );

      var numBodies      = m_simulation.getDynamicsSystem().getRigidBodies().Count;
      var numShapes      = m_simulation.getSpace().getGeometries().Count;
      var numConstraints = m_simulation.getDynamicsSystem().getConstraints().Count +
                           m_simulation.getSpace().getGeometryContacts().Count;

      GUILayout.Window( m_statisticsWindowData.Id,
                        m_statisticsWindowData.Rect,
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

    protected override void OnApplicationQuit()
    {
      base.OnApplicationQuit();
      if ( m_simulation != null )
        m_simulation.cleanup();
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      if ( m_simulation != null )
        m_simulation.cleanup();
      m_simulation = null;
    }

    private agxSDK.Simulation GetOrCreateSimulation()
    {
      if ( m_simulation == null ) {
        NativeHandler.Instance.MakeMainThread();

        m_simulation = new agxSDK.Simulation();
      }

      return m_simulation;
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

      string tmpFilename    = "openedInViewer.agx";
      string tmpLuaFilename = "openedInViewer.agxLua";

      var cameraData = new
      {
        Eye               = Camera.main.transform.position.ToHandedVec3().ToVector3(),
        Center            = ( Camera.main.transform.position + 25.0f * Camera.main.transform.forward ).ToHandedVec3().ToVector3(),
        Up                = Camera.main.transform.up.ToHandedVec3().ToVector3(),
        NearClippingPlane = Camera.main.nearClipPlane,
        FarClippingPlane  = Camera.main.farClipPlane,
        FOV               = Camera.main.fieldOfView
      };

      string luaFileContent = @"
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
        Process.Start( "luaagx.exe", path + tmpLuaFilename + " -p --renderDebug 1" );
      }
      catch ( System.Exception ) {
        // Installed version.
        try {
          Process.Start( path + tmpLuaFilename );
        }
        catch ( System.Exception e ) {
          Debug.LogException( e );
        }
      }
    }
  }
}
