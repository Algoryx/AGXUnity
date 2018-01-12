using System;
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
    public static string FindBinaryPath()
    {
      return ".";
    }

    private void InitPath()
    {
    }

    private void Configure()
    {
      string binaryPath = FindBinaryPath();

      // Check if agxDotNet.dll is in path.
      if ( !IO.Utils.IsFileInEnvironmentPath( "agxDotNet.dll" ) ) {
        // If it is not in path, lets look in the registry
        binaryPath = IO.Utils.ReadAGXRegistryPath();

        // If no luck, then we need to bail out
        if ( binaryPath.Length == 0 )
          throw new AGXUnity.Exception( "Unable to find agxDotNet.dll - part of the AGX Dynamics installation." );
        else
          IO.Utils.AddEnvironmentPath( binaryPath );
      }

      string pluginPath = binaryPath + @"\plugins";
      string dataPath = binaryPath + @"\data";
      string cfgPath = dataPath + @"\cfg";

      try {
        // Components are initialized in parallel and destroy is executed
        // from other worker threads. Enable local entity storages.
        agx.agxSWIG.setEntityCreationThreadSafe( true );

        m_ai = new agx.AutoInit();

        agx.agxSWIG.setNumThreads( 4 );

        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( binaryPath );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( pluginPath );

        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( binaryPath );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( pluginPath );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataPath );
        agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( cfgPath );

        Initialized = true;
      }
      catch ( System.Exception e ) {
        throw new AGXUnity.Exception( "Unable to instantiate first AGX Dynamics object. Some dependencies seems missing: " + e.ToString() );
      }
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
      HasValidLicense = false;
      m_isAgx         = new InitShutdownAGXDynamics();

      if ( m_isAgx.Initialized && !agx.Runtime.instance().isValid() )
        Debug.LogError( "AGX Dynamics: " + agx.Runtime.instance().getStatus() );
      else if ( m_isAgx.Initialized )
        HasValidLicense = true;
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
  }
}
