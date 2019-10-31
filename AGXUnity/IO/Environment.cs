using System.IO;
using System.Linq;

namespace AGXUnity.IO
{
  public static class Environment
  {
    /// <summary>
    /// Predefined variables with context.
    /// </summary>
    public enum Variable
    {
      AGX_DIR,
      AGX_PLUGIN_PATH,
      AGX_DEPENDENCIES_DIR
    }

    /// <summary>
    /// Finds result of predefined environment variable.
    /// </summary>
    /// <param name="variable">Predefined variable.</param>
    /// <returns>Value of predefined environment variable if set - otherwise string.isNullOrEmpty == true.</returns>
    public static string Get( Variable variable )
    {
      // Installed AGX Dynamics points to install directory and
      // in development mode to external dependencies directory.
      // Both version fulfill "dependencies" but, e.g., agxPhysics.dll
      // will be found in an installed version but not in development
      // mode. This should be used to find actual dependencies.
      if ( variable == Variable.AGX_DEPENDENCIES_DIR ) {
        var depPath = Get( variable.ToString() );
        if ( string.IsNullOrEmpty( depPath ) )
          return string.Empty;
        return depPath +
               Path.DirectorySeparatorChar +
               "bin" +
               Path.DirectorySeparatorChar +
               ( agx.agxSWIG.isBuiltWith( agx.BuildConfiguration.USE_64BIT_ARCHITECTURE ) ? "x64" : "x86" );
      }
      return Get( variable.ToString() );
    }

    /// <summary>
    /// Finds if predefined variable is set.
    /// </summary>
    /// <param name="variable">Predefined environment variable.</param>
    /// <returns>True if set - otherwise false.</returns>
    public static bool IsSet( Variable variable )
    {
      return !string.IsNullOrEmpty( Get( variable ) );
    }

    /// <summary>
    /// Finds path to installed AGX Dynamics. Fails if Unity isn't
    /// started in an AGX Dynamics environment.
    /// </summary>
    public static string AGXDynamicsPath
    {
      get
      {
        var agxPath = Get( Variable.AGX_DIR );
        if ( !string.IsNullOrEmpty( agxPath ) ) {
          // Installed AGX Dynamics will add an extra \ to AGX_DIR.
          if ( agxPath.Last() == '\\' || agxPath.Last() == '/' )
            agxPath.Remove( agxPath.Length - 1 );
          return agxPath;
        }

        return null;
      }
    }

    /// <summary>
    /// Find environment variable with given <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Name of environment variable.</param>
    /// <param name="target">Environment variable target.</param>
    /// <returns>Value the environment variable carries - string.isNullOrEmpty == true if not found.</returns>
    public static string Get( string name, System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      return GetAll( name, target ).FirstOrDefault();
    }

    /// <summary>
    /// Find environment variable values with given <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Name of environment variable.</param>
    /// <param name="target">Environment variable target.</param>
    /// <returns>Array of results if found, an empty array if not found.</returns>
    public static string[] GetAll( string name, System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      var result = System.Environment.GetEnvironmentVariable( name, target );
      if ( result == null )
        return new string[] { };
      return result.Split( Path.PathSeparator );
    }

    /// <summary>
    /// Find file with given <paramref name="filename"/> in environment <paramref name="envVariable"/>
    /// and environment target <paramref name="target"/>.
    /// </summary>
    /// <param name="filename">Name of file.</param>
    /// <param name="envVariable">Environment variable - default: PATH.</param>
    /// <param name="target">Environment target to search in.</param>
    /// <returns>FileInfo of file with given <paramref name="filename"/> if found - otherwise null.</returns>
    public static FileInfo FindFile( string filename,
                                     string envVariable = "PATH",
                                     System.EnvironmentVariableTarget target = System.EnvironmentVariableTarget.Process )
    {
      var env = GetAll( envVariable, target );
      foreach ( var path in env ) {
        if ( File.Exists( path + Path.DirectorySeparatorChar + filename ) )
          return new FileInfo( path + Path.DirectorySeparatorChar + filename );
      }
      return null;
    }

    /// <summary>
    /// Plugins path where the AGX Dynamics native modules are located.
    /// NOTE: Due to limitations in dll-loading in Unity version earlier
    ///       than 2019.2.4, the dlls are copied to the root folder for
    ///       2019.2.3 and earlier.
    /// </summary>
    /// <param name="dataPath">Path to player data folder - nameOfExecutable_Data.</param>
    /// <returns>Path to the plugins folder.</returns>
    public static string GetPlayerPluginPath( string dataPath )
    {
      return dataPath + Path.DirectorySeparatorChar + "Plugins";
    }

    /// <summary>
    /// AGX Dynamics runtime path where Components and other runtime
    /// data is located.
    /// </summary>
    /// <param name="dataPath">Path to player data folder - nameOfExecutable_Data.</param>
    /// <returns>Path to the AGX Dynamics runtime folder.</returns>
    public static string GetPlayerAGXRuntimePath( string dataPath )
    {
      return GetPlayerPluginPath( dataPath ) + Path.DirectorySeparatorChar + "agx";
    }
  }
}
