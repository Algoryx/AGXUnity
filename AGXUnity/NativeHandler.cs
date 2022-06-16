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
#if AGXUNITY_UPDATING
      throw new AGXUnity.Exception( "AGX Dynamics for Unity is updating and AGX Dynamics " +
                                    "binaries shouldn't be loaded into the process." );
#else
      if ( Application.isEditor ) {
        // This case is handled by AGXUnityEditor.Manager.
      }
      // Running build with environment set. Use default environment setup.
      // This can be useful to debug an application, being able to attach
      // to a process with AGX Dynamics + AGXUnity.
      else if ( IO.Environment.IsSet( IO.Environment.Variable.AGX_DEPENDENCIES_DIR ) ) {
      }
      // Running build without environment, assuming all binaries are
      // present in this process. Setup RUNTIME_PATH to Components in
      // the data agx directory. RESOURCE_PATH is where the license
      // file is assumed to be located.
      else {
        var dataPath           = Application.dataPath;
        var dataPluginsPath    = IO.Environment.GetPlayerPluginPath( dataPath );
        var dataAGXRuntimePath = IO.Environment.GetPlayerAGXRuntimePath( dataPath );

        IO.Environment.AddToPath( dataPluginsPath );

        var envInstance = agxIO.Environment.instance();

        // In case of installed AGX Dynamics, agxIO.Environment.instance() will
        // read data from the registry and add runtime and resource paths to
        // the installed version. Clear all, from registry, added paths since
        // we assume all data needed is present in this runtime.
        for ( int i = 0; i < (int)agxIO.Environment.Type.NUM_TYPES; ++i )
          envInstance.getFilePath( (agxIO.Environment.Type)i ).clear();

        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataAGXRuntimePath );
        envInstance.getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( dataAGXRuntimePath );

        if ( string.IsNullOrEmpty( envInstance.findComponent( "Referenced.agxEntity" ) ) )
          throw new AGXUnity.Exception( "Unable to find Components directory in RUNTIME_PATH." );
      }

      agx.agxSWIG.setEntityCreationThreadSafe( true );

      m_ai = new agx.AutoInit();

      agx.agxSWIG.setNumThreads( 4 );

      Initialized = true;
#endif
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
    NativeHandler()
    {
      m_isAgx = new InitShutdownAGXDynamics();

      var searchForLicenses = !Application.isEditor &&
                              m_isAgx.Initialized &&
                              // External scripts could load licenses, even before AGX
                              // has been initialized.
                              !LicenseManager.UpdateLicenseInformation().IsValid;
      if ( searchForLicenses && !LicenseManager.LoadFile() )
        LicenseManager.ActivateEncryptedRuntime( IO.Environment.GetPlayerPluginPath( Application.dataPath ) );

      // The environment probably hasn't been completely configured
      // when inside the editor so it's up to Manager to call
      // ValidateLicense post environment configuration.
      if ( !Application.isEditor )
        ValidateLicense();
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

    public void ValidateLicense()
    {
      HasValidLicense = m_isAgx.Initialized && agx.Runtime.instance().isValid();
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

    public void UnregisterCurrentThread()
    {
      if ( !agx.Thread.isMainThread() )
        agx.Thread.unregisterAsAgxThread();
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

      return LicenseManager.Load( licenseStr );
    }

    #region Singleton Stuff
    private static NativeHandler s_instance = null;
    public static NativeHandler Instance
    {
      get
      {
        if ( s_instance == null )
          s_instance = new NativeHandler();
        return s_instance;
      }
    }

    public static bool HasInstance => s_instance != null;
    #endregion

    private InitShutdownAGXDynamics m_isAgx = null;
  }
}
