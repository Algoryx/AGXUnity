using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.BrickUnity.Signals
{
  public abstract class BrickOutput<TSIGNAL, TDISPLAY> : MonoBehaviour
  {
    public TDISPLAY signalData;
    public Brick.Signal.Signal<TSIGNAL> signal;

    protected abstract TDISPLAY GetSignalData(TSIGNAL internalData);

    // Update is called once per frame
    protected virtual void Update()
    {
      signalData = GetSignalData(signal.GetData());
    }
  }
}