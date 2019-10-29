using System;
using System.Linq;
using System.IO;
using Microsoft.Win32;

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

      var installedPath = ReadAGXRegistryPath();
      if ( installedPath.Length > 0 ) {
        fileInfo = new FileInfo( installedPath + @"\" + filename );
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

    public static bool IsFileInEnvironmentPath( string filename )
    {
      return GetFileInEnvironmentPath( filename ) != null;
    }

    public static void AddEnvironmentPath( string path )
    {
      string currentPath = Environment.GetEnvironmentVariable( "PATH", EnvironmentVariableTarget.Process );
      Environment.SetEnvironmentVariable( "PATH", currentPath + Path.PathSeparator + path, EnvironmentVariableTarget.Process );
    }

    public static string ReadAGXRegistryPath()
    {
      return (string)Registry.GetValue( "HKEY_LOCAL_MACHINE\\Software\\Wow6432Node\\Algoryx Simulation AB\\Algoryx\\AgX",
                                        "runtime",
                                        "" );
    }
  }
}
