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

    /// <summary>
    /// Built runtime data directory, by default, relative to executable.
    /// The directory name is productName_Data where productName is the
    /// name stated in Player Settings - default the Unity project name.
    /// </summary>
    /// <param name="sourceDirectory">Source directory of executable ("." for relative path).</param>
    /// <returns>Runtime data directory.</returns>
    public static string GetRuntimeDataDirectory( string sourceDirectory = "." )
    {
      return sourceDirectory + Path.DirectorySeparatorChar + Application.productName + "_Data";
    }

    /// <summary>
    /// Built runtime AGX specific data directory where, e.g., Components
    /// should be located. This directory should be added to agxIO.Environment.Type.RUNTIME_PATH
    /// and is added by default by NativeHandler during setup of AGX Dynamics.
    /// The relative path will be productName_Data/agx where productName is
    /// the name stated in Player Settings - default the Unity project name.
    /// </summary>
    /// <param name="sourceDirectory">Source directory of executable ("." for relative path).</param>
    /// <returns></returns>
    public static string GetRuntimeAGXDataDirectory( string sourceDirectory = "." )
    {
      return GetRuntimeDataDirectory( sourceDirectory ) + Path.DirectorySeparatorChar + "agx";
    }
  }
}
