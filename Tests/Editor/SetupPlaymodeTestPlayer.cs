using AGXUnity.IO;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine.TestTools;

[assembly: TestPlayerBuildModifier( typeof( SetupPlaymodeTestPlayer ) )]
[assembly: PostBuildCleanup( typeof( SetupPlaymodeTestPlayer ) )]

public class SetupPlaymodeTestPlayer : ITestPlayerBuildModifier, IPostBuildCleanup
{
  const string CLI_ARG = "onlyBuildTestsTo";
  private static bool s_RunningPlayerTests;

  public BuildPlayerOptions ModifyOptions( BuildPlayerOptions playerOptions )
  {
    var CLI = Environment.CommandLine;
    if ( CLI.HasArg( CLI_ARG ) ) {
      playerOptions.options &= ~( BuildOptions.AutoRunPlayer | BuildOptions.ConnectToHost );
      string path = (CLI.GetValues(CLI_ARG) ? [0]) ?? "TestPlayers";
      var buildLocation = Path.GetFullPath( path );
      var fileName = Path.GetFileName(playerOptions.locationPathName);
      if ( !string.IsNullOrEmpty( fileName ) )
        buildLocation = Path.Combine( buildLocation, fileName );
      playerOptions.locationPathName = buildLocation;

      s_RunningPlayerTests = true;
    }

    var scenes = playerOptions.scenes.Where( s => s.StartsWith("Assets/InitTestScene"));

    playerOptions.scenes = scenes.ToArray();
    return playerOptions;
  }

  public void Cleanup()
  {
    if ( Environment.CommandLine.HasArg( CLI_ARG ) ) {
      EditorApplication.Exit( 0 );
    }
  }
}
