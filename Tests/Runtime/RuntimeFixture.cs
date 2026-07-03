using AGXUnity;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: TestMustExpectAllLogs]

namespace AGXUnityTesting.Runtime
{
  public class AGXUnityFixture
  {
    [UnitySetUp]
    public IEnumerator SetupSimulationInstance()
    {
      Simulation.Instance.GetInitialized();
      Simulation.Instance.PreIntegratePositions = true;
      Simulation.Instance.LogEnabled = true;
      Simulation.Instance.AGXUnityLogLevel = LogLevel.Warning;
      Simulation.Instance.LogToUnityConsole = true;
      Simulation.Instance.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;

      yield return TestUtils.WaitUntilLoaded();
    }

    [UnityTearDown]
    public IEnumerator TeardownSimulationInstance()
    {
      yield return TestUtils.DestroyAndWait( Object.FindObjectsByType<ScriptComponent>( FindObjectsSortMode.None ).Select( c => c.gameObject ).ToArray() );
    }
  }
}
