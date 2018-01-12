using System;

namespace AGXUnity.Utils
{
  public class DisposableCallback : IDisposable
  {
    public Action Callback { get; private set; }

    public DisposableCallback( Action callback )
    {
      Callback = callback;
    }

    public void Dispose()
    {
      Callback();
    }
  }
}
