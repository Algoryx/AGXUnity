using AGXUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: TestMustExpectAllLogs]

namespace AGXUnityTesting.Runtime
{
  public class AGXUnityFixture
  {
    [OneTimeSetUp]
    public void SetupSimulationInstance()
    {
      Simulation.Instance.GetInitialized();
      Simulation.Instance.PreIntegratePositions = true;
      Simulation.Instance.LogEnabled = true;
      Simulation.Instance.AGXUnityLogLevel = LogLevel.Warning;
      Simulation.Instance.LogToUnityConsole = true;
    }

    [OneTimeTearDown]
    public void TeardownSimulationInstance()
    {
      GameObject.Destroy( Simulation.Instance.gameObject );
    }
  }
}
