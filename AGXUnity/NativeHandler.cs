using System.IO;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Object handling initialize and shutdown of AGX Dynamics.
  /// </summary>
  public class InitShutdownAGXDynamics
  {
    private agx.AutoInit m_ai = null;

    public bool Initialized { get; private set; }

    /// <summary>
    /// Default constructor - configuring AGX Dynamics, making sure dll's are
    /// in path etc.
    /// </summary>
    public InitShutdownAGXDynamics()
    {
      Initialized = false;

      try {
        Configure();
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }
    }

    ~InitShutdownAGXDynamics()
    {
      m_ai.Dispose();
      m_ai = null;
    }

    private void Configure()
    {
      // Running from within the editor. Assuming AGX Dynamics environment.
      if ( Application.isEditor ) {
        if ( !IO.Utils.HasEnvironmentVariable( "AGX_DIR" ) )
          throw new AGXUnity.Exception( "Environment variable AGX_DIR not found. Make sure Unity is started in an AGX Dynamics environment (setup_env)." );
      }
      // Running build with environment set. Use default environment setup.
      // This can be useful to debug an application, being able to attach
      // to a process with AGX Dynamics + AGXUnity.
      else if ( IO.Utils.HasEnvironmentVariable( "AGX_DIR" ) ) {
      }
      // Running build without environment, assuming all binaries are
      // present in this process. Setup RUNTIME_PATH to Components in
      // the data agx directory. RESOURCE_PATH is where the license
      // file is assumed to be located.
      else {
        var dataDir = IO.Utils.GetRuntimeDataDirectory();
        var dataPluginsDir = IO.Utils.GetRuntimeAGXDataDirectory();
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( "." );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataDir );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataPluginsDir );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( dataPluginsDir );
        if ( string.IsNullOrEmpty( agxIO.Environment.instance().findComponent( "Referenced.agxEntity" ) ) )
          throw new AGXUnity.Exception( "Unable to find Components directory in RUNTIME_PATH." );
      }

      agx.agxSWIG.setEntityCreationThreadSafe( true );

      m_ai = new agx.AutoInit();

      agx.agxSWIG.setNumThreads( 4 );

      Initialized = true;
    }
  }

  /// <summary>
  /// Every ScriptComponent created will call "Register" and
  /// "Unregister" to this singleton. First call will initialize
  /// AGX Dynamics and when this static instance is deleted AGX
  /// will be uninitialized.
  /// </summary>
  public class NativeHandler
  {
    #region Singleton Stuff
    private static NativeHandler m_instance = null;
    public static NativeHandler Instance
    {
      get
      {
        if ( m_instance == null )
          m_instance = new NativeHandler();
        return m_instance;
      }
    }
    #endregion

    private InitShutdownAGXDynamics m_isAgx = null;

    NativeHandler()
    {
      m_isAgx         = new InitShutdownAGXDynamics();
      HasValidLicense = m_isAgx.Initialized && agx.Runtime.instance().isValid();
    }

    ~NativeHandler()
    {
      m_isAgx = null;
    }

    public bool HasValidLicense { get; private set; }

    public bool Initialized { get { return m_isAgx != null && m_isAgx.Initialized; } }

    public void Register( ScriptComponent component )
    {
    }

    public void Unregister( ScriptComponent component )
    {
    }

    public void MakeMainThread()
    {
      if ( !agx.Thread.isMainThread() )
        agx.Thread.makeCurrentThreadMainThread();
    }

    public void RegisterCurrentThread()
    {
      if ( !agx.Thread.isMainThread() )
        agx.Thread.registerAsAgxThread();
    }

    /// <summary>
    /// Unlock AGX Dynamics using obfuscated license string.
    /// </summary>
    /// <remarks>
    /// AGXUnity searches for an AGX Dynamics license directly when
    /// an AGXUnity component or script is instantiated. If no license
    /// file is found or the file is invalid, NativeHandler.Instance.HasValidLicense
    /// returns false. To minimize the risks of undefined behavior,
    /// AGX Dynamics has to be unlocked as soon as possible when the
    /// application is loading. E.g., RuntimeInitializeOnLoadMethod which
    /// executes after Awake.
    /// </remarks>
    /// <example>
    /// namespace Scripts
    /// {
    ///   class UnlockAGXDynamics
    ///   {
    ///     [UnityEngine.RuntimeInitializeOnLoadMethod]
    ///     static OnLoad()
    ///     {
    ///       var success = AGXUnity.NativeHandler.VerifyAndUnlock( "myobfuscatedlicensestring" );
    ///       if ( success )
    ///         Debug.Log( "Successfully unlocked AGX Dynamics" );
    ///       else {
    ///         // An overlay will be displayed in the game view.
    ///         Debug.LogError( "Unable to unlock AGX Dynamics." )
    ///       }
    ///     }
    ///   }
    /// }
    /// </example>
    /// <param name="licenseStr">Obfuscated license string.</param>
    /// <returns>True if unlocked successfully, otherwise false (uninitialized AGX Dynamics or invalid license).</returns>
    public bool VerifyAndUnlock( string licenseStr )
    {
      if ( !m_isAgx.Initialized ) {
        Debug.LogError( "Unable to unlock AGX Dynamics - AGX Dynamics failed to initialize." );
        return false;
      }

      return (HasValidLicense = agx.Runtime.instance().verifyAndUnlock( licenseStr ));
    }
  }
}
