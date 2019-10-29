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

    /// <summary>
    /// Binary path, when this module is part of a build, should be "." and
    /// therefore plugins in "./plugins", data in "./data" and license and
    /// other configuration files in "./cfg".
    /// </summary>
    //public static string FindBinaryPath()
    //{
    //  return ".";
    //}

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
      // the data plugins directory. RESOURCE_PATH is where the license
      // file is assumed to be located.
      else {
        var dataDir = "./" + Application.productName + "_Data";
        var dataPluginsDir = dataDir + "/Plugins";
        Debug.LogWarning( "AGXUnity data dir: " + dataDir );
        Debug.LogWarning( "AGXUnity data plugins dir: " + dataPluginsDir );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( "." );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataDir );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataPluginsDir );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( dataPluginsDir );
      }

      agx.agxSWIG.setEntityCreationThreadSafe( true );

      m_ai = new agx.AutoInit();
      agx.agxSWIG.setNumThreads( 4 );
      Initialized = true;

      //string binaryPath = FindBinaryPath();

      //// Check if agxDotNet.dll is in path.
      //if ( !IO.Utils.IsFileInEnvironmentPath( "agxDotNet.dll" ) ) {
      //  // If it is not in path, lets look in the registry
      //  binaryPath = IO.Utils.ReadAGXRegistryPath();

      //  // If no luck, then we need to bail out
      //  if ( binaryPath.Length == 0 )
      //    throw new AGXUnity.Exception( "Unable to find agxDotNet.dll - part of the AGX Dynamics installation." );
      //  else
      //    IO.Utils.AddEnvironmentPath( binaryPath );
      //}

      //string pluginPath = binaryPath + @"\plugins";
      //string dataPath = binaryPath + @"\data";
      //string cfgPath = dataPath + @"\cfg";

      //try {
      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( binaryPath );
      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( pluginPath );

      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( binaryPath );
      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( pluginPath );
      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataPath );
      //  agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( cfgPath );

      //  // Components are initialized in parallel and destroy is executed
      //  // from other worker threads. Enable local entity storages.
      //  agx.agxSWIG.setEntityCreationThreadSafe( true );

      //  m_ai = new agx.AutoInit();

      //  agx.agxSWIG.setNumThreads( 4 );

      //  Initialized = true;
      //}
      //catch ( System.Exception e ) {
      //  throw new AGXUnity.Exception( "Unable to instantiate first AGX Dynamics object. Some dependencies seems missing: " + e.ToString() );
      //}
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
