using System;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class TimerBlock : IDisposable
  {
    public agx.Timer Native { get; private set; }
    public string Name { get; set; }

    public TimerBlock( string name )
    {
      Name = name;
      Native = new agx.Timer( true );
    }

    public void Dispose()
    {
      Native.stop();
      Debug.Log( "(Timed): " + Name + ": " + Native.getTime() + " ms." );
    }
  }
}
