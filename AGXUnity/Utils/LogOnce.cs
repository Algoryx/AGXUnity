using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

using Object = UnityEngine.Object;

namespace AGXUnity.Utils
{
  public static class LogOnce
  {
    private static readonly ConditionalWeakTable<object,HashSet<string>> s_logged = new();
    private static readonly HashSet<string> s_global = new();

    private static bool FirstLog( object context, string id )
    {
      HashSet<string> loggedIds;
      if ( context == null )
        loggedIds = s_global;
      else if ( !s_logged.TryGetValue( context, out loggedIds ) ) {
        loggedIds = new HashSet<string>();
        s_logged.Add( context, loggedIds );
      }
      return loggedIds.Add( id );
    }

    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Clear()
    {
      s_logged.Clear();
      s_global.Clear();
    }

    /// <summary>
    /// Logs an info message only once for each id + context combination that is passed to the logging method.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="id">The id by which to decouple this log from other invokations in the same context</param>
    /// <param name="context">The context in which the message is logged, null means the static context</param>
    public static void Info( string message, object context, string id )
    {
      if ( FirstLog( context, id ) )
        Debug.Log( message, context as Object );
    }

    /// <summary>
    /// Logs warning message only once for each id + context combination that is passed to the logging method.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="id">The id by which to decouple this log from other invokations in the same context</param>
    /// <param name="context">The context in which the message is logged, null means the static context</param>
    public static void Warning( string message, object context, string id )
    {
      if ( FirstLog( context, id ) )
        Debug.LogWarning( message, context as Object );
    }

    /// <summary>
    /// Logs an error message only once for each id + context combination that is passed to the logging method.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="id">The id by which to decouple this log from other invokations in the same context</param>
    /// <param name="context">The context in which the message is logged, null means the static context</param>
    public static void Error( string message, object context, string id )
    {
      if ( FirstLog( context, id ) )
        Debug.LogError( message, context as Object );
    }

    /// <summary>
    /// Logs an info message only once for each object passed to the logging method. Uses file name + line of the
    /// call site to generate an id.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="context">The context in which the message is logged. Usually 'this' when used inside components</param>
    /// <param name="file">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Info(string, object, string)"/> to manually specify id.</param>
    /// <param name="line">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Info(string, object, string)"/> to manually specify id.</param>
    public static void Info(
      string message,
      object context,
      [System.Runtime.CompilerServices.CallerFilePath] string file = null,
      [System.Runtime.CompilerServices.CallerLineNumber] uint line = 0 )
      => Info( message, context, $"ID_{file}:{line}" );

    /// <summary>
    /// Logs warning message only once for each object passed to the logging method. Uses file name + line of the
    /// call site to generate an id.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="context">The context in which the message is logged. Usually 'this' when used inside components</param>
    /// /// <param name="file">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Warning(string, object, string)"/> to manually specify id.</param>
    /// <param name="line">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Warning(string, object, string)"/> to manually specify id.</param>
    public static void Warning(
      string message,
      object context,
      [System.Runtime.CompilerServices.CallerFilePath] string file = null,
      [System.Runtime.CompilerServices.CallerLineNumber] uint line = 0 )
      => Warning( message, context, $"ID_{file}:{line}" );

    /// <summary>
    /// Logs an error message only once for each object passed to the logging method. Uses file name + line of the
    /// call site to generate an id.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="context">The context in which the message is logged. Usually 'this' when used inside components</param>
    /// /// <param name="file">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Error(string, object, string)"/> to manually specify id.</param>
    /// <param name="line">Automatically populated, should not be passed explicitely. 
    /// Use <see cref="Error(string, object, string)"/> to manually specify id.</param>
    public static void Error(
      string message,
      object context,
      [System.Runtime.CompilerServices.CallerFilePath] string file = null,
      [System.Runtime.CompilerServices.CallerLineNumber] uint line = 0 )
      => Error( message, context, $"ID_{file}:{line}" );
  }
}
