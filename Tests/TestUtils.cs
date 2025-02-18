#if UNITY_EDITOR
#define TEST_REALTIME_SYNC
#endif

#if TEST_NO_REALTIME_SYNC
#undef TEST_REALTIME_SYNC
#endif

using AGXUnity;
using AGXUnity.Utils;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AGXUnityTesting
{
  public static class TestUtils
  {
    public static IEnumerator WaitUntilLoaded( Scene sceneToWaitFor )
    {
      if ( !Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        while ( !sceneToWaitFor.isLoaded )
          yield return new WaitUntil( () => sceneToWaitFor.isLoaded );
#if !TEST_REALTIME_SYNC
        Simulation.Instance.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;
#endif
      }
    }

    public static IEnumerator WaitUntilLoaded()
    {
      if ( !Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
#if !TEST_REALTIME_SYNC
        Simulation.Instance.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;
#endif
        yield return null;
      }
    }

    public static IEnumerator SimulateTo( float time )
    {
      if ( !Application.isPlaying )
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
      if ( !Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        yield return WaitUntilLoaded();
        var targetTime = Simulation.Instance.Native.getTimeStamp() + time;
        while ( Simulation.Instance.Native.getTimeStamp() < targetTime ) {
          yield return Step();
        }
      }
    }

    public static IEnumerator Step()
    {
      if ( !Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        yield return WaitUntilLoaded();
#if TEST_REALTIME_SYNC
        yield return new WaitForFixedUpdate();
#else
        Simulation.Instance.DoStep();
        yield return null;
#endif
      }
    }

    public static void InitializeAll()
    {
      if ( !Application.isPlaying )
        Debug.LogError( "TestUtils are not supported in edit-mode" );
      else {
        for ( int i = 0; i < SceneManager.sceneCount; i++ ) {
          var objects = SceneManager.GetSceneAt( i ).GetRootGameObjects();
          foreach ( var obj in objects )
            obj.InitializeAll();
        }
      }
    }

    public static IEnumerator DestroyAndWait( params GameObject[] toDestroy )
    {
      if ( !Application.isPlaying )
        foreach ( var obj in toDestroy )
          GameObject.DestroyImmediate( obj );
      else {
        foreach ( var obj in toDestroy )
          GameObject.Destroy( obj );
        yield return new WaitUntil( () => toDestroy.All( obj => obj == null ) );
      }
    }
  }
}
