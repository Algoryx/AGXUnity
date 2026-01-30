using AGXUnity;
using NUnit.Framework;
using UnityEngine;

namespace AGXUnityTesting.Runtime
{
  public class AGXUnityFixture
  {
    [OneTimeSetUp]
    public void SetupSimulationInstance()
    {
      Simulation.Instance.GetInitialized();
    }

    [OneTimeTearDown]
    public void TeardownSimulationInstance()
    {
      GameObject.Destroy( Simulation.Instance.gameObject );
    }
  }
}
