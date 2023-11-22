using System.Collections.Generic;

namespace AGXUnity.IO
{
  /// <summary>
  /// Command line arguments utility class assuming arguments to
  /// be similar to Unity CLI, e.g., -this-is-an-argument value1 value2.
  /// If the argument is a flag, e.g., -quit or -batchmode - GetArg will
  /// return null while HasArg will return true.
  /// 
  /// The first '-' is removed and the rest of the '-' are replaced with
  /// '_' such that:
  ///     -this-is-an-argument -> commandLine.HasArg( "this_is_an_argument" ) == true.
  /// </summary>
  public class CommandLine
  {
    public enum Arg
    {
      /// <summary>
      /// -generate-offline-activation -> generate_offline_activation
      /// </summary>
      GenerateOfflineActivation,
      /// <summary>
      /// -create-offline-license -> create_offline_license
      /// </summary>
      CreateOfflineLicense,
      /// <summary>
      /// -agx-log-file -> agx_log_file
      /// </summary>
      AgxLogFile
    }

    /// <summary>
    /// Construct given arguments, e.g., new CommandLine( System.Environemnt.GetCommandLineArgs() ),
    /// or new CommandLine( new string[] { "ignored application name", "-custom-command", "value" } ).
    /// </summary>
    /// <param name="args">
    /// List of arguments, note that the first entry is assumed to be the application name and is ignored.
    /// </param>
    public CommandLine( string[] args )
    {
      try {
        for ( int idx = 1; idx < args.Length; ++idx ) {
          var arg = args[ idx ];
          var isKey = arg != null &&
                      arg.Length > 1 &&
                      arg.StartsWith( "-" ) &&
                      arg.Replace( "-", "" ).Length > 1;
          if ( !isKey )
            continue;
          var key = arg.Remove( 0, 1 ).Replace( '-', '_' );

          m_commandLineArguments.TryGetValue( key, out var values );

          for ( int iValue = idx + 1; iValue < args.Length; ++iValue ) {
            var potentialValue = args[ iValue ];
            var isValue = !string.IsNullOrEmpty( potentialValue ) &&
                          !potentialValue.StartsWith( "-" );
            if ( isValue ) {
              if ( values == null )
                values = new List<string>();
              values.Add( potentialValue );
            }
            else
              break;
          }

          m_commandLineArguments[ key ] = values;
        }
      }
      catch ( System.Exception e ) {
        UnityEngine.Debug.LogException( e );
      }
    }

    /// <summary>
    /// Returns true if the argument is given regardless of the number of values
    /// it may contain.
    /// </summary>
    /// <param name="arg">Argument to check.</param>
    /// <returns>True if argument exist, otherwise false.</returns>
    public bool HasArg( string arg )
    {
      return !string.IsNullOrEmpty( arg ) && m_commandLineArguments.ContainsKey( arg );
    }

    /// <summary>
    /// Check whether a known argument exist.
    /// </summary>
    /// <param name="arg">Argument to check.</param>
    /// <returns>True if the argument exist, otherwise false.</returns>
    public bool HasArg( Arg arg )
    {
      return HasArg( ToArgName( arg ) );
    }

    /// <summary>
    /// Returns the values of a given argument. If the argument is given but has
    /// no values, null is returned. Use this method in combination with HasArg
    /// if the argument requires values.
    /// </summary>
    /// <param name="arg">Argument to get values for.</param>
    /// <returns>List of values, null if no values are given.</returns>
    public List<string> GetValues( string arg )
    {
      m_commandLineArguments.TryGetValue( arg, out var values );
      return values;
    }

    /// <summary>
    /// Returns the values of a given known argument. If the argument is given but has
    /// no values, null is returned. Use this method in combination with HasArg
    /// if the argument requires values.
    /// </summary>
    /// <param name="arg">Argument to get values for.</param>
    /// <returns>List of values, null if no values are given.</returns>
    public List<string> GetValues( Arg arg )
    {
      return GetValues( ToArgName( arg ) );
    }

    /// <summary>
    /// Splitting CamelCase and joining with '_' to lower case. E.g.,
    ///     CamelCase -> camel_case
    /// </summary>
    /// <param name="arg">Known argument enum value.</param>
    /// <returns>Argument string in parsed dictionary.</returns>
    public static string ToArgName( Arg arg )
    {
      return System.Text.RegularExpressions.Regex.Replace(
               System.Text.RegularExpressions.Regex.Replace(
                  arg.ToString(),
                  @"(\P{Ll})(\P{Ll}\p{Ll})",
                  "$1_$2"
              ),
              @"(\p{Ll})(\P{Ll})",
              "$1_$2"
            ).ToLower();
    }

    private Dictionary<string, List<string>> m_commandLineArguments = new Dictionary<string, List<string>>();
  }
}
