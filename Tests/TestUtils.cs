#if UNITY_STANDALONE
#define TEST_REALTIME_SYNC
#endif

using AGXUnity;
using System.Collections;
using UnityEngine;

namespace AGXUnityTesting
{
  public static class TestUtils
  {
    public static IEnumerator WaitUntilLoaded()
    {
      if ( Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        yield return new WaitForEndOfFrame();
#if !TEST_REALTIME_SYNC
    Simulation.Instance.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;
#endif
      }
    }

    public static IEnumerator SimulateTo( float time )
    {
      if ( Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        yield return WaitUntilLoaded();
        while ( Simulation.Instance.Native.getTimeStamp() < time ) {
#if TEST_REALTIME_SYNC
          yield return new WaitForFixedUpdate();
#else
      Simulation.Instance.DoStep();
#endif
        }
      }
    }

    public static IEnumerator SimulateSeconds( float time )
    {
      if ( Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        yield return WaitUntilLoaded();
        var targetTime = Simulation.Instance.Native.getTimeStamp() + time;
        while ( Simulation.Instance.Native.getTimeStamp() < targetTime ) {
#if TEST_REALTIME_SYNC
          yield return new WaitForFixedUpdate();
#else
      Simulation.Instance.DoStep();
#endif
        }
      }
    }
  }
}
