using agxROS2;
using System;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [Serializable]
  public class QOS
  {
    [SerializeField]
    public QOS_RELIABILITY reliabilityPolicy = QOS_RELIABILITY.BEST_EFFORT;

    [SerializeField]
    public QOS_DURABILITY durabilityPolicy = QOS_DURABILITY.VOLATILE;

    [SerializeField]
    public QOS_HISTORY historyPolicy = QOS_HISTORY.KEEP_LAST_HISTORY_QOS;

    [SerializeField]
    public uint historyDepth = 5;

    public agxROS2.QOS CreateNative()
    {
      var native = new agxROS2.QOS();

      native.durability = durabilityPolicy;
      native.reliability = reliabilityPolicy;
      native.history = historyPolicy;
      native.historyDepth = (int)historyDepth;

      return native;
    }
  }
}
