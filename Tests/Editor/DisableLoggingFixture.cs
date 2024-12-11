using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unity adds it's log messages to the test results. To have the results be usable by external tools
/// we therefore disable logging while running tests.
/// </summary>
[SetUpFixture]
public class DisableLoggingFixture
{
  [OneTimeSetUp]
  public void Setup()
  {
    Debug.unityLogger.logEnabled = false;
  }

  [OneTimeTearDown]
  public void Teardown()
  {
    Debug.unityLogger.logEnabled = true;
  }
}

