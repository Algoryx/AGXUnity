using System;
using System.Linq;
using System.IO;
using UnityEngine;

namespace AGXUnity.IO
{
  public static class Utils
  {
    public static FileInfo GetFileInEnvironmentPath( string filename )
    {
      var fileInfo = new FileInfo( filename );
      if ( fileInfo.Exists )
        return fileInfo;

      var pathVariables = GetEnvironmentVariableEx( "PATH" );
      foreach ( var p in pathVariables ) {
        var fullPath = p + @"\" + filename;
        fileInfo = new FileInfo( fullPath );
        if ( fileInfo.Exists )
          return fileInfo;
      }

      return null;
    }

    public static string[] GetEnvironmentVariableEx( string variable, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process )
    {
      var result = Environment.GetEnvironmentVariable( variable, target );
      if ( result == null )
        return new string[] { };
      return result.Split( Path.PathSeparator );
    }

    public static string GetEnvironmentVariable( string variable, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process )
    {
      return GetEnvironmentVariableEx( variable, target ).FirstOrDefault();
    }

    public static bool HasEnvironmentVariable( string variable, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process )
    {
      return GetEnvironmentVariable( variable, target ) != null;
    }

    public static string GetPlayerPluginPath( string dataPath )
    {
      return dataPath + Path.DirectorySeparatorChar + "Plugins";
    }

    public static string GetPlayerAGXRuntimePath( string dataPath )
    {
      return GetPlayerPluginPath( dataPath ) + Path.DirectorySeparatorChar + "agx";
    }
  }
}
