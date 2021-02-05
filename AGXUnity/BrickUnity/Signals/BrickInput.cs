using UnityEngine;

namespace AGXUnity.BrickUnity.Signals
{
  public abstract class BrickInput<TSIGNAL, TDISPLAY> : MonoBehaviour
  {
    [SerializeField]
    private TDISPLAY inputData = default;
    [SerializeField]
    private bool inputActive = false;
    public Brick.Signal.Input<TSIGNAL> signal;

    public abstract TSIGNAL ConvertData(TDISPLAY data);

    void Update()
    {
      if (inputActive)
      {
        signal.SetData(ConvertData(inputData));
      }
    }
  }
}