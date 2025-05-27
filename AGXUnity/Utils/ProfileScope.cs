using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;

/// <summary>
/// This utility class can be used to profile the current scope using the using/dispose pattern.
/// by default, the profiler sample will be named [FileName]:[method]
/// </summary>
public class ProfileScope : IDisposable
{
  public ProfileScope( [CallerMemberName] string name = "", [CallerFilePath] string context = "" )
  {
    if ( File.Exists( context ) )
      context = Path.GetFileName( context );
    Profiler.BeginSample( context + ":" + name );
  }

  public void Dispose()
  {
    Profiler.EndSample();
  }
}
